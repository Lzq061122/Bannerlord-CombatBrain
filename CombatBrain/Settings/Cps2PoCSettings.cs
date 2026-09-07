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

        // 路线 A Behavior 权重（t50 硬门⑤单维化：GetAiWeight 直接返回此常量，SetBehaviorWeight 也设此值——
        //   消除 t20 旧模型 GetAiWeight=10 × SetBehaviorWeight=10=100 的权重放大；单值竞争即可胜原版 0.2~1.0）。
        // 冲突行为降权表见 Cps2RouteAPolicy.ConflictingIntents（降权 SetBehaviorWeight(0f) 而非删除/清空）。
        public const float CbBehaviorWeight = 10f;

        // t50 硬门①⑧：RouteA 作用单编队数（每拍至多接管一支非玩家 AI 编队的单 Movement 域）——
        //   仅诊断/运行期验证，防多队并控
        public static int RouteAMaxFormations = 1;

        // t50 硬门③：Rejoin 贴身接敌判定阈值（米；IsInContact 距离）
        public const float RouteARejoinContactMeters = 12f;

        // 路线 B 观测输出开关：默认 false（无 5s [Cps2Observation] 日志；CPS-3 运行期开启）
        public static bool RouteBObservationEnabled = false;
    }
}
