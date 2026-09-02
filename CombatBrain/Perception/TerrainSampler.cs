namespace CombatBrain.Perception
{
    using System;

    // P1-① 真实地形高度查表：开战一次性预采样 → 战中只读 O(1) 双线性查表。
    // 替代已删除的 TerrainStub 合成正弦假地形（review-findings-2026-09-01 #7「假数据比没数据更糟」）。
    // 纯逻辑零依赖：真实采样函数由胶水层以委托注入（不引用 Mission/Scene，可离线回归；
    // v1.4.8 真实 API 见 CombatAiMissionLogic.EnsureTerrainCache 注释）。
    // T0 线程安全：Build 在战前单线程填好 float[]，此后数组只读——无锁共享（规范允许只读缓存）。
    public sealed class TerrainSampler
    {
        // 胶水层注入的真实高度采样回调（如 Scene.GetTerrainHeight）。
        public delegate float HeightQuery(float x, float y);

        private readonly float[] _heights;   // n*n 网格高度，Build 后只读
        private readonly float _originX;     // 网格最小角
        private readonly float _originY;
        private readonly float _step;        // 网格间距（米）
        private readonly int _n;             // 每轴采样点数（n>=2）

        private TerrainSampler(float[] heights, float originX, float originY, float step, int n)
        {
            _heights = heights;
            _originX = originX;
            _originY = originY;
            _step = step;
            _n = n;
        }

        // 每轴采样点数
        public int GridSize { get { return _n; } }

        // 总采样点数（开战一次性成本预算）
        public int Points { get { return _n * _n; } }

        // 实际网格间距（≤ 请求 cellSize，由战场尺寸整除对齐）
        public float Step { get { return _step; } }

        // 以 (centerX, centerY) 为核心、2*halfExtent 见方、约 cellSize 间距采样。
        // 战前一次性调用（主线程）；query 内部做真实场景查询，本方法不做任何缓存外的游戏访问。
        public static TerrainSampler Build(float centerX, float centerY, float halfExtent, float cellSize, HeightQuery query)
        {
            halfExtent = Math.Max(1f, halfExtent);
            cellSize = Math.Max(0.5f, cellSize);
            int cells = Math.Max(1, (int)Math.Ceiling(2f * halfExtent / cellSize));
            float step = 2f * halfExtent / cells;
            float originX = centerX - halfExtent;
            float originY = centerY - halfExtent;
            int n = cells + 1;
            float[] heights = new float[n * n];
            for (int j = 0; j < n; j++)
            {
                float y = originY + j * step;
                int rowBase = j * n;
                for (int i = 0; i < n; i++)
                {
                    heights[rowBase + i] = query(originX + i * step, y);
                }
            }
            return new TerrainSampler(heights, originX, originY, step, n);
        }

        // O(1) 双线性查表；越界取边缘值（优雅降级，不产生零高度断崖）。
        public float Height(float x, float y)
        {
            float fx = (x - _originX) / _step;
            float fy = (y - _originY) / _step;
            if (fx < 0f) fx = 0f; else if (fx > _n - 1) fx = _n - 1;
            if (fy < 0f) fy = 0f; else if (fy > _n - 1) fy = _n - 1;
            int i0 = (int)fx;
            int j0 = (int)fy;
            if (i0 > _n - 2) i0 = _n - 2;
            if (j0 > _n - 2) j0 = _n - 2;
            float tx = fx - i0;
            float ty = fy - j0;
            int idx = j0 * _n + i0;
            float h00 = _heights[idx];
            float h10 = _heights[idx + 1];
            float h01 = _heights[idx + _n];
            float h11 = _heights[idx + _n + 1];
            return (h00 * (1f - tx) + h10 * tx) * (1f - ty) + (h01 * (1f - tx) + h11 * tx) * ty;
        }
    }
}
