using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Morale;
using CombatBrain.Coordination;

namespace CombatBrain.Ai
{
    // F11 士气传导 + 分阶段撤退 —— 胶水层（编队级）。
    // 职责：0.5s 低频驱动纯逻辑（MoralePropagation + PhasedRetreatFSM，只读消费零改动）：
    //   * 邻队士气传染：本队士气 = Propagate(本队, 邻队士气均值, 邻队脱阵率均值)，再与队级士气(F2)按 15% 混合；
    //     脱阵率来自每编队 BreakRateWindow（OnAgentRemoved 阵亡事件驱动写入）。
    //   * 分阶段撤退：Hold → RequestRetreat(崩溃临界) → Disengage(批准) → Reform(重整) → Hold；
    //     批准源 = F2 意图 RETREAT（指挥官下令后撤）或本队士气跌破 0.15（绝望自救）；
    //     重整完成士气回升 +0.15（集结回稳）。
    // 协调：F2 RETREAT=批准信号；F2 REFORM=撤销撤退请求（危机解除回 Hold）；F7 重整信号=编队级"重整令"的
    //       编队层执行者（本层 Reform 状态实际执行脱离后重整），与 F7 事件互补不冲突。
    // 分域：骑兵编队归 F8 波次指挥（IsCavalryFormation 跳过本层，避免命令互踩）；本层管步兵/远程编队。
    //
    // 性能预算：0.5s 编队级聚合（O(编队数²)，n≤个位数）；命令仅状态跃迁下发；剧场上限 70m；
    // 无逐兵遍历、无空间查询、无每拍分配（编队列表用可复用字段）。
    // API 核实（BannerlordSage，游戏 v1.4.8；复用 F8 证据）：
    //   * Formation.CachedMedianPosition @ Formation.cs L247；QuerySystem.ClosestSignificantlyLargeEnemyFormation @ FormationQuerySystem.cs L275
    //   * Formation.SetMovementOrder(MovementOrder) @ Formation.cs L685；SetArrangementOrder @ L722
    //   * MovementOrder.MovementOrderRetreat @ MovementOrder.cs L55；MovementOrderStop @ L57
    //   * ArrangementOrder.ArrangementOrderLine @ ArrangementOrder.cs L36
    //   * Team.IsPlayerTeam @ Team.cs L40（v1.4.8，P0-④ 玩家队豁免）
    //   * Agent.Formation @ Agent.cs L1098（OnAgentRemoved 阵亡归队记录）
    public sealed class MoraleGlue
    {
        public const float TickInterval = 0.5f;

        private const float DriveRadius = 70f;           // 剧场上限（与 PlayerFocusRadius 对齐）
        private const float DisengageClearDistance = 25f; // 脱离完成判定：距敌编队 ≥25m
        private const float DisengageTimeout = 6f;        // 脱离超时兜底
        private const float ReformHoldSeconds = 3f;       // 重整集结时长
        private const float TeamMoralBlend = 0.15f;       // 队级士气(F2)混入权重
        private const float ReformMoraleBoost = 0.15f;    // 重整完成士气回升
        private const float DesperateMoral = 0.15f;       // 绝望阈值：士气跌破即自动批准
        private const float BreakRateRefSize = 20f;       // t50 脱阵率按编队规模归一的基准人数（≤基准不缩放）
        private const float RequestMinDwell = 3f;         // t50 撤退请求最短停留：RequestRetreat 3s 内不因危机短暂解除回 Hold（迟滞）

        private float _sinceTick;
        private readonly List<Formation> _teamFormations = new List<Formation>(); // 每队可复用列表（零分配）
        private readonly Dictionary<Formation, FormationMorale> _morale = new Dictionary<Formation, FormationMorale>();
        private readonly Dictionary<Formation, BreakRateWindow> _breakWindows = new Dictionary<Formation, BreakRateWindow>();
        private readonly Dictionary<Formation, RetreatState> _states = new Dictionary<Formation, RetreatState>();
        private readonly Dictionary<Formation, float> _stateElapsed = new Dictionary<Formation, float>();

        // 日志明细用：士气撤退 FSM 当前阶段名（无记录 → "-"，只读）
        public string GetStateName(Formation formation)
        {
            RetreatState s;
            return _states.TryGetValue(formation, out s) ? s.ToString() : "-";
        }

