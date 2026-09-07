namespace CombatBrain.Coordination
{
    // CPS-1 命令策略（FormationCommandPolicy）—— 纯逻辑，零 TaleWorlds 依赖，可离线测。
    // 规格 §3.2（初始优先级契约）/§5.1（纯逻辑模型）/§8.1（玩家与外部所有权）。
    // 职责：优先级集中定义（禁 magic number 散落）+ 来源→优先级映射 + P70 域内冲突矩阵
    //   （Morale Retreat vs Brace / Protect vs Brace）+ 跨域兼容规则（Retreat/Rejoin 的 Facing/Arrangement）。
    // 本卡只做模型与规则，不接线不迁移任何 SetOrder 调用（Executor/迁移属后续卡）。
    public static class CommandPriority
    {
        // 规格 §3.2 初始优先级契约（集中定义，测试锁定；不得在其它文件散落 magic number）
        public const int Player = 100;        // P100 玩家/外部明确控制
        public const int EngineForced = 90;   // P90 引擎强制安全状态：Routing、Mission Retreat、攻城器械、Detachment、任务结束
        public const int Emergency = 80;      // P80 CB Emergency / Survival / 贴身脱困
        public const int Defense = 70;        // P70 CB Morale Retreat / Protect / Brace
        public const int Commitment = 60;     // P60 Army Commitment 与 Formation Mission
        public const int Specialist = 50;     // P50 Cavalry / Skirmish / RangedPosition 等专业战术
        public const int Presentation = 40;   // P40 Facing / Arrangement 表现修正
        public const int NativeFallback = 0;  // P0 Native fallback
    }

    public static class FormationCommandPolicy
    {
        /// <summary>来源 → 优先级层（§3.2；生成者如未显式给 Priority 也可由此解析，集中口径）。</summary>
        public static int PriorityFor(CommandSource source)
        {
            switch (source)
            {
                case CommandSource.PlayerOrManual:
                    return CommandPriority.Player;
                case CommandSource.OriginalUnknown:      // 历史兼容/未归属 → Native fallback 层
                case CommandSource.ExternalUnknown:
                    return CommandPriority.NativeFallback;
                case CommandSource.Morale:               // Morale Retreat（P70 防御层）
                case CommandSource.Protect:
                case CommandSource.Brace:
                    return CommandPriority.Defense;
                case CommandSource.ArmyCoordination:     // ATC 系列
                case CommandSource.FormationControlLease:
                    return CommandPriority.Commitment;
                case CommandSource.RangedPosition:       // 专业战术（P50）
                case CommandSource.UnitPreservation:
                case CommandSource.Cavalry:
                case CommandSource.Skirmish:
                    return CommandPriority.Specialist;
                case CommandSource.FormationTactics:     // 表现修正（P40）
                case CommandSource.FacingCorrection:
                    return CommandPriority.Presentation;
                default:
                    return CommandPriority.NativeFallback;
            }
        }

        /// <summary>
        /// P70 域内冲突矩阵（§3.2「Retreat 与 Brace/Protect 的冲突必须有明确矩阵，不得继续留给后写者胜」）：
        ///   Morale Retreat vs Brace   → Morale Retreat 胜（撤退保命优先于架枪站定）
        ///   Morale Retreat vs Protect → Morale Retreat 胜（撤退优先）
        ///   Brace vs Protect          → Brace 胜（架枪反骑即时防御优先；保留 §1.1.4 既有「架枪优先」语义，
        ///                                 但改为显式矩阵而非调用顺序表达）
        /// 返回 true 表示 a 胜出；仅对同域同优先级（Defense 层）的 Movement 域冲突调用。
        /// </summary>
        public static bool ResolveDefenseConflict(FormationCommandProposal a, FormationCommandProposal b)
        {
            bool aRetreat = a.Source == CommandSource.Morale && a.Kind == CommandKind.Retreat;
            bool bRetreat = b.Source == CommandSource.Morale && b.Kind == CommandKind.Retreat;
            if (aRetreat != bRetreat) return aRetreat; // Retreat 一方胜出
            // cps1-low2：删除双 Retreat 死分支——Beats 仅在 (Source,Kind) 不同时调用本方法，
            //   双 Retreat（同 Source 同 Kind）不会进入此处，由 Beats 的 Source/Kind/ProposalId 链兜底。
            // 非 Retreat 的 P70 冲突：Brace vs Protect / Protect vs Brace / 同源
            bool aBrace = a.Source == CommandSource.Brace;
            bool bBrace = b.Source == CommandSource.Brace;
            if (aBrace != bBrace) return aBrace;       // Brace 一方胜出
            return a.ProposalId < b.ProposalId;        // 其余（同源）→ ProposalId 确定性
        }

        /// <summary>需要已知目标的命令种类（Fog 下未知敌不得构造目标，§10.2 第14项；§10.3 Fog 规则）。</summary>
        public static bool RequiresKnownTarget(CommandKind kind)
        {
            return kind == CommandKind.Move || kind == CommandKind.Advance
                || kind == CommandKind.Charge || kind == CommandKind.Facing;
        }

        /// <summary>
        /// 跨域兼容规则（§3.2「Movement 提案若为 Retreat/Rejoin，Facing 与 Arrangement 是否兼容必须由跨域约束明确说明」）：
        ///   Movement=Retreat → Facing/Arrangement 的 P40 表现修正不应用（撤退中不面向敌人、不切阵型表现）；
        ///   Movement=Rejoin（Move）→ 各域兼容（朝目标方向 Facing 合理，Arrangement 表现修正可应用）。
        /// 返回该域提案是否被允许应用（玩家/引擎/Emergency 级不受此限，仅限制 Presentation 层）。
        /// </summary>
        public static bool IsCrossDomainAllowed(CommandKind movementKind, CommandDomain domain,
            FormationCommandProposal candidate)
        {
            if (movementKind != CommandKind.Retreat) return true; // 非 Retreat：各域兼容
            if (candidate.Priority > CommandPriority.Presentation) return true; // 高优先级（玩家/引擎/紧急）不受限
            // Movement=Retreat 且候选为表现修正层：Facing/Arrangement 表现修正被阻止
            return false;
        }
    }

    // CPS-2 路线 A PoC（cps2-ownership-poc，§6.1/t20 研究）——纯逻辑判定，零 TaleWorlds 依赖可离线测。
    // 结论（t20）：Movement 域 Hold/Advance/Rejoin/Retreat 可用自定义 BehaviorComponent + per-Formation
    //   权重表达（FormationAI.AddAiBehavior/SetBehaviorWeight 公开 API）；Facing/Arrangement 不经 Behavior；
    //   冲突行为降权而非删除（FindBestBehavior 按 GetAIWeight()×WeightFactor 取 max，FormationAI.cs:179）；
    //   玩家接管天然恢复（FormationAI.cs:245/286 !IsAIControlled 不写）；拆分/增援会 ResetBehaviorWeights
    //   （TacticComponent.cs:455-456）→ 权重重设路径=每拍幂等 Apply（GetBehavior<T> 查重 + SetBehaviorWeight）。
    /// <summary>路线 A 意图（Movement 域表达目标）。</summary>
    public enum Cps2RouteAIntent
    {
        None = 0,
        Hold = 1,     // 站定（MovementOrderStop）
        Advance = 2,  // 推进（MovementOrderAdvance）
        Rejoin = 3,   // 回主力（MovementOrderMove 向 MainBody）
        Retreat = 4   // 撤退（MovementOrderRetreat）
    }

    public static class Cps2RouteAPolicy
    {
        /// <summary>意图 → 命令语义（Behavior.CalculateCurrentOrder 产出对应 MovementOrder）。</summary>
        public static CommandKind ToCommandKind(Cps2RouteAIntent intent)
        {
            switch (intent)
            {
                case Cps2RouteAIntent.Hold: return CommandKind.Hold;
                case Cps2RouteAIntent.Advance: return CommandKind.Advance;
                case Cps2RouteAIntent.Rejoin: return CommandKind.Move;
                case Cps2RouteAIntent.Retreat: return CommandKind.Retreat;
                default: return CommandKind.None;
            }
        }

        /// <summary>F2 意图 → 路线 A 意图（PoC 映射；Attack/Flank/Harass/Pursue→Advance、Reform/Defend→Hold、
        ///   Retreat→Retreat、ProtectArcher/BraceFormation→Advance——仅 Movement 域表达，Facing/Arrangement 不经 Behavior）。</summary>
        public static Cps2RouteAIntent MapIntent(IntentType intent)
        {
            switch (intent)
            {
                case IntentType.Retreat: return Cps2RouteAIntent.Retreat;
                case IntentType.Reform:
                case IntentType.Defend: return Cps2RouteAIntent.Hold;
                default: return Cps2RouteAIntent.Advance; // Attack/Flank/Harass/Pursue/ProtectArcher/BraceFormation
            }
        }

        /// <summary>冲突降权表（t20 Q2：降权而非删除）：返回与目标意图冲突的 CB 意图集（CB 域内互斥——
        ///   当前意图 Behavior 权重 10、其余 CB Behavior 权重 0；原版冲突行为降权由运行期按表 SetBehaviorWeight(0f)）。</summary>
        public static Cps2RouteAIntent[] ConflictingIntents(Cps2RouteAIntent intent)
        {
            switch (intent)
            {
                case Cps2RouteAIntent.Hold:
                    return new[] { Cps2RouteAIntent.Advance, Cps2RouteAIntent.Rejoin, Cps2RouteAIntent.Retreat };
                case Cps2RouteAIntent.Advance:
                    return new[] { Cps2RouteAIntent.Hold, Cps2RouteAIntent.Retreat };
                case Cps2RouteAIntent.Rejoin:
                    return new[] { Cps2RouteAIntent.Advance, Cps2RouteAIntent.Retreat };
                case Cps2RouteAIntent.Retreat:
                    return new[] { Cps2RouteAIntent.Advance, Cps2RouteAIntent.Hold };
                default:
                    return new Cps2RouteAIntent[0];
            }
        }

        /// <summary>玩家接管天然恢复（FormationAI.cs:245/286：!IsAIControlled → 不 tick 不写）；false 时不设权重。</summary>
        public static bool ShouldApplyWeights(bool isAiControlled)
        {
            return isAiControlled;
        }

        /// <summary>拆分/增援后权重重设路径（TacticComponent.cs:455-456 拆分会 ResetBehaviorWeights）：
        ///   人数变化=编队结构变化信号 → 下拍幂等 Apply 重设（生命周期钩子存在，PoC 用每拍幂等覆盖）。</summary>
        public static bool ShouldReapplyAfterReset(int prevCount, int newCount)
        {
            return prevCount > 0 && newCount > 0 && newCount != prevCount;
        }

        /// <summary>AddAiBehavior 幂等判定（FormationAI.cs:133 直接 Add 非幂等；GetBehavior&lt;T&gt; 查重后
        ///   已注册则不重复 Add——FormationAI.cs:138 GetBehavior 存在）。</summary>
        public static bool NeedsAddBehavior(bool alreadyRegistered)
        {
            return !alreadyRegistered;
        }
    }
}
