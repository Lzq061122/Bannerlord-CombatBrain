using System.Collections.Generic;

namespace CombatBrain.Ai
{
    // X4 接敌解阵框（FrameRelease）—— 纯逻辑判定 + 冷却（可离线测），零 TaleWorlds 依赖。
    //
    // 产品裁决（CB-PROMPT-0038，K1/D1，2026-09-11）：**保留**，并正式定性为
    //   **编队级统一接战政策**（不是逐兵决策）。引擎无编队级批量入口，逐兵调用
    //   Agent.SetFormationFrameDisabled() 属 API 形态所致；本层不做任何逐兵战术判断。
    //
    // v1.4.8 API 核验证据（反编译 TaleWorlds.MountAndBlade.Agent @ TaleWorlds.MountAndBlade.dll，
    //   本机 bin\Win64_Shipping_Client；行号 = ilspycmd 输出行）：
    //   * `public bool IsFormationFrameEnabled { get; private set; }`（:1444）——只读状态，可做 latch 判定。
    //   * `public void SetFormationFrameDisabled()`（:2305；内部先调 MBAPI，`if (IsFormationFrameEnabled)` 才翻 false）。
    //   * `public void SetFormationFrameEnabled(WorldPosition position, Vec2 direction, Vec2 positionVelocity,
    //      float formationDirectionEnforcingFactor)`（:2315）。
    //   * `public bool TrySetFormationFrame(in WorldPosition formationPosition, in Vec2 formationDirection)`（:3891）
    //      —— position 有效则 SetFormationFrameEnabled(...)，无效则 SetFormationFrameDisabled()。
    //   * `public bool GetBaseFormationFrame(out WorldPosition formationPosition, out Vec2 formationDirection)`（:2051）。
    //   * **谁在什么时机重新建立阵框**：`HumanAIComponent.ParallelUpdateFormationMovement()`
    //     —— AI 士兵的 `Agent.ForceUpdateCachedAndFormationValues`（:3707）→
    //     `ParallelUpdateCachedAndFormationValuesForAIAgent`（:3737）→ `HumanAIComponent.ParallelUpdateFormationMovement()`
    //     →（每帧）`GetBaseFormationFrame` + `TrySetFormationFrame`；非 AI 士兵走同一判定
    //     （:3725-3726 `GetBaseFormationFrame` + `TrySetFormationFrame`）。即：**阵框由原版 AI/引擎每帧按
    //     编队槽位重新建立**，CB 的一次性禁用会被原版行为自然覆盖（不会永久 skip），CB 侧无需 Reset 调用。
    //     `Agent.Tick`（:4745-4760）在位置变化时经 `TeamAI.OnFormationFrameChanged(isFrameEnabled:…)` 上报，
    //     满足 ④「激活/恢复/生命周期边界」有明确宿主。
    //
    // 判定链（保留 4 项硬条件，缺一即退役）：
    //   ① 编队级触发：MovementOrder 为 Charge(2)/ChargeToTarget(3) 且距最显著敌编队 ≤25m 且编队 >25 人；
    //   ② 剧场闸门（硬性）：仅 L3（玩家可见区）执行，L0-L2 交原版；
    //   ③ 非战术门控：原版逃跑态（IsRunningAway）跳过；**已在释放态（IsFormationFrameEnabled == false）
    //      直接跳过 = 一次性 latch**（禁止重复抢写）；
    //   ④ 逐 agent 5s 冷却字典限频（重复调用无意义，冷却窗内不重发）。
    // 已删除的跳过条件（不得改写为新逐兵判断）：F7 求生撤退、F9 远程后撤——所属个体模块已退役。
    // 玩家队豁免（前置）：TickFrameRelease 在编队层用 ShouldExempt 过滤，玩家队不进入本层、不写入。
    // 反作弊闸：只解除自身阵框约束（能力非信息），不改信息获取（编队中心/命令状态均为引擎公开聚合）。
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

        /// <summary>①+ 编队级触发：冲锋命令 且 距目标≤25m 且 编队&gt;25 人。</summary>
        public static bool ShouldReleaseFormation(bool isChargeOrder, float targetDist, int count)
        {
            return isChargeOrder && targetDist <= ReleaseRangeMeters && count > MinReleaseCount;
        }

        /// <summary>② 剧场闸门：仅 L3（玩家可见区）执行（Enums.cs LodLevel.L3=3）。</summary>
        public static bool IsReleaseLod(int lodLevel)
        {
            return lodLevel >= 3;
        }

        /// <summary>
        /// ③ 非战术门控 + 一次性 latch：仅 原版逃跑态 或 **已处于释放态**（引擎帧框已禁用）时跳过。
        /// 已释放 → 不重复写（latch 语义）；恢复边界由原版 AI 每帧重建帧框（见文件头 API 核验）。
        /// </summary>
        public static bool ShouldSkipAgent(bool runningAway, bool frameAlreadyDisabled)
        {
            return runningAway || frameAlreadyDisabled;
        }

        /// <summary>玩家队豁免判定（P0-④ MCM 开关；true=该队跳过解阵框）。</summary>
        public static bool ShouldExempt(bool teamIsPlayer, bool exemptionEnabled)
        {
            return exemptionEnabled && teamIsPlayer;
        }
    }

    // 逐 agent 冷却字典（键=agent.Index，秒数制）：解阵框是一次性状态切换，冷却窗防重复写；
    // 与 ③ 的 latch 判定叠加，构成「同一 agent 在释放态内零重复写入」的双保险。
    public sealed class FrameReleaseCooldown
    {
        public const float CooldownSeconds = 5f; // 冷却窗（秒）：解阵框后 5s 内不再调用

        private readonly Dictionary<int, float> _lastRelease = new Dictionary<int, float>();

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
