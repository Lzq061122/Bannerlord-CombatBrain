namespace CombatBrain
{
    // CombatBrain 静态配置（t66/t73）：MCM 可选集成 + 游戏内开关默认值。
    // 设计原则：
    //   * 编译期零 MCM 依赖（MCM 集成由 CombatBrain/Mcm/CombatBrainMcmSettings.cs 承担，
    //     仅在其源文件里编译期引用 MCMv5.dll；本文件保持纯静态配置）；
    //   * 豁免点读取逻辑：5 个编队层 Glue + TickIntent + 敌队聚合 2 处直接读本静态字段；
    //   * 反作弊闸不变：豁免只是「mod 是否接管玩家队」的指挥权开关，不新增任何禁读信息。
    public static class CombatBrainSettings
    {
        // 玩家队豁免开关：true=现状（P0-④，5 个编队层 Glue + TickIntent 不接管玩家队）；
        // false=mod 正常接管/扫描玩家队（编队层下令、意图决策、敌队聚合均纳入玩家队）。
        // 默认 true=保持现状；MCM 装载后可在「通用」组里热切（RequireRestart=false）。
        public static bool PlayerTeamExemptionEnabled = true;
    }
}
