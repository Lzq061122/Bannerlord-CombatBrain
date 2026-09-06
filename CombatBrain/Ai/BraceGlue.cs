using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain;
using CombatBrain.Coordination;
using CombatBrain.Intent;

namespace CombatBrain.Ai
{
    // F4 BraceGlue 架枪反骑阵地（H3，P3 级）——BraceFormation 意图执行层（pipeline §三 F4）。
    // 目标：敌骑逼近时步兵架枪站定。
    // 触发：敌骑编队 ≤55m 且净接近（编队级可观察运动学，防御自保；本队意图 == BraceFormation 时
    //   亦须过同一距离守卫，t71 敌骑尚远不站定）；剧集内站定（MovementOrderStop），退出（>70m/无敌骑）后交回正常 AI。
    // 动作：
    //   * 步兵编队 → 剧集武装沿统计架枪可用占比（多数兵零扫描：GetCountOfUnitsWithCondition 一次）
    //     达标 → 每拍站定（Stop）；引擎对静止持枪兵自动架枪（架枪是武器 usage 引擎行为，非 AI 命令）。
    //   * 无长杆降级：架枪可用占比 <30% → 不站定（交回正常防御 AI）。
    //   * 架枪优先：本段在 protect 之后运行（OnMissionTick 顺序 protect→brace→block），后写者胜，
    //     站定命令覆盖同拍保护机动（殿后档/回援），步兵就地架枪而非移动。
    // 反作弊闸（t12 行19 / t16 附 L131）：只读 己方编队位置(CachedMedianPosition)、敌编队位置
    //   （编队位置聚合，O(敌编队数)）、己方意图(F2)、己方兵装备架枪可用性(CanPerformBraceCached)；
    //   禁读：敌编队 MovementOrder/行为权重/士气值/血量/意图状态（proposal L166）。
    // 性能：0.5s 低频；敌骑扫描 O(敌编队数)；逐兵统计仅在剧集武装沿（多数兵零扫描，一次/编队/剧集）；
    //       复用列表零每拍分配。
    // API 核实（BannerlordSage，游戏 v1.4.8 精确匹配）：
    //   Agent.CanPerformBraceCached @ Agent.cs L506（public bool，引擎缓存架枪能力）；
    //     注意：Agent.CheckIfCanBrace 在本版本不存在——RBM 用 Utilities.CheckIfCanBrace(Agent) 重实现
    //     （遍历武器槽，WeaponDescriptionId 含 "bracing" 即架枪可用，@ RBM Utilities.cs:1406；调用点 OverrideAgent.cs:83）
    //   MovementOrder.MovementOrderStop（static readonly 字段）@ MovementOrder.cs（MoraleGlue.cs:179 已用）
    //   Formation.SetMovementOrder(MovementOrder) @ Formation.cs L383（sage read_csharp_type；CavalryGlue.cs:23 已用）
    //   Formation.GetCountOfUnitsWithCondition(Func<Agent,bool>) @ Formation.cs L438（sage）
    //   FormationQuerySystem.IsInfantryFormation @ RBM OverrideBehaviorAdvance.cs:149；IsCavalryFormation @ Utilities.cs:792；
    //   IsRangedCavalryFormation @ Utilities.cs:1035；Formation.CachedMedianPosition @ CavalryGlue.cs:68（F8 已用）
    //   Team.IsPlayerTeam @ Team.cs L40（v1.4.8，P0-④ 玩家队豁免）
    //   已核实但本层不需要：Agent.TryToWieldWeaponInSlot（Agent.cs L4121，F9）、GetLookDirection（L1453，F6）、
    //   SetMovementDirection（L2402，F6）——站定为编队级 Stop 命令，换武器/朝向由引擎架枪动画自理。
    public sealed class BraceGlue
    {
        public const float TickInterval = 0.5f;
        private const float DriveRadius = 70f; // 剧场上限（与 MoraleGlue/ProtectGlue 对齐）

        private float _sinceTick;
        private readonly Dictionary<int, float> _prevCavDist = new Dictionary<int, float>(); // 队 → 上拍最近敌骑距离(-1=未知)
        private readonly Dictionary<int, bool> _episode = new Dictionary<int, bool>();       // 队 → 架枪剧集中
        private readonly Dictionary<Formation, bool> _armed = new Dictionary<Formation, bool>(); // 编队 → 本剧集已评估（达标=已站定）
        private readonly List<Formation> _infantry = new List<Formation>(); // 复用列表（零分配）

