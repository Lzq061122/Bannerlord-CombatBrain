using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common; // Dropdown<T>（t28 repair：MCMv5 正确下拉类型，实证 BetterPikesSettings.cs:28）

namespace CombatBrain.Mcm
{
    // MCM 静态设置子类（t73，废弃 t66 动态 Emit，改用编译期静态子类）。
    // 基类说明：MCMv5 5.12.2 实测（MCMv5.dll 反射/元数据）——AttributeSettings<T> 已不存在，
    // 真正的属性式全局设置基类是 MCM.Abstractions.Base.Global.AttributeGlobalSettings<T>
    // （继承链 AttributeGlobalSettings<T> → GlobalSettings<T> → GlobalSettings → BaseSettings），
    // 抽象成员仅 Id/DisplayName；GlobalSettings<T> 提供静态 Instance 单例。
    // MCM 程序集扫描机制会枚举已加载程序集里继承 GlobalSettings 的非抽象子类自动发现，
    // 因此这里只需「编译期真实类型」即可被扫到（t66 的 AssemblyBuilderAccess.Run 动态程序集不在扫描范围）。
    // 属性 get/set 直读写 CombatBrainSettings 静态字段（无拷贝失步，MCM UI 改动即时生效）。
    public class CombatBrainMcmSettings : AttributeGlobalSettings<CombatBrainMcmSettings>
    {
        public override string Id { get; } = "CombatBrain";
        public override string DisplayName => "CombatBrain 战斗AI";
        public override string FolderName { get; } = "CombatBrain";
        public override string FormatType { get; } = "json";

        [SettingPropertyBool("玩家队豁免", RequireRestart = false)]
        [SettingPropertyGroup("通用")]
        public bool PlayerTeamExemptionEnabled
        {
            get => CombatBrainSettings.PlayerTeamExemptionEnabled;
            set => CombatBrainSettings.PlayerTeamExemptionEnabled = value;
        }

        [SettingPropertyBool("果断模式（关闭拟人延迟）", RequireRestart = false)]
        [SettingPropertyGroup("通用")]
        public bool DecisiveMode
        {
            get => CombatBrainSettings.DecisionDelayStrength <= 0f;
            set => CombatBrainSettings.DecisionDelayStrength = value ? 0f : 1f;
        }

        // X7 战场信息迷雾开关（观感补强 §X7，依赖 X6 接线）：开=迷雾生效（拟人，AI 只看得见已观察敌编队）；
        // 关=全知（查询直通 + 跳过感知计算=降开销）。默认开。硬红线：只切迷雾/全知，不触及 C 层
        // （禁读指针/禁全图遍历个体/性能红线）；关闭不新增信息读取（信息集= X6 前现状）。
        [SettingPropertyBool("战场信息迷雾", RequireRestart = false)]
        [SettingPropertyGroup("真实度")]
        public bool FogOfWarEnabled
        {
            get => CombatBrainSettings.FogOfWarEnabled;
            set => CombatBrainSettings.FogOfWarEnabled = value;
        }

        // N1 叙事双视角模式（战场叙事反馈系统 §2）：开=观战模式（双方阵营名播报，所有被 CB 接管队都播）；
        // 关=指挥模式（默认，只播与玩家交战相关 CB 队，主语我军/敌军）。玩家本队永不播。
        [SettingPropertyBool("观战模式（双方阵营名播报）", RequireRestart = false)]
        [SettingPropertyGroup("通用")]
        public bool NarrationSpectatorMode
        {
            get => CombatBrainSettings.NarrationSpectatorMode;
            set => CombatBrainSettings.NarrationSpectatorMode = value;
        }

        // ATC-5R 编队控制权租约 PoC 测试开关（atc5r-nits）：运行期 R1-R8 验证时置 true（仅普通野战 +
        //   Infantry 域单编队试验）；默认 false=安全态零 SetControlledByAI 零行为变化。
        // 本 PoC 不等同 ATC-5 全量门控；R1-R8 未验证前不得 GO（PoC Ready / Runtime Pending）。
        [SettingPropertyBool("编队控制权租约 PoC（实验）", RequireRestart = false)]
        [SettingPropertyGroup("真实度")]
        public bool FormationControlLeasePocEnabled
        {
            get => CombatBrainSettings.FormationControlLeasePocEnabled;
            set => CombatBrainSettings.FormationControlLeasePocEnabled = value;
        }

        // CPS-3 运行期验证四字段（cps3-mcm-switches，t24 的 Cps3RuntimeSettings 游戏内热切）：
        // get/set 直读写 Cps3RuntimeSettings 静态字段（无拷贝失步，MCM UI 改动即时生效，RequireRestart=false）。
        // 默认安全态：Enabled=false/ObserverEnabled=false/ShadowLog=false/Mode=A（零行为变化）。
        // 用途：用户游戏内切换 A/B/C/D 四组采样模式（§7.1），避免每次改源码重编译。
        [SettingPropertyBool("CPS 验证模式启用", RequireRestart = false,
            HintText = "开=按下方模式运行验证采样；关=现状（仲裁共存，零行为变化）")]
        [SettingPropertyGroup("CPS 验证")]
        public bool Cps3ValidationEnabled
        {
            get => CombatBrain.Settings.Cps3RuntimeSettings.Enabled;
            set => CombatBrain.Settings.Cps3RuntimeSettings.Enabled = value;
        }

