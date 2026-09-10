namespace CombatBrain.Settings
{
    using CombatBrain.Coordination; // IsForceTriggerReady 引用 FormationControlLeaseManager.SafeFallbackMeters

    // CPS-3 运行期 A/B 验证配置（cps3-runtime-prep，2026-09-07）——四组模式开关（§7.1）+ 观测 Enable 门控。
    // 安全态：Enabled=false（默认）→ 执行器按现状落地（仲裁共存=当前生产行为），零行为变化；
    //   ObserverEnabled=false（默认）→ SetOrderObserver.Enable 不接线（t3 近零开销），无观测日志。
    // 用户开启途径（CPS-3 操作指引，CONTEXT.md §CPS-3 段）：集中 static 热切（改字段后开新战斗）或 MCM
    //   （后续卡按需映射）；战斗后读 %TEMP%\CombatBrain.log 的 [Cps2Observation]/[ControlLease] 汇总。
    /// <summary>CPS-3 五组验证模式（§7.1：A/B/C/D/E 必须分别采样，禁止混同结论；E 为 t54 追加不重排既有值）。</summary>
    public enum Cps3Mode
    {
        A_NativeBaseline = 0, // 三域 CB 命令全关（Native baseline：执行器短路零落地；对照基线）
        B_ShadowOnly = 1,     // CB 决策运行不执行（Resolve + WouldApply 日志；决策与落地对比）
        C_ArbiterCoexist = 2, // 仲裁启用、原版仍控（=当前生产默认；执行器正常落地）
        D_ExclusivePoc = 3,   // 单域排他（租约路线 B：FormationControlLeasePocEnabled=true + 执行器落地）
        E_RouteA = 4          // t54：D-RouteA 运行期验证（单 Formation Route A：RouteAEnabled=true + 租约 PoC 关，
                              //   与 Native baseline(A)/Arbiter coexist(C) 同场景可对比；运行期验证不用 ForceLease——
                              //   外部验证 2026-09-08：关闭原版 FormationAI 会破坏路线 A 验证对象）
    }

    public static class Cps3RuntimeSettings
    {
        // 验证总开关：默认 false=安全态（执行器按现状落地，当前生产行为零变化）
        public static bool Enabled = false;

        // 验证模式（Enabled=true 时生效；默认 A=全关对照——未启用时字段值不影响生产）
        private static Cps3Mode _mode = Cps3Mode.A_NativeBaseline;
        public static Cps3Mode Mode
        {
            get { return _mode; }
            set
            {
                if (value != _mode && value == Cps3Mode.E_RouteA) Cps2PoCSettings.RouteAEnabled = true;
                _mode = value;
            }
        }

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

        // t48（cps3r-force-lease-arm-trigger-split）：武装/触发分离——战前武装、中段持续等待真实合格候选
        //   （合格即触发，Acquire 落在战斗中段满足条件 5 样本要求）。
        /// <summary>候选合格触发判定（纯逻辑，可离线测）：MainBody 有效 + 远离 MainBody&gt;30m（不降 30m 安全条件，
        ///   外部验证明确禁止）+ 无活跃租约。导航校验/NoSafeFallback 由真实 EvaluateFormation 判定（不绕过）。</summary>
        public static bool IsForceTriggerReady(int mainBodyStrength, float distToMain, bool leaseActive)
        {
            return mainBodyStrength > 0
                && distToMain > FormationControlLeaseManager.SafeFallbackMeters
                && !leaseActive;
        }

        /// <summary>
        /// 组合矩阵校验（C/D/E 与既有 ATC-5R PoC 开关 + Cps2PoCSettings.RouteAEnabled 语义不冲突，§7.1）：
        ///   C ArbiterCoexist：租约 PoC 关（原版仍控）+ 路线 A 关（避免同层对刷）
        ///   D ExclusivePoc：租约 PoC 开（路线 B 硬排他）+ 路线 A 关（两执行路线互斥）
        ///   E_RouteA：租约 PoC 关 + RouteAEnabled=true（t54：D-RouteA 运行期验证——单 Formation Route A 独立验证对象）
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
                case Cps3Mode.E_RouteA:
                    return !leasePocEnabled && routeAEnabled;
                default:
                    return true; // A/B：任意组合合法（对照/观测不参与执行路线）
            }
        }

        // cps3-mcm-switches repair（t28）：MCMv5 SettingPropertyDropdown 不支持原生 C# 枚举属性（实测不渲染下拉），
        //   正确写法为 Dropdown<string> 属性（实证 BetterPikesSettings.cs:28）；本方法提供 Dropdown SelectedIndex
        //   ↔ Cps3Mode 枚举的纯逻辑双向转换（离线可测；MCM 属性 get/set 薄转发调用）。
        /// <summary>Dropdown SelectedIndex（0..4）→ Cps3Mode（A..E）；越界钳制到 A（安全态）。</summary>
        public static Cps3Mode ModeFromSelectedIndex(int index)
        {
            if (index < 0 || index > 4) return Cps3Mode.A_NativeBaseline;
            return (Cps3Mode)index;
        }

        /// <summary>Cps3Mode（A..E）→ Dropdown SelectedIndex（0..4）。</summary>
        public static int SelectedIndexFromMode(Cps3Mode mode)
        {
            int i = (int)mode;
            return i >= 0 && i <= 4 ? i : 0;
        }

        /// <summary>t54：E_RouteA 模式是否启用 RouteA（MissionLogic 挂点 routeOn 自动判定）。</summary>
        public static bool RouteAActive(Cps3Mode mode)
        {
            return mode == Cps3Mode.E_RouteA;
        }

        // G-1（CB-REPAIR-0003，用户裁决）：恢复链路（外部覆盖检测→边沿重试→持续覆盖释放）依赖 SetOrderObserver
        //   观测；无观测时落地 Route A 命令会使覆盖恢复整链静默失效、产生无法验证的样本——故硬依赖 ObserverEnabled：
        //   E 模式无观测 → Route A 不执行（fail-safe）。
        public static bool RouteAExecutionEnabled()
        {
            return Enabled && Mode == Cps3Mode.E_RouteA && Cps2PoCSettings.RouteAEnabled && ObserverEnabled;
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
