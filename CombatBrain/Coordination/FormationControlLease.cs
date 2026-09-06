using System.Collections.Generic;

namespace CombatBrain.Coordination
{
    // ATC-5R 编队控制权租约纯逻辑状态机（FormationControlLeaseManager）—— 零 TaleWorlds 可离线测。
    // 回答：v1.4.8 零 Harmony 下 Formation.SetControlledByAI(false/true) 能否在有限时间内排他控制单个
    //   AI 编队（阻止原生 FormationAI 覆盖 CB 安全命令）并在释放后无损恢复原生 AI——本文件只建纯逻辑
    //   状态机与决策契约；运行期 R1-R8 验证未完成不得写 GO（本卡结论=PoC Ready / Runtime Pending）。
    // 硬约束（规格 §2）：禁 Harmony/MonoMod/IL patch/native hook；只调公开 API；O(Formation) 不逐 Agent；
    //   禁每帧高频 SetOrder 对刷；禁全局冻结 AI；禁 ClearTacticOptions 接管；禁 AddTeamAI 替换整队。
    // 状态四态：NativeOwned → AcquireRequested（连续 Denied 第 1 拍，毛刺过滤）→ CombatBrainOwned（第 2 拍
    //   确认 Acquire）→ ReleaseRequested（释放动作当拍执行后回 NativeOwned）。
    // 所有权安全：Acquire 最低条件全满足才夺权；Release 仅当 WasAIControlledBeforeAcquire 且
    //   AcquiredByCombatBrain 才 SetControlledByAI(true)；原本非 AI 控制的编队不接管也不擅自恢复。

    /// <summary>控制权所有者（§4.1）。</summary>
    public enum FormationControlOwner
    {
        Native = 0,
        CombatBrainLease = 1
    }

    /// <summary>租约释放原因（§4.1 九值）。</summary>
    public enum LeaseReleaseReason
    {
        None = 0,
        Granted = 1,              // 连续 2 拍 Granted（或 MinimumHold 已过后的 Granted）交还原生
        Expired = 2,              // TTL 到期（producer 停止续租，watchdog 释放）
        Disabled = 3,             // PoC 开关关闭
        InvalidSnapshot = 4,      // 数据失效（Commitment 缺失等）
        FormationRemoved = 5,     // 编队清空/移除/身份变化
        MissionEnding = 6,        // Mission 结束/清场
        PlayerOrExternalControl = 7, // 玩家/外部控制出现
        UnsupportedMission = 8,   // 非普通单机野战（攻城/守城/突围/Detachment）
        EmergencyFailOpen = 9     // 异常路径 fail-open（保底释放）
    }

    /// <summary>租约状态（§4.1 四态可表达）。</summary>
    public enum LeaseState
    {
        NativeOwned = 0,
        AcquireRequested = 1,     // 连续 Denied 第 1 拍（等待第 2 拍确认，滤单拍毛刺）
        CombatBrainOwned = 2,     // 已 Acquire（SetControlledByAI(false)+安全命令在沿执行）
        ReleaseRequested = 3      // 释放确认（当拍执行 SetControlledByAI(true) 后回 NativeOwned）
    }

    /// <summary>安全降级动作（§5.2：优先顺序 Rejoin → Hold → 无法证明安全不 Acquire）。</summary>
    public enum SafeFallbackKind
    {
        None = 0,
        Rejoin = 1,               // Denied + 远离 MainBody + 有合法 MainBody 点 → Rejoin/Move
        Hold = 2,                 // Hold + 已在安全带内 → 保持阵位
        NoSafeFallback = 3        // 无法证明安全 → 不 Acquire（Shadow 记录）
    }

    /// <summary>每拍输入（胶水层从公开聚合填充；全部纯数值，零 TaleWorlds）。</summary>
    public struct LeaseInput
    {
        public int TeamId;                 // 队索引
        public int FormationId;            // 编队索引
        public int GenerationGuard;        // 编队身份 guard（identity/generation；Id 复用防继承）
        public bool PocEnabled;            // PoC 开关（默认 false）
        public bool FieldBattle;           // 普通单机野战（Mission.IsFieldBattle）
        public bool FormationValid;        // 非空且身份有效
        public bool IsAIControlled;        // 当前 Formation.IsAIControlled
        public bool IsPlayerFormation;     // 玩家/玩家所有/玩家控制/含玩家兵员
        public bool HasCommitment;         // 存在当前拍有效 CommitmentDecision
        public int Permission;             // CommitmentPermission（int）
        public int Reasons;                // CommitmentReason flags（int）
        public bool InContact;             // 贴身接战（不夺权）
        public bool RetreatOrEmergencyOrDefensive; // Retreat/Emergency/Immediate Defensive Override
        public SafeFallbackKind Fallback;  // 安全降级提案（胶水层算；NoSafeFallback→不 Acquire）
        public float Now;                  // 当前时间（战斗秒）
    }

