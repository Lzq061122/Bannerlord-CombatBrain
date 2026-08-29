using System;

namespace CombatBrain.Perception
{
    // 合成高度场(米)：两个正弦叠加模拟丘陵，用于迷雾验收演示。
    // TODO: 替换为开战预缓存的场景高度场/遮挡网格，战中原样查表，绝不实时射线。
    public static class TerrainStub
    {
        public static float Height(float x, float y)
        {
            return (float)(2.0 * Math.Sin(x * 0.02) * Math.Cos(y * 0.025) + 1.5 * Math.Sin(x * 0.007 + 1.0));
        }
    }

    // 可见性判定：O(格) 而非 O(士兵)。当前为距离门 + 粗糙视线采样。
    public sealed class VisibilityOracle
    {
        private const float EyeHeight = 1.7f;
        private const int LoSSamples = 4;

        private readonly float _range;

        public VisibilityOracle(float range)
        {
            _range = range;
        }

        public float Range { get { return _range; } }

        public bool IsVisible(float ox, float oy, float tx, float ty)
        {
            float d = VecMath.Dist(ox, oy, tx, ty);
            if (d > _range) return false;

            float oh = TerrainStub.Height(ox, oy) + EyeHeight;
            float th = TerrainStub.Height(tx, ty) + EyeHeight;

            for (int i = 1; i < LoSSamples; i++)
            {
                float t = (float)i / LoSSamples;
                float sx = ox + (tx - ox) * t;
                float sy = oy + (ty - oy) * t;
                float terrain = TerrainStub.Height(sx, sy);
                float lineH = oh + (th - oh) * t;
                if (terrain > lineH) return false; // 丘陵背面不可见
            }
            return true;
        }
    }
}