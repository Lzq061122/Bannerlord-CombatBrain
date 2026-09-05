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
