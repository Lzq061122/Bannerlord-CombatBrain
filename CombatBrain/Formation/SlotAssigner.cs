namespace CombatBrain.Formations
{
    // X5-4 agent↔slot 稳定映射（SlotAssigner）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 语义：把编队真实 agent 稳定映射到 X5-2/X5-3 产出的 Slot 列表（设计稿 §六 SlotAssigner.cs：
    //   "agent↔slot 稳定映射（就近占位、Full/Slight 补位规则、跳过 X4 已解阵 agent；只指派位置，
    //   不下移动命令"——执行腿在 X5-6）。
    // 指派规则：
    //   ① 稳定保持：同一 agent 上一拍占用的 slot 本拍仍有效（agent 存活、未解阵、slot 角色匹配、
    //      未被他人占用）→ 直接保持（防抖动，§八.4 稳定映射）；
    //   ② 就近占位：未指派的 agent 找最近（距离 slot.WorldPos）的空闲匹配 slot；
    //   ③ 补位分流（消费 FormationIntegrity.Grade()，§七）：Full（≥80）严格维持——已指派 agent
    //      只允许补同 Rank 的空闲匹配 slot（跨 Rank 不补）；Slight（60~79）允许局部补位——Rank
    //      邻位互换（|rank差|≤1，不重新生成布局）；
    //   ④ X4 解阵跳过集：已 SetFormationFrameDisabled 的 agent 从 slot 刷新跳过（消费 X4 FrameRelease
    //      判定语义，不新增解阵触发——红线 2）。
    // 输入契约（胶水层喂）：slot 列表 + agent 角色表（agentId→TroopRole，X5-1 TroopClassifier 归类）
    //   + agent 当前位置 + 上一拍 assignments + 编队完整度 + X4 跳过集。
    // 输出：assignments（agent→slot 索引）。只指派位置，不下任何移动命令。
    // 反作弊闸：只消费己方编队内部信息（角色/位置/完整度均为己方公开聚合），无越权。
    // 约束：不改 SlotLayout/TerrainWarp/FormationIntegrity；不选型不解阵（红线 1/2）。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。

    /// <summary>指派结果：agent → slot 映射（SlotIndex 指向输入 slot 列表下标）。</summary>
    public struct SlotAssignment
    {
        public int AgentIndex;
        public int SlotIndex;

        public SlotAssignment(int agentIndex, int slotIndex)
        {
            AgentIndex = agentIndex;
            SlotIndex = slotIndex;
        }
    }

    /// <summary>agent 角色表条目（胶水层用 X5-1 TroopClassifier 归类喂入；X/Y 为 agent 当前位置）。</summary>
    public struct AgentRoleEntry
    {
        public int AgentIndex;
        public TroopRole Role;
        public float X;
        public float Y;

        public AgentRoleEntry(int agentIndex, TroopRole role, float x, float y)
        {
            AgentIndex = agentIndex;
            Role = role;
            X = x;
            Y = y;
        }
    }

    /// <summary>指派输入（纯逻辑契约，零 TaleWorlds）。</summary>
    public struct SlotAssignInput
    {
        public System.Collections.Generic.List<FormationSlot> Slots;        // X5-2/X5-3 输出
        public System.Collections.Generic.List<AgentRoleEntry> Agents;      // 活跃 agent（胶水层喂）
        public System.Collections.Generic.List<SlotAssignment> Last;        // 上一拍指派（防抖动）
        public float Integrity;                                            // 编队完整度（走 Grade() 分流）
        // CB-PROMPT-0038 §五.7：X4 解阵跳过集（FrameDisabledAgents）已删除——
        //   它只服务于已退役的「逐兵向 Slot 目标移动」执行腿；解阵框现在是一次性 latch（见 FrameReleaseGlue.cs）。
    }

    public static class SlotAssigner
    {
        /// <summary>
        /// 指派：稳定保持（上一拍）+ 就近占位（未指派）。Full 严格同 Rank 补位；Slight 允许 |rank差|≤1。
        /// 空输入返回空列表。只产出 assignments（编队级观察性绑定表），不下任何移动命令、无 Agent 写入。
        /// </summary>
        public static System.Collections.Generic.List<SlotAssignment> Assign(SlotAssignInput input)
        {
            var result = new System.Collections.Generic.List<SlotAssignment>();
            if (input.Slots == null || input.Slots.Count == 0 || input.Agents == null || input.Agents.Count == 0)
                return result;

            // 补位档位：Full（≥80）严格同 Rank；Slight（60~79）允许 Rank 邻位互换（|差|≤1）；更低档本步不指派
            FormationIntegrityGrade grade = FormationIntegrity.Grade(input.Integrity);
            int maxRankDiff = grade == FormationIntegrityGrade.Full ? 0 : 1;

            // agent 索引 → 角色 查表（posByAgent 死字典已删除——就近占位直接用 AgentRoleEntry.X/Y，见 x5-assign-posbyagent-unused）
            var roleByAgent = new System.Collections.Generic.Dictionary<int, TroopRole>();
            for (int i = 0; i < input.Agents.Count; i++)
            {
                AgentRoleEntry a = input.Agents[i];
                roleByAgent[a.AgentIndex] = a.Role;
            }

            // 已指派 agent 集合 + 已占用 slot 集合
            var assignedAgents = new System.Collections.Generic.HashSet<int>();
            var takenSlots = new System.Collections.Generic.HashSet<int>();

            // ① 稳定保持：上一拍 agent 存活、slot 有效、角色匹配、未被占用 → 保持
            if (input.Last != null)
            {
                for (int i = 0; i < input.Last.Count; i++)
                {
                    SlotAssignment prev = input.Last[i];
                    if (!roleByAgent.ContainsKey(prev.AgentIndex)) continue;               // 死亡/移除
                    if (prev.SlotIndex < 0 || prev.SlotIndex >= input.Slots.Count) continue; // slot 失效
                    if (takenSlots.Contains(prev.SlotIndex)) continue;                     // 已被他人占用
                    FormationSlot slot = input.Slots[prev.SlotIndex];
                    TroopRole role;
                    if (!roleByAgent.TryGetValue(prev.AgentIndex, out role)) continue;
                    if (slot.Role != role) continue;                                       // 角色不匹配（布局换过）
                    result.Add(prev);
                    assignedAgents.Add(prev.AgentIndex);
                    takenSlots.Add(prev.SlotIndex);
                }
            }

            // ② 就近占位：未指派 agent 找最近空闲匹配 slot（Slight 允许邻 Rank，Full 同 Rank）
            for (int i = 0; i < input.Agents.Count; i++)
            {
                AgentRoleEntry a = input.Agents[i];
                if (assignedAgents.Contains(a.AgentIndex)) continue;

                int prevRank = PreviousRank(input, a.AgentIndex); // -1 = 新兵（任意 Rank 可占）
                int best = -1;
                float bestD = float.MaxValue;
                for (int s = 0; s < input.Slots.Count; s++)
                {
                    if (takenSlots.Contains(s)) continue;
                    FormationSlot slot = input.Slots[s];
                    if (slot.Role != a.Role) continue;
                    if (prevRank >= 0 && System.Math.Abs(slot.Rank - prevRank) > maxRankDiff) continue; // Full 严格/ Slight 邻位
                    float dx = slot.WorldX - a.X;
                    float dy = slot.WorldY - a.Y;
                    float d = dx * dx + dy * dy;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = s;
                    }
                }
                if (best >= 0)
                {
                    result.Add(new SlotAssignment(a.AgentIndex, best));
                    assignedAgents.Add(a.AgentIndex);
                    takenSlots.Add(best);
                }
            }
            return result;
        }

        // 上一拍该 agent 的 slot Rank（无上一拍 → -1 = 新兵）
        private static int PreviousRank(SlotAssignInput input, int agentIndex)
        {
            if (input.Last == null) return -1;
            for (int i = 0; i < input.Last.Count; i++)
            {
                if (input.Last[i].AgentIndex != agentIndex) continue;
                int si = input.Last[i].SlotIndex;
                if (si >= 0 && si < input.Slots.Count) return input.Slots[si].Rank;
                return -1;
            }
            return -1;
        }
    }
}
