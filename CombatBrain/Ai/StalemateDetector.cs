namespace CombatBrain.Ai
{
    // T18-01 僵局打破（检测 + 6 方案择优）—— 纯逻辑判定（可离线测）。
    // 检测：战斗>90s 且伤亡<5% 且战线移动<20m → 僵局（阈值源 §T18-01）。
    // 打破：重算 7 类态势（兵力比/骑兵比/远程比/地形/士气/阵线/距离），从 6 方案择优（源 §T18 列表：
    //   推进压迫/侧翼包抄/骑射骚扰/佯退引敌/占高地/集中火力），映射到现有 9 意图。
    // 本类只产出「建议意图」，不直接干预命令层（交给意图引擎/Glue 消费）。
    public enum StalematePlan
    {
        None = 0,
        AdvancePressure = 1,   // 推进压迫 → Attack
        FlankEnvelop = 2,      // 侧翼包抄 → Flank
        HorseArcherHarass = 3, // 骑射骚扰 → Harass
        FeignRetreat = 4,      // 佯退引敌 → Retreat
        OccupyHighGround = 5,  // 占高地 → Defend
        ConcentrateFire = 6    // 集中火力 → ProtectArcher
    }

    // 僵局判定/打破输入快照（纯数值聚合，胶水层采集）
    public struct StalemateInput
    {
        public float CombatDuration;    // 战斗时长（秒）
        public float CasualtyRate;      // 伤亡率 0~1
        public float FrontlineMovement; // 战线移动（米）
        // 7 类态势（打破决策输入）
        public float PowerRatio;        // 兵力比（我方/敌方，>1 优势）
        public float CavalryRatio;      // 骑兵比（我方骑兵占比 0~1）
        public float RangedRatio;       // 远程比（我方远程占比 0~1）
        public float TerrainAdvantage;  // 地形优势（米，己方-敌方）
        public float Morale;            // 士气（存活率代理 0~1）
        public float LineIntegrity;     // 阵线完整度 0~100（t65）
        public float Distance;          // 敌我距离（米）
    }

    public static class StalemateDetector
    {
        // ---- 阈值（源《骑马与砍杀 II》战术 AI Mod.md §T18-01）----
        public const float DurationThreshold = 90f;      // 战斗 >90s
        public const float CasualtyThreshold = 0.05f;    // 伤亡 <5%
        public const float FrontlineThreshold = 20f;     // 战线移动 <20m

        // ---- 打破决策阈值（态势择优）----
        public const float CavalryAdvantageThreshold = 0.30f;  // 骑兵比 >30%
        public const float RangedAdvantageThreshold = 0.40f;   // 远程比 >40%
        public const float TerrainAdvantageThreshold = 10f;    // 地形优势 >10m
        public const float PowerAdvantageThreshold = 1.20f;    // 兵力比 >1.2
        public const float MoraleLowThreshold = 0.50f;         // 士气 <0.5

        /// <summary>僵局判定：战斗>90s 且伤亡<5% 且战线移动<20m。</summary>
        public static bool IsStalemate(StalemateInput i)
        {
            return i.CombatDuration > DurationThreshold
                && i.CasualtyRate < CasualtyThreshold
                && i.FrontlineMovement < FrontlineThreshold;
        }

        /// <summary>
        /// 打破方案择优（源 §T18 处理：重算态势选一）：
        ///   骑兵优势→骑射为主骚扰/重骑为主侧翼；地形优势→占高地；远程优势→集中火力；
        ///   兵力优势→推进压迫；士气低→佯退引敌；默认→推进压迫。
        /// </summary>
        public static StalematePlan ChoosePlan(StalemateInput i)
        {
            if (i.CavalryRatio > CavalryAdvantageThreshold)
                return i.RangedRatio > RangedAdvantageThreshold ? StalematePlan.HorseArcherHarass : StalematePlan.FlankEnvelop;
            if (i.TerrainAdvantage > TerrainAdvantageThreshold) return StalematePlan.OccupyHighGround;
            if (i.RangedRatio > RangedAdvantageThreshold) return StalematePlan.ConcentrateFire;
            if (i.PowerRatio > PowerAdvantageThreshold) return StalematePlan.AdvancePressure;
            if (i.Morale < MoraleLowThreshold) return StalematePlan.FeignRetreat;
            return StalematePlan.AdvancePressure;
        }

        /// <summary>方案 → 现有 9 意图（不干预命令层，仅输出建议意图）。</summary>
        public static IntentType MapIntent(StalematePlan plan)
        {
            switch (plan)
            {
                case StalematePlan.FlankEnvelop: return IntentType.Flank;
                case StalematePlan.HorseArcherHarass: return IntentType.Harass;
                case StalematePlan.FeignRetreat: return IntentType.Retreat;
                case StalematePlan.OccupyHighGround: return IntentType.Defend;
                case StalematePlan.ConcentrateFire: return IntentType.ProtectArcher;
                case StalematePlan.AdvancePressure:
                default: return IntentType.Attack;
            }
        }
    }
}