        /// <summary>架枪状态查询（诊断/测试用）。</summary>
        public bool IsBracing(int teamIndex)
        {
            bool v;
            return _episode.TryGetValue(teamIndex, out v) && v;
        }

        /// <summary>低频驱动：CombatAiMissionLogic.OnMissionTick 调用，内部按 TickInterval 节流。</summary>
        /// <param name="isEnemyFormationKnown">X6 感知接线：敌编队是否已知（观察者队+敌编队，KnowledgeView 过滤，可 null=不过滤=现状）。</param>
        public void Tick(Mission mission, float dt, float now, Vec2 playerCenter, Func<Team, IntentType> getIntent,
            Func<Team, Formation, bool> isEnemyFormationKnown = null)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;

            foreach (Team team in mission.Teams)
            {
                if (team == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)) continue; // P0-④ 玩家队豁免（MCM 可开关 t66）：不对玩家编队下站定令
                IntentType intent = getIntent != null ? getIntent(team) : IntentType.Attack;
                Vec2 tc = TeamCenter(team);
                if (VecMath.Dist(playerCenter.x, playerCenter.y, tc.x, tc.y) > DriveRadius)
                {
                    ReleaseOutsideRadius(team); // P0-⑤ 剧场外释放剧集状态与 Stop 命令（幂等）
                    continue;
                }

                float d;
                bool hasCav = TryNearestEnemyCavDist(team, tc, mission, isEnemyFormationKnown, out d);
                float prev = -1f;
                _prevCavDist.TryGetValue(team.TeamIndex, out prev);
                _prevCavDist[team.TeamIndex] = hasCav ? d : -1f;

