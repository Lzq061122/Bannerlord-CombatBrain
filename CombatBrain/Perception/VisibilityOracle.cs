namespace CombatBrain.Perception
{
    // 可见性判定：O(格) 而非 O(士兵)。距离门 + 粗糙视线采样。
    // P1-①：地形高度来自注入的 TerrainSampler（真实场景开战预缓存查表，与态势向量 Height 维共用）；
    // 未注入 → 平地降级（高度恒 0，视线仅受距离门限制）——宁缺毋假，
    // 合成正弦假地形 TerrainStub 已删除（review-findings-2026-09-01 #7）。
    public sealed class VisibilityOracle
    {
        private const float EyeHeight = 1.7f;
        private const int LoSSamples = 4;

        private readonly float _range;
        private TerrainSampler _terrain;   // 开战由胶水层 SetTerrain 一次性注入共享查表（主线程，战中只读）

        public VisibilityOracle(float range)
            : this(range, null)
        {
        }

        public VisibilityOracle(float range, TerrainSampler terrain)
        {
            _range = range;
            _terrain = terrain;
        }

        // P1-① 与态势向量共用同一真实地形查表（开战构建后一次性注入；保持可逆——不注入即平地降级）
        public void SetTerrain(TerrainSampler terrain)
        {
            _terrain = terrain;
        }

        public float Range { get { return _range; } }

        public bool IsVisible(float ox, float oy, float tx, float ty)
        {
            float d = VecMath.Dist(ox, oy, tx, ty);
            if (d > _range) return false;

            float oh = TerrainHeightAt(ox, oy) + EyeHeight;
            float th = TerrainHeightAt(tx, ty) + EyeHeight;

            for (int i = 1; i < LoSSamples; i++)
            {
                float t = (float)i / LoSSamples;
                float sx = ox + (tx - ox) * t;
                float sy = oy + (ty - oy) * t;
                float terrain = TerrainHeightAt(sx, sy);
                float lineH = oh + (th - oh) * t;
                if (terrain > lineH) return false; // 丘陵背面不可见
            }
            return true;
        }

        private float TerrainHeightAt(float x, float y)
        {
            return _terrain != null ? _terrain.Height(x, y) : 0f; // 未注入 → 平地降级
        }
    }
}
