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

        // X0 决策延迟强度（观感补强 §X0）：0=果断（无犹豫/无延迟、意图 hold 1.5s、切换余量 0%）、
        // 1=现状拟人（F12 抖动 + hold 6s + 15% 余量），中间线性。默认 1=现状。
        // MCM 装载后可在「通用」组里热切（RequireRestart=false，bool 快档：果断/拟人）。
        public static float DecisionDelayStrength = 1f;

        // X2 手活属性强度已退役（CB-PROMPT-0038 §五.1）：CombatBrain 不再覆盖单兵战斗能力数值，
        //   该设置项随即删除（原默认值 1f）。退役前它也没有对应 MCM 属性（死开关），删除不改变可配置面。

        // X7 战场信息迷雾开关（观感补强 §X7，依赖 X6 接线）：true=迷雾生效（拟人，AI 只看得见
        // KnowledgeView 已观察的敌编队，未见/超期→保守降级）；false=全知（查询直通返回真实值，
        // 且直接跳过感知计算——关闭降开销，开启才增开销）。
        // 硬红线：C 层（禁读指针/禁全图遍历个体/性能红线）永不开放，本开关只允许「迷雾/全知」切换，
        // 不新增任何信息读取；关闭后 AI 信息集等于 X6 前现状（全可见公开聚合），而非大于现状。
        // 默认 true=拟人；MCM 装载后可在「真实度」组里热切（RequireRestart=false）。
        public static bool FogOfWarEnabled = true;

        // N1 叙事双视角模式（战场叙事反馈系统 §2）：false=默认指挥模式（只播与玩家交战相关的 CB 队，
        // 主语代填我军/敌军）；true=观战模式（所有被 CB 接管的非玩家队都播，主语用阵营名）。
        // 玩家本队永不播（含豁免关闭时——旁白说给玩家听，不说玩家自己）；playerTeam==null 回退观战规则。
        // 默认 false=指挥模式；MCM 装载后可在「通用」组里热切（RequireRestart=false）。
        public static bool NarrationSpectatorMode = false;

        // ATC-5R 编队控制权租约 PoC 开关（2026-09-06）：默认 false=零 SetControlledByAI 调用、零战斗行为变化、
        //   Shadow 输出保持、性能差异接近零（producer 拍直接返回）。
        // true=仅对 FormationControlLeasePocFormationKind 域的单个 AI 编队试验租约（普通单机野战内）；
        //   本 PoC 不等同 ATC-5 全量门控；运行期 R1-R8 未验证前不得写 GO（PoC Ready / Runtime Pending）。
        // 现有集中配置文件落点（规格 §4.3/§11「CombatBrain/Settings 或现有集中配置文件」；禁 magic bool 散落）。
        public static bool FormationControlLeasePocEnabled = false;

        // PoC 试验编队域（0=Infantry；与 FormationCoordKind 语义对齐）：仅此域编队可 Acquire
        //   （远程/骑射/骑兵编队不试验；§4.3 单域原则）。
        public static int FormationControlLeasePocFormationKind = 0;
    }
}
