namespace CombatBrain.Coordination
{
    using System.Collections.Generic;

    // CPS-1 命令仲裁器（FormationCommandArbiter）—— 纯逻辑，零 TaleWorlds 依赖，可离线测。
    // 规格 §3（目标架构）/§5.1（纯逻辑模型）/§8.1（玩家与外部所有权）/§10.2（14 项测试）。
    // 职责：每 Formation 每命令域（Movement/Facing/Arrangement）从全部提案中选出唯一胜者（后写者胜
    //   不再是规则——优先级 + 显式冲突矩阵 + 稳定 tie-break 决定）；玩家/外部所有者出现时无迟滞
    //   立即撤销 CB 所有权；输出 RejectedProposals/RejectionReason/OwnershipDecision/
    //   CrossDomainCompatibility/ShouldApplyNow。
    // 本卡只做纯逻辑模型+测试，不接线不迁移任何 SetOrder 调用（Executor/迁移属后续卡）。
    /// <summary>提案被拒原因（§5.1 RejectionReason）。</summary>
    public enum RejectionReason
    {
        None = 0,
        LowerPriority = 1,               // 被更高优先级胜者拒绝
        TieBrokenAgainst = 2,            // 同级（同优先级）稳定 tie-break 落败
        Expired = 3,                     // 提案过期（§10.2 第11项）
        IdentityStale = 4,               // FormationIdentity 与当前身份不符（§10.2 第12项）
        ExclusiveWithoutSafeFallback = 5, // NoSafeFallback 却要求独占（§10.2 第13项）
        TargetUnknown = 6,               // Fog 下未知敌构造目标（§10.2 第14项）
        PlayerOverrides = 7,             // 玩家/外部所有者出现，CB 立即让位（§8.1 无迟滞）
        CrossDomainBlocked = 8           // 跨域不兼容被阻止（Movement=Retreat 时表现层 Facing/Arrangement）
    }

    /// <summary>被拒提案（§5.1 RejectedProposals 元素）。</summary>
    public struct RejectedProposal
    {
        public FormationCommandProposal Proposal;
        public RejectionReason Reason;
        public int ByProposalId; // 击败者的 ProposalId（LowerPriority/TieBrokenAgainst 时有效；-1=其它原因）
    }

    /// <summary>所有权判定（§5.1 OwnershipDecision）。</summary>
    public enum OwnershipDecision
    {
        None = 0,
        CbOwns = 1,        // 胜者为 CB 提案
        PlayerOwns = 2,    // 玩家/玩家提案胜出
        ExternalOwns = 3,  // 外部所有者（非玩家）出现
        Native = 4         // 无胜者：交还原版（Native fallback）
    }

    /// <summary>外部所有者信号（ArbiterInput；§8.1 玩家/外部所有者出现时立即撤销 CB 所有权）。</summary>
    public enum ExternalOwnerKind
    {
        None = 0,
        Player = 1,
        External = 2
    }

    /// <summary>跨域兼容判定（§5.1 CrossDomainCompatibility；Movement=Retreat 阻止表现层 Facing/Arrangement）。</summary>
    public struct CrossDomainCompatibility
    {
        public bool MovementAllowed;      // Movement 恒允许（主导域）
        public bool FacingAllowed;        // Movement=Retreat 且 Facing 胜者为表现层 → false
        public bool ArrangementAllowed;   // Movement=Retreat 且 Arrangement 胜者为表现层 → false
    }

    /// <summary>单域仲裁结果（§5.1 仲裁输出）。</summary>
    public struct FormationArbitrationResult
    {
        public int TeamId;
        public int FormationId;
        public CommandDomain Domain;
        public bool HasWinner;
        public FormationCommandProposal Winner;      // 胜者（HasWinner=false 时无意义）
        public List<RejectedProposal> RejectedProposals; // 被拒提案（含原因）
        public bool ShouldApplyNow;                  // 有胜者且跨域允许（执行器据此决定是否落地）
        public OwnershipDecision Ownership;          // 所有权判定
        public CrossDomainCompatibility Compatibility; // 跨域兼容
    }

