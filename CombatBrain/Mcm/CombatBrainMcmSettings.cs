using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

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
    }
}