        // cps3-mcm-switches repair（t28）：MCMv5 SettingPropertyDropdown 不支持原生 C# 枚举属性（实测只显示名称
        //   无下拉可点）——改 MCMv5 正确类型 Dropdown<string>（MCM.Common，实证 BetterPikesSettings.cs:28），
        //   SelectedIndex（0..4）↔ Cps3RuntimeSettings.Mode（Cps3Mode A..E）双向转换（Cps3RuntimeSettings
        //   ModeFromSelectedIndex/SelectedIndexFromMode 纯逻辑）；默认 SelectedIndex=0=A 安全态；RequireRestart=false 热切。
        // t32 repair（下拉切换失效）：MCMv5 对 Dropdown 引用类型可能直接改实例 SelectedIndex 而不调 setter
        //   （实证 BetterPikes/TAOM 用自动属性）——旧 getter 每次强制实例=Mode 对应值导致选 B 后 UI 恒显示 A。
        //   现 getter 改反向同步：读实例当前 SelectedIndex，与 Mode 不一致才写回 Mode（实例优先，覆盖直接改实例
        //   路径；一致时不写防漂移）；setter 路径保留（value.SelectedIndex → ModeFromSelectedIndex → Mode）。
        //   双路径（直接改实例 / 调 setter）都保证 Mode 最终正确。
        // t56 repair（下拉缺 E）：Cps3Mode.E_RouteA=4（t54）已加但选项数组仍 4 项——补第 5 项
        //   "E: 单编队 Route A"（index 4 ↔ E_RouteA），与 0..4 转换对应
        private static readonly string[] Cps3ModeNames =
            { "A: Native 全关", "B: Shadow", "C: 仲裁共存", "D: 单域排他", "E: 单编队 Route A" };
        private readonly Dropdown<string> _cps3ModeDropdown = new Dropdown<string>(Cps3ModeNames, 0);

        [SettingPropertyDropdown("CPS 验证模式", RequireRestart = false,
            HintText = "A=Native 全关对照（三域 CB 命令全关）/ B=Shadow（决策运行不落地）/ C=仲裁共存（原版仍控）/ D=单域排他（需租约 PoC 开）/ E=单编队 Route A（需 RouteA 开，自动启用）")]
        [SettingPropertyGroup("CPS 验证")]
        public Dropdown<string> Cps3ValidationMode
        {
            get
            {
                // get 反向同步（t32）：MCM 直接改实例 SelectedIndex（不调 setter）→ 不一致时写回 Mode（实例优先）；
                //   一致时不写（防漂移/防外部 static Mode 被实例旧值覆盖）
                if (CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(_cps3ModeDropdown.SelectedIndex))
                {
                    CombatBrain.Settings.Cps3RuntimeSettings.Mode =
                        CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(_cps3ModeDropdown.SelectedIndex);
                }
                return _cps3ModeDropdown;
            }
            set
            {
                // set 路径（MCM 调 setter）：value.SelectedIndex → ModeFromSelectedIndex → Mode（越界钳制安全态）
                CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(
                    value.SelectedIndex);
                _cps3ModeDropdown.SelectedIndex = value.SelectedIndex;
            }
        }

        [SettingPropertyBool("命令来源观测", RequireRestart = false,
            HintText = "开=启用 setter 级命令来源观测（三域 ExternalUnknown 计数，日志供 CPS-3 分析）；关=近零开销")]
        [SettingPropertyGroup("CPS 验证")]
        public bool Cps3ObserverEnabled
        {
            get => CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled;
            set => CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled = value;
        }

        [SettingPropertyBool("Route A 移动控制", RequireRestart = false,
            HintText = "进入 E 模式时自动开启；战中关闭立即释放 Route A，离开 E 模式同样释放。")]
        [SettingPropertyGroup("CPS 验证")]
        public bool RouteAMovementEnabled
        {
            get => CombatBrain.Settings.Cps2PoCSettings.RouteAEnabled;
            set => CombatBrain.Settings.Cps2PoCSettings.RouteAEnabled = value;
        }

        [SettingPropertyBool("Shadow 决策日志", RequireRestart = false,
            HintText = "B 模式下输出 WouldApply 决策日志（供决策 vs 落地对比）")]
        [SettingPropertyGroup("CPS 验证")]
        public bool Cps3ShadowLogEnabled
        {
            get => CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled;
            set => CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled = value;
        }

        // t44（cps3r-force-lease-candidate）：ForceLease 受控注入（D 组排他样本采集）——
        // 仅 D_ExclusivePoc 模式生效；只稳定制造前置条件（注入 Denied+EmergencyOverride 决策），
        // 走真实 Acquire/唯一执行器/TTL/Release/setter 观测全链路；默认关=零常态行为变化
        [SettingPropertyBool("ForceLease 候选注入（D 组样本）", RequireRestart = false,
            HintText = "D 模式下为单支非玩家 AI 编队注入许可决策稳定制造排他样本（走真实 Acquire 全链路）；默认关")]
        [SettingPropertyGroup("CPS 验证")]
        public bool Cps3ForceLeaseCandidateEnabled
        {
            get => CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseCandidateEnabled;
            set => CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseCandidateEnabled = value;
        }
    }
}
