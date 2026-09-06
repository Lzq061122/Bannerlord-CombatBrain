namespace CombatBrain.Coordination
{
    // CPS-1 命令提案（FormationCommandProposal）—— 纯数据结构，零 TaleWorlds 依赖，可离线测。
    // 规格「战场命令控制面收口与原版AI权衡正式派单规格-2026-09-06」§5.1 纯逻辑模型第一版。
    // 职责：战术模块只提交提案（谁/什么域/什么命令/优先级/何时过期），不再直接写 Formation SetOrder；
    //   执行器（后续卡）与迁移完成前，本模型零接线——本卡不做任何行为变化。
    // 反作弊闸：纯逻辑字段仅表达「提案」，不新增任何游戏读取。
    // 复用 CommandProvenance.cs 的 CommandDomain/CommandKind/CommandSource（Coordination 命名空间）。
    /// <summary>编队命令提案（每域每 Formation 每拍一个最终胜者由 Arbiter 决定）。</summary>
    public struct FormationCommandProposal
    {
        public int TeamId;               // 所属 Team.TeamIndex
        public int FormationId;          // Formation.Index
        public int FormationIdentity;    // 编队对象身份信号（防 Index 复用继承旧提案/所有权，§8.2）
        public float LogicalTick;        // 提案生成逻辑拍（Arbiter 同拍判定用）
        public CommandDomain Domain;     // Movement / Facing / Arrangement（CommandProvenance 枚举）
        public CommandKind Kind;         // 命令种类（CommandProvenance 枚举）
        public CommandSource Source;     // 提出子系统（CommandProvenance 枚举）
        public int Priority;             // 优先级（P0..P100，必须用 FormationCommandPolicy.CommandPriority 常量，
                                         //   禁止散落 magic number——§3.2「优先级值集中定义并由测试锁定」）
        public int ReasonCode;           // 原因码（预留：0=无，后续卡填充）
        public int TargetId;             // 目标编队/实体 id（-1=无）
        public float TargetX;            // 目标位置 X（无目标=0）
        public float TargetY;            // 目标位置 Y（无目标=0）
        public float CreatedAt;          // 提案创建时间（逻辑拍时间）
        public float ExpiresAt;          // 过期时间（>CreatedAt；now>ExpiresAt 的提案不参与仲裁，§10.2）
        public bool RequiresExclusiveControl; // 独占申请（如租约 Acquire）；无安全 fallback 不得申请（§10.2）
        public bool HasSafeFallback;     // 是否有安全 fallback（Rejoin/Hold）；false=NoSafeFallback（§10.2 第13项）
        public bool Emergency;           // 紧急/贴身脱困（P80 层语义）
        public bool PlayerSafe;          // 对玩家安全（玩家/外部所有者出现时 CB 立即让位，§8.1）
        public bool TargetKnown;         // 目标已知（Fog 下未知敌不得构造目标，§10.2 第14项）
        public int ProposalId;           // 生成者唯一递增序号（tie-break 最终键，保证与输入顺序无关的确定性）

        public static FormationCommandProposal Create(int teamId, int formationId, int identity,
            float logicalTick, CommandDomain domain, CommandKind kind, CommandSource source, int priority,
            int proposalId, float now, float ttl)
        {
            return new FormationCommandProposal
            {
                TeamId = teamId,
                FormationId = formationId,
                FormationIdentity = identity,
                LogicalTick = logicalTick,
                Domain = domain,
                Kind = kind,
                Source = source,
                Priority = priority,
                ReasonCode = 0,
                TargetId = -1,
                TargetX = 0f,
                TargetY = 0f,
                CreatedAt = now,
                ExpiresAt = now + ttl,
                RequiresExclusiveControl = false,
                HasSafeFallback = false,
                Emergency = false,
                PlayerSafe = true,
                TargetKnown = true,
                ProposalId = proposalId
            };
        }

        /// <summary>提案是否过期（now 超过 ExpiresAt；过期提案不参与仲裁，§10.2 第11项）。</summary>
        public bool IsExpired(float now)
        {
            return now > ExpiresAt;
        }

        // CPS-1 迁移卡（cps1-migrate-defense / repair-round-2 / cps1-migrate-specialists）：
        //   统一提案工厂（Defense P70 / Specialist P50 / Presentation P40）。
        // 目的：各 Glue 的直接 SetOrder 迁移为提交本提案，经 Arbiter 优先级矩阵 + Executor 唯一出口落地
        //   （§5.2 迁移要求）；单来源时胜者=原调用语义（行为等价），多来源冲突由矩阵显式裁决（不再靠 producer 顺序）。
        // TTL=1s（CommandTtlSeconds）：Glue 0.5s 节拍提交，拍末执行器消费，不跨拍残留（列表每拍 Clear）。
        // identity=Formation 对象引用哈希（胶水层 RuntimeHelpers.GetHashCode(f) 传入，与执行器 ArbiterInput.FormationIdentity 一致）。
        // ProposalId=Interlocked 全局单调递增（跨生成者唯一，tie-break 顺序无关性）。
        // Arrangement 域提案的 ReasonCode=阵型种类（ArrangementKind 枚举值）。
        public const float CommandTtlSeconds = 1f;
        private static int _nextProposalId;

        public static FormationCommandProposal DefenseProposal(int teamId, int formationId, int identity, float now,
            CommandSource source, CommandKind kind, CommandDomain domain, int targetId, float tx, float ty)
        {
            return Build(teamId, formationId, identity, now, source, kind, domain,
                CommandPriority.Defense, 0, targetId, tx, ty);
        }

        /// <summary>P50 专业战术层提案（Cavalry/Skirmish/RangedPosition/UnitPreservation，§3.2）。</summary>
        public static FormationCommandProposal SpecialistProposal(int teamId, int formationId, int identity, float now,
            CommandSource source, CommandKind kind, CommandDomain domain, int reasonCode, int targetId, float tx, float ty)
        {
            return Build(teamId, formationId, identity, now, source, kind, domain,
                CommandPriority.Specialist, reasonCode, targetId, tx, ty);
        }

        /// <summary>P40 表现修正层提案（FormationTactics/FacingCorrection，§3.2）。</summary>
        public static FormationCommandProposal PresentationProposal(int teamId, int formationId, int identity, float now,
            CommandSource source, CommandKind kind, CommandDomain domain, int reasonCode, int targetId, float tx, float ty)
        {
            return Build(teamId, formationId, identity, now, source, kind, domain,
                CommandPriority.Presentation, reasonCode, targetId, tx, ty);
        }

        private static FormationCommandProposal Build(int teamId, int formationId, int identity, float now,
            CommandSource source, CommandKind kind, CommandDomain domain, int priority, int reasonCode,
            int targetId, float tx, float ty)
        {
            int proposalId = System.Threading.Interlocked.Increment(ref _nextProposalId); // 全局单调递增，跨生成者唯一
            FormationCommandProposal p = Create(teamId, formationId, identity, now,
                domain, kind, source, priority, proposalId, now, CommandTtlSeconds);
            p.ReasonCode = reasonCode;
            p.TargetId = targetId;
            p.TargetX = tx;
            p.TargetY = ty;
            return p;
        }
    }

    // CPS-1 迁移（cps1-migrate-specialists）：Arrangement 域提案的 ReasonCode 阵型种类约定
    // （执行器 MapArrangementOrder 按此映射 ArrangementOrder；0=Line 与 t9 防御 Reform 提案兼容）。
    public enum ArrangementKind
    {
        Line = 0,
        Skein = 1,    // 楔形冲击阵（骑兵冲锋）
        Column = 2,   // 骑兵纵队（重整待命）
        ShieldWall = 3 // 盾墙（FormationTactics 全面接管）
    }
}