    /// <summary>状态机输出动作（胶水层在边沿执行 SetControlledByAI/SetOrder）。</summary>
    public struct LeaseDecision
    {
        public bool AcquireNow;            // 本拍执行 SetControlledByAI(false) + 安全命令
        public bool Renew;                 // 续租（TTL 延长；0.5s producer 拍）
        public bool ReleaseNow;            // 本拍执行 SetControlledByAI(true)
        public LeaseReleaseReason ReleaseReason;
        public SafeFallbackKind Fallback;  // 降级动作（Rejoin/Hold）
        public int TeamId;
        public int FormationId;
    }

    /// <summary>每租约状态字段（§4.1 字段全）。</summary>
    public sealed class FormationControlLease
    {
        public int TeamId;
        public int FormationId;
        public int GenerationGuard;                 // identity/generation guard
        public bool WasAIControlledBeforeAcquire;   // 取得前 IsAIControlled（Release 恢复前提）
        public float AcquiredAt;                    // Acquire 时刻
        public float LastRenewedAt;                 // 最近续租时刻
        public float MinimumHoldUntil;              // 最短持有（MinHold 内不 Release）
        public SafeFallbackKind SafeCommandKind;    // 当前安全降级动作
        public int CommitmentReasonSnapshot;        // Acquire 时 CommitmentReason flags 快照
        public LeaseState State;                    // 四态
    }

    /// <summary>
    /// 租约管理器（纯逻辑状态机）：Acquire 两拍 Denied 确认（滤毛刺）/ Release 两拍 Granted 迟滞 +
    ///   MinimumHold/TTL/Renew；跨 Team/Formation 隔离（键=team×formation）；watchdog 过期释放。
    /// </summary>
    public sealed class FormationControlLeaseManager
    {
        // §5.3 参数集中可测（实战后可调）
        public const float DecisionCadenceSeconds = 0.5f;   // 复用 ATC 协调拍
        public const float LeaseTtlSeconds = 1.5f;          // 租约 TTL
        public const float MinimumHoldSeconds = 1.0f;       // 最短持有
        public const int ReleaseHysteresisTicks = 2;        // Release 迟滞：连续 2 拍 Granted
        public const int AcquireHysteresisTicks = 2;        // Acquire 确认：连续 2 拍 Denied
        public const float RenewMinGapSeconds = 0.8f;       // 续租最小间隔（防每拍刷）

        private readonly Dictionary<long, FormationControlLease> _leases = new Dictionary<long, FormationControlLease>();
        private readonly Dictionary<long, int> _deniedTicks = new Dictionary<long, int>();  // 连续 Denied（Acquire 确认）
        private readonly Dictionary<long, int> _grantedTicks = new Dictionary<long, int>(); // 连续 Granted（Release 确认）

        public int ActiveCount { get { return _leases.Count; } }

        public static long Key(int teamId, int formationId)
        {
            return ((long)teamId << 32) | (uint)formationId;
        }

        /// <summary>
        /// §5.2 安全降级提案（纯函数集中可测；胶水层 ComputeFallback 移此——禁止各 Glue 复制）：
        ///   Denied+远离 MainBody(>30m)+有合法 MainBody 点 → Rejoin；
        ///   Hold+已在安全带内(≤30m)+有 MainBody 点 → Hold（保持阵位）；
        ///   任何无法证明安全（无 MainBody 点/Denied 已在安全带内/其他许可）→ NoSafeFallback（不 Acquire）。
        ///   Rejoin 只用 MainBody 点（ArmyContextSnapshot），绝不用未知敌位置构造（Fog 契约）。
        /// </summary>
        public static SafeFallbackKind ResolveFallback(CommitmentPermission permission, ArmyContextSnapshot army,
            float distToMain)
        {
            if (permission == CommitmentPermission.Denied)
            {
                if (army.MainBodyStrength > 0 && distToMain > SafeFallbackMeters) return SafeFallbackKind.Rejoin;
                return SafeFallbackKind.NoSafeFallback;
            }
            if (permission == CommitmentPermission.Hold)
            {
                if (army.MainBodyStrength > 0 && distToMain <= SafeFallbackMeters) return SafeFallbackKind.Hold;
                return SafeFallbackKind.NoSafeFallback;
            }
            return SafeFallbackKind.NoSafeFallback; // Granted/Limited/ScreenOnly 不夺权
        }

