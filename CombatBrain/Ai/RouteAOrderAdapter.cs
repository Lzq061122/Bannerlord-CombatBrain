namespace CombatBrain.Ai
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using CombatBrain.Battle;
    using CombatBrain.Coordination;
    using CombatBrain.Settings;
    using TaleWorlds.Core;
    using TaleWorlds.Engine;
    using TaleWorlds.Library;
    using TaleWorlds.MountAndBlade;

    // cps3r-routea-final-blocker-repair §三批准的适配器。普通执行器仍有三域出口，本文件只有一个 Movement 出口。
    public static class RouteAOrderAdapter
    {
        public static bool TryApply(BehaviorComponent behavior)
        {
            RouteABehavior cb = behavior as RouteABehavior;
            if (cb == null) return false;
            RouteAHandle h = cb.Handle;
            float now = CbRouteAPoC.Now;
            try
            {
                RouteAEligibility eligibility = CbRouteAPoC.Eligibility(h.Formation, Mission.Current);
                bool valid = CbRouteAPoC.CommandValid(h, behavior.CurrentOrder);
                if (!CbRouteAPoC.Control.TrySelect(h.Formation, behavior, h.Formation.AI.ActiveBehavior,
                    eligibility, valid, now)) return false;
                RouteAEvidence evidence = CbRouteAPoC.Control.Evidence;
                evidence.ExpectSetter(h.Formation);
                try
                {
                    using (CommandSourceScope.Enter(CommandSource.CombatBrainRouteA))
                    {
                        h.Formation.SetMovementOrder(behavior.CurrentOrder);
                    }
                    // 已完成方法返回才标 Applied；观测关闭/原方法被其它 Prefix 跳过时不计入可信 Stable。
                    MovementOrder landed = h.Formation.GetReadonlyMovementOrderReference();
                    bool orderMatches = landed.OrderEnum == behavior.CurrentOrder.OrderEnum;
                    if (orderMatches && h.Intent == Cps2RouteAIntent.Rejoin)
                    {
                        Vec2 landedPoint = landed.GetPosition(h.Formation);
                        orderMatches = landedPoint.x == h.Target.AsVec2.x && landedPoint.y == h.Target.AsVec2.y;
                    }
                    // MovementOrder == 在 v1.4.8 只比较 OrderEnum，不能用于验证 Move 的坐标。
                    if ((SetOrderObserver.Enabled && !evidence.DirectCompleted) || !orderMatches)
                    {
                        CbRouteAPoC.Control.Release(RouteAExit.AdapterFailed, now);
                        return false;
                    }
                    Vec2 target = h.Intent == Cps2RouteAIntent.Rejoin ? h.Target.AsVec2 : Vec2.Zero;
                    CbRouteAPoC.Control.Applied(now, SetOrderObserver.Enabled && evidence.DirectCompleted, target.x, target.y);
                    behavior.IsCurrentOrderChanged = false;
                    return true;
                }
                finally { evidence.CancelExpectedSetter(); }
            }
            catch (Exception)
            {
                CbRouteAPoC.Control.Release(RouteAExit.AdapterFailed, now);
                return false;
            }
        }
    }

    internal sealed class RouteAHandle : IRouteAWeights
    {
        public readonly Formation Formation;
        public readonly FormationAI AI;
        public readonly RouteABehavior[] Behaviors = new RouteABehavior[4];
        public Cps2RouteAIntent Intent;
        public WorldPosition Target;
        public float MainBodyX, MainBodyY;
        public bool MainBodyValid;
        public IList<FormationCoordSnapshot> EnemySnaps;
        public float ContextAt;
        public RouteAHandle(Formation f) { Formation = f; AI = f.AI; }
        public object Identity { get { return Formation; } }
        public object Behavior(int index) { return Behaviors[index]; }
        public float GetWeight(int index) { return Behaviors[index].WeightFactor; }
        public void SetWeight(int index, float weight)
        {
            Behaviors[index].WeightFactor = weight;
            if (weight == 0f) Behaviors[index].IsCurrentOrderChanged = false;
        }
    }

    public static class CbRouteAPoC
    {
        public const float BaseBehaviorWeight = 1f;
        public const float RouteAStabilityFlapWindowSeconds = 2f;
        public static readonly RouteAControl Control = new RouteAControl();
        private static Mission _mission;
        private static bool _ended;
        private static readonly string RunId = Guid.NewGuid().ToString("N");
        public static float Now { get { return Mission.Current != null ? Mission.Current.CurrentTime : SetOrderObserver.LastTick; } }
        public static bool Enabled { get { return Cps3RuntimeSettings.RouteAExecutionEnabled(); } }
        public static float EffectiveWeight(float ai, float factor) { return ai * factor; }
        public static bool HasArmed() { return Control.Target != null; }
        public static bool IsMovementOwnedByRouteA(object formation) { return Control.Owns(formation); }

        public static void BeginSession(Mission mission)
        {
            if (ReferenceEquals(_mission, mission)) return;
            ResetAll();
            _mission = mission;
            _ended = false;
            Control.BeginSession(mission, Now);
            SetOrderObserver.RouteAEvidenceSink = Control.Evidence;
        }
        public static void ResetAll()
        {
            // Mission.Current 可能已经指向下一场；退出事件保留旧 Mission 的时间基准。
            Control.EndSession(_mission != null ? _mission.CurrentTime : Now);
            FlushEvidence(); // Exit/Restore 必须先落盘；下一 Mission 才清计数。
            _ended = true;
            _mission = null;
        }

        public static RouteAEligibility Eligibility(Formation f, Mission mission)
        {
            var e = new RouteAEligibility { Enabled = Enabled && !_ended };
            if (mission == null || !ReferenceEquals(mission, _mission)) return e;
            e.FieldBattle = !GameNetwork.IsMultiplayer && Cps2RouteAPolicy.IsEligibleFieldBattle(
                mission.IsFieldBattle, mission.IsSiegeBattle, mission.IsSallyOutBattle);
            if (mission.MissionIsEnding || mission.IsMissionEnding) e.Enabled = false;
            e.ForcedTick = mission.ForceTickOccasionally;
            if (f == null) return e;
            try
            {
                // 不依赖候选筛选。旧对象即使被从 Teams/Formations 移除也会被判为不在场。
                foreach (Team team in mission.Teams)
                {
                    if (team == null || !team.MBTeam.IsValid || !ReferenceEquals(team, f.Team)) continue;
                    foreach (Formation current in team.FormationsIncludingEmpty)
                        if (ReferenceEquals(current, f)) { e.Present = true; break; }
                    break;
                }
                e.Alive = f.CountOfUnits > 0 && f.FormationIndex >= 0 && f.QuerySystem != null;
                e.AiControlled = f.IsAIControlled;
                e.PlayerOwned = f.Team == null || f.Team.IsPlayerTeam || f.PlayerOwner != null
                    || f.HasPlayerControlledTroop || f.IsPlayerTroopInFormation;
                e.Infantry = f.QuerySystem != null && f.QuerySystem.IsInfantryFormation;
            }
            catch (Exception) { e.Present = false; }
            return e;
        }

        public static void Reconcile(Mission mission)
        {
            Control.Evidence.Capture = Cps3RuntimeSettings.ObserverEnabled;
            RouteAHandle h = Control.Target as RouteAHandle;
            if (h != null)
            {
                try
                {
                    RouteAEligibility e = Eligibility(h.Formation, mission);
                    if (!ReferenceEquals(h.AI, h.Formation.AI)) e.Present = false;
                    Control.Reconcile(e, Now);
                }
                catch (Exception) { Control.Release(RouteAExit.EmptyOrInvalid, Now); }
            }
        }

        private static RouteAHandle EnsureBehaviors(Formation f)
        {
            CbHoldBehavior existing = f.AI.GetBehavior<CbHoldBehavior>();
            if (existing != null && ReferenceEquals(existing.Handle.AI, f.AI)) return existing.Handle;
            var h = new RouteAHandle(f);
            // 仅首次挂载分配，后续通过 FormationAI 的公开 GetBehavior 复用；不保留全局对象哈希缓存。
            h.Behaviors[0] = new CbHoldBehavior(f, h);
            h.Behaviors[1] = new CbAdvanceBehavior(f, h);
            h.Behaviors[2] = new CbRejoinBehavior(f, h);
            h.Behaviors[3] = new CbRetreatBehavior(f, h);
            for (int i = 0; i < 4; i++) f.AI.AddAiBehavior(h.Behaviors[i]);
            return h;
        }

        public static bool ApplySingle(Formation f, FormationMission missionIntent, BattlePhase phase,
            ArmyContextSnapshot army, IList<FormationCoordSnapshot> enemySnaps, int situationSignal)
        {
            try
            {
                RouteAEligibility e = Eligibility(f, Mission.Current);
                if (e.Rejection != RouteAExit.None) { Control.Release(e.Rejection, Now); return false; }
                Cps2RouteAIntent intent = Cps2RouteAPolicy.MapFormationToIntent(
                    missionIntent == FormationMission.Retreat, (int)missionIntent, phase);
                RouteAHandle h = EnsureBehaviors(f);
                h.Intent = intent;
                h.MainBodyX = army.MainBodyX; h.MainBodyY = army.MainBodyY;
                h.MainBodyValid = army.MainBodyStrength > 0 && army.MainBodyConfidence > 0f;
                h.EnemySnaps = enemySnaps; h.ContextAt = Now;
                bool valid = intent != Cps2RouteAIntent.None;
                if (intent == Cps2RouteAIntent.Rejoin) valid = TryRejoinTarget(h, out h.Target);
                return Control.Update(h, e, intent, valid,
                    intent == Cps2RouteAIntent.Rejoin ? h.MainBodyX : 0f,
                    intent == Cps2RouteAIntent.Rejoin ? h.MainBodyY : 0f,
                    situationSignal, f.Team.TeamIndex, f.Index, Now);
            }
            catch (Exception) { Control.Release(RouteAExit.InitializationFailed, Now); return false; }
        }

        internal static bool CommandValid(RouteAHandle h, MovementOrder order)
        {
            if (!ReferenceEquals(h.AI, h.Formation.AI) || Now - h.ContextAt > 1f) return false;
            MovementOrder.MovementOrderEnum expected;
            switch (h.Intent)
            {
                case Cps2RouteAIntent.Hold: expected = MovementOrder.MovementOrderEnum.Stop; break;
                case Cps2RouteAIntent.Advance: expected = MovementOrder.MovementOrderEnum.Advance; break;
                case Cps2RouteAIntent.Rejoin:
                    WorldPosition target;
                    if (!TryRejoinTarget(h, out target)) return false;
                    Vec2 actual = order.GetPosition(h.Formation);
                    if (actual.x != target.AsVec2.x || actual.y != target.AsVec2.y) return false;
                    expected = MovementOrder.MovementOrderEnum.Move; break;
                case Cps2RouteAIntent.Retreat: expected = MovementOrder.MovementOrderEnum.Retreat; break;
                default: return false;
            }
            return order.OrderEnum == expected && order.IsApplicable(h.Formation);
        }

        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        private static bool TryRejoinTarget(RouteAHandle h, out WorldPosition target)
        {
            target = default(WorldPosition);
            Mission mission = Mission.Current;
            if (!h.MainBodyValid || mission == null || mission.Scene == null
                || !Finite(h.MainBodyX) || !Finite(h.MainBodyY)) return false;
            Vec2 current = h.Formation.CachedMedianPosition.AsVec2;
            if (!Cps2RouteAPolicy.IsRejoinRouteValid(h.MainBodyX, h.MainBodyY, current.x, current.y,
                h.EnemySnaps, Cps2PoCSettings.RouteARejoinContactMeters)) return false;
            Vec2 point = new Vec2(h.MainBodyX, h.MainBodyY);
            if (!mission.IsPositionInsideBoundaries(point)) return false;
            WorldPosition start = h.Formation.CachedMedianPosition;
            target = start;
            target.SetVec2(point);
            if (start.GetNavMesh() == UIntPtr.Zero || target.GetNavMesh() == UIntPtr.Zero) return false;
            float distance;
            return mission.Scene.GetPathDistanceBetweenPositions(ref start, ref target, 0.5f, out distance)
                && Finite(distance) && distance >= 0f;
        }

        public static void FlushEvidence()
        {
            RouteAEvent item;
            while (Control.Evidence.TryRead(out item))
                CombatLog.Log(string.Format(CultureInfo.InvariantCulture,
                    "[RouteAEvent] run={0} session={1} seq={2} time={3:F6} identity={4} team={5} f={6} domain={7} event={8} intent={9} revision={10} setterSeq={11} relatedSeq={12} source={13} orderType={14} reason={15} signal={16} x={17:F3} y={18:F3} observerSeq={19}",
                    RunId, item.Session, item.Seq, item.Time, item.Identity, item.Team, item.Formation, item.Domain,
                    item.Kind, item.Intent, item.Revision, item.SetterSeq, item.RelatedSeq, item.Source, item.OrderType, item.Reason, item.Signal,
                    item.X, item.Y, item.ObserverSeq));
            if (!Control.Evidence.Capture) return;
            for (int i = 1; i <= Control.Evidence.Identities; i++)
                CombatLog.Log("[RouteA] run=" + RunId + " " + Control.Evidence.BuildSummary(i)
                    + " restoreFailures=" + Control.RestoreFailures);
        }
    }

    internal abstract class RouteABehavior : BehaviorComponent
    {
        internal readonly RouteAHandle Handle;
        protected RouteABehavior(Formation f, RouteAHandle handle) : base(f) { Handle = handle; }
        protected override float GetAiWeight() { return CbRouteAPoC.BaseBehaviorWeight; }
        // v1.4.8 实际 DLL：FindBestBehavior 的 PrecalculateMovementOrder 只计算候选；胜出后
        // ActiveBehavior.TickOccasionally -> return，不自动落单。四子类预计算绝不调用 setter。
        // SetControlledByAI(true) 另有 ForceTickOccasionally=true -> AI.Tick -> SetMovementOrder；
        // 此强制路径在 Adapter 的资格门释放 Route A，避免 Adapter + 引擎恢复的双写。
        public override void TickOccasionally() { RouteAOrderAdapter.TryApply(this); }
        protected override void OnBehaviorActivatedAux() { CbRouteAPoC.Control.Activated(this); }
        public override void OnLostAIControl()
        {
            if (CbRouteAPoC.Control.Owns(Formation)) CbRouteAPoC.Control.Release(RouteAExit.LostAIControl, CbRouteAPoC.Now);
        }
    }
    internal sealed class CbHoldBehavior : RouteABehavior
    {
        public CbHoldBehavior(Formation f, RouteAHandle h) : base(f, h) { }
        protected override void CalculateCurrentOrder()
        { CurrentOrder = MovementOrder.MovementOrderStop; IsCurrentOrderChanged = false; }
    }
    internal sealed class CbAdvanceBehavior : RouteABehavior
    {
        public CbAdvanceBehavior(Formation f, RouteAHandle h) : base(f, h) { }
        protected override void CalculateCurrentOrder()
        { CurrentOrder = MovementOrder.MovementOrderAdvance; IsCurrentOrderChanged = false; }
    }
    internal sealed class CbRejoinBehavior : RouteABehavior
    {
        public CbRejoinBehavior(Formation f, RouteAHandle h) : base(f, h) { }
        protected override void CalculateCurrentOrder()
        { CurrentOrder = MovementOrder.MovementOrderMove(Handle.Target); IsCurrentOrderChanged = false; }
    }
    internal sealed class CbRetreatBehavior : RouteABehavior
    {
        public CbRetreatBehavior(Formation f, RouteAHandle h) : base(f, h) { }
        protected override void CalculateCurrentOrder()
        { CurrentOrder = MovementOrder.MovementOrderRetreat; IsCurrentOrderChanged = false; }
    }
}
