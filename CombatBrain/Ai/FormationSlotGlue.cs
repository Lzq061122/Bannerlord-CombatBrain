using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using CombatBrain.Combat;
using CombatBrain.Formations;
using CombatBrain.Lod;
using CombatBrain.Perception;

namespace CombatBrain.Ai
{
    // X5-6 编队级槽位快照胶水（FormationSlotGlue）—— CB-PROMPT-0038 §五.7 退役后形态。
    //
    // 保留（编队级、纯观察性）：0.5s 节拍下把 X5-1..X5-5 纯逻辑链跑成一份**编队级快照**：
    //   TroopClassifier 归类（聚合计数）→ TroopComposition 分流 → SlotLayout.Generate（三布局）
    //   → TerrainWarp.Warp（地形形变）→ FormationDisciplineController 门控 → SlotAssigner.Assign（agent↔slot 绑定表）。
    //   **本层不再有任何 Agent 写入，也不产出逐兵导航目标**：绑定表仅作编队级观察/后续合法通道消费。
    //
    // 退役（逐兵执行腿，已删除）：原「执行腿 (b) L3 个体补位」——逐 agent 计算朝向 slot 的方向并调用
    //   Agent.SetMovementDirection，配套 X5SlotCooldown 逐 agent 冷却、Bench 回退闸 IndividualLegEnabled、
    //   以及 X4 解阵跳过集（SlotAssigner 的 FrameDisabledAgents）。具体怎么走到 slot 交还原版 AI。
    //   （原执行腿 (a)「编队纪律内手段」本就不下命令，保持：不 SetArrangementOrder，动阵型归 T2-01 领地。）
    //
    // 玩家队豁免（P0-④）：豁免开启时玩家队整体不进入本层（不计算、不消费）；豁免关闭时也只做编队级观察。
    // MCM 开关：X5Enabled 总开关（默认开，无 MCM 静默默认开）。
    // 反作弊闸：只读己方编队聚合（角色/构成/中心/完整度/公开装备）与公开地形，零读指针/全图/隐藏态。
    public sealed class FormationSlotGlue
    {
        public const float TickInterval = 0.5f; // 0.5s 编队级节拍（与 TickFormationTactics 同频）

        // X5 总开关（MCM「真实度」组接线点；无 MCM 静默默认开——t73 懒加载惯例）
        public static bool X5Enabled = true;

        // 每编队上拍指派（SlotAssigner 稳定映射基准，防抖动；编队解散/纪律停止时清理）
        private readonly Dictionary<Formation, List<SlotAssignment>> _lastByFormation = new Dictionary<Formation, List<SlotAssignment>>();

        // atc4-snapshot-optimization OPT-2：每编队 agent 快照缓冲（一次遍历生成本拍聚合数据——
        //   编成统计 + AgentRoleEntry 构建；字段级复用（Clear 后重填）避免每拍 new List/字典）
        private readonly Dictionary<Formation, FormationAgents> _agentSnap = new Dictionary<Formation, FormationAgents>();

        // OPT-2：编队 agent 快照（单次遍历填充；Comp=角色计数 / Entries=存活人类 AI 的 AgentRoleEntry）
        private sealed class FormationAgents
        {
            public TroopComposition Comp = new TroopComposition();
            public readonly List<AgentRoleEntry> Entries = new List<AgentRoleEntry>();
        }

        private float _sinceTick;

        /// <summary>
        /// 0.5s 编队级驱动：编队级槽位快照链（归类→分流→布局→地形→纪律门控→绑定表）。
        /// getIntegrity：编队完整度（Discipline 门控输入）；terrain：TerrainSampler（可 null → 降级不形变）。
        /// **无任何 Agent 写入**。
        /// </summary>
        public void Tick(Mission mission, float dt, float now,
            Func<Formation, float> getIntegrity, TerrainSampler terrain)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;
            if (!X5Enabled || mission == null) return;

            foreach (Team team in mission.Teams)
            {
                if (team == null || !team.MBTeam.IsValid) continue;
                if (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam) continue; // P0-④ 玩家队豁免

                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null)
                    {
                        RemoveFormation(f); // 空编队/解散：清缓存（防字典滞留，行为等价）
                        continue;
                    }
                    // 骑兵/骑射编队走 F8/Skirmish，不进 X5 近战布局通道
                    if (f.QuerySystem.IsCavalryFormation || f.QuerySystem.IsRangedCavalryFormation) continue;

                    // ① 编队级聚合快照（归类计数 + 条目，0.5s 一次；禁逐帧）
                    FormationAgents snap = BuildAgentSnap(f);
                    if (snap.Comp.Total == 0) continue;

                    // ② 分流（§五）：Ranged 为主 → RangedLoose；近战为主 → 消费 T2-01 结果
                    TacticsArrangement arrangement = MapArrangement(f.ArrangementOrder);

                    // ③ 布局生成（X5-2）+ 地形形变（X5-3）
                    SlotLayoutInput layoutInput = SlotLayoutInput.Default(snap.Comp, arrangement);
                    layoutInput.CenterX = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian).x;
                    layoutInput.CenterY = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian).y;
                    layoutInput.FacingX = f.Direction.x;
                    layoutInput.FacingY = f.Direction.y;
                    layoutInput.TerrainHeightBias = terrain != null ? terrain.Height(layoutInput.CenterX, layoutInput.CenterY) : 0f;
                    List<FormationSlot> slots = SlotLayout.Generate(layoutInput);
                    if (terrain != null) slots = TerrainWarp.Warp(slots, BuildTerrainContext(terrain, layoutInput));

                    // ④ 纪律门控（X5-5）：Severe/Collapsed 停止维护 → 跳过刷新（x5-discipline-assigner-redundancy）
                    float integrity = getIntegrity != null ? getIntegrity(f) : 100f;
                    DisciplineOutput discipline = FormationDisciplineController.Evaluate(integrity);
                    if (!X5ExecutionPolicy.ShouldRefreshSlots(discipline))
                    {
                        _lastByFormation.Remove(f); // 停刷：清上拍缓存
                        continue;
                    }

                    // ⑤ 稳定绑定表（X5-4，纯观察性）：上拍缓存防抖动；不产生任何写入/导航目标
                    List<SlotAssignment> last;
                    _lastByFormation.TryGetValue(f, out last);
                    var assignInput = new SlotAssignInput
                    {
                        Slots = slots,
                        Agents = snap.Entries,
                        Last = last,
                        Integrity = integrity
                    };
                    _lastByFormation[f] = SlotAssigner.Assign(assignInput);
                }
            }
        }

        // 编队解散/重排时清理（防字典只增不减）
        public void RemoveFormation(Formation f)
        {
            if (f != null)
            {
                _lastByFormation.Remove(f);
                _agentSnap.Remove(f);
            }
        }

        // ---- 编队聚合/映射辅助（反作弊闸：全部己方公开聚合）----

        // OPT-2：编队 agent 单次遍历快照（角色计数 + AgentRoleEntry；字段级缓冲复用）
        private FormationAgents BuildAgentSnap(Formation f)
        {
            FormationAgents s;
            if (!_agentSnap.TryGetValue(f, out s))
            {
                s = new FormationAgents();
                _agentSnap[f] = s;
            }
            s.Comp = new TroopComposition();
            s.Entries.Clear();

            foreach (IFormationUnit unit in f.Arrangement.GetAllUnits())
            {
                Agent agent = unit as Agent;
                if (agent == null) continue;
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
