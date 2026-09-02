using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Combat;
using CombatBrain.Lod;
using CombatBrain.Ranged;

namespace CombatBrain.Ai
{
    // F9 远程目标/弹药取舍 —— 胶水层。
    // 职责：
    //   * 0.3s 低频为远程兵选择目标：破甲箭打重甲、普通箭打轻甲、优先指挥官/重骑兵（纯逻辑 RangedBrain.Score）；
    //   * 弹药告罄（≤15%）时切换近战（TryToWieldWeaponInSlot）或后撤（SetMovementDirection 远离威胁）。
    // 纯逻辑 CombatBrain/Ranged/RangedBrain.cs 只读消费，零改动。
    //
    // 性能预算（三条铁律落地）：
    //   * 0.3s 低频周期 + L2+ 剧场上限 + StepFor 跳帧（F6/F12/F7 同款范式）；
    //   * 敌情用 Mission.GetNearbyEnemyAgents（引擎空间哈希 O(邻域)）+ MBList 缓冲复用；
    //   * 目标数组为可复用字段（仅必要时扩容），陈旧槽位以 Distance=float.MaxValue 哨兵填充，
    //     保证永不胜出 → 零稳态分配（SelectTarget 需整数组，纯逻辑不可改）。
    // API 核实（BannerlordSage，游戏 v1.4.8 精确匹配）：
    //   * Agent.SetTargetAgent(Agent)      —— Agent.cs L2878：public void SetTargetAgent(Agent agent)
    //   * Agent.TryToWieldWeaponInSlot(EquipmentIndex, WeaponWieldActionType, bool) —— Agent.cs L4121
    //   * Agent.WeaponWieldActionType      —— Agent.cs L451（嵌套枚举：WithAnimation/Instant/...）
    //   * Agent.HasRangedWeapon(bool)      —— Agent.cs L3006；Agent.HasMeleeWeaponCached —— L901
    //   * Agent.SpawnEquipment[EquipmentIndex].Item.ArmorComponent.MaterialType —— Equipment.cs L69 / EquipmentElement.cs L16 / ArmorComponent.cs L91
    //   * ArmorMaterialTypes               —— ArmorComponent.cs L11：None/Cloth/Leather/Chainmail/Plate
    //   * BasicCharacterObject.IsHero      —— BasicCharacterObject.cs L154
    //   * WeaponComponentData.AmmoClass(WeaponClass)/IsRangedWeapon/IsMeleeWeapon/IsConsumable —— WeaponComponentData.cs L59/L93/L91/L97
    //   * MissionWeapon.Amount/ModifiedMaxAmount/IsAnyConsumable —— MissionWeapon.cs L114/L132/L520
    //   * Agent.SetMovementDirection(in Vec2) —— Agent.cs L2402（F6 已核实）
    // 映射说明：箭种 AmmoClass.Arrow→LightArrow、Bolt/Javelin/ThrowingAxe/ThrowingKnife→ArmorPiercing（弩与投掷高刺伤穿甲更佳）；
    // 护甲 Plate→Heavy、Chainmail→Medium、Cloth/Leather/None→Light；指挥官 = Character.IsHero。
    public sealed class RangedGlue
    {
        public const float TickInterval = 0.3f;

        private const float ScanRadius = 45f;  // 远程交战扫描半径（弓弩有效射程内）
        private const float MeleeRange = 3.5f; // 近战判定半径（与 BehaviorLodScheduler.L3MeleeRadius 对齐）

        private float _sinceTick;
        private int _phase;

        private readonly MBList<Agent> _enemyBuffer = new MBList<Agent>();
        private readonly DecisionJitterGlue _jitter = new DecisionJitterGlue(); // F12 横切
        // P0-① 抖动时间基准：每 agent「上次调用 ShouldAct 的时刻」（now 基准 = 传入的 now），
        // 真实决策间隔 = now - last；传帧 dt 会让维持期/疲劳慢 6~20 倍。
        private readonly Dictionary<int, float> _lastJitterTime = new Dictionary<int, float>();
        private readonly Dictionary<int, bool> _fallingBack = new Dictionary<int, bool>(); // 后撤态（F6 跳过依据）
        private RangedTarget[] _targets = new RangedTarget[16]; // 可复用目标数组（仅必要时扩容）
        private Agent[] _targetAgents = new Agent[16];          // 与 _targets 平行，免 Index 反查

        /// <summary>F6 走位是否应跳过该兵（远程后撤中）。</summary>
        public bool IsFallingBack(int agentIndex)
        {
            bool v;
            return _fallingBack.TryGetValue(agentIndex, out v) && v;
        }

