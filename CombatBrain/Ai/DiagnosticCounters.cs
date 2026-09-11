using System.Collections.Generic;

namespace CombatBrain.Ai
{
    // 临时只读诊断计数（CB-PROMPT-0038 §九 · 游戏内 A/B 验收准备）
    //
    // 目的：为「被退役的 Agent 主动写入全部为零」与「阵框释放的实际刷新频率 / 是否重复抢写或振荡」
    //   提供可读的运行期计数；步骤与判定口径见 docs/agent-control-retirement-ab-checklist.md。
    //
    // 硬纪律（不得违反）：
    //   * **只读**：只做整数累加与（Formation/Agent 索引、时间戳）簿记，不读取任何新信息、
    //     不调用任何游戏写入 API、不改变任何决策与执行路径；
    //   * 常数级：每次计数 = O(1)（FrameRelease 的去重集合为 HashSet.Add，仍为 O(1) 摊销）；
    //   * 归零：跨 Mission 由 Reset() 清空（MissionStateLifecycle 单一入口），Agent/Formation 索引复用不串场。
    //
    // 六类计数（与派单 §九 一一对应）：
    //   ① Formation 级写入        ② Agent 移动写入   ③ Agent 格挡写入
    //   ④ Agent 目标写入          ⑤ Agent 换武器写入 ⑥ Agent 属性覆盖
    // 退役完成后 ②–⑥ 的生产写入点已全部删除（源码 + OfflineTests/AgentWriteStaticChecks.ps1 双证），
    //   计数恒为 0 即为「零写入」的运行期证据；① 用于对照 Formation 级写入仍然活跃（无过度退役）。
    // FrameRelease 单列：写入次数 / 去重后受影响 agent 数 / 首末时间与时间分布（按 1s 桶直方图）。
    public static class DiagnosticCounters
    {
        private const int DistributionBuckets = 64; // 1s 桶 × 64 = 覆盖战斗前 64s 的分布（超界归入末桶）

        // ---- 六类主动写入计数 ----
        // 观测语义（QC F-4）：**运行期证据只对 ①（Formation 级写入）与 ⑦（FrameRelease 单列）有观测意义**——
        //   它们有活的生产调用点（FormationCommandExecutor.ApplySetOrder / TickFrameRelease）。
        //   ②–⑥ 是**结构性恒 0 的回归哨兵**：其生产写入点已随 CB-PROMPT-0038 整链退役（源码零写入 +
        //   OfflineTests/AgentWriteStaticChecks.ps1 静态零容忍双证），因此这五个计数器**永远不会自增**；
        //   它们的用途是「若未来有人重新引入逐兵写入，计数与静态门禁会同时报警」，**不是**当前行为的观测证据。
        //   不得因「无调用点」而删除（门禁断言要求六类计数器与 FrameRelease 计数同时存在）。
        public static int FormationWrites;   // ① 编队级三域命令落地次数（FormationCommandExecutor）——有活调用点
        public static int AgentMovementWrites; // ② [哨兵·结构性恒 0] Agent.SetMovementDirection（生产写入点已退役）
        public static int AgentBlockWrites;    // ③ [哨兵·结构性恒 0] Agent.EnforceShieldUsage（生产写入点已退役）
        public static int AgentTargetWrites;   // ④ [哨兵·结构性恒 0] Agent.SetTargetAgent（生产写入点已退役）
        public static int AgentWeaponWrites;   // ⑤ [哨兵·结构性恒 0] Agent.TryToWieldWeaponInSlot（生产写入点已退役）
        public static int AgentStatsOverrides; // ⑥ [哨兵·结构性恒 0] AgentDrivenProperties 覆盖（生产写入点已退役）

        // ---- FrameRelease 单列（保留项：刷新频率与振荡证据）----
        // 去重后受影响 agent 数一律以 `_frameReleasePerAgent.Count` 为准（QC F-4：原公开字段
        //   `FrameReleaseDistinctAgents` 无任何写入点，属死字段，已删除；查询走 FrameReleaseDistinctAgentCount()）。
        public static int FrameReleaseWrites;          // ⑦ 写入调用次数——有活调用点
        public static float FrameReleaseFirstAt = -1f; // 首次写入时刻（战斗累计秒）
        public static float FrameReleaseLastAt = -1f;  // 最近写入时刻
        private static readonly Dictionary<int, int> _frameReleasePerAgent = new Dictionary<int, int>(); // agent → 写入次数
        private static readonly int[] _frameReleaseBuckets = new int[DistributionBuckets];

