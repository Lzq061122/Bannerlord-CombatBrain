namespace CombatBrain.Coordination
{
    using System.Collections.Generic;

    // ATC-2 Main Body 选择器（MainBodySelector）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // §4.1 最大连通编队簇：
    //   1. 输入己方全部非空 Formation 快照；
    //   2. 两编队中心距 ≤ MainBodyLinkMeters 视为连通；
    //   3. 找总兵力最大的连通簇；总兵力相同优先含更多 Infantry/Ranged，再按最小 FormationId 打破平局；
    //   4. MainBodyCenter = 簇内按 Count 加权中心；
    //   5. MainBodyStrength = 簇内兵力和；
    //   6. 骑兵/骑射可属于簇，但孤立在外不拉偏中心（孤立=不在最大簇内 → 不计入中心）。
    // 算法 O(F²) 纯逻辑确定性；空输入 → MainBody 全 0/Confidence=0。
    // 反作弊闸：输入=既有公开聚合快照，零新增禁读。
    public static class MainBodySelector
    {
        public const float MainBodyLinkMeters = 60f; // §4.1：中心距 ≤60m 视为连通（可离线断言）

        /// <summary>最大连通簇结果（MainBody 影子输出）。</summary>
        public struct MainBodyResult
        {
            public float X, Y;         // Count 加权中心
            public int Strength;       // 簇内兵力和
            public float Confidence;   // 簇兵力 / 己方总兵力（0..1；总兵力 0 → 0）
            public int MemberCount;    // 簇内编队数
        }

        /// <summary>选择最大连通簇（O(F²)）；空/全空输入 → 零结果。</summary>
        public static MainBodyResult Select(System.Collections.Generic.IList<FormationCoordSnapshot> formations)
        {
            var r = new MainBodyResult();
            if (formations == null || formations.Count == 0) return r;

            // 连通图邻接（O(F²)：两两中心距 ≤ MainBodyLinkMeters）
            int n = formations.Count;
            var adj = new bool[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    float dx = formations[i].X - formations[j].X;
                    float dy = formations[i].Y - formations[j].Y;
                    bool link = (dx * dx + dy * dy) <= MainBodyLinkMeters * MainBodyLinkMeters;
                    adj[i, j] = link;
                    adj[j, i] = link;
                }
            }

            // 全部编队总兵力（Confidence 分母）
            int totalStrength = 0;
            for (int i = 0; i < n; i++) totalStrength += formations[i].Count;

            // 找最大簇：BFS 每个未访问编队
            var visited = new bool[n];
            var bestMembers = new List<int>();
            for (int s = 0; s < n; s++)
            {
                if (visited[s]) continue;
                var members = new List<int>();
                var queue = new Queue<int>();
                queue.Enqueue(s);
                visited[s] = true;
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    members.Add(cur);
                    for (int k = 0; k < n; k++)
                    {
                        if (!adj[cur, k] || visited[k]) continue;
                        visited[k] = true;
                        queue.Enqueue(k);
                    }
                }
                // 簇评分：总兵力 → 含更多 Infantry/Ranged → 最小 FormationId（确定性平局）
                if (IsBetter(members, formations, bestMembers, formations))
                {
                    bestMembers = members;
                }
            }

            // 计算中心/兵力（仅最大簇成员；孤立骑/骑射不在簇内 → 不拉偏）
            int strength = 0;
            float sx = 0f, sy = 0f;
            for (int i = 0; i < bestMembers.Count; i++)
            {
                FormationCoordSnapshot f = formations[bestMembers[i]];
                strength += f.Count;
                sx += f.X * f.Count;
                sy += f.Y * f.Count;
            }
            if (strength > 0)
            {
                r.X = sx / strength;
                r.Y = sy / strength;
            }
            r.Strength = strength;
            r.Confidence = totalStrength > 0 ? (float)strength / totalStrength : 0f;
            r.MemberCount = bestMembers.Count;
            return r;
        }

        // 簇比较：总兵力大者胜；相等→更多 Infantry/Ranged；再相等→最小 FormationId（确定性）
        private static bool IsBetter(List<int> candidate, IList<FormationCoordSnapshot> candList,
            List<int> current, IList<FormationCoordSnapshot> curList)
        {
            int cStrength = SumStrength(candidate, candList);
            int curStrength = current.Count > 0 ? SumStrength(current, curList) : -1;
            if (cStrength != curStrength) return cStrength > curStrength;

            int cInfRng = SumInfantryRanged(candidate, candList);
            int curInfRng = current.Count > 0 ? SumInfantryRanged(current, curList) : -1;
            if (cInfRng != curInfRng) return cInfRng > curInfRng;

            int cMinId = MinFormationId(candidate, candList);
            int curMinId = current.Count > 0 ? MinFormationId(current, curList) : int.MaxValue;
            return cMinId < curMinId;
        }

        private static int SumStrength(List<int> members, IList<FormationCoordSnapshot> list)
        {
            int s = 0;
            for (int i = 0; i < members.Count; i++) s += list[members[i]].Count;
            return s;
        }

        private static int SumInfantryRanged(List<int> members, IList<FormationCoordSnapshot> list)
        {
            int s = 0;
            for (int i = 0; i < members.Count; i++)
            {
                var f = list[members[i]];
                if (f.Kind == FormationCoordKind.Infantry || f.Kind == FormationCoordKind.Ranged) s += f.Count;
            }
            return s;
        }

        private static int MinFormationId(List<int> members, IList<FormationCoordSnapshot> list)
        {
            int min = int.MaxValue;
            for (int i = 0; i < members.Count; i++)
            {
                int id = list[members[i]].FormationId;
                if (id < min) min = id;
            }
            return min;
        }
    }
}
