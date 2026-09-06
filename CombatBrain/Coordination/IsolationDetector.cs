namespace CombatBrain.Coordination
{
    using CombatBrain.Battle;

    // ATC-3 Unauthorized Isolation 检测器（IsolationDetector）—— 纯逻辑（零 TaleWorlds 可离线测）。
    // §4.4：不能使用单一距离阈值，至少同时满足以下条件才成立：
    //   1. Formation 与 MainBody 的距离正在增加 或 已超过安全带；
    //   2. FriendlySupport 低于阈值；
    //   3. LocalForceRatio 不利，或 预计敌先到达（ArrivalGap 不利）；
    //   4. SeparationIntent=None；
    //   5. 当前不是 Retreat/Emergency；
    //   6. 当前 Phase 不是允许自由 Pursuit 的 Exploit（Exploit+Pursuit 合法远离不误报）。
    // 输出只是一项 CommitmentReason（供 ATC-4 消费）；本卡不下任何召回命令（ATC 规格 §4.4）。
    // 反作弊闸：输入=既有公开聚合（距离/支持/局部兵力比/到达差/阶段/意图），零新增禁读。
    public struct IsolationInput
    {
        public float MainBodyDistance;      // 与 MainBody 的距离（m）
        public bool DistanceIncreasing;     // 距离正在增加
        public float SupportScore;          // FriendlySupport 0..1（支援充足度）
        public float LocalForceRatio;       // 己方局部兵力 / 敌方局部兵力（>1 有利）
        public bool EnemyArrivesFirst;      // 预计敌先到达（ArrivalGap 不利）
        public BattlePhase Phase;           // 当前战局阶段
        public SeparationIntent Separation; // 分离意图（§3.3 八值）
        public bool Emergency;              // 紧急自保（Brace/Protect/脱困）
        public bool InRetreat;              // Retreat 阶段
    }

    public struct IsolationDecision
    {
        public bool UnauthorizedIsolation; // 判定结果（只作 CommitmentReason，不下命令）
        public bool DistanceFactor;        // 因子1：距离增加或超安全带
        public bool SupportFactor;         // 因子2：支援低于阈值
        public bool LocalFactor;           // 因子3：局部劣势或敌先到达
        public bool SeparationFactor;      // 因子4：SeparationIntent=None
        public bool NotRetreatFactor;      // 因子5：非 Retreat/Emergency
        public bool NotExploitFactor;      // 因子6：非 Exploit 自由 Pursuit
    }

    public static class IsolationDetector
    {
        public const float SafeDistanceMeters = 80f;  // 安全带：距 MainBody >80m 视为超带
        public const float InnerSafeMeters = 30f;     // 主力怀抱半径（isolation-fix）：≤30m 内编队贴着主力，
                                                      //   距离趋势不构成孤立——场景④尾声 dist=4m 实锤误报
                                                      //   （SupportMainEffort 编队在 MainBody 中心被判 UnauthorizedIsolation，
                                                      //   全场濒死时 Support/Ratio 三项同成立但编队从未远离）；
                                                      //   与 ReserveClassifier.ReserveRadiusMeters=30 同口径（靠近主力边界）
        public const float SupportThreshold = 0.3f;   // 支援阈值：SupportScore <0.3 视为无支援
        public const float RatioThreshold = 1.0f;     // 局部兵力比 <1.0 视为不利

        /// <summary>
        /// §4.4 判定（六因子全满足才 UnauthorizedIsolation）。
        /// isolation-fix（方向①：趋势因子加最小距离门槛，最小侵入）：
        ///   DistanceFactor = (距离 ≥30m 且 距离增加) 或 超安全带(>80m)——主力怀抱内（≤30m）编队即使
        ///   距离微小增加也不构成「远离」，防尾声误报（编队贴着主力但全场濒死致 Support/Ratio 因子误触发）；
        ///   Support/Local/Separation 因子不动；80 人案例（>80m）仍命中；合法 Intercept/Flank 远出仍由
        ///   Separation 因子豁免（不回归）。
        /// </summary>
        public static IsolationDecision Evaluate(IsolationInput input)
        {
            var d = new IsolationDecision();

            // 因子1：距离增加（仅安全带外 >30m 生效——≤30m 怀抱内趋势无意义，含界）或 超过安全带(>80m)
            d.DistanceFactor = (input.MainBodyDistance > InnerSafeMeters && input.DistanceIncreasing)
                || input.MainBodyDistance > SafeDistanceMeters;
            // 因子2：支援低于阈值
            d.SupportFactor = input.SupportScore < SupportThreshold;
            // 因子3：局部劣势 或 敌先到达
            d.LocalFactor = input.LocalForceRatio < RatioThreshold || input.EnemyArrivesFirst;
            // 因子4：SeparationIntent=None（无合法分离意图）
            d.SeparationFactor = input.Separation == SeparationIntent.None;
            // 因子5：非 Retreat/Emergency
            d.NotRetreatFactor = !input.InRetreat && !input.Emergency;
            // 因子6：非 Exploit（允许自由 Pursuit 的阶段不误报）
            d.NotExploitFactor = input.Phase != BattlePhase.Exploit;

            // §4.4：至少同时满足全部六项才成立
            d.UnauthorizedIsolation = d.DistanceFactor && d.SupportFactor && d.LocalFactor
                && d.SeparationFactor && d.NotRetreatFactor && d.NotExploitFactor;
            return d;
        }
    }
}