        // 安全带内/远离边界（与 IsolationDetector.InnerSafeMeters=30 同口径：安全带内=就位）
        public const float SafeFallbackMeters = 30f;

        /// <summary>
        /// 0.5s producer 拍：单编队评估 → Acquire/Renew/Release 动作（不含命令执行，胶水层沿上执行）。
        /// </summary>
        public LeaseDecision Update(LeaseInput input)
        {
            var d = new LeaseDecision
            {
                TeamId = input.TeamId,
                FormationId = input.FormationId,
                Fallback = input.Fallback,
                ReleaseReason = LeaseReleaseReason.None
            };
            long key = Key(input.TeamId, input.FormationId);
            FormationControlLease lease;
            bool hasLease = _leases.TryGetValue(key, out lease);

            if (!hasLease)
            {
                // ---- NativeOwned：Acquire 评估 ----
                _grantedTicks.Remove(key);
                if (!input.PocEnabled) { _deniedTicks.Remove(key); return d; } // 开关关：零动作零状态
                if (!input.FieldBattle || !input.FormationValid || !input.IsAIControlled
                    || input.IsPlayerFormation || !input.HasCommitment)
                {
                    _deniedTicks.Remove(key); // 任何前提不满足：不进入 Acquire 路径
                    return d;
                }
                bool deniedOrHold = (input.Permission == (int)CommitmentPermission.Denied
                    || input.Permission == (int)CommitmentPermission.Hold)
                    && !input.InContact && !input.RetreatOrEmergencyOrDefensive;
                if (!deniedOrHold || input.Fallback == SafeFallbackKind.NoSafeFallback)
                {
                    _deniedTicks.Remove(key); // 单拍 Denied 毛刺/无法证明安全：不 Acquire
                    return d;
                }

                int ticks;
                _deniedTicks.TryGetValue(key, out ticks);
                ticks++;
                _deniedTicks[key] = ticks;
                if (ticks >= AcquireHysteresisTicks)
                {
                    // 两拍 Denied 确认 → Acquire（沿执行 SetControlledByAI(false)+安全命令）
                    lease = new FormationControlLease
                    {
                        TeamId = input.TeamId,
                        FormationId = input.FormationId,
                        GenerationGuard = input.GenerationGuard,
                        WasAIControlledBeforeAcquire = input.IsAIControlled,
                        AcquiredAt = input.Now,
                        LastRenewedAt = input.Now,
                        MinimumHoldUntil = input.Now + MinimumHoldSeconds,
                        SafeCommandKind = input.Fallback,
                        CommitmentReasonSnapshot = input.Reasons,
                        State = LeaseState.CombatBrainOwned
                    };
                    _leases[key] = lease;
                    _deniedTicks.Remove(key);
                    d.AcquireNow = true;
                    d.Fallback = input.Fallback;
                    return d;
                }
                // 第 1 拍：AcquireRequested（等待第 2 拍确认）
                return d;
            }

            // ---- CombatBrainOwned：Renew/Release 评估 ----
            _deniedTicks.Remove(key);

            // 立即 Release 条件（§5.3：开关关/失效/清空/玩家/非野战/身份变化/数据失效）
            LeaseReleaseReason immediate = ImmediateReleaseReason(input, lease);
            if (immediate != LeaseReleaseReason.None)
            {
                return Release(key, lease, d, immediate);
            }
            // 身份 guard 变化（FormationId 复用不继承旧租约）
            if (input.GenerationGuard != lease.GenerationGuard)
            {
                return Release(key, lease, d, LeaseReleaseReason.FormationRemoved);
            }

            // TTL 到期（producer 停止续租）→ 释放
            if (input.Now - lease.LastRenewedAt > LeaseTtlSeconds)
            {
                return Release(key, lease, d, LeaseReleaseReason.Expired);
            }

            // MinimumHold 未到：Granted 也不释放（迟滞确认延后）
            if (input.Now < lease.MinimumHoldUntil)
            {
                return RenewIfDue(input, lease, d); // 仅续租（不评估 Release）
            }

            // Granted 迟滞：连续 2 拍 Granted → 交还原生
            bool granted = input.Permission == (int)CommitmentPermission.Granted;
            int gTicks;
            _grantedTicks.TryGetValue(key, out gTicks);
            if (granted)
            {
                gTicks++;
                _grantedTicks[key] = gTicks;
                if (gTicks >= ReleaseHysteresisTicks)
                {
                    _grantedTicks.Remove(key);
                    return Release(key, lease, d, LeaseReleaseReason.Granted);
                }
            }
            else
            {
                _grantedTicks.Remove(key);
            }

            // 续租（0.5s producer 拍 + 最小间隔）
            return RenewIfDue(input, lease, d);
        }