        /// <summary>
        /// 低频驱动入口：CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// </summary>
        /// <param name="mission">当前 Mission</param>
        /// <param name="dt">帧时间</param>
        /// <param name="now">战斗累计时间（P0-①：F12 真实决策间隔基准，= CombatAiMissionLogic._elapsed）</param>
        /// <param name="getLod">agent LOD 查询（剧场上限）</param>
        /// <param name="getProfile">agent FighterProfile 查询（F12 纪律，真实兵种分类）</param>
        /// <param name="getMorale">队士气查询（F12 输入）</param>
        /// <param name="isSurvivalRetreat">F7 求生撤退态（撤退中跳过远程决策，优先级最高）</param>
        public void Tick(Mission mission, float dt, float now,
            Func<int, LodLevel> getLod,
            Func<Agent, FighterProfile> getProfile,
            Func<Team, float> getMorale,
            Func<int, bool> isSurvivalRetreat)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            int agentCount = mission.Agents.Count;
            _phase = (_phase + 1) % 64;

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive() || !agent.IsAIControlled) continue;
                if (!agent.IsHuman) continue; // 非人类远程单位不在此层
                if (agent.IsRunningAway) continue;
                Team team = agent.Team;
                if (team == null) continue;

                LodLevel lod = getLod(agent.Index);
                if (lod < LodLevel.L2) continue; // 剧场效应

                if (isSurvivalRetreat != null && isSurvivalRetreat(agent.Index)) continue; // F7 求生优先

                int step = BehaviorLodScheduler.StepFor(lod, agentCount);
                if (((agent.Index + _phase) % step) != 0) continue; // 跳帧决策

                EquipmentIndex rangedSlot = FindRangedSlot(agent);
                if (rangedSlot == EquipmentIndex.None) continue; // 非远程兵

