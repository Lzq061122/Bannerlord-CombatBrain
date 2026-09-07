using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain;
using CombatBrain.Coordination;

namespace CombatBrain.Ai
{
    // ATC-5R 编队控制权租约胶水层（FormationControlLeaseGlue）—— 职责局限（规格 §4.2）：
    //   检查 mission/team/formation 是否允许 PoC；读 ATC-4 per-formation CommitmentDecision；调租约状态机
    //   （FormationControlLeaseManager 纯逻辑）；Acquire 边沿 SetControlledByAI(false)+安全命令变化沿 SetOrder；
    //   Release 边沿 SetControlledByAI(true)（仅 if WasAIControlledBeforeAcquire 且已由 CB Acquire 且外部未接管）；
    //   异常/清场/Mission 结束幂等 ReleaseAll；watchdog 每帧只遍历活跃租约（无租约 O(1) 零分配）。
    //   acquire/release 逻辑不复制进多个 Glue（本文件唯一）。
    // atc5r-nits low2：watchdog 语义透明化——本 watchdog 依赖 MissionBehavior.OnMissionTick 每帧调度
    //   （CombatAiMissionLogic lease Bench 段调用），是「进程内 fail-open」而非 OS 级真 fail-open：
    //   若引擎停止 tick 本行为（崩溃/挂起/场景异常），租约不会自动释放，SetControlledByAI(false) 可能滞留；
    //   兜底语义=引擎正常 tick 时 producer 停止续租 → TTL 到期释放（Expired），Mission 收尾钩子
    //   ReleaseAll(MissionEnding) 幂等兜底。透明记录，不做虚假 fail-open 承诺。
    // PoC 开关默认关闭（CombatBrainSettings.FormationControlLeasePocEnabled=false）：关闭时本层零
    //   SetControlledByAI 调用、零战斗行为变化、Shadow 输出保持、性能差异接近零（producer 拍直接返回）。
    // 硬约束（规格 §2）：禁 Harmony/反射私有；禁全图逐 Agent；禁每帧高频 SetOrder 对刷；禁全局冻结 AI。
    // 只接管「CB 明确选择且当前 IsAIControlled==true」的单个编队；取得前非 AI 控制的编队不接管也不擅自恢复。
    // 运行期 R1-R8 未验证前不得 GO（本卡结论=PoC Ready / Runtime Pending）。
    public sealed class FormationControlLeaseGlue
    {
        private readonly FormationControlLeaseManager _manager = new FormationControlLeaseManager();
        private float _sinceTick;   // producer 0.5s 节拍
        private float _logTimer;    // [ControlLease] 5s 汇总兜底
        private readonly Dictionary<long, string> _lastFallback = new Dictionary<long, string>(); // 边沿日志防抖（每租约上次 fallback）
        private readonly HashSet<long> _loggedAcquire = new HashSet<long>();  // Acquire 边沿只记一次
        private readonly HashSet<long> _loggedRelease = new HashSet<long>();  // Release 边沿只记一次
        // atc5r-fix OPT：watchdog 过期租约复用缓冲（每帧 Clear 复用，无租约时零分配）
        private readonly List<KeyValuePair<int, int>> _expiredBuffer = new List<KeyValuePair<int, int>>();

        public int ActiveCount { get { return _manager.ActiveCount; } }

        // cps2-lease-integration：§6.2.7 租约持有期间 ExternalUnknown 写入观测（路线 B 失败判定输入；
        //   仅诊断观测开启时有数据——SetOrderObserver.Enabled=false 时跳过，运行期 cps3 开启）
        private long _lastExternalWriteCount;

        /// <summary>读 ATC-4 per-formation CommitmentDecision（null=无有效决策）。</summary>
        public delegate CommitmentDecision? CommitmentReader(Team team, Formation formation);

        /// <summary>
        /// cps2-lease-integration：租约活跃判定（执行器 FormationStatus.LeaseOk 实际接入——
        ///   租约域 fallback 提案仅在活跃租约编队上被执行）。公开只读，零副作用。
        /// </summary>
        public bool HasActiveLease(int teamIndex, int formationIndex)
        {
            return _manager.GetLease(teamIndex, formationIndex) != null;
        }

