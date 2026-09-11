using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using CombatBrain.Ranged;

namespace CombatBrain.Ai
{
    // F9 远程目标/弹药取舍 —— 编队级只读快照层（CB-PROMPT-0038 §五.6 退役后形态）。
    //
    // 退役内容（本类原有，已全部删除）：
    //   * 周期性逐兵 Agent.SetTargetAgent(target)   —— 逐兵选敌交还原版 AI；
    //   * 逐兵 Agent.TryToWieldWeaponInSlot(...)    —— 何时改近战交还原版 AI；
    //   * 逐兵 Agent.SetMovementDirection(dir) 后撤 —— 个体移动交还原版 AI；
    //   * 每 agent 状态字典（_lastJitterTime/_fallingBack）、F12 抖动闸门、可复用目标数组。
    //
    // 保留内容（编队级判断，派单 §五.6「保留编队级远程任务判断」）：
    //   * 按编队统计远程兵弹药余量（只读公开装备/弹药数据）；
    //   * RangedBrain.DecideFallback 给出编队级取舍建议（是否继续射击 / 是否整体后撤 / 是否转换任务）；
    //   * 结果仅写入日志与编队级快照，**不产生任何 Agent 写入，也不下任何命令**。
    //
    // 纪律（三条铁律 + 派单 §二）：
    //   * 0.3s 低频编队级节拍，O(编队数 × 远程成员装备槽)，不逐帧、不逐兵决策；
    //   * 反作弊闸：只读己方编队公开装备与公开弹药量，零读指针/全图/隐藏态；
    //   * 玩家队豁免：与其余编队层同口径（P0-④ MCM 开关），豁免开启时玩家队不进入本层。
    //
    // 后续接线说明（不在本任务范围）：若需要把编队级取舍真正落地为「整体后撤 / 转换任务」，
    //   应经 CPS-1 唯一执行器的编队命令提案通道表达（合法编队命令），不得恢复逐兵写入。
    public sealed class RangedGlue
    {
        public const float TickInterval = 0.3f;
        private const float ReportInterval = 10f; // 编队级快照日志间隔（防刷屏）

        private float _sinceTick;
        private float _lastReportTime = float.MinValue; // 日志节拍用真实战斗时间基准（P0-① 口径：不平摊帧 dt）

        // 编队级快照（键 = Formation，值 = 0..1 弹药余量）：仅用于日志/诊断与后续合法通道消费
        private readonly Dictionary<Formation, float> _formationAmmoRatio = new Dictionary<Formation, float>();

        /// <summary>
        /// 低频驱动入口：CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// 只做编队级只读统计；**不调用任何 Agent 写入 API**。
        /// </summary>
        /// <param name="mission">当前 Mission</param>
        /// <param name="dt">帧时间</param>
        /// <param name="now">战斗累计时间（日志节拍基准）</param>
        public void Tick(Mission mission, float dt, float now)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;
            if (mission == null) return;

            _formationAmmoRatio.Clear();

            foreach (Team team in mission.Teams)
            {
                if (team == null || !team.MBTeam.IsValid) continue;
                if (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam) continue; // P0-④ 玩家队豁免

                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;

                    // 编队级远程兵比例：无远程兵 → 不是远程任务编队，跳过
                    // （FormationQuerySystem 公开聚合字段，v1.4.8 已核实：RangedUnitRatio/RangedCavalryUnitRatio）
                    float rangedRatio = f.QuerySystem.RangedUnitRatio + f.QuerySystem.RangedCavalryUnitRatio;
                    if (rangedRatio <= 0.05f) continue;

                    // 弹药余量：编队内远程成员的弹药槽合计（只读公开装备数据，聚合口径）
                    float ratio = ComputeFormationAmmoRatio(f);
                    _formationAmmoRatio[f] = ratio;

                    // 编队级取舍建议（纯逻辑）：是否继续射击 / 转换任务 / 整体后撤
                    // inMeleeRange 用引擎公开编队旗标 IsMeleeFormation（已转近战编队态），
                    // 语义等价旧逐兵 MeleeRange 判定，但改为编队级读取（FormationQuerySystem.cs 已核实）。
                    bool inMeleeRange = f.QuerySystem.IsMeleeFormation;
                    RangedFallback fallback = RangedBrain.DecideFallback(ratio, inMeleeRange);
                    if (fallback != RangedFallback.KeepShooting)
                    {
                        InGameFeedback.Count("远程编队取舍"); // 编队级聚合反馈（防逐兵刷屏）
                    }
                }
            }

            // 日志节拍按真实战斗时间（now）判定，不按帧 dt 累加（P0-① 口径）
            if (_lastReportTime == float.MinValue || now - _lastReportTime >= ReportInterval)
            {
                Report();
                _lastReportTime = now;
            }
        }

        /// <summary>编队级只读查询：上次快照的弹药余量（无记录 → 1=不缺弹）。</summary>
        public float FormationAmmoRatio(Formation f)
        {
            float v;
            if (f != null && _formationAmmoRatio.TryGetValue(f, out v)) return v;
            return 1f;
        }

        /// <summary>Mission/编队生命周期清理（防字典只增不减）。</summary>
        public void RemoveFormation(Formation f)
        {
            if (f != null) _formationAmmoRatio.Remove(f);
        }

        // 编队弹药余量：遍历编队成员的弹药槽合计（Amount / ModifiedMaxAmount）。
        // 只读聚合，不做任何写入；成员筛选与 X5 快照同口径（活体人类）。
        private static float ComputeFormationAmmoRatio(Formation f)
        {
            int total = 0;
            int max = 0;
            foreach (IFormationUnit unit in f.Arrangement.GetAllUnits())
            {
                Agent agent = unit as Agent;
                if (agent == null || !agent.IsActive() || !agent.IsHuman) continue;
                for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
                {
                    MissionWeapon w = agent.Equipment[i];
                    if (w.IsEmpty || w.Item == null || w.Item.PrimaryWeapon == null) continue;
                    if (!w.IsAnyConsumable()) continue;
                    max += w.ModifiedMaxAmount;
                    total += w.Amount;
                }
            }
            if (max <= 0) return 1f; // 无弹药槽（如纯近战）：视为不缺弹，不产生取舍信号
            float ratio = (float)total / (float)max;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }

        private void Report()
        {
            if (_formationAmmoRatio.Count == 0) return;
            // 编队级快照：只输出存在缺弹取舍风险的编队（≤阈值），禁每拍每队刷日志（规格 §14 口径）
            foreach (KeyValuePair<Formation, float> kv in _formationAmmoRatio)
            {
                if (kv.Value > RangedBrain.AmmoLowThreshold) continue;
                CombatLog.Log(string.Format(
                    "[F9] 编队 {0} 弹药余量 {1:0.00} → 编队级取舍={2}（不逐兵写入，交原版 AI 执行）",
                    kv.Key.Index, kv.Value, RangedBrain.DecideFallback(kv.Value, false)));
            }
        }
    }
}
