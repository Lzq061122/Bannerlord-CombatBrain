namespace CombatBrain.Ranged
{
    // F9 远程目标选择/弹药取舍 —— 纯逻辑。
    // 0.3 秒低频换目标；按弹药类型匹配护甲；优先集火指挥官与重骑兵；弹药见底转近战/后撤。
    public enum ArmorClass
    {
        Light,
        Medium,
        Heavy
    }

    public enum AmmoKind
    {
        LightArrow,
        ArmorPiercing
    }

    public enum RangedFallback
    {
        KeepShooting,
        SwitchToMelee,
        Retreat
    }

    public struct RangedTarget
    {
        public int Index;
        public float Distance;
        public ArmorClass Armor;
        public bool IsCommander;
        public bool IsHeavyCavalry;
        public float Threat; // 0..1
    }

    public static class RangedBrain
    {
        public const float AmmoLowThreshold = 0.15f;

        public static int SelectTarget(RangedTarget[] targets, AmmoKind ammo)
        {
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < targets.Length; i++)
            {
                float s = Score(targets[i], ammo);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            return best;
        }

        private static float Score(RangedTarget t, AmmoKind ammo)
        {
            float s = 0f;
            if (t.IsCommander) s += 3.0f;
            if (t.IsHeavyCavalry) s += 1.5f;
            s += t.Threat;
            s -= t.Distance * 0.02f;

            // 弹药-护甲匹配：穿甲箭打重甲、轻羽箭打轻甲；错配降低收益。
            if (ammo == AmmoKind.ArmorPiercing)
            {
                if (t.Armor == ArmorClass.Heavy) s += 1.2f;
                else if (t.Armor == ArmorClass.Light) s -= 0.6f;
            }
            else
            {
                if (t.Armor == ArmorClass.Light) s += 1.2f;
                else if (t.Armor == ArmorClass.Heavy) s -= 0.6f;
            }
            return s;
        }

        public static RangedFallback DecideFallback(float ammoRatio, bool inMeleeRange)
        {
            if (ammoRatio <= AmmoLowThreshold)
                return inMeleeRange ? RangedFallback.SwitchToMelee : RangedFallback.Retreat;
            return RangedFallback.KeepShooting;
        }
    }
}