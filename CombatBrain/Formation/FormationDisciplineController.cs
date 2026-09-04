namespace CombatBrain.Formations
{
    // X5-5 阵型纪律控制（FormationDisciplineController）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 语义：直接调 FormationIntegrity.Grade()，输出"站位维持强度"（设计稿 §七 四态），
    //   供 X5-4 SlotAssigner / X5-2 SlotLayout 消费（X5-6 胶水挂接 TickFormationSlots 时接线）。
    // 四态（§七 逐条照抄；阈值来自 FormationIntegrity 常量，只消费不改）：
    //   Full（≥80）            严格维持 Slot——刷新周期正常（0.5s）、指派稳定（MaxRankDiff=0 严格同 Rank）；
    //   Slight（60~79）        允许局部补位——Rank 邻位互换（MaxRankDiff=1），不重新生成布局；
    //   Moderate（40~59）      降低约束——Slot 目标加噪声（NoiseAmplitude>0）、降刷新优先级（Refresh 变慢），
    //                          允许局部变形（MaxRankDiff=1）；
    //   Severe+Collapsed（<40）停止维护——MaintainSlots=false（停刷 Slot 目标，交 F6 近战走位；
    //                          X4 已解阵语义独立保持，本层不触发解阵——红线 2）。
    // 只输出约束参数（刷新周期/噪声幅度/是否维护），不直接改布局、不触发解阵（X4 领地）。
    // 与 X5-4 衔接（low finding x5-assign-grade-collapse 门控）：DisciplineOutput.MaxRankDiff
    //   与 SlotAssigner 语义一致（Full=0 严格 / Slight=1 邻位）；Moderate 允许局部变形=1；
    //   Severe+Collapsed 停止维护（MaintainSlots=false），X5-4 在 X5-6 接线时经此门控不指派。
    // 反作弊闸：只消费己方编队完整度（公开聚合），无越权。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。
    public enum DisciplineState
    {
        Full = 0,          // ≥80：严格维持
        Slight = 1,        // 60~79：局部补位
        Moderate = 2,      // 40~59：降约束
        Stopped = 3        // <40（Severe+Collapsed）：停止维护
    }

    /// <summary>纪律输出（约束参数，供 SlotAssigner/SlotLayout 消费；X5-6 接线）。</summary>
    public struct DisciplineOutput
    {
        public DisciplineState State;   // 四态
        public bool MaintainSlots;      // 是否维护 Slot（Severe+Collapsed=false → 停止维护）
        public float RefreshInterval;   // 刷新周期（秒）：Full/Slight=0.5 正常、Moderate=1.0 降优先级、Stopped=0
        public float NoiseAmplitude;    // Slot 目标噪声（米）：Full/Slight=0、Moderate=0.25、Stopped=0
        public int MaxRankDiff;         // 供 SlotAssigner：Full=0（严格同 Rank）、Slight/Moderate=1（邻位互换/局部变形）
    }

    public static class FormationDisciplineController
    {
        // 刷新周期（秒）：与现有 Glue 0.5s 编队级节拍同频；Moderate 降为 1.0s（降刷新优先级）
        public const float FullRefreshInterval = 0.5f;
        public const float ModerateRefreshInterval = 1.0f;
        // Moderate 允许局部变形：Slot 目标加噪声（米）
        public const float ModerateNoiseAmplitude = 0.25f;

        /// <summary>
        /// Grade() → 四态约束参数。只消费 FormationIntegrity.Grade()（阈值表 80/60/40 勿改，只读）。
        /// </summary>
        public static DisciplineOutput Evaluate(float integrity)
        {
            FormationIntegrityGrade grade = FormationIntegrity.Grade(integrity);
            switch (grade)
            {
                case FormationIntegrityGrade.Full:
                    return new DisciplineOutput
                    {
                        State = DisciplineState.Full,
                        MaintainSlots = true,
                        RefreshInterval = FullRefreshInterval,
                        NoiseAmplitude = 0f,
                        MaxRankDiff = 0 // 严格维持：只补同 Rank（与 SlotAssigner Full 分支一致）
                    };
                case FormationIntegrityGrade.Slight:
                    return new DisciplineOutput
                    {
                        State = DisciplineState.Slight,
                        MaintainSlots = true,
                        RefreshInterval = FullRefreshInterval,
                        NoiseAmplitude = 0f,
                        MaxRankDiff = 1 // 局部补位：Rank 邻位互换，不重新生成布局
                    };
                case FormationIntegrityGrade.Moderate:
                    return new DisciplineOutput
                    {
                        State = DisciplineState.Moderate,
                        MaintainSlots = true,
                        RefreshInterval = ModerateRefreshInterval, // 降刷新优先级
                        NoiseAmplitude = ModerateNoiseAmplitude,   // 允许局部变形
                        MaxRankDiff = 1
                    };
                default: // Severe + Collapsed（<40）
                    return new DisciplineOutput
                    {
                        State = DisciplineState.Stopped,
                        MaintainSlots = false,  // 停止维护：停刷 Slot 目标，交 F6 近战走位
                        RefreshInterval = 0f,
                        NoiseAmplitude = 0f,
                        MaxRankDiff = 0
                    };
            }
        }
    }
}
