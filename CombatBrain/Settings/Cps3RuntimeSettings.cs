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

        // t44（cps3r-force-lease-candidate）：ForceLeaseCandidate 受控注入（P1，D 组排他样本采集）——
        // 仅诊断注入工具，默认关=零常态行为变化；仅 D_ExclusivePoc 模式生效（ForceLeaseActive）；
        // 只稳定制造前置条件（注入 Denied+EmergencyOverride 决策），仍走真实 Acquire/唯一执行器/TTL/Release/
        // setter 观测全链路，不伪造最终结果（NoSafeFallback 时真实不 Acquire）。
        public static bool ForceLeaseCandidateEnabled = false;

        /// <summary>ForceLease 武装/触发超时（秒；超时未 Acquire 放弃武装）。</summary>
        public const float ForceArmTimeoutSeconds = 30f;

        /// <summary>t44：ForceLease 候选资格（纯逻辑）：非玩家队 + 不含玩家兵员 + AI 控制。</summary>
        public static bool IsForceLeaseCandidate(bool isPlayerTeam, bool playerOwned, bool aiControlled)
        {
            return !isPlayerTeam && !playerOwned && aiControlled;
        }

        /// <summary>t44：ForceLease 仅 D_ExclusivePoc 模式生效（组合矩阵：D 模式启用时 ForceLease 才允许）。</summary>
        public static bool ForceLeaseActive(Cps3Mode mode)
        {
            return mode == Cps3Mode.D_ExclusivePoc;
        }

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

        // cps3-mcm-switches repair（t28）：MCMv5 SettingPropertyDropdown 不支持原生 C# 枚举属性（实测不渲染下拉），
        //   正确写法为 Dropdown<string> 属性（实证 BetterPikesSettings.cs:28）；本方法提供 Dropdown SelectedIndex
        //   ↔ Cps3Mode 枚举的纯逻辑双向转换（离线可测；MCM 属性 get/set 薄转发调用）。
        /// <summary>Dropdown SelectedIndex（0..3）→ Cps3Mode（A..D）；越界钳制到 A（安全态）。</summary>
        public static Cps3Mode ModeFromSelectedIndex(int index)
        {
            if (index < 0 || index > 3) return Cps3Mode.A_NativeBaseline;
            return (Cps3Mode)index;
        }

        /// <summary>Cps3Mode（A..D）→ Dropdown SelectedIndex（0..3）。</summary>
        public static int SelectedIndexFromMode(Cps3Mode mode)
        {
            int i = (int)mode;
            return i >= 0 && i <= 3 ? i : 0;
        }

        // t32 repair（cps3-mcm-switches 下拉切换失效）：MCMv5 对 Dropdown 引用类型可能直接改实例 SelectedIndex
        //   而不调 setter（实证 BetterPikes/TAOM 用自动属性）——旧 getter 每次强制实例=Mode 对应值导致
        //   选 B 后 UI 恒显示 A。修复：getter 反向同步（读实例 SelectedIndex，与 Mode 不一致才写回 Mode——
        //   实例优先，覆盖 MCM 直接改实例路径；一致时不写防漂移）。
        /// <summary>实例 SelectedIndex 与当前 Mode 是否不一致（不一致 → getter 需反向同步写回 Mode）。</summary>
        public static bool NeedsModeSync(int instanceSelectedIndex)
        {
            return ModeFromSelectedIndex(instanceSelectedIndex) != Mode;
        }
    }
}
