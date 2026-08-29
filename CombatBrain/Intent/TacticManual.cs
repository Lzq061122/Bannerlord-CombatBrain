namespace CombatBrain.Intent
{
    // 冷启动“战术手册”权重矩阵（7 意图 × 8 维度）。
    // 权重 W = 预设战术手册(冷启动) × 运行时经验修正(F10，V3 叠加)。
    public static class TacticManual
    {
        public const int IntentCount = 7;
        public const int DimensionCount = 8;

        public static readonly IntentType[] Order =
        {
            IntentType.Attack, IntentType.Defend, IntentType.Retreat,
            IntentType.Flank, IntentType.Harass, IntentType.Reform, IntentType.Pursue
        };

        // 维度顺序：Power, Line, Cav, Height, Moral, Arc, PenB, Dist
        private static readonly float[][] Matrix =
        {
            //                 Power   Line   Cav  Height Moral  Arc   PenB   Dist
            new float[] {      0.30f, 0.10f, 0.10f, 0.05f, 0.20f, 0.20f, 0.30f,-0.40f }, // Attack
            new float[] {     -0.10f, 0.40f, 0.00f, 0.30f, 0.10f, 0.10f,-0.30f, 0.20f }, // Defend
            new float[] {     -0.50f, 0.00f,-0.10f, 0.10f,-0.40f,-0.10f,-0.40f, 0.50f }, // Retreat
            new float[] {      0.10f,-0.20f, 0.50f,-0.10f, 0.05f, 0.10f, 0.10f, 0.05f }, // Flank
            new float[] {     -0.10f,-0.10f, 0.30f, 0.10f, 0.05f, 0.50f, 0.00f, 0.40f }, // Harass
            new float[] {      0.10f,-0.40f, 0.00f, 0.05f,-0.30f, 0.00f, 0.20f, 0.05f }, // Reform
            new float[] {      0.20f, 0.00f, 0.20f, 0.00f, 0.10f, 0.10f, 0.50f,-0.10f }, // Pursue
        };

        public static float[][] BuildMatrix()
        {
            return Matrix;
        }
    }
}