        /// <summary>
        /// 驱动：每帧 watchdog（轻量，无租约 O(1) 零分配，复用缓冲）+ 0.5s producer 拍（默认关闭时零动作）。
        /// </summary>
        public void Tick(Mission mission, float dt, float now,
            CommitmentReader commitmentReader, System.Func<Team, ArmyContextSnapshot> armyReader,
            List<FormationCommandProposal> proposals)
        {
            // 每帧 watchdog：只遍历活跃租约，TTL 到期安全释放（不下战术命令）；复用缓冲（OPT 零分配）
            _manager.UpdateWatchdog(now, _expiredBuffer);
            for (int i = 0; i < _expiredBuffer.Count; i++)
            {
                ReleaseFormation(mission, _expiredBuffer[i].Key, _expiredBuffer[i].Value, LeaseReleaseReason.Expired);
            }
            _expiredBuffer.Clear();

            _sinceTick += dt;
            if (_sinceTick < FormationControlLeaseManager.DecisionCadenceSeconds) return;
            _sinceTick = 0f;

            if (!CombatBrainSettings.FormationControlLeasePocEnabled)
            {
                // 开关关闭：释放全部 CB 租约（幂等；零 SetControlledByAI 调用当无租约）
                List<KeyValuePair<int, int>> all = _manager.ReleaseAll(LeaseReleaseReason.Disabled);
                for (int i = 0; i < all.Count; i++)
                {
                    ReleaseFormation(mission, all[i].Key, all[i].Value, LeaseReleaseReason.Disabled);
                }
                return;
            }
            if (mission == null || !IsFieldBattle(mission)) return; // 非普通单机野战 fail-open（租约由立即条件释放）

            // §6.2.7：租约期间 ExternalUnknown 写入记录（路线 B 失败判定输入；诊断观测关闭时跳过）
            LogExternalWrites();

            // 5s 汇总日志（§7.2：只在 5s 汇总/边沿/异常输出，禁每帧刷）
            _logTimer -= FormationControlLeaseManager.DecisionCadenceSeconds;
            if (_logTimer <= 0f)
            {
                _logTimer = 5f;
                CombatLog.Log(string.Format(
                    "[ControlLease] summary active={0} ttl={1:F2} minhold={2:F2}",
                    _manager.ActiveCount, FormationControlLeaseManager.LeaseTtlSeconds,
                    FormationControlLeaseManager.MinimumHoldSeconds));
            }

            // atc5r-fix P3 单编队域：PocFormationKind 过滤（§4.3）+ 每 Team 只选最小 FormationId 候选
            //   （确定性选择；禁止开关一开同时接管多支编队——每 Team 至多一个活跃租约）
            foreach (Team team in mission.Teams)
            {
                if (team == null || !team.MBTeam.IsValid) continue;
                Formation candidate = null;
                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    if (!MatchesPocKind(f)) continue; // PocFormationKind 实际过滤
                    if (candidate == null || f.Index < candidate.Index) candidate = f;
                }
                if (candidate != null)
                {
                    // atc5r-nits low1：同队已有活跃租约（最小编队解散后 TTL 窗口内）且候选非该租约编队
                    //   → 新候选不 Acquire（前置防御防短暂同队双租约；watchdog fail-open 仅兜底）
                    bool candidateHasLease = _manager.GetLease(team.TeamIndex, candidate.Index) != null;
                    if (!candidateHasLease && _manager.HasActiveForTeam(team.TeamIndex))
                    {
                        CombatLog.Log(string.Format(
                            "[ControlLease] team={0} f={1} state=Skip permission=? skip=SameTeamLeaseActive ai={2}",
                            team.TeamIndex, candidate.Index, candidate.IsAIControlled ? 1 : 0));
                        continue;
                    }
                    EvaluateFormation(mission, team, candidate, now, commitmentReader, armyReader, proposals);
                }
            }
        }