                ApplyRanged(mission, agent, team, rangedSlot, now, getProfile(agent), getMorale(team));
            }
        }

        public void Remove(int agentIndex)
        {
            _jitter.Remove(agentIndex);
            _lastJitterTime.Remove(agentIndex);
            _fallingBack.Remove(agentIndex);
        }

        // ---------- 决策 ----------

        private void ApplyRanged(Mission mission, Agent agent, Team team, EquipmentIndex rangedSlot, float now, FighterProfile profile, float morale)
        {
            Vec2 pos = agent.Position.AsVec2;
            mission.GetNearbyEnemyAgents(pos, ScanRadius, team, _enemyBuffer);

            AmmoKind ammo = ResolveAmmoKind(agent, rangedSlot);
            float ammoRatio = ComputeAmmoRatio(agent, rangedSlot);

            int count = 0;
            Agent nearest = null;
            float nearestD = float.MaxValue;
            for (int i = 0; i < _enemyBuffer.Count; i++)
            {
                Agent e = _enemyBuffer[i];
                if (e == null || !e.IsActive()) continue;
                float d = VecMath.Dist(pos.x, pos.y, e.Position.x, e.Position.y);
                if (d < nearestD)
                {
                    nearestD = d;
                    nearest = e;
                }
                if (count >= _targets.Length)
                {
                    Array.Resize(ref _targets, _targets.Length * 2);
                    Array.Resize(ref _targetAgents, _targetAgents.Length * 2);
                }
                ArmorClass armor = ClassifyArmor(e);
                _targets[count] = new RangedTarget
                {
                    Index = e.Index,
                    Distance = d,
                    Armor = armor,
                    IsCommander = e.Character != null && e.Character.IsHero,
                    IsHeavyCavalry = e.HasMount && armor == ArmorClass.Heavy,
                    Threat = VecMath.Clamp01(1f - d / ScanRadius)
                };
                _targetAgents[count] = e;
                count++;
            }

            // 陈旧槽位哨兵：Distance=float.MaxValue 得分恒为极小，绝不赢过真实目标（SelectTarget 走整数组）
            for (int i = count; i < _targets.Length; i++)
                _targets[i].Distance = float.MaxValue;

            bool inMeleeRange = nearest != null && nearestD <= MeleeRange;
            RangedFallback fallback = RangedBrain.DecideFallback(ammoRatio, inMeleeRange);

            // P0-① 真实决策间隔：now - last（跨节流+跳帧空拍也在内）；首次近似=节流周期；时钟异常兜底。
            float last;
            if (!_lastJitterTime.TryGetValue(agent.Index, out last)) last = now - TickInterval;
            float elapsed = now - last;
            if (elapsed <= 0f) elapsed = TickInterval;
            _lastJitterTime[agent.Index] = now;

            // F12 横切：犹豫/延迟维持期跳过本轮（拟人化，不侵入纯逻辑）
            float signedWobble;
            if (!_jitter.ShouldAct(agent.Index, elapsed, DecisionJitterGlue.DisciplineFrom(profile), morale, out signedWobble))
                return;

            switch (fallback)
            {
                case RangedFallback.KeepShooting:
                    _fallingBack[agent.Index] = false;
                    if (count > 0)
                    {
                        int best = RangedBrain.SelectTarget(_targets, ammo);
                        if (best >= 0 && best < count)
                        {
                            Agent target = _targetAgents[best];
                            if (target != null && target.IsActive())
                                agent.SetTargetAgent(target); // 引擎级远程瞄准目标
                        }
                    }
                    break;

                case RangedFallback.SwitchToMelee:
                    _fallingBack[agent.Index] = false;
                    DecisionObservatory.Observe(DecisionObservatory.BRangedFall); // T1 决策分支计数：远程弹药回退
                    EquipmentIndex melee = FindMeleeSlot(agent);
                    if (melee != EquipmentIndex.None)
                        agent.TryToWieldWeaponInSlot(melee, Agent.WeaponWieldActionType.WithAnimation, false);
                    else
                        Retreat(agent, pos, nearest, signedWobble); // 无近战武器：退为后撤
                    break;

                case RangedFallback.Retreat:
                    Retreat(agent, pos, nearest, signedWobble);
                    DecisionObservatory.Observe(DecisionObservatory.BRangedFall); // T1 决策分支计数：远程后撤回退
                    break;
            }
        }

        private void Retreat(Agent agent, Vec2 pos, Agent nearest, float signedWobble)
        {
            _fallingBack[agent.Index] = true;
            Vec2 dir;
            if (nearest != null)
            {
                dir = new Vec2(pos.x - nearest.Position.x, pos.y - nearest.Position.y); // 远离最近威胁
            }
            else
            {
                Formation f = agent.Formation;
                Vec2 fpos = TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage); // P1-④ 统一入口
                dir = f != null
                    ? new Vec2(fpos.x - pos.x, fpos.y - pos.y)
                    : Vec2.Zero;
            }
            float len = (float)Math.Sqrt(dir.x * dir.x + dir.y * dir.y);
            if (len < 1e-4f) return;
            dir.x /= len;
            dir.y /= len;
            if (Math.Abs(signedWobble) > 0.01f)
                dir = DecisionJitterGlue.ApplyWobble(dir, signedWobble);
            agent.SetMovementDirection(dir);
        }

        // ---------- 装备读取 ----------

        private static EquipmentIndex FindRangedSlot(Agent agent)
        {
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon w = agent.Equipment[i];
                if (w.IsEmpty || w.CurrentUsageItem == null) continue;
                if (w.CurrentUsageItem.IsRangedWeapon) return i;
            }
            return EquipmentIndex.None;
        }

        private static EquipmentIndex FindMeleeSlot(Agent agent)
        {
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon w = agent.Equipment[i];
                if (w.IsEmpty || w.CurrentUsageItem == null) continue;
                if (w.CurrentUsageItem.IsMeleeWeapon) return i;
            }
            return EquipmentIndex.None;
        }

        // 箭种映射：Bolt/Thrown → 穿甲；Arrow 及未知 → 轻羽（见文件头说明）
        private static AmmoKind ResolveAmmoKind(Agent agent, EquipmentIndex rangedSlot)
        {
            WeaponClass ammoClass = WeaponClass.Undefined;
            MissionWeapon w = agent.Equipment[rangedSlot];
            if (!w.IsEmpty && w.CurrentUsageItem != null)
                ammoClass = w.CurrentUsageItem.AmmoClass;
            switch (ammoClass)
            {
                case WeaponClass.Bolt: return AmmoKind.ArmorPiercing;        // 弩箭高刺伤穿甲更佳
                case WeaponClass.Javelin: return AmmoKind.ArmorPiercing;     // 投掷系穿甲
                case WeaponClass.ThrowingAxe: return AmmoKind.ArmorPiercing;
                case WeaponClass.ThrowingKnife: return AmmoKind.ArmorPiercing;
                default: return AmmoKind.LightArrow;                          // Arrow 及其余
            }
        }

        // 弹药余量比：匹配箭种的全部弹药槽 Amount/ModifiedMaxAmount 合计（无弹药槽 → 0）
        private static float ComputeAmmoRatio(Agent agent, EquipmentIndex rangedSlot)
        {
            WeaponClass need = WeaponClass.Undefined;
            MissionWeapon rw = agent.Equipment[rangedSlot];
            if (!rw.IsEmpty && rw.CurrentUsageItem != null)
                need = rw.CurrentUsageItem.AmmoClass;
            if (need == WeaponClass.Undefined) return 0f;

            int total = 0;
            int max = 0;
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon w = agent.Equipment[i];
                if (w.IsEmpty || w.Item == null || w.Item.PrimaryWeapon == null) continue;
                if (!w.IsAnyConsumable()) continue;
                if (w.Item.PrimaryWeapon.AmmoClass != need) continue;
                max += w.ModifiedMaxAmount;
                total += w.Amount;
            }
            if (max <= 0) return 0f;
            float ratio = (float)total / (float)max;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }

        // 护甲分级：Plate→Heavy、Chainmail→Medium、Cloth/Leather/None→Light
        private static ArmorClass ClassifyArmor(Agent agent)
        {
            try
            {
                EquipmentElement body = agent.SpawnEquipment[EquipmentIndex.Body];
                if (body.IsEmpty || body.Item == null || body.Item.ArmorComponent == null)
                    return ArmorClass.Light; // 无甲
                switch (body.Item.ArmorComponent.MaterialType)
                {
                    case ArmorComponent.ArmorMaterialTypes.Plate: return ArmorClass.Heavy;
                    case ArmorComponent.ArmorMaterialTypes.Chainmail: return ArmorClass.Medium;
                    default: return ArmorClass.Light; // Cloth/Leather/None
                }
            }
            catch
            {
                return ArmorClass.Medium; // 装备读取异常兜底
            }
        }
    }
}