    /// <summary>仲裁输入（一 Formation 一拍全部域的提案 + 当前身份 + 外部所有者信号）。</summary>
    public struct ArbiterInput
    {
        public int TeamId;
        public int FormationId;
        public int FormationIdentity;  // 当前编队身份信号（与提案不符的提案失效，§10.2 第12项）
        public float Now;              // 当前逻辑拍时间（过期判定）
        public ExternalOwnerKind Owner; // 玩家/外部所有者（None=无）
        public List<FormationCommandProposal> Proposals; // 全部提案（只读遍历，不修改）
    }

    public static class FormationCommandArbiter
    {
        /// <summary>
        /// 仲裁：按域选出唯一胜者（§3.1「每域唯一胜者」）。稳定且与输入顺序无关：
        ///   比较器 = 优先级降序 → Defense 层冲突矩阵 → Source 升序 → Kind 升序 → ProposalId 升序。
        /// 玩家/外部所有者出现（Owner != None）→ CB 提案全部拒绝，无迟滞（§8.1）。
        /// </summary>
        public static List<FormationArbitrationResult> Resolve(ArbiterInput input)
        {
            var results = new List<FormationArbitrationResult>(3);

            // 先解 Movement（主导域，跨域兼容判定依据）；每域独立 rejected 列表（域间不串扰）
            FormationArbitrationResult movement = ResolveDomain(input, CommandDomain.Movement, new List<RejectedProposal>());
            bool retreat = movement.HasWinner && movement.Winner.Kind == CommandKind.Retreat;

            // Facing / Arrangement：跨域兼容（Movement=Retreat → 表现层被阻止）
            FormationArbitrationResult facing = ResolveDomain(input, CommandDomain.Facing, new List<RejectedProposal>());
            FormationArbitrationResult arrangement = ResolveDomain(input, CommandDomain.Arrangement, new List<RejectedProposal>());

            ApplyCrossDomain(retreat, ref facing);
            ApplyCrossDomain(retreat, ref arrangement);

            movement.Compatibility = new CrossDomainCompatibility { MovementAllowed = true,
                FacingAllowed = facing.Compatibility.FacingAllowed, ArrangementAllowed = arrangement.Compatibility.ArrangementAllowed };
            results.Add(movement);
            results.Add(facing);
            results.Add(arrangement);
            return results;
        }

        private static FormationArbitrationResult ResolveDomain(ArbiterInput input, CommandDomain domain,
            List<RejectedProposal> rejected)
        {
            var result = new FormationArbitrationResult
            {
                TeamId = input.TeamId,
                FormationId = input.FormationId,
                Domain = domain,
                RejectedProposals = rejected,
                Compatibility = new CrossDomainCompatibility { MovementAllowed = true, FacingAllowed = true, ArrangementAllowed = true }
            };

            // pass 0：基本资格过滤（过期/身份/独占安全/目标已知/玩家覆盖），不合格记拒绝
            var candidates = new List<FormationCommandProposal>();
            for (int i = 0; i < input.Proposals.Count; i++)
            {
                FormationCommandProposal p = input.Proposals[i];
                if (p.Domain != domain) continue;
                RejectionReason? reject = BasicRejection(input, p);
                if (reject.HasValue)
                {
                    rejected.Add(new RejectedProposal { Proposal = p, Reason = reject.Value, ByProposalId = -1 });
                    continue;
                }
                candidates.Add(p);
            }
            if (candidates.Count == 0)
            {
                result.Ownership = ResolveOwnership(input, false, default(FormationCommandProposal));
                return result;
            }

            // pass 1：选 best（稳定比较器，与输入顺序无关）
            FormationCommandProposal best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (Beats(candidates[i], best)) best = candidates[i];
            }

            // pass 2：其余候选记拒绝（同优先级 → TieBrokenAgainst；低优先级 → LowerPriority）
            for (int i = 0; i < candidates.Count; i++)
            {
                FormationCommandProposal c = candidates[i];
                if (c.ProposalId == best.ProposalId) continue;
                bool samePriority = c.Priority == best.Priority;
                rejected.Add(new RejectedProposal
                {
                    Proposal = c,
                    Reason = samePriority ? RejectionReason.TieBrokenAgainst : RejectionReason.LowerPriority,
                    ByProposalId = best.ProposalId
                });
            }

