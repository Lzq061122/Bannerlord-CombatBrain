namespace CombatBrain.Ai
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using CombatBrain.Battle;
    using CombatBrain.Coordination;
    using TaleWorlds.Core;
    using TaleWorlds.Engine;
    using TaleWorlds.Library;
    using TaleWorlds.MountAndBlade;

    // CPS-1 唯一命令执行器（FormationCommandExecutor）—— 胶水层（规格 §5.3）。
    // 职责：每协调拍先收集全部提案（本拍所有 producer 已跑完）→ 统一 Resolve（FormationCommandArbiter）
    //   → 再执行（编队级三个 SetOrder 的唯一出口，§3.1「唯一出口」；玩家 UI/引擎路径除外）。
    //   - 每 Formation 每命令域最多一个最终胜者；单拍每域最多一次实际写入（§5.3.3）；
    //   - 相同语义不重复下发 + 目标位置/方向迟滞（§5.3.4，PositionHysteresisMeters/FacingHysteresisRadians）；
    //   - 执行前复核（§5.3.5）：Formation identity（对象引用一致性，防 Index 复用）/存活/玩家所有权/
    //     任务类型（普通野战 fail-open）/租约状态（FormationStatus 注入）；
    //   - 玩家/外部所有者出现 → 停止执行，无迟滞（§8.1）；
    //   - 执行失败 fail-open（§5.3.6）：本执行器从不 SetControlledByAI（关 AI 只属租约 Acquire 且租约已先
    //     验证安全命令），写失败仅本拍跳过、下拍 producer 重提，不留「AI 已关但安全命令未下达」窗口；
    //   - 所有实际写入自动进入 CPS-0 CommandSourceScope + CommandProvenance.Record（§5.3.7）。
    // 本卡接线最小化：CombatAiMissionLogic 只加挂点（本拍 producer 后调用）；proposals 由迁移卡接入，
    //   当前为空列表 → Tick 近 O(1) 空转；不迁移任何现有 Glue 写入点（cps1-migrate-* 后续卡）。
    // 反作弊闸：只读编队公开聚合（CountOfUnits/PlayerOwner/HasPlayerControlledTroop/IsPlayerTroopInFormation/
    //   IsPlayerTeam）+ mission 公开任务类型；零禁读。执行器是批准的唯一 SetOrder 出口
    //   （静态扫描 = 既有 29 处 + 执行器自身）。
    public sealed class FormationCommandExecutor
    {
        // §5.3.4 迟滞阈值（集中常量，防 magic number 散落）
        public const float PositionHysteresisMeters = 5f;   // Move/Advance/Charge 目标位置小幅变化不重写
        public const float FacingHysteresisRadians = 0.2f;  // Facing 方向小幅变化不重写（单位方向向量弦长近似）

        // 上拍实际写入快照（key = teamId*1024 + formationId；每域一条）
        private readonly Dictionary<long, LastDomainWrite[]> _lastWrites = new Dictionary<long, LastDomainWrite[]>();
        // 上拍 Formation identity（对象引用哈希；对象重建/Index 复用 → 检测并清理，§8.2 不继承旧状态）
        private readonly Dictionary<long, int> _identityHashes = new Dictionary<long, int>();
        // 本拍提案分组复用缓冲（每拍 Clear 复用，无逐拍分配）
        private readonly Dictionary<long, List<FormationCommandProposal>> _batch = new Dictionary<long, List<FormationCommandProposal>>();
        private readonly List<FormationCommandProposal> _group = new List<FormationCommandProposal>();

        /// <summary>跳过写入的原因（§10.3 决策可观测）。</summary>
        public enum WriteSkipReason
        {
            None = 0,
            TargetHysteresis = 1,   // 目标小幅变化命中迟滞（相同语义不重复下发）
            FormationInvalid = 2,   // Formation 失效（null/空/identity 改变）
            PlayerOwned = 3,        // 玩家/外部所有者接管（无迟滞停止执行）
            NotFieldBattle = 4,     // 非普通单机野战（fail-open）
            LeaseNotOk = 5,         // 租约状态复核未过
            ApplyFailed = 6         // 实际写入异常（fail-open：本拍跳过，下拍重试）
        }

        /// <summary>上拍某域实际写入摘要（纯数据，供 ShouldWrite 迟滞判定；可离线测）。</summary>
        public struct LastDomainWrite
        {
            public bool Written;      // 上拍该域是否实际写入
            public CommandKind Kind;  // 上拍命令语义
            public int TargetId;      // 上拍目标编队/实体 id（-1=无）
            public float X, Y;        // 上拍目标位置（Facing 域=方向向量端点）
        }

        /// <summary>执行前 Formation 状态复核（胶水层填充；测试可离线构造）。</summary>
        public struct FormationStatus
        {
            public bool Valid;        // Formation 存在且 CountOfUnits>0 且 identity 一致（§5.3.5）
            public bool PlayerOwned;  // 玩家/玩家控制兵员/玩家队（§8.1；豁免开关口径）
            public bool FieldBattle;  // 普通单机野战（攻城/守城/突围 fail-open，§8.2）
            public bool LeaseOk;      // 租约状态复核（本卡挂点默认 true；迁移卡接入真实租约判定）
        }

        /// <summary>
        /// §5.3.4 决策：相同语义不重复下发 + 目标位置/方向迟滞（纯逻辑核心，可离线测）。
        /// 返回 true=应写入；false=命中迟滞/重复，reason 记录原因。
        /// </summary>
        public static bool ShouldWrite(LastDomainWrite last, FormationCommandProposal winner,
            float positionHysteresis, float facingHysteresis, out WriteSkipReason reason)
        {
            reason = WriteSkipReason.None;
            if (!last.Written) return true;                                       // 上拍未写（新编队/已清理）→ 写
            if (last.Kind != winner.Kind || last.TargetId != winner.TargetId) return true; // 语义/目标变化 → 写
            // 相同语义：目标位置/方向小幅变化命中迟滞 → 不重写；显著变化 → 重写
            float dx = last.X - winner.TargetX;
            float dy = last.Y - winner.TargetY;
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
            if (winner.Domain == CommandDomain.Facing)
            {
                // 方向迟滞：Facing 目标为方向向量（单位），端点弦长 ≈ 夹角（小角度近似）
                if (dist < facingHysteresis)
                {
                    reason = WriteSkipReason.TargetHysteresis;
                    return false;
                }
            }
            else if (dist < positionHysteresis)
            {
                reason = WriteSkipReason.TargetHysteresis;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 执行单域胜者（§5.3 第 3/5/6/7 条）：复核 → 迟滞决策 → scope+provenance+实际写入（sink 注入，可离线测）。
        /// 返回 true=实际写入。执行失败 fail-open：不关 AI（本执行器从不 SetControlledByAI），本拍跳过。
        /// t40（cps3r-evidence-chain）：recordTrace=false 时不写 CommandProvenance.Record（B Shadow only 只记
        ///   WouldApply 决策，不产生 Applied/OrderTrace——消除拟执行决策记录混淆）。
        /// </summary>
        public static bool ExecuteWinner(FormationCommandProposal winner, FormationStatus status,
            LastDomainWrite last, float positionHysteresis, float facingHysteresis,
            System.Action<FormationCommandProposal> applyWrite, List<WriteSkipReason> skipLog,
            bool recordTrace = true)
        {
            // §5.3.5 执行前复核：identity/存活/玩家所有权/任务类型/租约
            if (!status.Valid) { if (skipLog != null) skipLog.Add(WriteSkipReason.FormationInvalid); return false; }
            if (status.PlayerOwned) { if (skipLog != null) skipLog.Add(WriteSkipReason.PlayerOwned); return false; }
            if (!status.FieldBattle) { if (skipLog != null) skipLog.Add(WriteSkipReason.NotFieldBattle); return false; }
            // cps2-lease-integration：租约域提案（Source=FormationControlLease，Acquire 后 fallback）需租约活跃
            //   （status.LeaseOk=HasActiveLease 实际判定）；其它域提案不受租约约束——§6.2.4 租约期间所有 CB 提案
            //   仍经仲裁（租约 fallback P80 通常胜出），普通编队防御/专业提案不得被 LeaseOk=false 误拒。
            if (winner.Source == CommandSource.FormationControlLease && !status.LeaseOk)
            {
                if (skipLog != null) skipLog.Add(WriteSkipReason.LeaseNotOk);
                return false;
            }
            // §5.3.4 相同语义/迟滞
            WriteSkipReason reason;
            if (!ShouldWrite(last, winner, positionHysteresis, facingHysteresis, out reason))
            {
                if (skipLog != null) skipLog.Add(reason);
                return false;
            }
            // §5.3.7 实际写入：CPS-0 来源 scope + 统一 provenance；§5.3.6 fail-open（using=try/finally 保证 scope 清理）
            try
            {
                using (CommandSourceScope.Enter(winner.Source))
                {
                    if (recordTrace)
                    {
                        CommandProvenance.Instance.Record(winner.FormationId, winner.Source, winner.Kind,
                            winner.ReasonCode, winner.TargetId, winner.TargetX, winner.TargetY);
                    }
                    if (applyWrite != null) applyWrite(winner);
                }
            }
            catch (System.Exception)
            {
                if (skipLog != null) skipLog.Add(WriteSkipReason.ApplyFailed);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 每协调拍主流程（§5.3.1/§5.3.2）：调用方已收集本拍全部提案（producers 之后）→ 本方法
        /// 统一 Resolve → 再执行。无提案 → 近 O(1) 空转（零 SetOrder、零分配）。
        /// </summary>
        /// <param name="leaseActiveReader">cps2-lease-integration：租约活跃判定（teamId, formationId → 是否持有活跃租约；
        ///   null=无租约判定（LeaseOk 默认 true，t7 挂点兼容））。</param>
        public void Tick(Mission mission, float now, List<FormationCommandProposal> proposals,
            System.Func<int, int, bool> leaseActiveReader = null)
        {
            if (mission == null || proposals == null || proposals.Count == 0) return; // 近 O(1)
            // CPS-3 模式（cps3-runtime-prep，§7.1）：A Native baseline=三域 CB 命令全关——
            // 执行器短路零落地（proposals 收集照旧，供与 A/B/C/D 对比；默认未启用时走现状）
            if (CombatBrain.Settings.Cps3RuntimeSettings.Enabled
                && CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.A_NativeBaseline)
            {
                return;
            }
            // B Shadow only：决策运行（Resolve+ExecuteWinner 决策）但执行器不落地（WouldApply 日志对比）
            bool shadowOnly = CombatBrain.Settings.Cps3RuntimeSettings.Enabled
                && CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.B_ShadowOnly;

            // 1) 收集分组（复用缓冲，零逐拍分配）
            _batch.Clear();
            for (int i = 0; i < proposals.Count; i++)
            {
                FormationCommandProposal p = proposals[i];
                long gk = Key(p.TeamId, p.FormationId);
                List<FormationCommandProposal> group;
                if (!_batch.TryGetValue(gk, out group))
                {
                    group = new List<FormationCommandProposal>();
                    _batch[gk] = group;
                }
                group.Add(p);
            }

            // 2) 每 Formation：统一 Resolve → 3) 执行
            foreach (KeyValuePair<long, List<FormationCommandProposal>> kv in _batch)
            {
                int teamId = (int)(kv.Key >> 10);
                int formationId = (int)(kv.Key & 1023);
                Formation f = FindFormation(mission, teamId, formationId);
                Team team = FindTeam(mission, teamId);
                long key = kv.Key;

                // §8.2 identity 复核：对象引用一致性（Index 复用/对象重建 → 清快照，本拍跳过）
                if (f == null || team == null)
                {
                    _lastWrites.Remove(key);
                    _identityHashes.Remove(key);
                    continue;
                }
                int hash = RuntimeHelpers.GetHashCode(f);
                int prevHash;
                if (_identityHashes.TryGetValue(key, out prevHash) && HasIdentityChanged(prevHash, hash))
                {
                    _lastWrites.Remove(key);   // 不继承旧状态（§8.2）
                    _identityHashes[key] = hash;
                    continue;
                }
                _identityHashes[key] = hash;

                // 状态复核输入（§8.1 玩家所有权 / §8.2 任务类型 / §6.2 租约状态）
                var status = new FormationStatus
                {
                    Valid = f.CountOfUnits > 0,
                    PlayerOwned = f.PlayerOwner != null || f.HasPlayerControlledTroop
                        || f.IsPlayerTroopInFormation
                        || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam),
                    FieldBattle = mission.IsFieldBattle && !mission.IsSiegeBattle && !mission.IsSallyOutBattle,
                    // cps2-lease-integration：租约活跃实际判定（t7 挂点默认 true 改为真实；null=兼容无判定）
                    LeaseOk = leaseActiveReader == null || leaseActiveReader(teamId, formationId)
                };

                // 统一 Resolve（每域唯一胜者）
                var input = new ArbiterInput
                {
                    TeamId = teamId,
                    FormationId = formationId,
                    FormationIdentity = hash, // 对象引用哈希作为当前身份信号
                    Now = now,
                    Owner = status.PlayerOwned ? ExternalOwnerKind.Player : ExternalOwnerKind.None,
                    Proposals = kv.Value
                };
                List<FormationArbitrationResult> results = FormationCommandArbiter.Resolve(input);
                for (int d = 0; d < results.Count; d++)
                {
                    FormationArbitrationResult r = results[d];
                    if (!r.HasWinner) continue;
                    LastDomainWrite last = GetLastWrite(key, r.Domain);
                    if (shadowOnly)
                    {
                        // B Shadow only：applyWrite=null + recordTrace=false → 决策判定但不落地、不写
                        //   CommandProvenance.Record（t40：B 组 OrderTraceAgg 不得有拟执行决策记录）；
                        //   WouldApply 日志（门控，供决策 vs 落地对比）
                        bool decided = ExecuteWinner(r.Winner, status, last,
                            PositionHysteresisMeters, FacingHysteresisRadians, null, null, false);
                        if (decided && CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled)
                        {
                            CombatLog.Log(string.Format(
                                "[Cps3Shadow] WouldApply team={0} f={1} src={2} kind={3} domain={4}",
                                teamId, formationId, r.Winner.Source, r.Winner.Kind, r.Domain));
                        }
                        continue; // shadow 不更新 _lastWrites（不落地）
                    }
                    // 正常落地（C ArbiterCoexist / D ExclusivePoc / 未启用=现状）：唯一 SetOrder 出口
                    bool wrote = ExecuteWinner(r.Winner, status, last,
                        PositionHysteresisMeters, FacingHysteresisRadians,
                        w => ApplySetOrder(mission, f, w), null);
                    if (wrote) SetLastWrite(key, r.Domain, r.Winner);
                }
            }
        }

        // ---- 内部 ----

        /// <summary>
        /// §8.2 身份一致性判定（纯逻辑，供离线测；executor.Tick 对象引用哈希复核路径）：
        /// 上拍哈希 → 本拍哈希 变化 = Formation 对象重建/Index 复用 → 不继承旧状态。
        /// 首见（prevHash 不可用，由调用方 TryGetValue 控制）不视为变化。
        /// repair-round-2（收 cps1-exec-low1）：本判定与生产者 DefenseProposal identity（同一哈希信号）配合，
        /// 使 executor.Tick 身份一致性路径可被离线断言锁定。
        /// </summary>
        public static bool HasIdentityChanged(int prevHash, int currentHash)
        {
            return currentHash != 0 && prevHash != 0 && prevHash != currentHash;
        }

        private static long Key(int teamId, int formationId) { return ((long)teamId << 10) | (uint)formationId; }

        private static Formation FindFormation(Mission mission, int teamId, int formationId)
        {
            Team team = FindTeam(mission, teamId);
            if (team == null) return null;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f != null && f.Index == formationId) return f;
            }
            return null;
        }

        private static Team FindTeam(Mission mission, int teamId)
        {
            foreach (Team t in mission.Teams)
            {
                if (t != null && t.TeamIndex == teamId) return t;
            }
            return null;
        }

        private LastDomainWrite GetLastWrite(long key, CommandDomain domain)
        {
            LastDomainWrite[] arr;
            if (!_lastWrites.TryGetValue(key, out arr)) return default(LastDomainWrite);
            int idx = DomainIndex(domain);
            return idx >= 0 ? arr[idx] : default(LastDomainWrite);
        }

        private void SetLastWrite(long key, CommandDomain domain, FormationCommandProposal winner)
        {
            LastDomainWrite[] arr;
            int idx = DomainIndex(domain);
            if (idx < 0) return;
            if (!_lastWrites.TryGetValue(key, out arr))
            {
                arr = new LastDomainWrite[3];
                _lastWrites[key] = arr;
            }
            arr[idx] = new LastDomainWrite
            {
                Written = true,
                Kind = winner.Kind,
                TargetId = winner.TargetId,
                X = winner.TargetX,
                Y = winner.TargetY
            };
        }

        private static int DomainIndex(CommandDomain domain)
        {
            return domain == CommandDomain.Movement ? 0
                : domain == CommandDomain.Facing ? 1
                : domain == CommandDomain.Arrangement ? 2 : -1;
        }

        // 唯一 SetOrder 出口（§3.1；执行器是批准的写入点）
        private static void ApplySetOrder(Mission mission, Formation f, FormationCommandProposal winner)
        {
            switch (winner.Domain)
            {
                case CommandDomain.Movement:
                    f.SetMovementOrder(MapMovementOrder(mission, f, winner));
                    break;
                case CommandDomain.Facing:
                    // Facing 目标为方向向量（FacingCorrection 输出归一化方向）
                    f.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(new Vec2(winner.TargetX, winner.TargetY)));
                    break;
                case CommandDomain.Arrangement:
                    f.SetArrangementOrder(MapArrangementOrder(winner.ReasonCode));
                    break;
            }
        }

        /// <summary>
        /// t16 repair（QC BLOCKER）：Charge 提案是否应使用定向冲锋（MovementOrderChargeToTarget）。
        /// 旧 CavalryGlue.IssueChargeOrder 语义 target!=null ? ChargeToTarget(target) : Charge——
        /// 迁移后提案携带 TargetId=目标编队 Index；本判定=TargetId 可消费（≥0）。
        /// 纯逻辑可离线测；实际目标 Formation 解析在 MapMovementOrder 胶水（敌队查找，fail-open）。
        /// </summary>
        public static bool ShouldUseChargeToTarget(FormationCommandProposal p)
        {
            return p.Kind == CommandKind.Charge && p.TargetId >= 0;
        }

        private static MovementOrder MapMovementOrder(Mission mission, Formation f, FormationCommandProposal p)
        {
            switch (p.Kind)
            {
                case CommandKind.Hold:
                    return MovementOrder.MovementOrderStop;
                case CommandKind.Advance:
                    return MovementOrder.MovementOrderAdvance;
                case CommandKind.Charge:
                    // t16 repair：定向冲锋（旧语义）——TargetId 消费：敌队编队解析 → ChargeToTarget(target)；
                    // 无 TargetId 或目标消失/找不到 → 无目标 Charge（fail-open，不误冲友军）
                    if (ShouldUseChargeToTarget(p))
                    {
                        Formation target = FindChargeTarget(mission, f, p.TargetId);
                        if (target != null) return MovementOrder.MovementOrderChargeToTarget(target);
                    }
                    return MovementOrder.MovementOrderCharge;
                case CommandKind.Retreat:
                    return MovementOrder.MovementOrderRetreat;
                case CommandKind.FallBack:
                    return MovementOrder.MovementOrderFallBack;
                default: // Move/Rejoin：向目标点移动
                    WorldPosition wp = f.CachedMedianPosition;
                    wp.SetVec2(new Vec2(p.TargetX, p.TargetY));
                    return MovementOrder.MovementOrderMove(wp);
            }
        }

        // 冲锋目标解析（v1.4.8 已核实：MovementOrder.MovementOrderChargeToTarget(Formation targetFormation)，参数为 Formation）。
        // Cavalry 冲锋目标是敌编队（GetKnownEnemy → 敌队），按 formation.Index 在敌队内查找；
        // 目标消失/找不到 → null → 调用方退回无目标 Charge（fail-open，不误冲友军/不崩）。
        private static Formation FindChargeTarget(Mission mission, Formation self, int targetId)
        {
            if (mission == null || self == null || self.Team == null) return null;
            foreach (Team t in mission.Teams)
            {
                if (t == null || t == self.Team || !t.IsEnemyOf(self.Team)) continue;
                foreach (Formation ef in t.FormationsIncludingEmpty)
                {
                    if (ef != null && ef.Index == targetId && ef.CountOfUnits > 0) return ef;
                }
            }
            return null;
        }

        // Arrangement 域阵型映射（cps1-migrate-specialists）：提案 ReasonCode=ArrangementKind 枚举值
        // （Line/Skein/Column/ShieldWall；默认 Line——与 t9 防御 Reform 提案 ReasonCode=0 兼容）。
        private static ArrangementOrder MapArrangementOrder(int reasonCode)
        {
            switch ((ArrangementKind)reasonCode)
            {
                case ArrangementKind.Skein:
                    return ArrangementOrder.ArrangementOrderSkein;     // 骑兵楔形冲击阵
                case ArrangementKind.Column:
                    return ArrangementOrder.ArrangementOrderColumn;    // 骑兵纵队重整
                case ArrangementKind.ShieldWall:
                    return ArrangementOrder.ArrangementOrderShieldWall; // FormationTactics 盾墙
                default:
                    return ArrangementOrder.ArrangementOrderLine;      // 线列（默认/防御 Reform）
            }
        }
    }

    // CPS-2 路线 A PoC 胶水（cps2-ownership-poc，§6.1/t20 研究）——默认关闭安全态。
    // 机制：自定义 BehaviorComponent 表达 Movement 域 Hold/Advance/Rejoin/Retreat，经公开 API
    //   FormationAI.AddAiBehavior（FormationAI.cs:133）/SetBehaviorWeight<T>（FormationAI.cs:120）挂载；
    //   权重法竞争（FindBestBehavior max(GetAIWeight()×WeightFactor)，FormationAI.cs:179）——
    //   CB 权重 10 > 原版默认 0.2~1.0，意图胜出无需全量降权；冲突降权表（Cps2RouteAPolicy.ConflictingIntents）
    //   供运行期对具体原版类型 SetBehaviorWeight(0f)（降权而非删除，§6.1）。
    // 约束（§6.1 禁止项）：不 ClearTacticOptions 重建、不替换 TeamAI、不复制第三方实现（RBM/OF 仅借鉴架构）。
    // 仅 Movement 域（Behavior 只写 MovementOrder；Facing/Arrangement 不经 Behavior——t20 结论）。
    // 安全：玩家接管天然恢复（FormationAI.cs:245/286 !IsAIControlled 停写）；每拍幂等 Apply
    //   （GetBehavior<T> 查重 + AddAiBehavior + SetBehaviorWeight）覆盖拆分/增援重置。
    // PoC 开关默认关闭（Cps2PoCSettings.RouteAEnabled=false）→ Apply 零 AddAiBehavior/SetBehaviorWeight 调用。
    // CPS-2 路线 A PoC 胶水（cps2-ownership-poc，§6.1/t20 研究）——默认关闭安全态。
    // t50（cps3r-route-a-hardgates）六硬门修复：①单编队单 Movement 域隔离（弃 Teams×Formations 遍历）；
    //   ②完整可逆（开关关/玩家接管/identity 变化/Mission 结束恢复原权重无残留，热关闭=真恢复）；
    //   ③per-Formation Mission（弃 Team 意图）+ Rejoin 导航校验；④权威归因（RouteA scope + Intent→Behavior选
    //   →OrderApplied 事件链）；⑤冲突降权单维（GetAiWeight=10 单常量子，不再×SetBehaviorWeight=10=100）；
    //   ⑥幂等无分配（实例缓存复用，不每拍 new）。
    public static class CbRouteAPoC
    {
        // t50 硬门⑥：Behavior 实例缓存（key=identity；按需创建，命中复用不每拍 new）；与 _armed 同生命周期
        private static readonly Dictionary<int, object> _cache = new Dictionary<int, object>();

        // t52 repair（MEDIUM ⑤单维降权）：Behavior 基权重（GetAiWeight 返此值=1f 归一；有效权重=
        //   GetAiWeight×WeightFactor=1×SetBehaviorWeight(CbBehaviorWeight=10)=10——FindBestBehavior 语义，
        //   消除 t20 GetAiWeight=10×SetBehaviorWeight=10=100 放大）
        public const float BaseBehaviorWeight = 1f;

        /// <summary>FindBestBehavior 有效权重（纯逻辑可离线测）：GetAiWeight×WeightFactor（FormationAI.cs:179）。</summary>
        public static float EffectiveWeight(float getAiWeight, float setBehaviorWeight)
        {
            return getAiWeight * setBehaviorWeight;
        }

        // t50 硬门②：已接管编队登记（key=identity → Formation 引用；恢复时幂等还原）
        private static readonly HashSet<int> _armed = new HashSet<int>();

        /// <summary>是否有编队仍处于 CB 接管（供 MissionLogic 热关闭仍调 ApplySingle 以恢复；幂等）。</summary>
        public static bool HasArmed()
        {
            return _armed.Count > 0;
        }

        /// <summary>单编队幂等 Apply（t50 硬门①：单 Movement 域，不遍历 Teams×Formations）。
        /// <paramref name="routeEnabled"/> 预判 RouteAEnabled；<paramref name="eligibleField"/> 为
        ///   Cps2RouteAPolicy.IsEligibleFieldBattle(IsFieldBattle,IsSiege,IsSallyOut)；null→直接认定不合格。
        /// <paramref name="formationMission"/>=-1 / inRetreat 供 per-Formation 意图映射；Rejoin 需 mainBody 有效 +
        ///   非贴身接敌（IsRejoinRouteValid 纯逻辑）；归因：落地 SetMovementOrder 在 CommandSourceScope.Enter(CombatBrainRouteA) 内。</summary>
        public static void ApplySingle(Formation f, bool routeEnabled, bool eligibleField, bool isAIControlled,
            bool formationValid, int formationMission, bool inRetreat,
            float mainBodyX, float mainBodyY,
            System.Collections.Generic.IList<FormationCoordSnapshot> enemySnaps)
        {
            if (f == null) return;
            int id = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(f);
            // 硬门②：不合格（开关关/非野战/玩家接管/编队失效）→ 恢复（热关闭=真恢复）
            if (!routeEnabled || !eligibleField || !isAIControlled || !formationValid)
            {
                Restore(f, id);
                return;
            }
            // 硬门①③：仅单编队相互作用；per-Formation 意图映射（阶段按当前——胶水未传阶段，用 Deployment 保守）
            Cps2RouteAIntent intent = Cps2RouteAPolicy.MapFormationToIntent(
                inRetreat, formationMission, BattlePhase.Deployment);
            ApplySingleIntent(f, id, intent, mainBodyX, mainBodyY, enemySnaps);
            _armed.Add(id);
        }

        private static void ApplySingleIntent(Formation f, int id, Cps2RouteAIntent intent,
            float mainBodyX, float mainBodyY,
            System.Collections.Generic.IList<FormationCoordSnapshot> enemySnaps)
        {
            if (intent == Cps2RouteAIntent.None) return;
            float w = CombatBrain.Settings.Cps2PoCSettings.CbBehaviorWeight; // t50 硬门⑤：单维权重
            switch (intent)
            {
                case Cps2RouteAIntent.Hold:
                    EnsureBehavior(f, id, typeof(CbHoldBehavior), () => new CbHoldBehavior(f));
                    f.AI.SetBehaviorWeight<CbHoldBehavior>(w);
                    break;
                case Cps2RouteAIntent.Advance:
                    EnsureBehavior(f, id, typeof(CbAdvanceBehavior), () => new CbAdvanceBehavior(f));
                    f.AI.SetBehaviorWeight<CbAdvanceBehavior>(w);
                    break;
                case Cps2RouteAIntent.Rejoin:
                    // 硬门③：Rejoin 导航校验——MainBody 有效 + 非贴身接敌（无效→不挂载 Rejoin，防无人负责）
                    Vec2 median = f.CachedMedianPosition.AsVec2;
                    if (!Cps2RouteAPolicy.IsRejoinRouteValid(mainBodyX, mainBodyY,
                        median.x, median.y,
                        enemySnaps, CombatBrain.Settings.Cps2PoCSettings.RouteARejoinContactMeters))
                    {
                        return;
                    }
                    EnsureBehavior(f, id, typeof(CbRejoinBehavior), () => new CbRejoinBehavior(f));
                    f.AI.GetBehavior<CbRejoinBehavior>().SetTarget(mainBodyX, mainBodyY);
                    f.AI.SetBehaviorWeight<CbRejoinBehavior>(w);
                    break;
                case Cps2RouteAIntent.Retreat:
                    EnsureBehavior(f, id, typeof(CbRetreatBehavior), () => new CbRetreatBehavior(f));
                    f.AI.SetBehaviorWeight<CbRetreatBehavior>(w);
                    break;
                default:
                    return;
            }
            // CB 域内互斥：冲突意图 behavior 权重 0（降权而非删除/清空，§6.1）
            Cps2RouteAIntent[] conflicts = Cps2RouteAPolicy.ConflictingOwn(intent);
            for (int i = 0; i < conflicts.Length; i++)
            {
                SetOwnWeightZero(f, conflicts[i]);
            }
            // t50 硬门④：归因事件链记录（Intent→BehaviorSelected→OrderApplied 的 part1/2；落地自带 RouteA scope）
            RouteAEventLogger.RecordIntentApplied(f, intent);
        }

        private static void SetOwnWeightZero(Formation f, Cps2RouteAIntent intent)
        {
            switch (intent)
            {
                case Cps2RouteAIntent.Hold: if (f.AI.GetBehavior<CbHoldBehavior>() != null) f.AI.SetBehaviorWeight<CbHoldBehavior>(0f); break;
                case Cps2RouteAIntent.Advance: if (f.AI.GetBehavior<CbAdvanceBehavior>() != null) f.AI.SetBehaviorWeight<CbAdvanceBehavior>(0f); break;
                case Cps2RouteAIntent.Rejoin: if (f.AI.GetBehavior<CbRejoinBehavior>() != null) f.AI.SetBehaviorWeight<CbRejoinBehavior>(0f); break;
                case Cps2RouteAIntent.Retreat: if (f.AI.GetBehavior<CbRetreatBehavior>() != null) f.AI.SetBehaviorWeight<CbRetreatBehavior>(0f); break;
            }
        }

        // t50 硬门⑥：实例缓存幂等挂载（命中缓存复用；未命中按需创建后缓存；GetBehavior 查重不重复 Add）
        private static void EnsureBehavior(Formation f, int id, System.Type type,
            System.Func<object> factory)
        {
            int key = id * 31 + type.GetHashCode();
            object cached;
            if (_cache.TryGetValue(key, out cached) && cached != null)
            {
                // 已缓存实例：确保已挂在 AI（拆分/增援可能重置移除——GetBehavior 查重后补挂）
                if (GetOwnBehavior(f, type) == null) f.AI.AddAiBehavior((BehaviorComponent)cached);
                return;
            }
            var existing = GetOwnBehavior(f, type);
            if (existing != null)
            {
                _cache[key] = existing; // 复用引擎已有实例（幂等无 new）
                return;
            }
            var created = (BehaviorComponent)factory();
            f.AI.AddAiBehavior(created);
            _cache[key] = created;
        }

        // GetBehavior<T> 只有泛型重载（FormationAI.cs:138）；按类型分派查重
        private static BehaviorComponent GetOwnBehavior(Formation f, System.Type type)
        {
            if (type == typeof(CbHoldBehavior)) return f.AI.GetBehavior<CbHoldBehavior>();
            if (type == typeof(CbAdvanceBehavior)) return f.AI.GetBehavior<CbAdvanceBehavior>();
            if (type == typeof(CbRejoinBehavior)) return f.AI.GetBehavior<CbRejoinBehavior>();
            if (type == typeof(CbRetreatBehavior)) return f.AI.GetBehavior<CbRetreatBehavior>();
            return null;
        }

        // t50 硬门②：恢复原状（开关关/玩家接管/编队失效/Mission 结束）——移除 CB 加权（权重 0），清残留
        private static void Restore(Formation f, int id)
        {
            if (!_armed.Contains(id)) return; // 未接管无操作（幂等）
            // 还原原版状态唯一动作：CB 全部 behavior 权重 0（原版 FindBestBehavior 不再选中 CB——
            //   等效还原原行为竞争；不 ClearTacticOptions/不整套清空，§6.1/硬门⑤可恢复）
            if (f.AI != null)
            {
                if (f.AI.GetBehavior<CbHoldBehavior>() != null) f.AI.SetBehaviorWeight<CbHoldBehavior>(0f);
                if (f.AI.GetBehavior<CbAdvanceBehavior>() != null) f.AI.SetBehaviorWeight<CbAdvanceBehavior>(0f);
                if (f.AI.GetBehavior<CbRejoinBehavior>() != null) f.AI.SetBehaviorWeight<CbRejoinBehavior>(0f);
                if (f.AI.GetBehavior<CbRetreatBehavior>() != null) f.AI.SetBehaviorWeight<CbRetreatBehavior>(0f);
            }
            _armed.Remove(id);
            // 缓存清理：编队失效清该编队全部缓存（释放实例防泄漏）；否则保留复用（同 identity 重武装）。
            //   t50 简化：恢复即清该 identity 全部缓存（编队对象生命周期结束/重武装语义下缓存无复用价值）
            for (int t = 0; t < 4; t++)
            {
                _cache.Remove(id * 31 + OwnTypeHash(t));
            }
        }

        private static int OwnTypeHash(int index)
        {
            switch (index)
            {
                case 0: return typeof(CbHoldBehavior).GetHashCode();
                case 1: return typeof(CbAdvanceBehavior).GetHashCode();
                case 2: return typeof(CbRejoinBehavior).GetHashCode();
                default: return typeof(CbRetreatBehavior).GetHashCode();
            }
        }

        /// <summary>Mission 结束/清场：全量恢复（幂等；由 CombatAiMissionLogic 调用）。</summary>
        public static void ResetAll()
        {
            _armed.Clear();
            _cache.Clear();
        }

        // ---- 归因事件链（t50 硬门④）----
        /// <summary>意图→Behavior 选中→OrderApplied 事件链记录（RouteA 专用观测；不靠命令内容相似推断）。
        ///   OrderApplied 由落地 SetMovementOrder 在 CommandSourceScope.Enter(CombatBrainRouteA) 内经
        ///   SetOrderObserver 全参 Observe 归因（CommandSource=CombatBrainRouteA）。</summary>
        public static class RouteAEventLogger
        {
            private static long _intentCount;

            public static void RecordIntentApplied(Formation f, Cps2RouteAIntent intent)
            {
                _intentCount = (_intentCount + 1) % 1000000;
                if (CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled)
                {
                    CombatLog.Log(string.Format(
                        "[RouteA] intent→{0} team={1} f={2} seq={3}（Behavior 选中→OrderApplied 由 RouteA scope 归因）",
                        intent, f.Team != null ? f.Team.TeamIndex : 0, f.Index, _intentCount));
                }
            }

            /// <summary>纯逻辑断言语义：意图选择计数>&lt;限（t50 离线测试）。</summary>
            public static long IntentCount { get { return _intentCount; } }
            public static void Reset() { _intentCount = 0; }
        }
    }

    // ---- 路线 A 自定义 BehaviorComponent 子类（Movement 域；继承 TaleWorlds.MountAndBlade.BehaviorComponent，
    //     构造 base(formation)（BehaviorComponent.cs:76），override protected abstract GetAiWeight（:154）
    //     + protected virtual CalculateCurrentOrder（:172）设 CurrentOrder（protected set，:65）） ----
    // t50：GetAiWeight 返回单值 w（硬门⑤消除 100 放大）；落地 SetMovementOrder 在 RouteA scope 内（硬门④归因）。
    // t52 repair：scope 不再只包 CurrentOrder——Behavior 是引擎驱动，引擎在 Behavior 返回后才有真正 SetMovementOrder
    //   （Formation.FormationAI.Tick），不在 Behavior scope 内 → 归因仍是 ExternalUnknown。改为在 CalculateCurrentOrder
    //   内直接 Formation.SetMovementOrder 并包 scope（Behavior 即真实 setter 来源，消除间接路径归因盲区）。

    internal sealed class CbHoldBehavior : BehaviorComponent
    {
        public CbHoldBehavior(Formation formation) : base(formation) { }
        // t52 repair（MEDIUM ⑤单维降权）：GetAiWeight 返 1f（基权重归一），SetBehaviorWeight 应用 CbBehaviorWeight=10
        //   → 有效权重=1×10=10（消除 t20 GetAiWeight=10×SetBehaviorWeight=10=100 放大；FindBestBehavior 语义 GetAiWeight×WeightFactor）
        protected override float GetAiWeight() { return CbRouteAPoC.BaseBehaviorWeight; }
        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = MovementOrder.MovementOrderStop;
            using (CommandSourceScope.Enter(CommandSource.CombatBrainRouteA))
                Formation.SetMovementOrder(CurrentOrder);
        }
    }

    internal sealed class CbAdvanceBehavior : BehaviorComponent
    {
        public CbAdvanceBehavior(Formation formation) : base(formation) { }
        protected override float GetAiWeight() { return CbRouteAPoC.BaseBehaviorWeight; }
        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = MovementOrder.MovementOrderAdvance;
            using (CommandSourceScope.Enter(CommandSource.CombatBrainRouteA))
                Formation.SetMovementOrder(CurrentOrder);
        }
    }

    internal sealed class CbRejoinBehavior : BehaviorComponent
    {
        private float _targetX, _targetY;
        public CbRejoinBehavior(Formation formation) : base(formation) { }
        public void SetTarget(float x, float y) { _targetX = x; _targetY = y; }
        protected override float GetAiWeight() { return CbRouteAPoC.BaseBehaviorWeight; }
        protected override void CalculateCurrentOrder()
        {
            WorldPosition wp = Formation.CachedMedianPosition;
            wp.SetVec2(new Vec2(_targetX, _targetY));
            CurrentOrder = MovementOrder.MovementOrderMove(wp); // Rejoin：向 MainBody 移动（§5.2）
            using (CommandSourceScope.Enter(CommandSource.CombatBrainRouteA))
                Formation.SetMovementOrder(CurrentOrder);
        }
    }

    internal sealed class CbRetreatBehavior : BehaviorComponent
    {
        public CbRetreatBehavior(Formation formation) : base(formation) { }
        protected override float GetAiWeight() { return CbRouteAPoC.BaseBehaviorWeight; }
        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = MovementOrder.MovementOrderRetreat;
            using (CommandSourceScope.Enter(CommandSource.CombatBrainRouteA))
                Formation.SetMovementOrder(CurrentOrder);
        }
    }
}
