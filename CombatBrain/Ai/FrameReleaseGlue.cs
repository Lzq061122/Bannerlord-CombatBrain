namespace CombatBrain.Ai
{
    // X4 接敌解阵框（FrameRelease）—— 纯逻辑判定（可离线测），零 TaleWorlds 依赖。
    // 语义：接敌时（编队 MovementOrder 为 Charge/ChargeToTarget 且距最显著敌编队 ≤25m）让士兵脱离
    //   刚性阵型槽位自由接敌——战线从一条直线变成犬牙交错。
    //   执行 = CombatAiMissionLogic.TickFrameRelease 对每个合格 agent 调 Agent.SetFormationFrameDisabled()
    //   （v1.4.8 Agent.cs:2306-2308 已核实：public void SetFormationFrameDisabled()，Agent 级非 Formation 级），
    //   与 RBM RBMAI OverrideFormation.PostfixParallelUpdateFormationMovement 同款触发
    //   （RBMAI.decompiled.cs:7362-7408：MovementOrder==Charge/ChargeToTarget(2/3) 时解阵框）。
    // 本层只产出判定与冷却状态，不下任何命令（分域：不改 Formation 排列/阵深= T2-01 领地，
    //   不动 ArrangementOrder，零 Harmony）。
    // 判定链：
    //   ① 编队级触发：MovementOrder 为 Charge(2)/ChargeToTarget(3)（MovementOrder.cs:16-17 已核实）
    //      且 距 X1 t93 TryGetFormationTarget 最显著敌编队 ≤ ReleaseRangeMeters=25m；
    //   ② 阵型规模闸门：编队 ≤25 人不启用（RBM 同款阈值，人太少不值得散开）；
    //   ③ 剧场效应闸门（硬性）：仅 L3（玩家可见区）执行，L0-L2 交原版；
    //   ④ 逐 agent 跳过态：F7 求生撤退 / 原版逃跑（IsRunningAway）中的兵跳过
    //      （F9 远程后撤态已随 CB-PROMPT-0038 §五.6 退役；求生优先于散开接敌）；
    //   ⑤ 逐 agent 冷却字典限频（RBM Frontline.cs:909 aiDecisionCooldownDict 同款；p3-1 设计：
    //      键=agent.Index（int 防引用泄漏），秒数制冷却，随 OnAgentRemoved 清理）。
    // 反作弊闸：只解除自身阵框约束（能力非信息），不改信息获取（编队中心/命令状态均为引擎公开聚合）。
    // 玩家队豁免：ShouldExempt 与 FacingCorrection 同语义（P0-④ MCM 开关）。
    // 性能：本项是唯一「个体遍历」例外（铁律②），靠 L3 闸门 + 冷却字典把规模压住；
    //   若 500 人 [Bench] 个体段 avgAi 超 5ms 立即回退本项。
    public static class FrameReleaseRules
    {
        public const float ReleaseRangeMeters = 25f; // 接敌距离阈值（RBM 蓝本 ≤25m）
        public const int MinReleaseCount = 25;       // 编队 ≤25 人不启用（RBM 同款阈值）
        public const int ChargeOrderEnum = 2;        // MovementOrderEnum.Charge（MovementOrder.cs:16）
        public const int ChargeToTargetOrderEnum = 3;// MovementOrderEnum.ChargeToTarget（MovementOrder.cs:17）

        /// <summary>① 命令判定：Charge/ChargeToTarget 才算接敌冲锋。</summary>
        public static bool IsChargeOrder(int orderEnum)
        {
            return orderEnum == ChargeOrderEnum || orderEnum == ChargeToTargetOrderEnum;
        }

        /// <summary>①+② 编队级触发：冲锋命令 且 距目标≤25m 且 编队&gt;25 人。</summary>
        public static bool ShouldReleaseFormation(bool isChargeOrder, float targetDist, int count)
        {
            return isChargeOrder && targetDist <= ReleaseRangeMeters && count > MinReleaseCount;
        }

        /// <summary>③ 剧场闸门：仅 L3（玩家可见区）执行（Enums.cs LodLevel.L3=3）。</summary>
        public static bool IsReleaseLod(int lodLevel)
        {
            return lodLevel >= 3;
        }

        /// <summary>④ 跳过态：求生撤退/远程后撤/原版逃跑 → 跳过（求生存优先于散开）。</summary>
        public static bool ShouldSkipAgent(bool survivalRetreat, bool fallingBack, bool runningAway)
        {
            return survivalRetreat || fallingBack || runningAway;
        }

        /// <summary>玩家队豁免判定（P0-④ MCM 开关；true=该队跳过解阵框）。</summary>
        public static bool ShouldExempt(bool teamIsPlayer, bool exemptionEnabled)
        {
            return exemptionEnabled && teamIsPlayer;
        }
    }

    // 逐 agent 冷却字典（p3-1-design-decision-cooldown 设计落地：键=agent.Index，秒数制；
    // RBM Frontline.cs:909 aiDecisionCooldownDict 同款，解阵框后冷却窗内不重复调——SetFormationFrameDisabled
    // 是一次性状态切换，重复调用无意义，冷却窗防每拍重发）。
    public sealed class FrameReleaseCooldown
    {
        public const float CooldownSeconds = 5f; // 冷却窗（秒）：解阵框后 5s 内不再调用

        private readonly System.Collections.Generic.Dictionary<int, float> _lastRelease =
            new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>冷却判定：无记录或已过冷却窗 → 允许调用。</summary>
        public bool CanRelease(int agentIndex, float now)
        {
            float last;
            if (!_lastRelease.TryGetValue(agentIndex, out last)) return true;
            return now - last >= CooldownSeconds;
        }

        /// <summary>登记调用时刻（调用 SetFormationFrameDisabled 后调用）。</summary>
        public void MarkReleased(int agentIndex, float now)
        {
            _lastRelease[agentIndex] = now;
        }

        /// <summary>Agent 生命周期清理（OnAgentRemoved 挂点）。</summary>
        public void Remove(int agentIndex)
        {
            _lastRelease.Remove(agentIndex);
        }
    }
}
