using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Combat;
using CombatBrain.Formations;
using CombatBrain.Lod;
using CombatBrain.Perception;

namespace CombatBrain.Ai
{
    // X5-6 执行映射胶水（FormationSlotGlue）—— X5 全链的落地步（唯一触碰游戏 API 的接线）。
    // 串联（0.5s 编队级节拍，由 CombatAiMissionLogic.TickFormationSlots 驱动，TickFormationTactics 之后）：
    //   TroopClassifier 归类（逐 agent 低频聚合，0.5s 一次，非逐帧）→ TroopComposition 分流
    //   → SlotLayout.Generate（三布局）→ TerrainWarp.Warp（地形形变）→ FormationDisciplineController
    //   Evaluate 门控 → SlotAssigner.Assign（稳定指派）→ 执行腿 (b) L3 个体补位。
    // 执行腿 (a) 编队纪律内手段（L0-L2 主）：本层不改编队命令/排列（红线 1 不 SetArrangementOrder——
    //   T2-01 领地），编队级只产出 Slot 目标并记录日志，纪律维持交原版（设计稿 §六）。
    // 执行腿 (b) L3 个体补位（X4 冷却模式）：仅 L3（玩家可见区）逐 agent SetMovementDirection
    //   朝向 slot 目标，复用 X5SlotCooldown 冷却字典限频（p3-1 设计，键=agent.Index，OnAgentRemoved 清理）；
    //   X4 已解阵 agent（IsFormationFrameEnabled=false，Agent.cs:1445 已核实）跳过（红线 2 语义独立）。
    // Discipline 门控（x5-discipline-assigner-redundancy 落点）：MaintainSlots=false（Severe/Collapsed）
    //   → 跳过指派/刷新（交 F6 近战走位）。
    // MCM 开关：X5Enabled 总开关（默认开，注释声明 MCM「真实度」组接线点——无 MCM 静默默认开）；
    //   玩家队豁免沿用 P0-④ CombatBrainSettings.PlayerTeamExemptionEnabled。
    // Bench 回退闸（铁律②）：IndividualLegEnabled 静态开关，集成轮 [Bench] 个体段 avgAi 超 5ms 时置 false
    //   回退个体腿（仅编队纪律内手段保留）——清 x5-runtime-bench-debt。
    // 反作弊闸：只读己方编队聚合（角色/构成/中心/完整度）与公开地形，零读指针/全图/隐藏态。
    public sealed class FormationSlotGlue
    {
        public const float TickInterval = 0.5f; // 0.5s 编队级节拍（与 TickFormationTactics 同频）

        // X5 总开关（MCM「真实度」组接线点；无 MCM 静默默认开——t73 懒加载惯例）
        public static bool X5Enabled = true;

        // Bench 回退闸：500 人 [Bench] 个体段 avgAi 超 5ms → 集成轮置 false 回退个体腿（仅纪律内手段保留）
        public static bool IndividualLegEnabled = true;

        // 个体补位冷却（X4 FrameReleaseCooldown 同款：键=agent.Index，秒数制，OnAgentRemoved 清理）
        private readonly X5SlotCooldown _slotCooldown = new X5SlotCooldown();

        // 每编队上拍指派（SlotAssigner 稳定映射基准，防抖动；编队解散/纪律停止时清理）
        private readonly Dictionary<Formation, List<SlotAssignment>> _lastByFormation = new Dictionary<Formation, List<SlotAssignment>>();

        // atc4-snapshot-optimization OPT-2：每编队 agent 快照缓冲（一次遍历生成本拍全部聚合数据——
        //   编成统计/失效 Agent 收集/AgentEntry 构建/个体腿索引表；原四段各自遍历 GetAllUnits() 合并为一次；
        //   字段级复用（Clear 后重填）避免每拍 new List/HashSet/Dictionary）
        private readonly Dictionary<Formation, FormationAgents> _agentSnap = new Dictionary<Formation, FormationAgents>();