        /// <summary>阵亡事件写入所属编队脱阵窗口（由 CombatAiMissionLogic.OnAgentRemoved 调用，事件驱动）。</summary>
        public void RecordCasualty(Formation formation, float now)
        {
            if (formation == null) return;
            BreakRateWindow w;
            if (!_breakWindows.TryGetValue(formation, out w))
            {
                w = new BreakRateWindow();
                _breakWindows[formation] = w;
            }
            w.Record(now);
        }

        /// <summary>
        /// 编队级低频驱动：CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// </summary>
        /// <param name="mission">当前 Mission</param>
        /// <param name="dt">帧时间</param>
        /// <param name="now">战斗累计时间（脱阵窗口时间戳）</param>
        /// <param name="playerCenter">玩家焦点中心（剧场上限基准）</param>
        /// <param name="getIntent">各队当前意图（F2：RETREAT=批准、REFORM=撤销请求）</param>
        /// <param name="getMorale">各队士气（F2 意图引擎 0.5s 一刷，混入基线）</param>
        /// <param name="proposals">CPS-1 提案收集（本拍防御提案提交处；执行器拍末统一 Resolve+落地）。</param>
        public void Tick(Mission mission, float dt, float now, Vec2 playerCenter,
            Func<Team, IntentType> getIntent, Func<Team, float> getMorale,
            List<FormationCommandProposal> proposals)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            foreach (Team team in mission.Teams)
            {
                if (team == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)) continue; // P0-④ 玩家队豁免（MCM 可开关 t66）：不接管玩家编队士气/撤退
                IntentType intent = getIntent != null ? getIntent(team) : IntentType.Attack;
                float teamMoral = getMorale != null ? getMorale(team) : 0.5f;

