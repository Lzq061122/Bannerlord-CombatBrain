namespace CombatBrain.Ai
{
    // T8-01 骑射手保存有生力量（五档兵力分档）—— 纯逻辑判定（可离线测）。
    // 语义：骑射编队按「当前兵力 / 初始兵力」比例分档，低档位减少接战/后撤/优先保存/撤离，
    //   避免残局把珍贵骑射送掉（用户实测：骑射开局送死已修，残局保存未做）。
    //   与个体级 SurvivalGlue 分域：SurvivalGlue 管濒死个体靠友军，本项管编队兵力比例决策。
    //   只对骑射编队（IsRangedCavalryFormation，与 Skirmish 同域）；胶水层（CombatAiMissionLogic）
    //   0.5s 编队级驱动（反作弊闸：CountOfUnits 公开可观察，禁全图禁个体）。
    public enum PreservationBand
    {
        Normal = 0,                // >70% 正常
        Cautious = 1,              // 50~70% 谨慎
        ReduceEngagement = 2,      // 30~50% 减接战
        PrioritizePreservation = 3,// 20~30% 优先保存
        Withdraw = 4               // <20% 撤离
    }

    // 分档 → 行为建议（胶水层据此驱动；低档位配合 SkirmishGlue Withdraw 模式）
    public enum PreservationAction
    {
        None = 0,                 // 正常：现状
        ReduceFrontal = 1,        // 谨慎：减少正面接战
        StrengthenWithdraw = 2,   // 减接战：加强 Withdraw
        SignificantRetreat = 3,   // 优先保存：显著后撤
        Disengage = 4             // 撤离：脱离战场或交原版
    }

    public static class UnitPreservation
    {
        // ---- 兵力分档阈值（源《骑马与砍杀 II》战术 AI Mod.md §T8-01）----
        public const float NormalThreshold = 0.70f;   // >70% 正常
        public const float CautiousThreshold = 0.50f; // 50~70% 谨慎
        public const float ReduceThreshold = 0.30f;   // 30~50% 减接战
        public const float PreserveThreshold = 0.20f; // 20~30% 优先保存；<20% 撤离

        /// <summary>按当前/初始兵力比例分档（ratio = current / initial，0~1）。</summary>
        public static PreservationBand ClassifyBand(float ratio)
        {
            if (ratio > NormalThreshold) return PreservationBand.Normal;
            if (ratio > CautiousThreshold) return PreservationBand.Cautious;
            if (ratio > ReduceThreshold) return PreservationBand.ReduceEngagement;
            if (ratio > PreserveThreshold) return PreservationBand.PrioritizePreservation;
            return PreservationBand.Withdraw;
        }

        /// <summary>分档 → 行为建议。</summary>
        public static PreservationAction ActionFor(PreservationBand band)
        {
            switch (band)
            {
                case PreservationBand.Cautious: return PreservationAction.ReduceFrontal;
                case PreservationBand.ReduceEngagement: return PreservationAction.StrengthenWithdraw;
                case PreservationBand.PrioritizePreservation: return PreservationAction.SignificantRetreat;
                case PreservationBand.Withdraw: return PreservationAction.Disengage;
                default: return PreservationAction.None;
            }
        }
    }
}
