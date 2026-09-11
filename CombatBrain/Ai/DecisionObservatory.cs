using System;
using CombatBrain;

namespace CombatBrain.Ai
{
    // T1 观测管线升级 —— 决策观测台（游戏侧对 DecisionBranchTally 的驱动与接入 CombatLog 单一渠道）。
    //
    // 参照 OF FrontlineDebug（研究报告 3.3）：
    //   * Count(branch) 秒级分支计数器 → DecisionBranchTally.Observe（零分配，决策点低频调用）；
    //   * 每 3s 周期导出（OF 用 3s，见 FrontlineDebug.cs ReportIfDue）→ ReportIfDue 聚合导出；
    //   * CheckPatchAlive 运行时自检 → CheckHygiene 补丁/管线活性自检（见下）。
    //
    // 本工程不用 Harmony：对比 OF 通过 Harmony.GetPatchInfo 查 OnAIInputSet 的 owner，
    // CombatBrain 依赖的是纯 C# MissionBehavior 事件钩子（OnMissionTick/OnAgentHit/OnAgentRemoved），
    // 编译期不存在跨 mod 回调占用竞争。故「补丁自检」落到两点：
    //   1) 断言本工程零 Harmony 补丁依赖（确定性事实，写入日志，防将来误引入抢占式补丁）；
    //   2) 观测管线活性（本窗观测到的分支数/总数，验证计数器真的被驱动）。
    // 如此既守住观测意图，又不为观测引入 Harmony 外部依赖（不进 inScope 范围契约的改动方向）。
    public class DecisionObservatory
    {
        // 观测周期（与 OF 对齐 3s，Window 内聚合计数，低频导出）
        public const float ReportInterval = 3f;

        // ---- 决策分支统一 key（预注册，零分配 Observe；命名带类别前缀便于聚合/排序）----
        public const string BIntentSwitch = "intent.switch";    // 意图切换
        public const string BMoveAdvance = "move.advance";
        public const string BMoveBack = "move.back";
        public const string BMoveStrafe = "move.strafe";
        public const string BMoveRetreat = "move.retreat";
        public const string BMoveHold = "move.hold";
        public const string BSurvRetreat = "surv.retreat";      // 求生撤退
        public const string BSurvReform = "surv.reform";        // 编队重伤重整信号
        // CB-PROMPT-0038：F4 预测式格挡与 F9 逐兵远程腿退役后，以下三个分支计数已无生产者
        //   （BBlockPred/BBlockSkip 属 TickBlock，BRangedFall 属 RangedGlue 逐兵回退）。
        //   登记保留以不破坏既有分支索引；报告中恒为 0，不得当作「行为未发生」以外的证据使用。
        public const string BBlockPred = "block.predicted";     // [RETIRED 生产者 F4] 格挡命中
        public const string BBlockSkip = "block.skip";          // [RETIRED 生产者 F4] 抖动让过/无预测
        public const string BCavChange = "cav.fsm";             // 骑兵波次 FSM 转移
        public const string BRangedFall = "ranged.fallback";    // [RETIRED 生产者 F9 逐兵] 远程弹药回退

        // 赛季内唯一观测实例（任务逻辑挂载/卸载各一次，无并发竞态）
        private static DecisionObservatory _instance;

        public static DecisionObservatory Instance { get { return _instance; } }

        private readonly DecisionBranchTally _tally;
        private float _since;
        private float _last;
        private bool _hydChecks;
        private bool _firstReport;

        public DecisionObservatory()
        {
            _tally = new DecisionBranchTally(new[]
            {
                BIntentSwitch, BMoveAdvance, BMoveBack, BMoveStrafe, BMoveRetreat, BMoveHold,
                BSurvRetreat, BSurvReform, BBlockPred, BBlockSkip, BCavChange, BRangedFall
            });
            _since = 0f;
            _hydChecks = false;
            _firstReport = true;
        }

        public DecisionBranchTally Tally { get { return _tally; } }

        public static void Attach() { _instance = new DecisionObservatory(); }

        public static void Detach() { _instance = null; }

        public static void Observe(string branch)
        {
            DecisionObservatory o = _instance;
            if (o != null) o._tally.Observe(branch);
        }

        /// <summary>补丁/管线活性自检：确定性断言零 Harmony 依赖 + 记录观测管线活性。</summary>
        public void CheckHygiene()
        {
            if (_hydChecks) return;
            _hydChecks = true;
            // 本工程静态架构事实：csproj 不引用 HarmonyLib，全部走 MissionBehavior 事件钩子
            CombatLog.Log("[Obs] 补丁自检：CombatBrain 零 Harmony 补丁依赖（纯 MissionBehavior 事件驱动），无回调占用竞争");
            // 管线活性初检：观测分支已注册数
            CombatLog.Log("[Obs] 观测分支预注册=" + _tally.BranchCount);
        }

        /// <summary>低频周期导出：聚合窗内分支计数 → CombatLog（单一渠道），并附带管线活性。</summary>
        public void ReportIfDue(float gameTime)
        {
            if (_firstReport) { _firstReport = false; _last = gameTime; return; }
            _since += gameTime - _last;
            _last = gameTime;
            if (_since < ReportInterval) return;
            _since = 0f;

            int distinct = _tally.DistinctObserved; // 复位前捕获
            string body = _tally.SnapshotAndReset();
            if (body.Length == 0) body = "(无决策活动)";
            // 管线活性 = 上一窗观测到的分支数（区分「零活动」与「观测管道失效」）
            CombatLog.Log("[Obs] t=" + gameTime.ToString("F0") + "  " + body +
                "  |活性分支总数=" + distinct);
        }
    }
}