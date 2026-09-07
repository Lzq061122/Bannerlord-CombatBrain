namespace CombatBrain.Settings
{
    // CPS-2 所有权 PoC 配置（cps2-ownership-poc，2026-09-07）——集中 static 开关，默认全关安全态。
    // 路线 A（§6.1）：自定义 BehaviorComponent 表达 Movement 域 Hold/Advance/Rejoin/Retreat（权重法竞争）。
    // 路线 B（§6.2）：租约单域排他（t19 已接入执行器），本配置控制运行期观测输出。
    // 安全态：RouteAEnabled=false → CbRouteAPoC.Apply 零 AddAiBehavior/SetBehaviorWeight 调用、零行为变化；
    //   RouteBObservationEnabled=false → 无 5s 观测日志。CPS-3 运行期验证时由用户/运行期钩子开启。
    public static class Cps2PoCSettings
    {
        // 路线 A Behavior PoC 开关：默认 false（关闭时零 AddAiBehavior/SetBehaviorWeight 调用，安全态）
        public static bool RouteAEnabled = false;

        // CB 自定义 Behavior 权重（FindBestBehavior 按 GetAIWeight()×WeightFactor 取 max，FormationAI.cs:179）：
        // 原版 Behavior GetAiWeight 通常 0.2~1.0，CB 权重 10 保证意图竞争胜出（无需全量降权即可 PoC；
        // 冲突行为降权表见 Cps2RouteAPolicy.ConflictingIntents，运行期按表对具体类型 SetBehaviorWeight(0f)）。
        public const float CbBehaviorWeight = 10f;

        // 路线 B 观测输出开关：默认 false（无 5s [Cps2Observation] 日志；CPS-3 运行期开启）
        public static bool RouteBObservationEnabled = false;
    }
}