        /// <summary>幂等 ReleaseAll（Mission 结束/清场/OnMissionStateFinalized；由 CombatAiMissionLogic 调用）。</summary>
        public void ReleaseAll(Mission mission, LeaseReleaseReason reason)
        {
            List<KeyValuePair<int, int>> all = _manager.ReleaseAll(reason);
            for (int i = 0; i < all.Count; i++)
            {
                ReleaseFormation(mission, all[i].Key, all[i].Value, reason);
            }
            _lastFallback.Clear();
            _loggedAcquire.Clear();
            _loggedRelease.Clear();
        }

        // §6.2.7：租约持有期间 ExternalUnknown 写入记录（SetOrderObserver 观测计数增量；
        //   诊断关闭（Enabled=false）无法观测 → 跳过——cps3 运行期开启后生效，路线 B 失败判定输入）
        private void LogExternalWrites()
        {
            if (!SetOrderObserver.Enabled) return;
            long extNow = SetOrderObserver.CountFor(CommandDomain.Movement, CommandSource.ExternalUnknown)
                + SetOrderObserver.CountFor(CommandDomain.Facing, CommandSource.ExternalUnknown)
                + SetOrderObserver.CountFor(CommandDomain.Arrangement, CommandSource.ExternalUnknown);
            if (extNow > _lastExternalWriteCount)
            {
                CombatLog.Log(string.Format(
                    "[ControlLease] external write detected delta={0} total={1} (§6.2.7 路线 B 判定输入)",
                    extNow - _lastExternalWriteCount, extNow));
            }
            _lastExternalWriteCount = extNow;
        }

        private void EvaluateFormation(Mission mission, Team team, Formation f, float now,
            CommitmentReader commitmentReader, System.Func<Team, ArmyContextSnapshot> armyReader,
            List<FormationCommandProposal> proposals)
        {
            if (commitmentReader == null || armyReader == null) return;
            CommitmentDecision? decision = commitmentReader(team, f);
            ArmyContextSnapshot army = armyReader(team);
            if (!decision.HasValue) return;

            // 玩家/外部控制编队跳过（PlayerOwner/HasPlayerControlledTroop/IsPlayerTroopInFormation 无条件；
            // 玩家队豁免 P0-④ 走 CombatBrainSettings.PlayerTeamExemptionEnabled（MCM 可开关 t66，与其它 6 个 Glue 口径一致）；
            // 红线 3：玩家所属/含玩家控制兵员的编队任何时候不接管，不随开关放开）
            bool isPlayerFormation = (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)
                || f.PlayerOwner != null || f.HasPlayerControlledTroop || f.IsPlayerTroopInFormation;

            // 距 MainBody 距离（安全降级判定）
            Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
            float distToMain = VecMath.Dist(fc.x, fc.y, army.MainBodyX, army.MainBodyY);

            // 安全降级提案（§5.2 纯逻辑 ResolveFallback：Denied+远离→Rejoin；Hold+安全带内→Hold；否则不 Acquire）
            SafeFallbackKind fallback = FormationControlLeaseManager.ResolveFallback(
                decision.Value.Permission, army, distToMain);

            // atc5r-fix P2：先验证安全命令有效再 Acquire（消除「关 AI 后无安全命令」无人负责窗口）——
            //   Rejoin 需 MainBody 点导航有效（boundaries + navmesh）；无效 → 降级 NoSafeFallback 不 Acquire 不关 AI
            if (fallback == SafeFallbackKind.Rejoin)
            {
                WorldPosition wp = f.CachedMedianPosition;
                wp.SetVec2(new Vec2(army.MainBodyX, army.MainBodyY));
                if (wp.GetNavMesh() == UIntPtr.Zero || !mission.IsPositionInsideBoundaries(wp.AsVec2))
                {
                    fallback = SafeFallbackKind.NoSafeFallback;
                }
            }

            var input = new LeaseInput
            {
                TeamId = team.TeamIndex,
                FormationId = f.Index,
                Identity = f, // atc5r-fix P4：编队对象引用作真实 identity 信号（非 f.Index——对象重建/Index 复用不继承旧租约）
                PocEnabled = CombatBrainSettings.FormationControlLeasePocEnabled, // 现有集中配置文件（规格 §4.3）
                FieldBattle = IsFieldBattle(mission),
                FormationValid = f.CountOfUnits > 0,
                IsAIControlled = f.IsAIControlled,
                IsPlayerFormation = isPlayerFormation,
                HasCommitment = decision.HasValue,
                Permission = (int)decision.Value.Permission,
                Reasons = (int)decision.Value.Reasons,
                InContact = decision.Value.Permission == CommitmentPermission.Limited, // Limited=贴身脱困（不夺权）
                RetreatOrEmergencyOrDefensive =
                    (decision.Value.Reasons & CommitmentReason.EmergencyOverride) != 0
                    || (decision.Value.Reasons & CommitmentReason.DefensiveOverride) != 0,
                Fallback = fallback,
                Now = now
            };

            LeaseDecision outD = _manager.Update(input);
            if (outD.AcquireNow)
            {
                Acquire(mission, team, f, outD, army, distToMain, now, proposals);
            }
            else if (outD.ReleaseNow)
            {
                Release(mission, team, f, outD);
            }
            // Renew 无需动作（TTL 已延长）；不刷日志
        }

