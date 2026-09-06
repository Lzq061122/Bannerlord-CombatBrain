namespace CombatBrain.Ai
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
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
        /// </summary>
        public static bool ExecuteWinner(FormationCommandProposal winner, FormationStatus status,
            LastDomainWrite last, float positionHysteresis, float facingHysteresis,
            System.Action<FormationCommandProposal> applyWrite, List<WriteSkipReason> skipLog)
        {
            // §5.3.5 执行前复核：identity/存活/玩家所有权/任务类型/租约
            if (!status.Valid) { if (skipLog != null) skipLog.Add(WriteSkipReason.FormationInvalid); return false; }
            if (status.PlayerOwned) { if (skipLog != null) skipLog.Add(WriteSkipReason.PlayerOwned); return false; }
            if (!status.FieldBattle) { if (skipLog != null) skipLog.Add(WriteSkipReason.NotFieldBattle); return false; }
            if (!status.LeaseOk) { if (skipLog != null) skipLog.Add(WriteSkipReason.LeaseNotOk); return false; }
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
                    CommandProvenance.Instance.Record(winner.FormationId, winner.Source, winner.Kind,
                        winner.ReasonCode, winner.TargetId, winner.TargetX, winner.TargetY);
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
        public void Tick(Mission mission, float now, List<FormationCommandProposal> proposals)
        {
            if (mission == null || proposals == null || proposals.Count == 0) return; // 近 O(1)

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
                if (_identityHashes.TryGetValue(key, out prevHash) && prevHash != hash)
                {
                    _lastWrites.Remove(key);   // 不继承旧状态（§8.2）
                    _identityHashes[key] = hash;
                    continue;
                }
                _identityHashes[key] = hash;

                // 状态复核输入（§8.1 玩家所有权 / §8.2 任务类型）
                var status = new FormationStatus
                {
                    Valid = f.CountOfUnits > 0,
                    PlayerOwned = f.PlayerOwner != null || f.HasPlayerControlledTroop
                        || f.IsPlayerTroopInFormation
                        || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam),
                    FieldBattle = mission.IsFieldBattle && !mission.IsSiegeBattle && !mission.IsSallyOutBattle,
                    LeaseOk = true // 本卡挂点默认 true（无租约域提案；迁移卡接入真实租约判定）
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
                    // 执行（sink=真实 SetOrder；写入成功 → 更新快照）
                    LastDomainWrite last = GetLastWrite(key, r.Domain);
                    bool wrote = ExecuteWinner(r.Winner, status, last,
                        PositionHysteresisMeters, FacingHysteresisRadians,
                        w => ApplySetOrder(f, w), null);
                    if (wrote) SetLastWrite(key, r.Domain, r.Winner);
                }
            }
        }

        // ---- 内部 ----

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
        private static void ApplySetOrder(Formation f, FormationCommandProposal winner)
        {
            switch (winner.Domain)
            {
                case CommandDomain.Movement:
                    f.SetMovementOrder(MapMovementOrder(f, winner));
                    break;
                case CommandDomain.Facing:
                    // Facing 目标为方向向量（FacingCorrection 输出归一化方向）
                    f.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(new Vec2(winner.TargetX, winner.TargetY)));
                    break;
                case CommandDomain.Arrangement:
                    f.SetArrangementOrder(MapArrangementOrder(winner.Kind));
                    break;
            }
        }

        private static MovementOrder MapMovementOrder(Formation f, FormationCommandProposal p)
        {
            switch (p.Kind)
            {
                case CommandKind.Hold:
                    return MovementOrder.MovementOrderStop;
                case CommandKind.Advance:
                    return MovementOrder.MovementOrderAdvance;
                case CommandKind.Charge:
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

        private static ArrangementOrder MapArrangementOrder(CommandKind kind)
        {
            // 第一版：Arrangement 域仅 Line（迁移卡按模块扩展具体阵型）
            return ArrangementOrder.ArrangementOrderLine;
        }
    }
}
