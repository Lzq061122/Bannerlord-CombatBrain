namespace CombatBrain.Combat
{
    // X5-1 兵种分类输入（SoldierInput / PrimaryWeaponClass）—— **保留**（有在用的编队级消费者）。
    //
    // CB-PROMPT-0038 R3（消费者归零即删）/ SCOPE-AMEND-2：本文件原有的 `FighterProfile` 四字段素质表与
    //   `FighterProfileFactory` 已随个体控制面退役删除（四字段读取点 FlankAware@MovementGlue、
    //   PredictConf@DecisionJitterGlue 随模块退役归零；FeintRate/Shakiness 在 t2 后即无读取者）。
    //   保留的 `SoldierInput` / `PrimaryWeaponClass` 仍被编队级链路消费：
    //   `Ai/FormationSlotGlue.BuildSoldierInput`（构造）→ `Formation/TroopClassifier.Classify`（消费）。
    //   `ArmorGrade` / `SoldierKind` 定义在 `Enums.cs`。
    public struct SoldierInput
    {
        public SoldierKind Kind;   // 兵种（步兵/射手/骑兵）
        public int Level;          // 兵种等级 1-6（英雄等级由胶水层折算；越界自动 clamp）
        public ArmorGrade Armor;   // 护甲材质（视觉可辨：布/皮/锁/板）
        public bool IsVeteran;     // 老兵旗标（久战精锐；英雄可折算为 true）

        // X5-1 扩展字段（设计稿 §二 裁决 2-a）：装备角色轴输入，由胶水层从 Agent 主手/副手提取
        // （Agent.GetPrimaryWieldedItemIndex / Equipment 查询），纯逻辑零依赖。
        public bool HasShield;                          // 是否持盾（副手盾牌；盾墙前排核心特征）
        public PrimaryWeaponClass PrimaryWeaponClass;   // 主武器类别（单手/双手/长杆/弓/弩/投掷）
    }

    // X5-1 主武器类别（TroopClassifier 输入，设计稿 §四 规则表）：
    // 胶水层从 Agent.GetPrimaryWieldedItemIndex 主手武器提取；
    // 近战武器不改变 Ranged 角色（弓弩兵切近战副手仍算 Ranged，规则表语义）。
    public enum PrimaryWeaponClass
    {
        None = 0,      // 徒手/未知（兜底近战）
        OneHanded = 1, // 单手（剑/斧/锤）
        TwoHanded = 2, // 双手重武器（双手剑/斧/锤）
        Polearm = 3,   // 长杆（枪/矛/戟）
        Bow = 4,       // 弓
        Crossbow = 5,  // 弩
        Thrown = 6     // 投掷（标枪/飞斧/飞刀，远程系）
    }
}
