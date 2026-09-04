namespace CombatBrain.Ai
{
    // X1 编队级目标选择（FormationTargeting）—— 纯逻辑判定（可离线测），零 TaleWorlds 依赖。
    // 语义：每个编队从「剧场半径内的可见敌方编队」中选一个有意义的目标——杜绝被小股敌人勾引
    //   走散（骑兵被 3 人勾走 / 阵型追散兵 = "一眼没脑子"头号来源，派单 X1）。
    // 只产出选择（TargetSelection）供下游消费（X3 朝向 / X4 解阵框 / 骑兵冲锋目标），本层不下任何命令。
    // 反作弊闸：输入只有引擎公开聚合（Formation.QuerySystem 兵种旗标 / CountOfUnits / 编队中心位置，
    //   Formation.cs L241/L182），禁全图、禁个体遍历。
    // 优先级链（契约①）：敌射手 > 敌骑射 > 敌骑兵 > 敌步兵；同类内按「距离 × 兵力」加权取最优。
    // 小股过滤（RBM Utilities.cs:124-127）：候选兵力 < 己方 60% 不计入（己方零兵力不除零 → 全部计入）。
    // 迟滞缓存（RBM OverrideMovementOrder.cs:29 7m 迟滞）：新目标距旧目标 ≤7m 沿用旧目标，消除目标点抖动。
    // 0.5s 编队级缓存：缓存窗内直接返回上次选择（胶水层 0.5s 节拍 + 本层时间缓存双保险）。
    public enum FormationTargetKind
    {
        Infantry = 0,      // 敌步兵
        Cavalry = 1,       // 敌骑兵
        RangedCavalry = 2, // 敌骑射
        Ranged = 3         // 敌射手（最高优先：对步兵线威胁最大）
    }

    // 候选敌方编队（纯数值快照，胶水层构造；零 TaleWorlds 类型）
    public struct TargetCandidate
    {
        public int Index;   // 编队索引（Formation.Index，引擎公开只读字段）
        public float X, Y;  // 编队中心位置（胶水层按 TeamPosKind 口径取数）
        public int Count;   // 编队兵力（Formation.CountOfUnits，公开聚合）
        public FormationTargetKind Kind;
    }

    // 目标选择结果（纯数值；Index=-1/HasTarget=false = 无目标降级）
    public struct TargetSelection
    {
        public bool HasTarget;
        public int Index;
        public float X, Y;
        public FormationTargetKind Kind;
        public float Time; // 选择时刻（缓存窗口判定用，秒）
        public static readonly TargetSelection None = new TargetSelection { HasTarget = false, Index = -1, Time = -1f };
    }

    public static class FormationTargeting
    {
        public const float SignificantRatio = 0.6f;  // RBM Utilities.cs:124-127：敌兵力 < 己方 60% 不计入候选
        public const float TheaterRadius = 150f;     // 剧场半径（沿用 150m 先例：F8 骑兵/骑射 150m）
        public const float HysteresisMeters = 7f;    // RBM OverrideMovementOrder.cs:29：目标点 7m 迟滞
        public const float CacheSeconds = 0.5f;      // 编队级缓存窗（契约③ 0.5s）

        /// <summary>
        /// 从候选敌方编队中选目标：
        /// ① 过滤：空编队（Count&lt;=0）剔除；己方有兵力时 敌兵力&lt;己方×60% 剔除（小股不勾引）；
        ///    超剧场半径（&gt;150m）剔除；② 优先级链：射手&gt;骑射&gt;骑兵&gt;步兵，同类内 score=Count/(1+距离) 取最优
        ///    （近+人多 = 高分，距离加权×威胁加权）；③ 7m 迟滞：新目标距上次目标 ≤7m 沿用上次；
        /// ④ 0.5s 缓存：now-上次选择时刻 &lt; 0.5s 直接返回上次。
        /// count = 候选有效数量（胶水层复用缓冲数组，只遍历前 count 项）。
        /// 无候选/全部被滤 → TargetSelection.None（无目标降级，调用方交原版）。
        /// </summary>
        public static TargetSelection Select(float ownX, float ownY, int ownCount,
            TargetCandidate[] candidates, int count, TargetSelection last, float now)
        {
            // ④ 缓存窗内直接返回上次（不重算；Time=-1 的 None 不命中缓存）
            if (last.HasTarget && now - last.Time < CacheSeconds) return last;

            TargetSelection best = TargetSelection.None;
            float bestScore = -1f;
            int bestKind = -1;

            if (candidates != null)
            {
                for (int i = 0; i < count; i++)
                {
                    TargetCandidate c = candidates[i];
                    if (c.Count <= 0) continue;                                  // 空编队剔除
                    if (ownCount > 0 && c.Count < ownCount * SignificantRatio) continue; // 小股过滤（零兵力不除零）
                    float dx = c.X - ownX, dy = c.Y - ownY;
                    float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
                    if (dist > TheaterRadius) continue;                          // 剧场半径内可见
                    float score = c.Count / (1f + dist);                         // 距离加权 × 威胁加权

                    int kind = (int)c.Kind;
                    // 优先级链：类别高者胜出；同类内 score 高者胜出
                    if (kind > bestKind || (kind == bestKind && score > bestScore))
                    {
                        bestKind = kind;
                        bestScore = score;
                        best = new TargetSelection
                        {
                            HasTarget = true,
                            Index = c.Index,
                            X = c.X,
                            Y = c.Y,
                            Kind = c.Kind,
                            Time = now
                        };
                    }
                }
            }

            if (!best.HasTarget) return TargetSelection.None; // 无目标降级

            // ③ 7m 迟滞：新目标距上次目标 ≤7m → 沿用上次（刷新 Time 维持缓存，防目标点抖动）
            if (last.HasTarget)
            {
                float ldx = best.X - last.X, ldy = best.Y - last.Y;
                if (ldx * ldx + ldy * ldy <= HysteresisMeters * HysteresisMeters)
                {
                    last.Time = now;
                    return last;
                }
            }
            return best;
        }
    }
}
