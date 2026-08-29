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

        // 稳定、确定性的分级；TODO: 用兵种等级/护甲/老兵旗标细化
        public static SoldierTier Classify(int agentIndex)
        {
            int h = (agentIndex * 1103515245 + 12345) & 0x7fffffff;
            int bucket = h % 100;
            if (bucket < 25) return SoldierTier.Recruit;
            if (bucket < 65) return SoldierTier.Soldier;
            if (bucket < 90) return SoldierTier.Veteran;
            return SoldierTier.Elite;
        }
    }
}