                bool ep = IsBracing(team.TeamIndex);
                bool intentBrace = intent == IntentType.BraceFormation;
                // t71：意图驱动不再绕过距离守卫——统一走 BracePlan.ShouldBrace（≤55m 且净接近才武装；
                //       首拍仅记录基线不武装），敌骑尚远时即使意图选 Brace 也不站定。
                bool arm = (intentBrace || hasCav) && (ep ? !BracePlan.ShouldRelease(d) : BracePlan.ShouldBrace(d, prev));
                if (arm)
                {
                    _episode[team.TeamIndex] = true; // 武装或维持剧集（意图/威胁驱动，均经距离守卫）
                    if (!ep) Narrator.Notify(CombatAiMissionLogic.NarrateInfo(team,
                        Mission.Current != null ? Mission.Current.PlayerTeam : null),
                        "架枪", // N1 叙事统一出口（编队级边沿，激活一次性）
                        intentBrace
                            ? "{S}步兵架枪迎敌（奉命）"
                            // narrate-fix P0：模板含 {S} 占位符不得经 string.Format（{S} 非合法格式项→FormatException
                            // 崩溃）；动态值用拼接，{S} 保留给 Narrator.ComposeText 代填。文案逐字保持。
                            : "{S}步兵架枪迎敌（敌骑距" + d.ToString("F0") + "米）",
                        InGameFeedback.FormationColor, InGameFeedback.NotifyTactical);
                    Execute(team, mission);
                }
                else
                {
                    _episode[team.TeamIndex] = false; // 释放，交回正常 AI
                    if (ep) Narrator.Notify(CombatAiMissionLogic.NarrateInfo(team,
                        Mission.Current != null ? Mission.Current.PlayerTeam : null),
                        "架枪", // N1 叙事统一出口（编队级边沿，释放一次性）
                        "{S}解除架枪，恢复推进", InGameFeedback.FormationColor, InGameFeedback.NotifyTactical);
                    Release(team);
                }
            }
        }

        // 步兵编队站定：占比达标 → Stop（架枪优先；F7 个体撤退不受编队命令影响）
        private void Execute(Team team, Mission mission)
        {
            _infantry.Clear();
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                if (!f.QuerySystem.IsInfantryFormation) continue;
                _infantry.Add(f);
            }
            if (_infantry.Count == 0) return;

            foreach (Formation inf in _infantry)
            {
                bool armed;
                if (!_armed.TryGetValue(inf, out armed))
                {
                    // 无长杆降级（多数兵零扫描：仅剧集武装沿做一次全阵统计）
                    int capable = inf.GetCountOfUnitsWithCondition(a => a != null && a.CanPerformBraceCached);
                    armed = BracePlan.HasEnoughBraceUnits(capable, inf.CountOfUnits);
                    _armed[inf] = armed;
                }
                if (!BracePlan.ShouldIssueBraceOrder(true, armed, false)) continue; // 降级：交回正常防御 AI
                CommandProvenance.Instance.Record(inf.Index, CommandSource.Brace, CommandKind.Hold); // ATC-0 记录
                inf.SetMovementOrder(MovementOrder.MovementOrderStop); // 站定（引擎对静止持枪兵自动架枪）
            }
        }

        // 剧集结束：清空本队编队评估缓存
        private void Release(Team team)
        {
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f != null) _armed.Remove(f);
            }
        }

        // P0-⑤ 剧场外命令泄漏修复：剧集激活（Stop 已下发）后玩家走出 70m，Tick 不再驱动本队——
        // 若不清理，_episode/_armed 永不释放、Stop 永久残留（原版 AI 未必覆盖）。出半径 → 复位剧集
        // + 距离基线 + 只给本剧集站定过的步兵编队（_armed 命中）下发 Advance 恢复命令，交回原版。
        // 幂等：未激活直接返回，重复调用无害（不刷命令）。
        private void ReleaseOutsideRadius(Team team)
        {
            if (!IsBracing(team.TeamIndex)) return;
            _episode[team.TeamIndex] = false;
            _prevCavDist[team.TeamIndex] = -1f; // 距离基线复位（回场后按首次观测重新武装）
            InGameFeedback.NotifyTactical("架枪", // 游戏内反馈：剧场外释放边沿（上方 IsBracing 保证一次性）
                string.Format("{0}解除架枪（超出战场）", FeedbackText.Faction(team)), InGameFeedback.FormationColor);
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                if (_armed.ContainsKey(f))
                {
                    CommandProvenance.Instance.Record(f.Index, CommandSource.Brace, CommandKind.Advance); // ATC-0 记录
                    f.SetMovementOrder(MovementOrder.MovementOrderAdvance); // 交回原版默认推进（MovementOrder.cs L59，v1.4.8 核实）
                }
            }
            Release(team); // 清 _armed 评估缓存（幂等）
        }

        // 最近敌骑距离：遍历敌编队（O(敌编队数)，只读 CachedMedianPosition），仅骑兵/骑射编队。
        // X6 感知接线：isEnemyFormationKnown 非空时，未知敌骑编队（KnowledgeView 未见/超期）不计入——
        // 保守降级=无敌骑威胁（不架枪站定，交原版），避免对看不见的骑兵过度反应；null=不过滤=现状等价。
        private static bool TryNearestEnemyCavDist(Team own, Vec2 ownCenter, Mission mission,
            Func<Team, Formation, bool> isEnemyFormationKnown, out float d)
        {
            d = -1f;
            bool found = false;
            foreach (Team team in mission.Teams)
            {
                if (team == null || team == own || (CombatBrainSettings.PlayerTeamExemptionEnabled && team.IsPlayerTeam)) continue; // P0-④ 玩家队不纳入敌骑扫描（MCM 可开关 t66：豁免关闭=玩家队正常纳入敌队聚合）
                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    if (!f.QuerySystem.IsCavalryFormation && !f.QuerySystem.IsRangedCavalryFormation) continue;
                    if (isEnemyFormationKnown != null && !isEnemyFormationKnown(own, f)) continue; // X6：未知敌骑不算威胁
                    Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // P1-④ 统一入口
                    float dist = VecMath.Dist(p.x, p.y, ownCenter.x, ownCenter.y);
                    if (!found || dist < d)
                    {
                        d = dist;
                        found = true;
                    }
                }
            }
            return found;
        }

        private static Vec2 TeamCenter(Team team)
        {
            // P1-④ 口径收敛：几何聚合进 TeamGeometry（CachedMedian 口径单一实现，原私有重复版删除）
            Vec2 c;
            return TeamGeometry.TryGetTeamCenter(team, TeamPosKind.CachedMedian, out c) ? c : Vec2.Zero;
        }
    }
}
