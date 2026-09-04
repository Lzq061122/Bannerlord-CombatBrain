namespace CombatBrain.Combat
{
    // 士兵素质四级分化：生成时算一次，运行时查表，零成本。
    public struct FighterProfile
    {
        public float PredictConf;  // 格挡预测置信度  菜鸟 0.35 → 老兵 0.85
        public float FeintRate;    // 虚招频率        菜鸟 0.05 → 精锐 0.30
        public float FlankAware;   // 绕侧/观察意识   菜鸟 0.10 → 老兵 0.70
        public float Shakiness;    // 疲劳走形抖动基底

        public FighterProfile(float predictConf, float feintRate, float flankAware, float shakiness)
        {
            PredictConf = predictConf;
            FeintRate = feintRate;
            FlankAware = flankAware;
            Shakiness = shakiness;
        }
    }

    // 士兵分类输入（兵种/等级/护甲/老兵旗标）。由胶水层从游戏侧提取
    // （CharacterObject.Level、ArmorMaterialTypes、IsHero/久战折算），纯逻辑零依赖。
    public struct SoldierInput
    {
        public SoldierKind Kind;   // 兵种（步兵/射手/骑兵）
        public int Level;          // 兵种等级 1-6（英雄等级由胶水层折算；越界自动 clamp）
        public ArmorGrade Armor;   // 护甲材质（视觉可辨：布/皮/锁/板）
        public bool IsVeteran;     // 老兵旗标（久战精锐；英雄可折算为 true）

        // X5-1 扩展字段（设计稿 §二 裁决 2-a）：装备角色轴输入，由胶水层从 Agent 主手/副手提取
        // （先例 MovementGlue.cs:25 GetPrimaryWieldedItemIndex / Equipment 查询），纯逻辑零依赖。
        public bool HasShield;                          // 是否持盾（副手盾牌；盾墙前排核心特征）
        public PrimaryWeaponClass PrimaryWeaponClass;   // 主武器类别（单手/双手/长杆/弓/弩/投掷）
    }

    // X5-1 主武器类别（TroopClassifier 输入，设计稿 §四 规则表）：
    // 胶水层从 Agent.GetPrimaryWieldedItemIndex 主手武器提取（MovementGlue.cs:25 先例）；
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

    public static class FighterProfileFactory
    {
        public static FighterProfile Create(SoldierTier tier)
        {
            switch (tier)
            {
                case SoldierTier.Recruit: return new FighterProfile(0.35f, 0.05f, 0.10f, 0.80f);
                case SoldierTier.Soldier: return new FighterProfile(0.50f, 0.12f, 0.30f, 0.50f);
                case SoldierTier.Veteran: return new FighterProfile(0.68f, 0.20f, 0.50f, 0.25f);
                case SoldierTier.Elite:   return new FighterProfile(0.85f, 0.30f, 0.70f, 0.05f);
            }
            return new FighterProfile(0.50f, 0.12f, 0.30f, 0.50f);
        }

        // 稳定、确定性的分级：按兵种/等级/护甲/老兵旗标判定（不再依赖 agentIndex 伪随机）。
        // 等级基线（Bannerlord 兵种阶级 1-6）：1→Recruit、2-3→Soldier、4→Veteran、5-6→Elite；
        // 板甲 +1 档（重装）；老兵旗标 +1 档（不超 Elite）；骑兵保底 Soldier（马匹昂贵，低级骑兵罕见）。
        public static SoldierTier Classify(SoldierInput input)
        {
            int level = input.Level < 1 ? 1 : (input.Level > 6 ? 6 : input.Level);
            SoldierTier tier;
            if (level <= 1) tier = SoldierTier.Recruit;
            else if (level <= 3) tier = SoldierTier.Soldier;
            else if (level == 4) tier = SoldierTier.Veteran;
            else tier = SoldierTier.Elite;

            if (input.Armor == ArmorGrade.Plate && tier < SoldierTier.Elite) tier++;
            if (input.IsVeteran && tier < SoldierTier.Elite) tier++;
            if (input.Kind == SoldierKind.Cavalry && tier < SoldierTier.Soldier) tier = SoldierTier.Soldier;
            return tier;
        }
    }
}