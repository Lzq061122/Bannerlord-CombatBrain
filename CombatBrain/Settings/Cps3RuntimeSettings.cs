namespace CombatBrain.Settings
{
    // CPS-3 运行期 A/B 验证配置（cps3-runtime-prep，2026-09-07）——四组模式开关（§7.1）+ 观测 Enable 门控。
    // 安全态：Enabled=false（默认）→ 执行器按现状落地（仲裁共存=当前生产行为），零行为变化；
    //   ObserverEnabled=false（默认）→ SetOrderObserver.Enable 不接线（t3 近零开销），无观测日志。
    // 用户开启途径（CPS-3 操作指引，CONTEXT.md §CPS-3 段）：集中 static 热切（改字段后开新战斗）或 MCM
    //   （后续卡按需映射）；战斗后读 %TEMP%\CombatBrain.log 的 [Cps2Observation]/[ControlLease] 汇总。
    /// <summary>CPS-3 四组验证模式（§7.1：A/B/C/D 必须分别采样，禁止混同结论）。</summary>
    public enum Cps3Mode
    {
        A_NativeBaseline = 0, // 三域 CB 命令全关（Native baseline：执行器短路零落地；对照基线）
        B_ShadowOnly = 1,     // CB 决策运行不执行（Resolve + WouldApply 日志；决策与落地对比）
        C_ArbiterCoexist = 2, // 仲裁启用、原版仍控（=当前生产默认；执行器正常落地）
        D_ExclusivePoc = 3    // 单域排他（租约路线 B：FormationControlLeasePocEnabled=true + 执行器落地）
    }

    public static class Cps3RuntimeSettings
    {
        // 验证总开关：默认 false=安全态（执行器按现状落地，当前生产行为零变化）
        public static bool Enabled = false;

        // 验证模式（Enabled=true 时生效；默认 A=全关对照——未启用时字段值不影响生产）
        public static Cps3Mode Mode = Cps3Mode.A_NativeBaseline;

        // SetOrderObserver.Enable 门控（t3 遗留启动接线；默认关=近零开销，开启后三域 ExternalUnknown 可观测）
        public static bool ObserverEnabled = false;

        // B Shadow only 的 WouldApply 决策日志门控（默认关；开启后每次胜者决策输出 [Cps3Shadow] 行）
        public static bool ShadowWouldApplyLogEnabled = false;

        /// <summary>
        /// 组合矩阵校验（C/D 与既有 ATC-5R PoC 开关 + Cps2PoCSettings.RouteAEnabled 语义不冲突，§7.1）：
        ///   C ArbiterCoexist：租约 PoC 关（原版仍控）+ 路线 A 关（避免同层对刷）
        ///   D ExclusivePoc：租约 PoC 开（路线 B 硬排他）+ 路线 A 关（两执行路线互斥）
        ///   A/B：对照/观测模式，租约与路线 A 任意（A 全关 CB 命令；B 决策不落地）
        /// </summary>
        public static bool IsValidCombination(Cps3Mode mode, bool leasePocEnabled, bool routeAEnabled)
        {
            switch (mode)
            {
                case Cps3Mode.C_ArbiterCoexist:
                    return !leasePocEnabled && !routeAEnabled;
                case Cps3Mode.D_ExclusivePoc:
                    return leasePocEnabled && !routeAEnabled;
                default:
                    return true; // A/B：任意组合合法（对照/观测不参与执行路线）
            }
        }
    }
}
