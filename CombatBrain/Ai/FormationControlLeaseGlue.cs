using System;
using System.Collections.Generic;
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
    //   Release 边沿 SetControlledByAI(true)（仅 if WasAIControlledBeforeAcquire 且已由 CB Acquire）；
    //   异常/清场/Mission 结束幂等 ReleaseAll；watchdog 每帧只遍历活跃租约（无租约 O(1)）。
    //   acquire/release 逻辑不复制进多个 Glue（本文件唯一）。
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

        public int ActiveCount { get { return _manager.ActiveCount; } }

        /// <summary>读 ATC-4 per-formation CommitmentDecision（null=无有效决策）。</summary>
        public delegate CommitmentDecision? CommitmentReader(Team team, Formation formation);

        /// <summary>
        /// 驱动：每帧 watchdog（轻量，无租约 O(1)）+ 0.5s producer 拍（默认关闭时零动作）。
        /// </summary>
        public void Tick(Mission mission, float dt, float now,
            CommitmentReader commitmentReader, System.Func<Team, ArmyContextSnapshot> armyReader)
        {
            // 每帧 watchdog：只遍历活跃租约，TTL 到期安全释放（不下战术命令）
            List<KeyValuePair<int, int>> expired = _manager.UpdateWatchdog(now);
            for (int i = 0; i < expired.Count; i++)
            {
                ReleaseFormation(mission, expired[i].Key, expired[i].Value, LeaseReleaseReason.Expired);
            }

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

            foreach (Team team in mission.Teams)
            {
                if (team == null || !team.MBTeam.IsValid) continue;
                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    // PoC 编队域（§4.3）：仅 Infantry 域（远程/骑射/骑兵编队不试验）
                    if (f.QuerySystem.IsRangedFormation || f.QuerySystem.IsCavalryFormation
                        || f.QuerySystem.IsRangedCavalryFormation) continue;
                    EvaluateFormation(mission, team, f, now, commitmentReader, armyReader);
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

        private void EvaluateFormation(Mission mission, Team team, Formation f, float now,
            CommitmentReader commitmentReader, System.Func<Team, ArmyContextSnapshot> armyReader)
        {
            if (commitmentReader == null || armyReader == null) return;
            CommitmentDecision? decision = commitmentReader(team, f);
            ArmyContextSnapshot army = armyReader(team);
            if (!decision.HasValue) return;

            // 玩家/外部控制编队跳过（PlayerOwner/HasPlayerControlledTroop/IsPlayerTroopInFormation/玩家队）
            bool isPlayerFormation = team.IsPlayerTeam || f.PlayerOwner != null
                || f.HasPlayerControlledTroop || f.IsPlayerTroopInFormation;

            // 距 MainBody 距离（安全降级判定）
            Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
            float distToMain = VecMath.Dist(fc.x, fc.y, army.MainBodyX, army.MainBodyY);

            // 安全降级提案（§5.2 纯逻辑 ResolveFallback：Denied+远离→Rejoin；Hold+安全带内→Hold；否则不 Acquire）
            SafeFallbackKind fallback = FormationControlLeaseManager.ResolveFallback(
                decision.Value.Permission, army, distToMain);

            var input = new LeaseInput
            {
                TeamId = team.TeamIndex,
                FormationId = f.Index,
                GenerationGuard = f.Index, // 第一版 identity guard=编队索引（FormationId 复用由 RemoveFormation/清空检测兜底）
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
                Acquire(mission, team, f, outD, army, distToMain, now);
            }
            else if (outD.ReleaseNow)
            {
                Release(mission, team, f, outD);
            }
            // Renew 无需动作（TTL 已延长）；不刷日志
        }

        // Acquire 边沿：SetControlledByAI(false) + 安全命令变化沿 SetOrder（防每帧刷）
        private void Acquire(Mission mission, Team team, Formation f, LeaseDecision d, ArmyContextSnapshot army,
            float distToMain, float now)
        {
            FormationControlLease lease = _manager.GetLease(team.TeamIndex, f.Index);
            if (lease == null || !lease.WasAIControlledBeforeAcquire) return; // 所有权安全：原本非 AI 控制不夺权
            f.SetControlledByAI(false); // v1.4.8 已核实签名 SetControlledByAI(bool, bool=false)

            // 安全命令变化沿：仅当 fallback 目标变化时下发（防每帧高频 SetOrder）
            long key = FormationControlLeaseManager.Key(team.TeamIndex, f.Index);
            string fb = d.Fallback.ToString();
            string last;
            if (!_lastFallback.TryGetValue(key, out last) || last != fb)
            {
                _lastFallback[key] = fb;
                if (d.Fallback == SafeFallbackKind.Rejoin && army.MainBodyStrength > 0)
                {
                    // Rejoin：向合法 MainBody 点 Move（§5.2：不用未知敌位置构造 Rejoin）
                    WorldPosition wp = f.CachedMedianPosition;
                    wp.SetVec2(new Vec2(army.MainBodyX, army.MainBodyY));
                    if (wp.GetNavMesh() != UIntPtr.Zero && mission.IsPositionInsideBoundaries(wp.AsVec2))
                    {
                        CommandProvenance.Instance.Record(f.Index, CommandSource.FormationControlLease,
                            CommandKind.Move, 0, -1, army.MainBodyX, army.MainBodyY); // §7 provenance
                        f.SetMovementOrder(MovementOrder.MovementOrderMove(wp));
                    }
                }
                else if (d.Fallback == SafeFallbackKind.Hold)
                {
                    // Hold：安全带内保持阵位（§5.2 允许；非「对所有 Denied 统一 Stop」——仅 Hold+安全带内）
                    CommandProvenance.Instance.Record(f.Index, CommandSource.FormationControlLease,
                        CommandKind.Hold, 0);
                    f.SetMovementOrder(MovementOrder.MovementOrderStop);
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
            // 状态机已移除租约；恢复条件=曾由 CB Acquire（_lastFallback 有记录）且当前非 AI 控制
            long key = FormationControlLeaseManager.Key(team.TeamIndex, f.Index);
            bool acquiredByCb = _lastFallback.ContainsKey(key);
            if (acquiredByCb && !f.IsAIControlled)
            {
                f.SetControlledByAI(true); // 无损恢复原生 AI（§5.3：仅已 Acquire 的编队）
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
                if (acquiredByCb && !f.IsAIControlled)
                {
                    f.SetControlledByAI(true); // watchdog 只安全释放，不下战术命令
                }
                _lastFallback.Remove(key);
                break;
            }
            CombatLog.Log(string.Format(
                "[ControlLease] team={0} f={1} state=ExpiredRelease release={2}", teamIndex, formationId, reason));
        }

        // 普通单机野战（攻城/守城/突围 fail-open）
        private static bool IsFieldBattle(Mission mission)
        {
            return mission != null && mission.IsFieldBattle
                && !mission.IsSiegeBattle && !mission.IsSallyOutBattle;
        }
    }
}