        // OPT-2：编队 agent 快照（单次遍历填充；Comp=角色计数 / FrameDisabled=X4 解阵索引集 /
        //   Entries=存活人类 AI 的 AgentRoleEntry / ByIndex=存活 agent 索引表（个体腿 O(1) 查表））
        private sealed class FormationAgents
        {
            public TroopComposition Comp = new TroopComposition();
            public readonly HashSet<int> FrameDisabled = new HashSet<int>();
            public readonly List<AgentRoleEntry> Entries = new List<AgentRoleEntry>();
            public readonly Dictionary<int, Agent> ByIndex = new Dictionary<int, Agent>();
        }

        private float _sinceTick;

        /// <summary>
        /// 0.5s 编队级驱动：X5 全链（归类→分流→布局→地形→纪律门控→指派→L3 个体补位）。
        /// getLod：agent LOD 查询（剧场闸）；getIntegrity：编队完整度（Discipline 门控输入）；
        /// terrain：TerrainSampler（可 null → TerrainWarp 降级不形变）。
        /// </summary>
        public void Tick(Mission mission, float dt, float now,
            Func<int, LodLevel> getLod, Func<Formation, float> getIntegrity, TerrainSampler terrain)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;
            if (!X5Enabled || mission == null) return;

            bool budgetOver = !IndividualLegEnabled; // 集成轮已回退个体腿 → 等价于超预算

            foreach (Team team in mission.Teams)
            {
                if (team == null || !team.MBTeam.IsValid) continue;
                if (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam) continue; // P0-④ 玩家队豁免

                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null)
                    {
                        RemoveFormation(f); // 空编队/解散：清 _lastByFormation（防字典滞留，行为等价）
                        continue;
                    }
                    // 骑兵/骑射编队走 F8/Skirmish，不进 X5 近战布局通道
                    if (f.QuerySystem.IsCavalryFormation || f.QuerySystem.IsRangedCavalryFormation) continue;

                    // ① 逐 agent 快照（OPT-2 合并：归类计数+解阵索引+条目+索引表一次遍历，0.5s 低频聚合，禁逐帧）
                    FormationAgents snap = BuildAgentSnap(f);
                    if (snap.Comp.Total == 0) continue;

                    // ② 分流（§五）：Ranged 为主 → RangedLoose；近战为主 → 消费 T2-01 结果
                    TacticsArrangement arrangement = MapArrangement(f.ArrangementOrder);
                    LayoutKind kind = SlotLayout.SelectLayout(snap.Comp, arrangement);

