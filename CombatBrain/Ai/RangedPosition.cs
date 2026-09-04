using System;

namespace CombatBrain.Ai
{
    // T3-01 射手射击阵地转移（7 种允许原因）—— 纯逻辑判定（可离线测）。
    // 语义：射手编队维护 CurrentPosition/TargetPosition；仅在源清单 §T3-01 的 7 种允许原因下
    //   主动转移阵地（其余情况保持阵地，不随机游荡）。胶水层（CombatAiMissionLogic.TickRangedPosition）
    //   负责编队遍历 + 只发 MovementOrderMove（不动 ArrangementOrder，避免与 t79 阵型三态互踩）。
    public enum RangedPositionReason
    {
        None = 0,             // 不允许转移（保持当前阵地）
        CavalryThreat = 1,    // ① 当前阵地受到骑兵威胁（敌骑≤45m，或 ≤70m 且数量≥5）
        FiringArcBlocked = 2, // ② 当前射界不足（射界比例过低）
        TargetGone = 3,       // ③ 当前目标消失（无敌队）
        BetterPosition = 4,   // ④ 新射击阵地明显更好（价值高出阈值）
        LineAdvance = 5,      // ⑤ 主阵线整体推进（推进距离超阈值）
        FormationBroken = 6,  // ⑥ 阵型被破坏（完整度低于阈值，t65）
        TerrainChange = 7     // ⑦ 地形价值明显变化（变化量超阈值）
    }

    // 阵地转移判定输入快照（纯数值聚合，胶水层采集）。-1 = 无该敌骑（哨兵值）。
    public struct RangedPositionInput
    {
        public float EnemyCavalryDistance;  // 敌骑最近距离（米，-1=无敌骑）
        public int EnemyCavalryCount;       // 敌骑数量
        public float FiringArcRatio;        // 射界比例（0~1，越低越不足）
        public bool HasTarget;              // 是否有当前目标（敌队存在）
        public float CurrentPositionValue;  // 当前阵地价值（射界/地形综合，后续接线）
        public float TargetPositionValue;   // 候选新阵地价值（后续接线）
        public float LineAdvanceDistance;   // 主阵线推进距离（米）
        public float FormationIntegrity;    // 编队完整度（0~100，t65）
        public float TerrainValueDelta;     // 地形价值变化量（绝对值，米）
    }

    public static class RangedPosition
    {
        // ---- 阈值集中定义（源《骑马与砍杀 II》战术 AI Mod.md §T3-01 建议阈值）----
        public const float CavalryDisengageDistance = 45f;   // ① 敌骑≤45m → 脱离
        public const float CavalryThreatDistance = 70f;      // ① 敌骑≤70m 且数量≥5 → 威胁
        public const float CavalryThreatCount = 5f;          // ① 敌骑数量阈值
        public const float FiringArcThreshold = 0.3f;        // ② 射界比例 <0.3 → 射界不足
        public const float BetterValueThreshold = 0.2f;      // ④ 新阵地价值高出 >0.2 → 明显更好
        public const float LineAdvanceThreshold = 15f;       // ⑤ 主阵线推进 >15m → 跟随推进
        public const float BrokenIntegrityThreshold = 50f;   // ⑥ 完整度 <50 → 阵型被破坏（t65）
        public const float TerrainValueDeltaThreshold = 20f; // ⑦ 地形价值变化 >20m → 明显变化
        public const float MoveHysteresis = 3f;              // 防抖：目标点漂移 >3m 才下发 Move（同 ProtectGlue）

        /// <summary>按优先级判定是否允许转移；返回触发原因，无允许原因 → None（保持阵地）。</summary>
        public static RangedPositionReason Decide(RangedPositionInput i)
        {
            // ① 骑兵威胁（最紧急，先判）
            if (i.EnemyCavalryDistance >= 0f && i.EnemyCavalryDistance <= CavalryDisengageDistance)
                return RangedPositionReason.CavalryThreat;
            if (i.EnemyCavalryDistance >= 0f && i.EnemyCavalryDistance <= CavalryThreatDistance
                && i.EnemyCavalryCount >= CavalryThreatCount)
                return RangedPositionReason.CavalryThreat;
            // ⑥ 阵型被破坏
            if (i.FormationIntegrity < BrokenIntegrityThreshold) return RangedPositionReason.FormationBroken;
            // ② 射界不足
            if (i.FiringArcRatio < FiringArcThreshold) return RangedPositionReason.FiringArcBlocked;
            // ③ 目标消失
            if (!i.HasTarget) return RangedPositionReason.TargetGone;
            // ⑦ 地形价值明显变化
            if (Math.Abs(i.TerrainValueDelta) > TerrainValueDeltaThreshold) return RangedPositionReason.TerrainChange;
            // ④ 新阵地明显更好
            if (i.TargetPositionValue - i.CurrentPositionValue > BetterValueThreshold) return RangedPositionReason.BetterPosition;
            // ⑤ 主阵线推进
            if (i.LineAdvanceDistance > LineAdvanceThreshold) return RangedPositionReason.LineAdvance;
            return RangedPositionReason.None;
        }

        /// <summary>3m 迟滞：状态切换（原因变化）或目标点漂移 >3m 才下发 Move。</summary>
        public static bool ShouldIssueMove(RangedPositionReason reason, RangedPositionReason lastReason, float driftMeters)
        {
            if (reason != lastReason) return true; // 原因切换立即下发
            return driftMeters > MoveHysteresis;   // 同原因仅漂移>3m 才下发
        }
    }
}
