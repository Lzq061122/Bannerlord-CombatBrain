namespace CombatBrain.Coordination
{
    using CombatBrain.Battle;
    using CombatBrain.Intent;

    // ATC-3 Formation Mission 分配器（FormationMissionAssigner）—— 纯逻辑（零 TaleWorlds 可离线测）。
    // §9 第一版任务映射：
    //   主力步兵 → SupportMainEffort，Separation=None；
    //   远程 → SupportMainEffort 或 Screen（不得自动成独立 Main Effort）；
    //   近战骑兵 → Flank 或 Intercept（由队级 Intent 和阶段决定）；
    //   骑射 → Screen；Exploit 阶段方可 Pursuit；
    //   未投入且靠近 MainBody 的有效编队 → Reserve；
    //   Morale 已进入撤离 → Retreat。
    // 映射为纯函数可替换（输入=公开聚合，输出=任务+分离意图），不得把文化写成固定脚本（§9）。
    // 反作弊闸：输入=既有公开聚合（兵种旗标/阶段/意图/撤离标志/距 MainBody 距离），零新增禁读。
    public enum FormationMission
    {
        SupportMainEffort = 0,
        Screen = 1,
        Flank = 2,
        Intercept = 3,
        Reserve = 4,
        Pursuit = 5,
        Occupy = 6,
        Retreat = 7
    }

    /// <summary>任务映射输入（胶水层从 Formation.QuerySystem/阶段/意图/Morale 聚合填充）。</summary>
    public struct MissionInput
    {
        public FormationCoordKind Kind;   // 兵种（ATC-2 五类）
        public BattlePhase Phase;         // 当前战局阶段（ATC-1）
        public IntentType Intent;         // 队级意图（Pursue 触发追击）
        public bool InRetreat;            // Morale 已进入撤退（F11 Disengage/Reform）
        public bool IsReserve;            // 未投入且靠近 MainBody（胶水层按距离/参与判定）
    }

    /// <summary>任务映射输出。</summary>
    public struct MissionDecision
    {
        public FormationMission Mission;
        public SeparationIntent Separation; // §3.3 八值
    }

    // §3.3 SeparationIntent 枚举（八值；不新增 Delay/RearGuard/Block/Scout/Sacrifice——
    //   等真实消费者出现再追加，只追加到末尾）
    public enum SeparationIntent
    {
        None = 0,
        Screen = 1,
        Flank = 2,
        Intercept = 3,
        Reserve = 4,
        Pursuit = 5,
        Occupy = 6,
        Retreat = 7
    }

    /// <summary>
    /// P0-4 Reserve 判定输入（atc4-reserve-classifier）：胶水层从既有公开聚合填充。
    /// 语义：Reserve 需同时满足——非主动任务（推进/突击/主动支援意图）+ 未投入接敌 +
    ///   (显式预备队分配 或 距离辅助带内含迟滞)。距离只能作辅助不能单独决定（主动任务/接敌硬否决优先）；
    ///   MainBody 成员不因仅靠近中心成为 Reserve。
    /// </summary>
    public struct ReserveInput
    {
        public bool IsActiveTask;      // 处于推进/突击/主动支援任务（队级意图主动进攻）→ 硬否决
        public bool InContact;         // 已投入接敌（近战接触）→ 硬否决
        public bool ExplicitReserve;   // 显式预备队分配（第一版无命令源，胶水层恒 false；预留可靠角色依据接口）
        public float MainBodyDistance; // 距 MainBody 距离（m，辅助条件）
    }

    /// <summary>
    /// P0-4 Reserve 判定器（纯逻辑，零 TaleWorlds 可单测）：ATC-3（FormationMissionAssigner 输入构建）与
    ///   ATC-4（Commitment 输入构建）共用的唯一 Reserve 判定源（规格 §二 P0-4④：禁止复制两套表达式）。
    /// 距离只作辅助（带内才可能 Reserve），主动任务/接敌为硬否决；ExplicitReserve 显式分配直接 Reserve；
    ///   迟滞（HysteresisMeters）防位置微小变化造成 Reserve/非 Reserve 抖动。
    /// </summary>
    public static class ReserveClassifier
    {
        public const float ReserveRadiusMeters = 30f; // 预备带半径（辅助条件：进入 ≤30m 才可能 Reserve）
        public const float HysteresisMeters = 10f;    // 迟滞带（已 Reserve 需 >40m 才解除，防抖动）
        public const float ContactMeters = 20f;       // 接敌近似阈值（ATC-3 队级近似用；ATC-4 用 CommitmentContact 编队级）

        /// <summary>判定是否 Reserve（lastIsReserve=上拍结果，供迟滞；首拍 false）。</summary>
        public static bool IsReserve(ReserveInput input, bool lastIsReserve)
        {
            // 硬否决：推进/突击/主动支援任务（不属 MainEffort 的近似——主动任务=参与 MainEffort）
            if (input.IsActiveTask) return false;
            // 硬否决：已投入接敌
            if (input.InContact) return false;
            // 显式预备队分配 → Reserve（可靠角色依据；第一版无命令源恒 false，预留）
            if (input.ExplicitReserve) return true;
            // 无显式分配：距离仅辅助 + 迟滞防抖（lastIsReserve=true 时需超出半径+迟滞才解除）
            float threshold = lastIsReserve ? ReserveRadiusMeters + HysteresisMeters : ReserveRadiusMeters;
            return input.MainBodyDistance <= threshold;
        }

        /// <summary>队级意图是否主动（推进/突击/主动支援）——Reserve 硬否决输入之一。</summary>
        public static bool IsActiveIntent(IntentType intent)
        {
            return intent == IntentType.Attack || intent == IntentType.Flank
                || intent == IntentType.Harass || intent == IntentType.Pursue;
        }
    }

    public static class FormationMissionAssigner
    {
        /// <summary>§9 第一版任务映射（纯函数，可替换）。</summary>
        public static MissionDecision Assign(MissionInput input)
        {
            var d = new MissionDecision();
            // Morale 已进入撤离 → Retreat（最高优先，§9）
            if (input.InRetreat)
            {
                d.Mission = FormationMission.Retreat;
                d.Separation = SeparationIntent.Retreat;
                return d;
            }
            // 未投入且靠近 MainBody → Reserve（§9）
            if (input.IsReserve)
            {
                d.Mission = FormationMission.Reserve;
                d.Separation = SeparationIntent.Reserve;
                return d;
            }
            switch (input.Kind)
            {
                case FormationCoordKind.Ranged:
                    // 远程：SupportMainEffort 或 Screen（不得自动成独立 Main Effort，§9）
                    d.Mission = FormationMission.SupportMainEffort;
                    d.Separation = SeparationIntent.None;
                    return d;
                case FormationCoordKind.MeleeCavalry:
                    // 近战骑兵：Flank 或 Intercept（由队级 Intent 和阶段决定，§9）
                    // 进攻意图（Attack/Flank/Harass）→ Flank；防守意图（Defend/Brace/ProtectArcher）→ Intercept
                    d.Mission = IsAggressive(input.Intent) ? FormationMission.Flank : FormationMission.Intercept;
                    d.Separation = d.Mission == FormationMission.Flank ? SeparationIntent.Flank : SeparationIntent.Intercept;
                    return d;
                case FormationCoordKind.RangedCavalry:
                    // 骑射：Screen；Exploit 阶段方可 Pursuit（§9）
                    if (input.Phase == BattlePhase.Exploit && input.Intent == IntentType.Pursue)
                    {
                        d.Mission = FormationMission.Pursuit;
                        d.Separation = SeparationIntent.Pursuit;
                    }
                    else
                    {
                        d.Mission = FormationMission.Screen;
                        d.Separation = SeparationIntent.Screen;
                    }
                    return d;
                default:
                    // 主力步兵（Infantry/Other）：SupportMainEffort，Separation=None（§9）
                    d.Mission = FormationMission.SupportMainEffort;
                    d.Separation = SeparationIntent.None;
                    return d;
            }
        }

        private static bool IsAggressive(IntentType intent)
        {
            return intent == IntentType.Attack || intent == IntentType.Flank || intent == IntentType.Harass;
        }
    }
}