        /// <summary>每帧 watchdog：只遍历活跃租约（无租约 O(1)），TTL 到期安全释放，不下战术命令。</summary>
        public List<KeyValuePair<int, int>> UpdateWatchdog(float now)
        {
            var expired = new List<KeyValuePair<int, int>>();
            if (_leases.Count == 0) return expired; // O(1) 无租约
            var staleKeys = new List<long>();
            foreach (KeyValuePair<long, FormationControlLease> kv in _leases)
            {
                if (now - kv.Value.LastRenewedAt > LeaseTtlSeconds) staleKeys.Add(kv.Key);
            }
            for (int i = 0; i < staleKeys.Count; i++)
            {
                FormationControlLease lease;
                if (_leases.TryGetValue(staleKeys[i], out lease))
                {
                    _leases.Remove(staleKeys[i]);
                    _grantedTicks.Remove(staleKeys[i]);
                    expired.Add(new KeyValuePair<int, int>(lease.TeamId, lease.FormationId));
                }
            }
            return expired;
        }

        /// <summary>幂等 ReleaseAll（Mission 结束/清场/开关关闭）。</summary>
        public List<KeyValuePair<int, int>> ReleaseAll(LeaseReleaseReason reason)
        {
            var released = new List<KeyValuePair<int, int>>();
            foreach (KeyValuePair<long, FormationControlLease> kv in _leases)
            {
                released.Add(new KeyValuePair<int, int>(kv.Value.TeamId, kv.Value.FormationId));
            }
            _leases.Clear();
            _deniedTicks.Clear();
            _grantedTicks.Clear();
            return released;
        }

        /// <summary>编队移除清理（FormationId 复用不继承旧租约）。</summary>
        public bool RemoveFormation(int teamId, int formationId)
        {
            long key = Key(teamId, formationId);
            _deniedTicks.Remove(key);
            _grantedTicks.Remove(key);
            return _leases.Remove(key);
        }

        /// <summary>只读租约（测试/日志用）；无 → null。</summary>
        public FormationControlLease GetLease(int teamId, int formationId)
        {
            FormationControlLease lease;
            return _leases.TryGetValue(Key(teamId, formationId), out lease) ? lease : null;
        }

        private static LeaseReleaseReason ImmediateReleaseReason(LeaseInput input, FormationControlLease lease)
        {
            if (!input.PocEnabled) return LeaseReleaseReason.Disabled;          // 开关关
            if (!input.FieldBattle) return LeaseReleaseReason.UnsupportedMission; // 非普通野战
            if (input.IsPlayerFormation) return LeaseReleaseReason.PlayerOrExternalControl; // 玩家出现
            if (!input.FormationValid) return LeaseReleaseReason.FormationRemoved; // 清空/移除
            if (!input.HasCommitment) return LeaseReleaseReason.InvalidSnapshot;  // 数据失效
            return LeaseReleaseReason.None;
        }

        private LeaseDecision Release(long key, FormationControlLease lease, LeaseDecision d, LeaseReleaseReason reason)
        {
            _leases.Remove(key);
            _deniedTicks.Remove(key);
            _grantedTicks.Remove(key);
            d.ReleaseNow = true;
            d.ReleaseReason = reason;
            d.TeamId = lease.TeamId;
            d.FormationId = lease.FormationId;
            return d;
        }

        private LeaseDecision RenewIfDue(LeaseInput input, FormationControlLease lease, LeaseDecision d)
        {
            if (input.Now - lease.LastRenewedAt >= RenewMinGapSeconds)
            {
                lease.LastRenewedAt = input.Now;
                d.Renew = true;
            }
            return d;
        }
    }
}
