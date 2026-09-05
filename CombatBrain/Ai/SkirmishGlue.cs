namespace CombatBrain.Ai
{
    using System;
    using System.Collections.Generic;
    using TaleWorlds.Engine;
    using TaleWorlds.Library;
    using TaleWorlds.MountAndBlade;
    using CombatBrain.Coordination;

    // T5 骑射游击（SkirmishGlue）—— 编队级 0.5s 低频（与 F8/F11 并列挂 CombatAiMissionLogic）。
    // 用户实测 P0：骑射手开局正面冲锋送死——骑射（IsRangedCavalryFormation）不在 F8（实测 骑FSM=-），
    // 原版 AI 让其正面冲阵。本模块把骑射行为改为 消耗/游击/夹击：
    //   * 游击：保持 30-60m 距离环绕敌阵射击（射击由原版/引擎自理，本层只管走位）；
    //   * 后撤：被敌近身（<18m）向己方方向（本队步兵线中心）拉开；
    //   * 夹击：敌编队与我方步兵接战且我距敌 ≥30m → 停位侧翼输出（MovementOrderStop）；
    //   * 绝不下 MovementOrderCharge（不冲锋送死）。
    // 目标选择：敌队编队优先敌射手（+2）/敌骑兵（+1，威胁我方骑射），同分取最近（可观察分类）。
    // 分域（命令不互踩）：
    //   * F8 CavalryGlue 只接 IsCavalryFormation（骑射实测不在其中）；
    //   * F11 MoraleGlue t55 同步收窄跳过 IsRangedCavalryFormation（骑射归本模块）；
    //   * F3 ProtectGlue 激活时（队级保护中）骑射听 F3（退向阵后），本模块让位（isTeamProtected）。
    // 反作弊闸：目标=敌编队 QuerySystem 分类 + CachedMedian 距离（可观察）。
    // 性能：0.5s 编队级 O(编队数)；命令防抖（模式切换或目标点漂移 >3m 才下发）；剧场 150m（与 F8 对齐）。
    // API 核实（v1.4.8 反编译源）：MovementOrderMove(WorldPosition)/MovementOrderStop @ MovementOrder.cs
    // L61/L57（ProtectGlue.IssueMove 同款已用模式：CachedMedianPosition 复制+SetVec2 重定向+导航校验）；
    // MovementOrderAdvance @ L59；CachedMedianPosition @ Formation.cs L247。
    public sealed class SkirmishGlue
    {
        public const float TickInterval = 0.5f;

        private const float DriveRadius = 150f;        // 剧场半径（与 F8 CavalryDriveRadius 对齐）
        private const float WithdrawDist = 18f;        // 被近身阈值：距目标编队 <18m 即后撤拉开
        private const float SkirmishOuter = 60f;       // 游击环外沿（>60m 先接近）
        private const float RingRadius = 45f;          // 环绕射击半径（30-60m 中值）
        private const float FlankHoldDist = 30f;       // 夹击位判定：敌接战我方步兵且我距敌 ≥30m → 停位射击
        private const float EngagedDist = 25f;         // 敌编队与我方步兵接战判定距离
        private const float ReissueMinDrift = 3f;      // 命令防抖：目标点漂移 >3m 才重下发
        private const float WithdrawStep = 25f;        // 无己方参照时的后撤步长

        private enum Mode { Skirmish, Withdraw, FlankHold }

        private readonly Dictionary<Formation, Mode> _modes = new Dictionary<Formation, Mode>();
        private readonly Dictionary<Formation, Vec2> _lastOrder = new Dictionary<Formation, Vec2>();
        private float _sinceTick;

        // 日志明细用：骑射游击当前模式名（无记录 → "-"，只读）
        public string GetModeName(Formation formation)
        {
            Mode m;
            return _modes.TryGetValue(formation, out m) ? m.ToString() : "-";
        }

        /// <summary>编队级低频驱动：CombatAiMissionLogic.OnMissionTick 调用，内部按 TickInterval 节流。</summary>
        /// <param name="isEnemyFormationKnown">X6 感知接线：敌编队是否已知（观察者队+敌编队，KnowledgeView 过滤，可 null=不过滤=现状）。</param>
        public void Tick(Mission mission, float dt, Vec2 playerCenter, Func<int, bool> isTeamProtected,
            Func<Team, Formation, bool> isEnemyFormationKnown = null)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            foreach (Team team in mission.Teams)
            {
                if (team == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)) continue; // P0-④ 玩家队豁免（MCM 可开关 t66）：玩家骑射归玩家指挥
                if (isTeamProtected != null && isTeamProtected(team.TeamIndex)) continue; // F3 保护激活：骑射听 F3

                foreach (Formation formation in team.FormationsIncludingEmpty)
                {
                    if (formation == null || formation.CountOfUnits == 0) continue;
                    if (formation.QuerySystem == null || !formation.QuerySystem.IsRangedCavalryFormation) continue; // 只接骑射

                    Vec2 med = TeamGeometry.GetFormationPosition(formation, TeamPosKind.CachedMedian); // P1-④ 统一入口
                    if (VecMath.Dist(med.x, med.y, playerCenter.x, playerCenter.y) > DriveRadius)
                    {
                        ReleaseOutsideRadius(formation); // P0-⑤ 剧场外释放（幂等，删状态+Advance 交回原版）
                        continue;
                    }

                    TickFormation(mission, team, formation, med, isEnemyFormationKnown);
                }
            }
        }

        private void TickFormation(Mission mission, Team team, Formation formation, Vec2 own,
            Func<Team, Formation, bool> isEnemyFormationKnown)
        {
            Formation target = SelectTarget(mission, team, own, isEnemyFormationKnown);
            if (target == null)
            {
                // 无合适目标：不冲不站桩，向己方方向拉开待机
                ApplyMode(formation, Mode.Withdraw, WithdrawPoint(team, own, own));
                return;
            }

            Vec2 tPos = TeamGeometry.GetFormationPosition(target, TeamPosKind.CachedMedian);
            float dist = VecMath.Dist(own.x, own.y, tPos.x, tPos.y);
            bool enemyEngaged = IsTargetEngagedWithAllies(team, tPos);

            Mode mode;
            Vec2 dest;
            if (dist < WithdrawDist)
            {
                mode = Mode.Withdraw; // 被近身：向己方方向拉开
                dest = WithdrawPoint(team, own, tPos);
            }
            else if (enemyEngaged && dist >= FlankHoldDist)
            {
                mode = Mode.FlankHold; // 夹击位：敌接战我方步兵线，我停位侧翼输出
                dest = own;
            }
            else if (dist > SkirmishOuter)
            {
                mode = Mode.Skirmish; // 接近游击环
                dest = RingPoint(own, tPos, 0f);
            }
            else
            {
                mode = Mode.Skirmish; // 环内游击：切向环绕（每编队固定环绕方向，避免扎堆同侧）
                dest = RingPoint(own, tPos, OrbitDir(formation));
            }

            ApplyMode(formation, mode, dest);
        }

        // 目标选择：敌队（IsEnemyOf，P1-③ 同源判定）编队——敌射手 +2（优先消耗输出源）、敌骑兵 +1
        // （威胁我方骑射）；同分取最近（CachedMedian 距离）。无敌编队 → null（调用方拉开待机）。
        // X6 感知接线：isEnemyFormationKnown 非空时，未知敌编队（KnowledgeView 未见/超期）不计入候选——
        // 保守降级=无目标→拉开待机（已有 null 分支，不硬冲看不见的敌人）；null=不过滤=现状等价。
        private static Formation SelectTarget(Mission mission, Team own, Vec2 ownPos,
            Func<Team, Formation, bool> isEnemyFormationKnown)
        {
            Formation best = null;
            int bestScore = -1;
            float bestDist = float.MaxValue;
            foreach (Team t in mission.Teams)
            {
                if (t == null || !t.MBTeam.IsValid || !t.IsEnemyOf(own)) continue;
                foreach (Formation f in t.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    if (isEnemyFormationKnown != null && !isEnemyFormationKnown(own, f)) continue; // X6：未知敌编队不算候选
                    int score = 0;
                    if (f.QuerySystem.IsRangedFormation) score += 2; // 优先消耗敌输出源
                    if (f.QuerySystem.IsCavalryFormation) score += 1; // 敌骑威胁我方骑射
                    Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                    float d = VecMath.Dist(ownPos.x, ownPos.y, p.x, p.y);
                    if (score > bestScore || (score == bestScore && d < bestDist))
                    {
                        bestScore = score;
                        bestDist = d;
                        best = f;
                    }
                }
            }
            return best;
        }

        private void ApplyMode(Formation formation, Mode mode, Vec2 dest)
        {
            Mode prev;
            bool had = _modes.TryGetValue(formation, out prev);
            Vec2 last;
            bool hasOrder = _lastOrder.TryGetValue(formation, out last);
            if (had && hasOrder && prev == mode && VecMath.Dist(dest.x, dest.y, last.x, last.y) <= ReissueMinDrift)
                return; // 无变化：不刷命令

            _modes[formation] = mode;
            _lastOrder[formation] = dest;
            if (mode == Mode.FlankHold)
            {
                CommandProvenance.Instance.Record(formation.Index, CommandSource.Skirmish, CommandKind.Hold); // ATC-0 记录
                formation.SetMovementOrder(MovementOrder.MovementOrderStop); // 夹击位：停位射击
            }
            else
                IssueMove(formation, dest);
        }

        // 下发移动（ProtectGlue.IssueMove 同款已核实模式：CachedMedianPosition 复制+SetVec2 重定向+导航校验）
        private static void IssueMove(Formation formation, Vec2 target)
        {
            WorldPosition wp = formation.CachedMedianPosition;
            wp.SetVec2(target);
            if (wp.GetNavMesh() == UIntPtr.Zero) return; // 无导航点不下发
            CommandProvenance.Instance.Record(formation.Index, CommandSource.Skirmish,
                CommandKind.Move, 0, -1, target.x, target.y); // ATC-0 记录
            formation.SetMovementOrder(MovementOrder.MovementOrderMove(wp));
        }

        // 环绕点：敌中心 + (我→敌反向单位向量旋转 radians) * RingRadius
        private static Vec2 RingPoint(Vec2 own, Vec2 enemy, float radians)
        {
            float dx = own.x - enemy.x;
            float dy = own.y - enemy.y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-4f) { dx /= len; dy /= len; } else { dx = 1f; dy = 0f; }
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Vec2(enemy.x + (dx * cos - dy * sin) * RingRadius,
                            enemy.y + (dx * sin + dy * cos) * RingRadius);
        }

        // 环绕方向：按编队实例确定性哈希取 ±29° 切向偏移（避免全部骑射挤同一弧段）
        private static float OrbitDir(Formation formation)
        {
            return (formation.GetHashCode() & 1) == 0 ? 0.5f : -0.5f;
        }

        // 后撤点：己方方向（本队步兵线中心，排除同类骑射）优先；无参照 → 背敌方向拉开 25m
        private static Vec2 WithdrawPoint(Team team, Vec2 own, Vec2 enemyPos)
        {
            float sx = 0f, sy = 0f;
            int n = 0;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                if (f.QuerySystem.IsRangedCavalryFormation) continue; // 排除同类骑射
                Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                sx += p.x;
                sy += p.y;
                n++;
            }
            if (n > 0) return new Vec2(sx / n, sy / n);

            float dx = own.x - enemyPos.x;
            float dy = own.y - enemyPos.y;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 1e-4f) { dx /= len; dy /= len; } else { dx = 0f; dy = -1f; }
            return new Vec2(own.x + dx * WithdrawStep, own.y + dy * WithdrawStep);
        }

        // 敌编队是否已与我方步兵接战（敌中位距我队步兵编队中位 ≤EngagedDist，可观察）
        private static bool IsTargetEngagedWithAllies(Team team, Vec2 tPos)
        {
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                if (!f.QuerySystem.IsInfantryFormation) continue;
                Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                if (VecMath.Dist(tPos.x, tPos.y, p.x, p.y) <= EngagedDist) return true;
            }
            return false;
        }

        // P0-⑤ 剧场外命令泄漏修复（300m 内由暂停语义不适用本模块，骑射应随战场走）：
        // 出 150m 剧场 → 丢弃游击状态 + Advance 交回原版。幂等：无状态直接返回。
        private void ReleaseOutsideRadius(Formation formation)
        {
            if (!_modes.ContainsKey(formation)) return;
            _modes.Remove(formation);
            _lastOrder.Remove(formation);
            CommandProvenance.Instance.Record(formation.Index, CommandSource.Skirmish, CommandKind.Advance); // ATC-0 记录
            formation.SetMovementOrder(MovementOrder.MovementOrderAdvance); // 交回原版默认推进（MovementOrder.cs L59）
        }
    }
}
