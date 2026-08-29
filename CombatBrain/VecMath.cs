using System;

namespace CombatBrain
{
    public static class VecMath
    {
        public static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx;
            float dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public static float DistSq(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx;
            float dy = ay - by;
            return dx * dx + dy * dy;
        }

        public static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        // r∈[0,1] → 对数压到[0,1]，放大悬殊，避免 0.5 附近线性拥堵
        public static float LogMapRatio(float r)
        {
            if (r <= 0f) return 0f;
            if (r >= 1f) return 1f;
            return (float)(Math.Log10(1.0 + 9.0 * r));
        }
    }
}