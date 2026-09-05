namespace CombatBrain.Coordination
{
    using System.Collections.Generic;

    // ATC-2 Operational Objective 选择器（OperationalObjectiveSelector）—— 纯逻辑（零 TaleWorlds 可离线测）。
    // §4.2：
    //   敌军也按已知 Formation 构造连通簇（中心距 ≤ ObjectiveLinkMeters 连通）；
    //   Operational Objective = 已知敌军中总有效兵力最大的组织化簇（非按敌射手类别硬优先）；
    //   无已知敌簇 / 置信不足 / 全部过期 → HasOperationalObjective=false，不得用真实敌军位置补齐。
    // 迷雾一致（X6 IsEnemyFormationKnown）：只消费 IsKnown=true 的编队——未知大敌不能成为 Objective，
    //   重新观察（IsKnown=true）后可成为；过期（IsKnown=false）后撤销。
    // EffectiveStrength = Count（第一版 §4.3 允许仅 Count；本卡影子计算不乘系数，后续卡再加）。
    // 反作弊闸：输入=已知编队快照（胶水层已按 KnowledgeView 过滤），零新增禁读。
    public static class OperationalObjectiveSelector
    {
        public const float ObjectiveLinkMeters = 60f;  // 敌簇连通半径（与 MainBody 同口径）
        public const float MinObjectiveConfidence = 0.5f; // 置信下限：目标簇兵力 / 已知敌总兵力 ≥0.5 才算有效

        /// <summary>目标簇结果（影子输出）。</summary>
        public struct ObjectiveResult
        {
            public bool HasObjective;      // 是否存在有效目标
            public int FormationId;        // 目标簇代表编队（簇内最小 FormationId，确定性）
            public float X, Y;             // 簇内 Count 加权中心
            public int Strength;           // 簇内兵力和
            public float Confidence;       // 簇兵力 / 已知敌总兵力（0..1）
        }

        /// <summary>选择已知敌最大有效兵力组织化簇；空/无已知 → HasObjective=false。</summary>
        public static ObjectiveResult Select(System.Collections.Generic.IList<FormationCoordSnapshot> enemyFormations)
        {
            var r = new ObjectiveResult();
            if (enemyFormations == null || enemyFormations.Count == 0) return r;

            // 只消费已知编队（迷雾一致）；未知编队完全排除
            var known = new List<int>();
            int totalKnown = 0;
            for (int i = 0; i < enemyFormations.Count; i++)
            {
                if (!enemyFormations[i].IsKnown) continue;
                known.Add(i);
                totalKnown += enemyFormations[i].Count;
            }
            if (known.Count == 0) return r; // 全过期/全未知 → 无目标（§4.2 不得补齐）

            // 连通图（仅已知编队间）
            int n = known.Count;
            var adj = new bool[n, n];
            for (int a = 0; a < n; a++)
            {
                for (int b = a + 1; b < n; b++)
                {
                    var fa = enemyFormations[known[a]];
                    var fb = enemyFormations[known[b]];
                    float dx = fa.X - fb.X, dy = fa.Y - fb.Y;
                    bool link = (dx * dx + dy * dy) <= ObjectiveLinkMeters * ObjectiveLinkMeters;
                    adj[a, b] = link;
                    adj[b, a] = link;
                }
            }

            // 最大有效兵力簇（BFS；平局→最小 FormationId）
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
                if (IsBetter(members, known, enemyFormations, bestMembers, known, enemyFormations))
                    bestMembers = members;
            }

            // 聚合
            int strength = 0;
            float sx = 0f, sy = 0f;
            int minId = int.MaxValue;
            for (int i = 0; i < bestMembers.Count; i++)
            {
                var f = enemyFormations[known[bestMembers[i]]];
                strength += f.Count;
                sx += f.X * f.Count;
                sy += f.Y * f.Count;
                if (f.FormationId < minId) minId = f.FormationId;
            }
            if (strength <= 0) return r;

            r.FormationId = minId;
            r.X = sx / strength;
            r.Y = sy / strength;
            r.Strength = strength;
            r.Confidence = totalKnown > 0 ? (float)strength / totalKnown : 0f;

            // 置信不足 → 无目标（§4.2）
            if (r.Confidence < MinObjectiveConfidence) return new ObjectiveResult();
            r.HasObjective = true;
            return r;
        }

        // 簇比较：总兵力大者胜；相等→最小 FormationId（确定性）
        private static bool IsBetter(List<int> candidate, List<int> known, IList<FormationCoordSnapshot> list,
            List<int> current, List<int> knownCur, IList<FormationCoordSnapshot> curList)
        {
            int cStrength = SumStrength(candidate, known, list);
            int curStrength = current.Count > 0 ? SumStrength(current, knownCur, curList) : -1;
            if (cStrength != curStrength) return cStrength > curStrength;

            int cMinId = MinFormationId(candidate, known, list);
            int curMinId = current.Count > 0 ? MinFormationId(current, knownCur, curList) : int.MaxValue;
            return cMinId < curMinId;
        }

        private static int SumStrength(List<int> members, List<int> known, IList<FormationCoordSnapshot> list)
        {
            int s = 0;
            for (int i = 0; i < members.Count; i++) s += list[known[members[i]]].Count;
            return s;
        }

        private static int MinFormationId(List<int> members, List<int> known, IList<FormationCoordSnapshot> list)
        {
            int min = int.MaxValue;
            for (int i = 0; i < members.Count; i++)
            {
                int id = list[known[members[i]]].FormationId;
                if (id < min) min = id;
            }
            return min;
        }
    }
}