        // Acquire 边沿：SetControlledByAI(false) + fallback 提交提案（cps2-lease-integration：
        // 命令写入迁移到唯一执行器——§6.2.1 Lease 只决定所有权边沿；§6.2.3 先验最终方案再关 AI，
        // 随后由执行器落地三域；本层不再旁路 SetMovementOrder）
        private void Acquire(Mission mission, Team team, Formation f, LeaseDecision d, ArmyContextSnapshot army,
            float distToMain, float now, List<FormationCommandProposal> proposals)
        {
            FormationControlLease lease = _manager.GetLease(team.TeamIndex, f.Index);
            if (lease == null || !lease.WasAIControlledBeforeAcquire) return; // 所有权安全：原本非 AI 控制不夺权
            f.SetControlledByAI(false); // v1.4.8 已核实签名 SetControlledByAI(bool, bool=false)

            // 安全命令变化沿：仅当 fallback 目标变化时提交提案（防每帧高频；执行器迟滞再兜底）
            long key = FormationControlLeaseManager.Key(team.TeamIndex, f.Index);
            string fb = d.Fallback.ToString();
            string last;
            if (!_lastFallback.TryGetValue(key, out last) || last != fb)
            {
                _lastFallback[key] = fb;
                int identity = RuntimeHelpers.GetHashCode(f);
                if (d.Fallback == SafeFallbackKind.Rejoin && army.MainBodyStrength > 0)
                {
                    // Rejoin：向合法 MainBody 点 Move（§5.2：不用未知敌位置构造 Rejoin）；
                    // P80 Emergency 层提案（安全降级语义，§3.2）——唯一执行器落地（目标=MainBody）
                    proposals.Add(FormationCommandProposal.EmergencyProposal(team.TeamIndex, f.Index, identity, now,
                        CommandSource.FormationControlLease, CommandKind.Move, CommandDomain.Movement,
                        0, -1, army.MainBodyX, army.MainBodyY));
                }
                else if (d.Fallback == SafeFallbackKind.Hold)
                {
                    // Hold：安全带内保持阵位（§5.2 允许；非「对所有 Denied 统一 Stop」——仅 Hold+安全带内）
                    proposals.Add(FormationCommandProposal.EmergencyProposal(team.TeamIndex, f.Index, identity, now,
                        CommandSource.FormationControlLease, CommandKind.Hold, CommandDomain.Movement,
                        0, -1, 0f, 0f));
                }
            }

            // Acquire 边沿日志（只记一次；permission/reason 从 lease 快照）
            if (_loggedAcquire.Add(key))
            {
                string permName = lease.SafeCommandKind == SafeFallbackKind.Rejoin ? "Denied" : "Hold";
                CombatLog.Log(string.Format(
                    "[ControlLease] team={0} f={1} state=Acquire prevAI={2} permission={3} reason={4} fallback={5} ttl={6:F2}",
                    team.TeamIndex, f.Index, 1, permName,
                    (CommitmentReason)lease.CommitmentReasonSnapshot,
                    lease.SafeCommandKind.ToString().ToLowerInvariant(),
                    FormationControlLeaseManager.LeaseTtlSeconds));
            }
        }