                    // ③ 布局生成（X5-2）+ 地形形变（X5-3）
                    SlotLayoutInput layoutInput = SlotLayoutInput.Default(snap.Comp, arrangement);
                    layoutInput.CenterX = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian).x;
                    layoutInput.CenterY = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian).y;
                    layoutInput.FacingX = f.Direction.x;
                    layoutInput.FacingY = f.Direction.y;
                    layoutInput.TerrainHeightBias = terrain != null ? terrain.Height(layoutInput.CenterX, layoutInput.CenterY) : 0f;
                    List<FormationSlot> slots = SlotLayout.Generate(layoutInput);
                    if (terrain != null) slots = TerrainWarp.Warp(slots, BuildTerrainContext(terrain, layoutInput));

                    // ④ 纪律门控（X5-5）：Severe/Collapsed 停止维护 → 跳过指派/刷新（x5-discipline-assigner-redundancy）
                    float integrity = getIntegrity != null ? getIntegrity(f) : 100f;
                    DisciplineOutput discipline = FormationDisciplineController.Evaluate(integrity);
                    if (!X5ExecutionPolicy.ShouldRefreshSlots(discipline))
                    {
                        _lastByFormation.Remove(f); // 停刷：清上拍缓存（交 F6 近战走位）
                        continue;
                    }

                    // ⑤ 稳定指派（X5-4）：上拍缓存 + X4 已解阵跳过集（OPT-2：快照已含 frameDisabled/entries）
                    List<SlotAssignment> last;
                    _lastByFormation.TryGetValue(f, out last);
                    var assignInput = new SlotAssignInput
                    {
                        Slots = slots,
                        Agents = snap.Entries,
                        Last = last,
                        Integrity = integrity,
                        FrameDisabledAgents = snap.FrameDisabled
                    };
                    List<SlotAssignment> assignments = SlotAssigner.Assign(assignInput);
                    _lastByFormation[f] = assignments;

                    // ⑥ 执行腿 (b)：L3 个体补位（冷却 + 未解阵 + 未豁免 + 未超预算；OPT-2 复用快照索引表/解阵集）
                    RunIndividualLeg(f, slots, assignments, now, getLod, budgetOver, snap.ByIndex, snap.FrameDisabled);
                }
            }
        }

        // Agent 生命周期清理（CombatAiMissionLogic.OnAgentRemoved 挂点）
        public void RemoveAgent(int agentIndex)
        {
            _slotCooldown.Remove(agentIndex);
        }

        // 编队解散/重排时清理（防字典只增不减；OPT-2：同步清 agent 快照缓冲）
        public void RemoveFormation(Formation f)
        {
            if (f != null)
            {
                _lastByFormation.Remove(f);
                _agentSnap.Remove(f);
            }
        }

        // ---- 执行腿 (b)：仅 L3 逐 agent 微调（SetMovementDirection 朝向 slot）----

        private void RunIndividualLeg(Formation f, List<FormationSlot> slots, List<SlotAssignment> assignments,
            float now, Func<int, LodLevel> getLod, bool budgetOver,
            Dictionary<int, Agent> agentByIndex, HashSet<int> frameDisabled)
        {
            // OPT-2：agentByIndex/frameDisabled 由 BuildAgentSnap 单次遍历提供（原每编队再遍历建表，O(M) 合并）

            for (int i = 0; i < assignments.Count; i++)
            {
                SlotAssignment a = assignments[i];
                if (a.SlotIndex < 0 || a.SlotIndex >= slots.Count) continue;
                Agent agent;
                if (!agentByIndex.TryGetValue(a.AgentIndex, out agent)) continue;
                if (agent == null || !agent.IsActive() || !agent.IsAIControlled || !agent.IsHuman) continue;

                // 剧场闸（硬性）：仅 L3（玩家可见区）执行个体腿，L0-L2 交原版/纪律内手段
                if (!X5ExecutionPolicy.ShouldRunIndividualLeg(
                    (int)getLod(a.AgentIndex), false, frameDisabled.Contains(a.AgentIndex), budgetOver)) continue;
                if (agent.IsRunningAway) continue; // 原版逃跑态尊重（与 MovementGlue 同语义）
                if (!_slotCooldown.CanRelease(a.AgentIndex, now)) continue; // 冷却限频

                FormationSlot slot = slots[a.SlotIndex];
                Vec2 dir = new Vec2(slot.WorldX - agent.Position.x, slot.WorldY - agent.Position.y);
                float len = (float)Math.Sqrt(dir.x * dir.x + dir.y * dir.y);
                if (len < 0.3f) continue; // 已到位（防微颤）
                dir.x /= len; dir.y /= len;
                agent.SetMovementDirection(dir); // 引擎级移动方向输入（MovementGlue.cs:223 先例）
                _slotCooldown.MarkReleased(a.AgentIndex, now);
            }
        }

        // ---- 编队聚合/映射辅助（反作弊闸：全部己方公开聚合）----

        // OPT-2：编队 agent 单次遍历快照——合并原 AggregateComposition（角色计数）/BuildAgentEntries
        //   （AgentRoleEntry 列表）/CollectFrameDisabled（X4 解阵索引集）/个体腿索引表 四段遍历为一次
        //   GetAllUnits() 扫描；字段级缓冲复用（Clear 后重填，零每拍 new 大容器）。
        private FormationAgents BuildAgentSnap(Formation f)
        {
            FormationAgents s;
            if (!_agentSnap.TryGetValue(f, out s))
            {
                s = new FormationAgents();
                _agentSnap[f] = s;
            }
            s.Comp = new TroopComposition();
            s.FrameDisabled.Clear();
            s.Entries.Clear();
            s.ByIndex.Clear();

            foreach (IFormationUnit unit in f.Arrangement.GetAllUnits())
            {
                Agent agent = unit as Agent;
                if (agent == null) continue;
                // X4 已解阵索引（对任意 agent，与原 CollectFrameDisabled 口径一致：不要求 active/human）
                if (!agent.IsFormationFrameEnabled) s.FrameDisabled.Add(agent.Index);
                // 编成统计/条目/索引表（与原 AggregateComposition/BuildAgentEntries/个体腿建表口径一致）
                if (!agent.IsActive() || !agent.IsHuman || !agent.IsAIControlled) continue;
                TroopRole role = TroopClassifier.Classify(BuildSoldierInput(agent));
                switch (role)
                {
                    case TroopRole.ShieldBearer: s.Comp.ShieldBearer++; break;
                    case TroopRole.Polearm: s.Comp.Polearm++; break;
                    case TroopRole.Ranged: s.Comp.Ranged++; break;
                    case TroopRole.HeavyMelee: s.Comp.HeavyMelee++; break;
                    case TroopRole.LightMelee: s.Comp.LightMelee++; break;
                    case TroopRole.Cavalry: s.Comp.Cavalry++; break;
                }
                s.Entries.Add(new AgentRoleEntry(agent.Index, role, agent.Position.x, agent.Position.y));
                s.ByIndex[agent.Index] = agent;
            }
            return s;
        }

        // T2-01 已选阵型 → 纯枚举（胶水层映射；不写原生 ArrangementOrder 到纯逻辑）
        private static TacticsArrangement MapArrangement(ArrangementOrder order)
        {
            if (order == ArrangementOrder.ArrangementOrderShieldWall) return TacticsArrangement.ShieldWall;
            return TacticsArrangement.Line;
        }

        // TerrainWarp 输入契约（从 TerrainSampler 映射；坡度=高度梯度近似，Uphill=最陡上坡方向）
        private static TerrainContext BuildTerrainContext(TerrainSampler terrain, SlotLayoutInput layoutInput)
        {
            var ctx = new TerrainContext();
            float step = terrain.Step > 0f ? terrain.Step : 4f;
            ctx.Height = terrain.Height;
            ctx.Slope = (x, y) =>
            {
                float sh0 = terrain.Height(x, y);
                float shx = terrain.Height(x + step, y);
                float shy = terrain.Height(x, y + step);
                float sgx = (shx - sh0) / step;
                float sgy = (shy - sh0) / step;
                return (float)(Math.Atan(Math.Sqrt(sgx * sgx + sgy * sgy)) * 180.0 / Math.PI); // 坡度角（度）
            };
            // 坡顶方向：最陡上坡方向（高度梯度方向）
            float h0 = terrain.Height(layoutInput.CenterX, layoutInput.CenterY);
            float hx = terrain.Height(layoutInput.CenterX + step, layoutInput.CenterY);
            float hy = terrain.Height(layoutInput.CenterX, layoutInput.CenterY + step);
            float gx = (hx - h0) / step;
            float gy = (hy - h0) / step;
            float glen = (float)Math.Sqrt(gx * gx + gy * gy);
            if (glen > 1e-4f) { ctx.UphillX = gx / glen; ctx.UphillY = gy / glen; }
            else { ctx.UphillX = 0f; ctx.UphillY = 0f; }
            return ctx;
        }

        // 装备/等级/护甲 → SoldierInput（X5-1 输入；复用 CombatAiMissionLogic.BuildSoldierInput 等价口径）
        public static SoldierInput BuildSoldierInput(Agent agent)
        {
            var input = new SoldierInput();
            if (agent.HasMount) input.Kind = SoldierKind.Cavalry;
            else if (HasRangedWeapon(agent)) input.Kind = SoldierKind.Ranged;
            else input.Kind = SoldierKind.Infantry;

            input.Level = agent.Character != null ? agent.Character.Level : 3;
            input.Armor = ClassifyArmorGrade(agent);
            input.IsVeteran = (agent.Character != null && agent.Character.IsHero) || input.Level >= 4;
            input.HasShield = HasShield(agent);
            input.PrimaryWeaponClass = PrimaryWeaponClassOf(agent);
            return input;
        }

        private static bool HasShield(Agent agent)
        {
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon w = agent.Equipment[i];
                if (w.IsEmpty || w.CurrentUsageItem == null) continue;
                if (w.CurrentUsageItem.IsShield) return true;
            }
            return false;
        }

        private static PrimaryWeaponClass PrimaryWeaponClassOf(Agent agent)
        {
            try
            {
                EquipmentIndex idx = agent.GetPrimaryWieldedItemIndex();
                if (idx < EquipmentIndex.WeaponItemBeginSlot) return PrimaryWeaponClass.None;
                MissionWeapon w = agent.Equipment[idx];
                if (w.IsEmpty || w.CurrentUsageItem == null) return PrimaryWeaponClass.None;
                switch (w.CurrentUsageItem.WeaponClass)
                {
                    case WeaponClass.OneHandedSword:
                    case WeaponClass.OneHandedAxe:
                    case WeaponClass.Mace:
                    case WeaponClass.Pick:
                        return PrimaryWeaponClass.OneHanded;
                    case WeaponClass.TwoHandedSword:
                    case WeaponClass.TwoHandedAxe:
                    case WeaponClass.TwoHandedMace:
                        return PrimaryWeaponClass.TwoHanded;
                    case WeaponClass.OneHandedPolearm:
                    case WeaponClass.TwoHandedPolearm:
                    case WeaponClass.LowGripPolearm:
                        return PrimaryWeaponClass.Polearm;
                    case WeaponClass.Bow:
                        return PrimaryWeaponClass.Bow;
                    case WeaponClass.Crossbow:
                        return PrimaryWeaponClass.Crossbow;
                    case WeaponClass.Javelin:
                    case WeaponClass.ThrowingAxe:
                    case WeaponClass.ThrowingKnife:
                        return PrimaryWeaponClass.Thrown;
                    default:
                        return PrimaryWeaponClass.None;
                }
            }
            catch
            {
                return PrimaryWeaponClass.None;
            }
        }

        private static bool HasRangedWeapon(Agent agent)
        {
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon w = agent.Equipment[i];
                if (w.IsEmpty || w.CurrentUsageItem == null) continue;
                if (w.CurrentUsageItem.IsRangedWeapon) return true;
            }
            return false;
        }

        private static ArmorGrade ClassifyArmorGrade(Agent agent)
        {
            try
            {
                EquipmentElement body = agent.SpawnEquipment[EquipmentIndex.Body];
                if (body.IsEmpty || body.Item == null || body.Item.ArmorComponent == null)
                    return ArmorGrade.Cloth;
                switch (body.Item.ArmorComponent.MaterialType)
                {
                    case ArmorComponent.ArmorMaterialTypes.Plate: return ArmorGrade.Plate;
                    case ArmorComponent.ArmorMaterialTypes.Chainmail: return ArmorGrade.Chainmail;
                    case ArmorComponent.ArmorMaterialTypes.Leather: return ArmorGrade.Leather;
                    default: return ArmorGrade.Cloth;
                }
            }
            catch
            {
                return ArmorGrade.Cloth;
            }
        }
    }
}