        // ---- 只读计数入口（全部 O(1)，无分配——首次见到某 agent 时一次字典扩容）----

        public static void CountFormationWrite()
        {
            FormationWrites++;
        }

        public static void CountAgentMovementWrite()
        {
            AgentMovementWrites++;
        }

        public static void CountAgentBlockWrite()
        {
            AgentBlockWrites++;
        }

        public static void CountAgentTargetWrite()
        {
            AgentTargetWrites++;
        }

        public static void CountAgentWeaponWrite()
        {
            AgentWeaponWrites++;
        }

        public static void CountAgentStatsOverride()
        {
            AgentStatsOverrides++;
        }

        public static void CountFrameReleaseWrite(float now, int agentIndex)
        {
            FrameReleaseWrites++;
            if (FrameReleaseFirstAt < 0f) FrameReleaseFirstAt = now;
            FrameReleaseLastAt = now;

            int seen;
            if (_frameReleasePerAgent.TryGetValue(agentIndex, out seen)) _frameReleasePerAgent[agentIndex] = seen + 1;
            else _frameReleasePerAgent[agentIndex] = 1;

            int bucket = (int)now;
            if (bucket < 0) bucket = 0;
            if (bucket >= DistributionBuckets) bucket = DistributionBuckets - 1;
            _frameReleaseBuckets[bucket]++;
        }

        /// <summary>去重后受影响 agent 数（§九 判定口径：写入次数 ÷ 该数 = 平均每兵重复写次数）。</summary>
        public static int FrameReleaseDistinctAgentCount()
        {
            return _frameReleasePerAgent.Count;
        }

        /// <summary>某 agent 的 FrameRelease 写入次数（0 = 从未写入）。</summary>
        public static int FrameReleaseWritesFor(int agentIndex)
        {
            int seen;
            return _frameReleasePerAgent.TryGetValue(agentIndex, out seen) ? seen : 0;
        }

        /// <summary>时间分布桶（1s 粒度）的第 i 桶计数。</summary>
        public static int FrameReleaseBucket(int i)
        {
            return (i < 0 || i >= DistributionBuckets) ? 0 : _frameReleaseBuckets[i];
        }

        /// <summary>是否存在重复抢写嫌疑（任一 agent 写入次数 &gt; 1）。</summary>
        public static bool FrameReleaseHasRepeatWrite()
        {
            foreach (KeyValuePair<int, int> kv in _frameReleasePerAgent)
            {
                if (kv.Value > 1) return true;
            }
            return false;
        }

        /// <summary>Mission 级摘要（供 5s 汇总日志；无写入时返回空串，避免刷屏）。</summary>
        public static string BuildFrameReleaseSummary()
        {
            if (FrameReleaseWrites == 0) return string.Empty;
            return string.Format(
                "[Diag] FrameReleaseWrites={0} distinctAgents={1} first={2:0.0}s last={3:0.0}s repeatWrite={4}",
                FrameReleaseWrites, FrameReleaseDistinctAgentCount(),
                FrameReleaseFirstAt < 0f ? 0f : FrameReleaseFirstAt,
                FrameReleaseLastAt < 0f ? 0f : FrameReleaseLastAt,
                FrameReleaseHasRepeatWrite());
        }

        /// <summary>六类计数摘要（供收尾日志；每项独立可读）。</summary>
        public static string BuildWriteSummary()
        {
            return string.Format(
                "[Diag] FormationWrites={0} AgentMovement={1} AgentBlock={2} AgentTarget={3} AgentWeapon={4} AgentStatsOverride={5}",
                FormationWrites, AgentMovementWrites, AgentBlockWrites,
                AgentTargetWrites, AgentWeaponWrites, AgentStatsOverrides);
        }

        /// <summary>跨 Mission 幂等复位（由 MissionStateLifecycle 单一入口调用）。</summary>
        public static void Reset()
        {
            FormationWrites = 0;
            AgentMovementWrites = 0;
            AgentBlockWrites = 0;
            AgentTargetWrites = 0;
            AgentWeaponWrites = 0;
            AgentStatsOverrides = 0;
            FrameReleaseWrites = 0;
            FrameReleaseFirstAt = -1f;
            FrameReleaseLastAt = -1f;
            _frameReleasePerAgent.Clear(); // 去重 agent 数即以本字典 Count 呈现（无独立字段）
            for (int i = 0; i < DistributionBuckets; i++) _frameReleaseBuckets[i] = 0;
        }
    }
}