                // 收集本队可见编队（非空、非骑兵——骑兵归 F8、70m 内）
                _teamFormations.Clear();
                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0) continue;
                    // t55：骑射（IsRangedCavalryFormation）归 SkirmishGlue 游击分域，F11 不再对其下令（命令不互踩）
                    if (f.QuerySystem == null || f.QuerySystem.IsCavalryFormation || f.QuerySystem.IsRangedCavalryFormation) continue;
                    Vec2 med = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // P1-④ 统一入口
                    if (VecMath.Dist(med.x, med.y, playerCenter.x, playerCenter.y) > DriveRadius)
                    {
                        ReleaseOutsideRadius(f, now, proposals); // P0-⑤ 剧场外释放 FSM 状态与命令（幂等）
                        continue;
                    }
                    EnsureState(f, teamMoral);
                    _teamFormations.Add(f);
                }

                // 邻队聚合（O(n²)，n≤个位数）：先算各队 BreakRate 与本队 Moral
                // t50 口径统一：脱阵率按编队规模归一（基准 20 人）——大编队正常伤亡波不再=满脱阵率，
                // 与 getMorale（F2 存活率维，同为相对比例口径）对齐；崩判据见 MoralePropagation.AtCollapse
                //（士气为主判据，脱阵率仅士气受创后加速）。
                int n = _teamFormations.Count;
                for (int i = 0; i < n; i++)
                {
                    Formation self = _teamFormations[i];
                    FormationMorale m = _morale[self];
                    m.BreakRate = NormalizedRate(self, now);
                    float sumM = 0f;
                    float sumB = 0f;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        Formation other = _teamFormations[j];
                        FormationMorale om = _morale[other];
                        sumM += om.Moral;
                        sumB += NormalizedRate(other, now);
                    }
                    float neighborMoralAvg = n > 1 ? sumM / (n - 1) : m.Moral;
                    float neighborBreakRate = n > 1 ? sumB / (n - 1) : 0f;

                    m.Moral = MoralePropagation.Propagate(m.Moral, neighborMoralAvg, neighborBreakRate);
                    m.Moral = Clamp01(m.Moral * (1f - TeamMoralBlend) + teamMoral * TeamMoralBlend);
                    _morale[self] = m;

                    TickRetreatFSM(self, m, intent, dt, now, proposals);
                }
            }
        }

        private void TickRetreatFSM(Formation formation, FormationMorale m, IntentType teamIntent, float dt,
            float now, List<FormationCommandProposal> proposals)
        {
            RetreatState state = _states[formation];
            float elapsed = _stateElapsed[formation] + dt;
            _stateElapsed[formation] = elapsed;

            bool collapse = MoralePropagation.AtCollapse(m.Moral, m.BreakRate);
            if (teamIntent == IntentType.Reform && state == RetreatState.RequestRetreat)
                collapse = false; // F2 重整令：危机解除，撤销撤退请求
            else if (state == RetreatState.RequestRetreat && !collapse && elapsed < RequestMinDwell)
                collapse = true; // t50 迟滞：请求撤退最短停留 3s——阵亡事件进/出 5s 窗的抖动不再秒切回 Hold
                                 //（消除 Hold↔RequestRetreat 0.5-1s 振荡；F2 Reform 撤销优先于迟滞）

            var inp = new RetreatInput
            {
                Collapse = collapse,
                Approved = teamIntent == IntentType.Retreat || m.Moral < DesperateMoral
            };

            switch (state)
            {
                case RetreatState.Disengage:
                    inp.DisengageDone = DistanceToEnemy(formation) >= DisengageClearDistance
                        || elapsed > DisengageTimeout; // 拉到安全距离 / 超时兜底
                    break;
                case RetreatState.Reform:
                    inp.ReformDone = elapsed >= ReformHoldSeconds; // 重整集结完成
                    break;
            }

            RetreatState next = PhasedRetreatFSM.Advance(state, inp);
            if (next != state)
            {
                _states[formation] = next;
                _stateElapsed[formation] = 0f;
                IssueOrder(formation, next, now, proposals);

                if (next == RetreatState.Hold && state == RetreatState.Reform)
                {
                    // 重整完成：士气回升（集结回稳）
                    FormationMorale m2 = _morale[formation];
                    m2.Moral = Clamp01(m2.Moral + ReformMoraleBoost);
                    _morale[formation] = m2;
                }
                CombatLog.Log(string.Format(
                    "[F11] 编队(team {0}) 士气={1:F2} 脱阵率={2:F2} {3} → {4}",
                    formation.Team.TeamIndex, m.Moral, m.BreakRate, state, next));
                Narrator.Notify(CombatAiMissionLogic.NarrateInfo(formation.Team,
                    Mission.Current != null ? Mission.Current.PlayerTeam : null),
                    "士气", // N1 叙事统一出口（编队级全量，撤退 FSM 跃迁一次性事件，正常模式显示）
                    // narrate-fix P0：模板含 {S} 占位符不得经 string.Format（{S} 非合法格式项→FormatException 崩溃）；
                    // 动态值用拼接，{S} 保留给 Narrator.ComposeText 代填。文案逐字保持（士气 F2 两位小数）。
                    "{S}编队 " + FeedbackText.Morale(state) + "→" + FeedbackText.Morale(next)
                        + "（士气" + m.Moral.ToString("F2") + "）",
                    InGameFeedback.FormationColor, InGameFeedback.NotifyTactical);

                // N2 E3 自身崩溃（§3 E3 备注）：被指挥队自身士气崩溃 → 撤退 FSM 跃迁到 Disengage 时补一条。
                // 文案统一 {S}=崩溃队自身（Subject 自动代填敌军/我军）——不用 {O}（enemyOfPlayer=true 时
                // {O} 会代填成玩家队=我军，产出「我军损失惨重」+优势绿语义矛盾，t28 修复）。
                // 颜色=事件对被叙事队的利害：指挥模式观看方自动获得敌我语义（敌侧绿=我军优势，友侧红=我军崩溃）。
                if (next == RetreatState.Disengage && state != RetreatState.Disengage)
                {
                    bool enemyOfPlayer = formation.Team.IsPlayerTeam ? false
                        : Mission.Current != null && Mission.Current.PlayerTeam != null
                            && Mission.Current.PlayerTeam.IsEnemyOf(formation.Team);
                    Narrator.Notify(CombatAiMissionLogic.NarrateInfo(formation.Team,
                        Mission.Current != null ? Mission.Current.PlayerTeam : null),
                        "事件",
                        "{S}损失惨重，阵线开始崩溃", // {S}=崩溃队自身（Subject 代填敌军/我军）
                        enemyOfPlayer ? InGameFeedback.AdvantageColor : InGameFeedback.CrisisColor,
                        InGameFeedback.NotifyTactical);
                }
            }
        }

        // 命令只在状态跃迁时提交提案一次（CPS-1 迁移：直接 SetOrder → P70 Defense 层提案，
        // 落地走唯一执行器；Retreat 经 P70 矩阵路径——Retreat 胜 Brace/Protect，不再依赖调用顺序）
        private void IssueOrder(Formation formation, RetreatState state, float now,
            List<FormationCommandProposal> proposals)
        {
            int teamId = formation.Team.TeamIndex;
            int identity = RuntimeHelpers.GetHashCode(formation); // repair-round-2：与执行器身份信号一致
            switch (state)
            {
                case RetreatState.Disengage:
                    proposals.Add(FormationCommandProposal.DefenseProposal(teamId, formation.Index, identity, now,
                        CommandSource.Morale, CommandKind.Retreat, CommandDomain.Movement, -1, 0f, 0f)); // 分阶段脱离
                    break;
                case RetreatState.Reform:
                    proposals.Add(FormationCommandProposal.DefenseProposal(teamId, formation.Index, identity, now,
                        CommandSource.Morale, CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f)); // 重整站定
                    proposals.Add(FormationCommandProposal.DefenseProposal(teamId, formation.Index, identity, now,
                        CommandSource.Morale, CommandKind.Arrangement, CommandDomain.Arrangement, -1, 0f, 0f)); // 重整列阵
                    break;
                // Hold / RequestRetreat：不下令（保持原状，等待批准）
            }
        }

        // P0-⑤ 剧场外命令泄漏修复：编队出 70m 后士气/撤退 FSM 停驱，已下发的 Retreat/Stop 与 FSM
        // 状态会永久残留。出半径 → 丢弃 FSM 状态 + 提交 Advance 提案交回原版；回场后 EnsureState 重建。
        // 幂等：无状态直接返回，重复调用无害。_breakWindows 为事件驱动输入（RecordCasualty 写入），保留。
        private void ReleaseOutsideRadius(Formation formation, float now, List<FormationCommandProposal> proposals)
        {
            if (!_states.ContainsKey(formation)) return;
            _states.Remove(formation);
            _stateElapsed.Remove(formation);
            _morale.Remove(formation);
            // CPS-1 迁移：直接 SetOrder → P70 Defense 层提案（Advance 交回原版默认推进）
            proposals.Add(FormationCommandProposal.DefenseProposal(formation.Team.TeamIndex, formation.Index,
                RuntimeHelpers.GetHashCode(formation), now,
                CommandSource.Morale, CommandKind.Advance, CommandDomain.Movement, -1, 0f, 0f));
        }

        private void EnsureState(Formation formation, float teamMoral)
        {
            if (_states.ContainsKey(formation)) return;
            float seed = teamMoral > 0.05f ? teamMoral : 1f; // 初始士气取队级士气（F2 可观察基线）
            _morale[formation] = new FormationMorale { Moral = seed, BreakRate = 0f };
            if (!_breakWindows.ContainsKey(formation))
                _breakWindows[formation] = new BreakRateWindow();
            _states[formation] = RetreatState.Hold;
            _stateElapsed[formation] = 0f;
        }

        private float RateOf(Formation formation, float now)
        {
            BreakRateWindow w;
            return _breakWindows.TryGetValue(formation, out w) ? w.Rate(now) : 0f;
        }

        // t50 脱阵率按编队规模归一：基准 20 人（≤基准不缩放；100 人编队阵亡 5 人=0.1 而非 0.5），
        // 消除「大编队正常伤亡波=满脱阵率」的口径失真（与 getMorale 存活率同为相对比例口径）。
        private float NormalizedRate(Formation formation, float now)
        {
            float raw = RateOf(formation, now);
            return raw * BreakRateRefSize / Math.Max(BreakRateRefSize, formation.CountOfUnits);
        }

        private static float DistanceToEnemy(Formation formation)
        {
            FormationQuerySystem enemy = formation.QuerySystem != null
                ? formation.QuerySystem.ClosestSignificantlyLargeEnemyFormation
                : null;
            // P1-④ 统一入口：跨子系统敌我对比，双方显式同口径（CachedMedian）
            Vec2 a = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian);
            Vec2 b = enemy != null && enemy.Formation != null
                ? TeamGeometry.GetFormationPosition(enemy.Formation, TeamPosKind.CachedMedian)
                : a;
            return VecMath.Dist(a.x, a.y, b.x, b.y);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