            result.HasWinner = true;
            result.Winner = best;
            result.ShouldApplyNow = true;
            result.Ownership = ResolveOwnership(input, true, best);
            return result;
        }

        // 基本资格检查：返回拒绝原因；null=合格
        private static RejectionReason? BasicRejection(ArbiterInput input, FormationCommandProposal p)
        {
            // §8.1：玩家/外部所有者出现 → CB 提案立即让位（无迟滞）；玩家提案仍可参与（P100 恒最高）
            if (input.Owner != ExternalOwnerKind.None && p.Source != CommandSource.PlayerOrManual)
                return RejectionReason.PlayerOverrides;
            if (p.IsExpired(input.Now)) return RejectionReason.Expired;                    // §10.2 第11项
            if (p.FormationIdentity != input.FormationIdentity) return RejectionReason.IdentityStale; // §10.2 第12项
            if (p.RequiresExclusiveControl && !p.HasSafeFallback)
                return RejectionReason.ExclusiveWithoutSafeFallback;                        // §10.2 第13项
            if (FormationCommandPolicy.RequiresKnownTarget(p.Kind) && !p.TargetKnown)
                return RejectionReason.TargetUnknown;                                       // §10.2 第14项
            return null;
        }

        // 稳定比较器：优先级降序 → Defense 冲突矩阵 → Source 升序 → Kind 升序 → ProposalId 升序
        private static bool Beats(FormationCommandProposal a, FormationCommandProposal b)
        {
            if (a.Priority != b.Priority) return a.Priority > b.Priority;
            if (a.Priority == CommandPriority.Defense && (a.Source != b.Source || a.Kind != b.Kind))
            {
                return FormationCommandPolicy.ResolveDefenseConflict(a, b); // P70 冲突矩阵（§3.2）
            }
            if (a.Source != b.Source) return (int)a.Source < (int)b.Source;
            if (a.Kind != b.Kind) return (int)a.Kind < (int)b.Kind;
            return a.ProposalId < b.ProposalId; // 最终键：ProposalId（固有属性，与输入顺序无关）
        }

        // 跨域兼容：Movement=Retreat 时表现层（P40）Facing/Arrangement 胜者被阻止（§3.2 跨域约束）。
        // cps1-low1：跨域阻断判定收敛到 FormationCommandPolicy.IsCrossDomainAllowed 单一入口（不再内联重复）。
        private static void ApplyCrossDomain(bool retreat, ref FormationArbitrationResult result)
        {
            if (!retreat || !result.HasWinner) return;
            if (!FormationCommandPolicy.IsCrossDomainAllowed(CommandKind.Retreat, result.Domain, result.Winner))
            {
                result.RejectedProposals.Add(new RejectedProposal
                {
                    Proposal = result.Winner,
                    Reason = RejectionReason.CrossDomainBlocked,
                    ByProposalId = -1
                });
                result.HasWinner = false;
                result.Winner = default(FormationCommandProposal);
                result.ShouldApplyNow = false;
                if (result.Domain == CommandDomain.Facing) result.Compatibility.FacingAllowed = false;
                if (result.Domain == CommandDomain.Arrangement) result.Compatibility.ArrangementAllowed = false;
            }
        }

        private static OwnershipDecision ResolveOwnership(ArbiterInput input, bool hasWinner,
            FormationCommandProposal winner)
        {
            if (input.Owner == ExternalOwnerKind.Player) return OwnershipDecision.PlayerOwns;
            if (input.Owner == ExternalOwnerKind.External) return OwnershipDecision.ExternalOwns;
            if (hasWinner && winner.Source == CommandSource.PlayerOrManual) return OwnershipDecision.PlayerOwns;
            return hasWinner ? OwnershipDecision.CbOwns : OwnershipDecision.Native;
        }
    }
}