        // Release 边沿：SetControlledByAI(true)（仅 if WasAIControlledBeforeAcquire 且已由 CB Acquire）
        private void Release(Mission mission, Team team, Formation f, LeaseDecision d)
        {
            // 状态机已移除租约；恢复条件=曾由 CB Acquire + 当前非 AI 控制 + 外部未接管（P1）
            long key = FormationControlLeaseManager.Key(team.TeamIndex, f.Index);
            bool acquiredByCb = _lastFallback.ContainsKey(key);
            // atc5r-fix P1：外部接管（玩家成为 owner / 外部已把 IsAIControlled 改回 true）→ 绝不 SetControlledByAI(true)
            //   （不得把玩家刚取得的控制权交回 AI）；三条件全满足才恢复：WasAIControlledBeforeAcquire（租约
            //   存在即隐含）+ AcquiredByCombatBrain（acquiredByCb）+ 外部未接管
            if (acquiredByCb && !f.IsAIControlled && !IsPlayerControlled(team, f))
            {
                f.SetControlledByAI(true); // 无损恢复原生 AI（§5.3：仅已 Acquire 且外部未接管的编队）
            }
            _lastFallback.Remove(key);
            if (_loggedRelease.Add(key))
            {
                CombatLog.Log(string.Format(
                    "[ControlLease] team={0} f={1} state=Release ai={2} release={3}",
                    team.TeamIndex, f.Index, acquiredByCb ? 1 : 0, d.ReleaseReason));
            }
        }

        // 异常/清场路径释放（watchdog TTL/ReleaseAll）：由 Manager 移除租约后执行 SetControlledByAI(true)
        private void ReleaseFormation(Mission mission, int teamIndex, int formationId, LeaseReleaseReason reason)
        {
            if (mission == null) return;
            Team team = null;
            foreach (Team t in mission.Teams)
            {
                if (t != null && t.TeamIndex == teamIndex) { team = t; break; }
            }
            if (team == null) return;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.Index != formationId) continue;
                long key = FormationControlLeaseManager.Key(teamIndex, formationId);
                bool acquiredByCb = _lastFallback.ContainsKey(key);
                // P1：外部接管 → 不 SetControlledByAI(true)；watchdog 只安全释放，不下战术命令
                if (acquiredByCb && !f.IsAIControlled && !IsPlayerControlled(team, f))
                {
                    f.SetControlledByAI(true);
                }
                _lastFallback.Remove(key);
                break;
            }
            CombatLog.Log(string.Format(
                "[ControlLease] team={0} f={1} state=ExpiredRelease release={2}", teamIndex, formationId, reason));
        }

        // atc5r-fix P1：外部/玩家接管判定（PlayerOwner/HasPlayerControlledTroop/IsPlayerTroopInFormation 无条件；
        // 玩家队豁免 P0-④ 走 CombatBrainSettings.PlayerTeamExemptionEnabled（MCM 可开关 t66，与其它 6 个 Glue 口径一致）；
        // 红线 3：玩家所属/含玩家控制兵员的编队任何时候不接管，不随开关放开）
        private static bool IsPlayerControlled(Team team, Formation f)
        {
            return team != null && ((CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)
                || f.PlayerOwner != null || f.HasPlayerControlledTroop || f.IsPlayerTroopInFormation);
        }

        // atc5r-fix P3：PocFormationKind 过滤（§4.3 单域；0=Infantry——远程/骑射/骑兵不试验）
        private static bool MatchesPocKind(Formation f)
        {
            int kind = CombatBrainSettings.FormationControlLeasePocFormationKind;
            if (kind == 0)
            {
                return !f.QuerySystem.IsRangedFormation && !f.QuerySystem.IsCavalryFormation
                    && !f.QuerySystem.IsRangedCavalryFormation;
            }
            return false; // 其它 Kind 域第一版不支持（0=Infantry 唯一）
        }

        // 普通单机野战（攻城/守城/突围 fail-open）
        private static bool IsFieldBattle(Mission mission)
        {
            return mission != null && mission.IsFieldBattle
                && !mission.IsSiegeBattle && !mission.IsSallyOutBattle;
        }
    }
}
