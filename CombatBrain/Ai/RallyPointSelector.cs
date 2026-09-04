namespace CombatBrain.Ai
{
    // T6-01 骑兵重整点选址（优先级链 + 距离档位）—— 纯逻辑判定（可离线测）。
    // 语义：骑兵 Recall/Reform 需要回撤/重整目标点；按优先级链选择（己方侧翼 > 主阵线后方 >
    //   安全距离区域 > 战术目标附近安全区），并校验候选点距主战线的距离档位（40~80m 正常 /
    //   80~120m 警告 / >120m 禁止——禁止档与过近档不可用）。胶水层（CavalryGlue）负责
    //   从可观察聚合（TeamGeometry 编队中心/阵线/敌我距离）构造候选并映射到实际坐标。
    // CavalryWaveFSM 纯逻辑零改动（本类只提供选址输入）。
    public enum RallyPriority
    {
        Flank = 0,        // ① 己方侧翼（最高优先）
        BehindLine = 1,   // ② 己方主阵线后方
        SafeDistance = 2, // ③ 安全距离区域
        NearTarget = 3,   // ④ 当前战术目标附近的安全区域
        None = 4          // 无安全点（降级）
    }

    public enum RallyBand
    {
        TooClose = 0,   // <40m（过近，不视为重整点）
        Normal = 1,     // 40~80m（正常档）
        Warning = 2,    // 80~120m（警告档）
        Forbidden = 3   // >120m（禁止长期游荡）
    }

    // 候选重整点（纯数值聚合：优先级 + 距主战线距离，胶水层构造）
    public struct RallyCandidate
    {
        public RallyPriority Priority;
        public float DistanceToMainLine;
    }

    public static class RallyPointSelector
    {
        // ---- 距离档位阈值（源《骑马与砍杀 II》战术 AI Mod.md §T6-01 建议距离）----
        public const float NormalMin = 40f;   // 重整点距主战线 40~80m 正常
        public const float NormalMax = 80f;
        public const float WarningMax = 120f; // 80~120m 警告；>120m 禁止

        /// <summary>距离档位：>120 禁止 / 80~120 警告 / 40~80 正常 / <40 过近。</summary>
        public static RallyBand ClassifyBand(float distanceToMainLine)
        {
            if (distanceToMainLine > WarningMax) return RallyBand.Forbidden;
            if (distanceToMainLine > NormalMax) return RallyBand.Warning;
            if (distanceToMainLine >= NormalMin) return RallyBand.Normal;
            return RallyBand.TooClose;
        }

        /// <summary>可用档位 = 正常/警告（禁止与过近档不可作为重整点）。</summary>
        public static bool IsUsable(RallyBand band)
        {
            return band == RallyBand.Normal || band == RallyBand.Warning;
        }

        /// <summary>
        /// 按优先级链选最优可用候选（Flank > BehindLine > SafeDistance > NearTarget），
        /// 只接受可用档位（Normal/Warning）；全不可用 → Priority=None（无安全点降级）。
        /// </summary>
        public static RallyCandidate SelectBest(RallyCandidate[] candidates)
        {
            if (candidates == null) return new RallyCandidate { Priority = RallyPriority.None };
            for (int p = (int)RallyPriority.Flank; p <= (int)RallyPriority.NearTarget; p++)
            {
                foreach (RallyCandidate c in candidates)
                {
                    if ((int)c.Priority == p && IsUsable(ClassifyBand(c.DistanceToMainLine)))
                        return c;
                }
            }
            return new RallyCandidate { Priority = RallyPriority.None };
        }
    }
}
