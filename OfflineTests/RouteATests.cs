namespace OfflineTests
{
    using System;
    using System.Collections.Generic;
    using CombatBrain.Ai;
    using CombatBrain.Battle;
    using CombatBrain.Coordination;
    using CombatBrain.Settings;

    internal static partial class Program
    {
        private sealed class RouteFake : IRouteAWeights
        {
            public readonly float[] Weights = { 0.25f, 0.5f, 0.75f, 1.25f };
            public readonly object[] Behaviors = { new object(), new object(), new object(), new object() };
            public int Writes;
            public bool Destroyed;
            public object Identity { get { return this; } }
            public object Behavior(int i) { return Behaviors[i]; }
            public float GetWeight(int i) { return Weights[i]; }
            public void SetWeight(int i, float w)
            {
                if (Destroyed) throw new InvalidOperationException("destroyed engine object");
                Writes++; Weights[i] = w;
            }
            public override int GetHashCode() { return 42; } // 故意碰撞，不能充当对象身份。
            public bool Original { get { return Weights[0] == .25f && Weights[1] == .5f
                && Weights[2] == .75f && Weights[3] == 1.25f; } }
        }
        private static RouteAEligibility RouteEligible()
        {
            return new RouteAEligibility { Enabled = true, FieldBattle = true, Present = true,
                Alive = true, AiControlled = true, Infantry = true };
        }
        private static RouteAControl RouteController()
        {
            var c = new RouteAControl(); c.BeginSession(new object(), 0f); c.Evidence.Capture = true; return c;
        }
        private static bool Arm(RouteAControl c, RouteFake f, Cps2RouteAIntent intent = Cps2RouteAIntent.Hold,
            float now = 0f, int signal = 5)
        { return c.Update(f, RouteEligible(), intent, true, 0f, 0f, signal, 1, 0, now); }
        private static RouteASetterToken ObservedRouteWrite(RouteAEvidence e, object f, long sequence, float now)
        {
            e.ExpectSetter(f);
            RouteASetterToken t = e.SetterBeginning(f, CommandDomain.Movement, CommandSource.CombatBrainRouteA, sequence, 7, now);
            e.SetterCompleted(t, true, now);
            return t;
        }

        private static void TestRouteAFinalRepair()
        {
            var c = RouteController(); var f = new RouteFake(); var e = RouteEligible();
            Check(Arm(c, f), "routeA live registration owns one formation");
            int writes = 0;
            object competitor = new object();
            if (c.TrySelect(f, f.Behaviors[0], competitor, e, true, .1f)) writes++;
            Check(writes == 0, "routeA losing candidate cannot reach write sink");
            Arm(c, f);
            if (c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], e, true, .5f))
            {
                ObservedRouteWrite(c.Evidence, f, 1, .5f); writes++; c.Applied(.5f, true);
            }
            Check(writes == 1, "routeA selected callback reaches write sink once");
            for (int i = 0; i < 20; i++)
                if (c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], e, true, 1f + i)) writes++;
            Check(writes == 1, "routeA stable selected ticks do not spam equivalent orders");
            c.Activated(f.Behaviors[0]);
            Check(c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], e, true, 22f), "routeA real reactivation may execute again");
            int weightWrites = f.Writes;
            for (int i = 0; i < 1000; i++) Arm(c, f);
            Check(f.Writes == weightWrites, "routeA 1000 steady updates do not rewrite weights");

            // 调生产 ExecuteWinner，检查返回值及实际 sink；不能只验证一个空注册表。
            var winner = new FormationCommandProposal { Domain = CommandDomain.Movement, Kind = CommandKind.Hold };
            var status = new FormationCommandExecutor.FormationStatus { Valid = true, FieldBattle = true,
                LeaseOk = true, RouteAMovementOwned = c.Owns(f) };
            var skips = new List<FormationCommandExecutor.WriteSkipReason>();
            int ordinary = 0;
            bool result = FormationCommandExecutor.ExecuteWinner(winner, status, default(FormationCommandExecutor.LastDomainWrite),
                5f, .2f, p => ordinary++, skips, false);
            Check(!result && ordinary == 0 && skips.Contains(FormationCommandExecutor.WriteSkipReason.RouteAMovementOwned),
                "routeA owned Movement: executor returns skipped and never invokes sink");
            foreach (CommandDomain domain in new[] { CommandDomain.Facing, CommandDomain.Arrangement })
            {
                winner.Domain = domain;
                Check(FormationCommandExecutor.ExecuteWinner(winner, status, default(FormationCommandExecutor.LastDomainWrite),
                    5f, .2f, p => ordinary++, null, false), "routeA leaves executor " + domain + " available");
            }
            c.Release(RouteAExit.Disabled, 23f); status.RouteAMovementOwned = c.Owns(f); winner.Domain = CommandDomain.Movement;
            Check(FormationCommandExecutor.ExecuteWinner(winner, status, default(FormationCommandExecutor.LastDomainWrite),
                5f, .2f, p => ordinary++, null, false), "routeA exit releases ordinary Movement sink");

            // 每条恢复边都先真实改写四权重，再触发生产 Reconcile/Release 并检查保存值和重复释放。
            for (int fault = 0; fault < 7; fault++)
            {
                c = RouteController(); f = new RouteFake(); Arm(c, f);
                e = RouteEligible();
                switch (fault)
                {
                    case 0: e.Enabled = false; break;
                    case 1: e.PlayerOwned = true; break;
                    case 2: e.Alive = false; break;
                    case 3: e.Present = false; break;
                    case 4: e.AiControlled = false; break;
                    case 5: e.FieldBattle = false; break;
                    case 6: e.Infantry = false; break;
                }
                c.Reconcile(e, 1f);
                Check(!c.Owns(f) && f.Original, "routeA lifecycle restores captured weights: " + e.Rejection);
                weightWrites = f.Writes; c.Reconcile(e, 2f); c.Release(RouteAExit.Disabled, 2f);
                Check(f.Writes == weightWrites, "routeA lifecycle repeated release is inert: " + e.Rejection);
            }
            c = RouteController(); f = new RouteFake(); Arm(c, f);
            e = RouteEligible(); e.PlayerOwned = true;
            Check(!c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], e, true, .01f) && f.Original && !c.Owns(f),
                "routeA player acquired between Mission tick and callback is protected immediately");
            c = RouteController(); f = new RouteFake(); Arm(c, f); e = RouteEligible(); e.ForcedTick = true;
            Check(!c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], e, true, .01f) && !c.Owns(f),
                "routeA forced engine tick releases before potential SetControlledByAI follow-up write");

            c = RouteController(); f = new RouteFake(); var replacement = new RouteFake(); Arm(c, f);
            int firstIdentity = c.IdentityId;
            Check(Arm(c, replacement) && f.Original && !c.Owns(f) && c.Owns(replacement)
                && c.IdentityId != firstIdentity, "routeA same Index and colliding hash replacement restores old object first");
            Check(!c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 2f), "routeA stale callback cannot command replacement");
            c.BeginSession(new object(), 3f);
            Check(replacement.Original && c.Target == null && c.Evidence.Identities == 0 && c.Revision == 0,
                "routeA Mission change restores old weights and clears session identities");
            c = RouteController(); f = new RouteFake(); Arm(c, f); f.Destroyed = true;
            c.Release(RouteAExit.MissionEnding, 1f);
            Check(c.Target == null && c.RestoreFailures == 4, "routeA destroyed target cannot prevent registry cleanup");

            c = RouteController(); f = new RouteFake(); Arm(c, f, Cps2RouteAIntent.Rejoin);
            Check(!c.Update(f, RouteEligible(), Cps2RouteAIntent.Rejoin, false, 20f, 30f, 5, 1, 0, 1f)
                && f.Original && !c.Owns(f), "routeA failed fresh path validation revokes previous Rejoin and restores weights");
            foreach (Cps2RouteAIntent intent in new[] { Cps2RouteAIntent.Hold, Cps2RouteAIntent.Advance,
                Cps2RouteAIntent.Rejoin, Cps2RouteAIntent.Retreat })
            {
                c = RouteController(); f = new RouteFake(); Arm(c, f, intent);
                bool onlyOne = true;
                for (int i = 0; i < 4; i++) onlyOne &= f.Weights[i] == (i == (int)intent - 1 ? 10f : 0f);
                Check(onlyOne, "routeA exactly one weighted behavior for " + intent);
            }
            Check(Cps2RouteAPolicy.MapFormationToIntent(false, (int)FormationMission.SupportMainEffort, BattlePhase.Engage)
                == Cps2RouteAIntent.Advance, "routeA real Engage phase is not treated as Deployment/Rejoin");

            TestRouteAEvidence();
            TestRouteAFlapAndMode();
            TestRouteADeliveryDeadline();
        }

        // CB-REPAIR-0004：运行期 run=b0e2e0a4 session 4 证明「覆盖后 armed 的单次边沿重试永不执行」——
        // 00:15:39.418 ExternalReplaced → 00:17:20.681 Exit(LostAIControl) 之间 101s（764 任务秒）
        // BehaviorSelected 0 次、edgeRetries 0 次，而所有权一直被持有（externalWhileActive=1780）。
        // 本测试锁定修复口径：交付上限内必须释放所有权，且不得以周期占位的方式重新取得。
        private static void TestRouteADeliveryDeadline()
        {
            RouteAControl c = RouteController(); RouteFake f = new RouteFake(); RouteAEvidence e = c.Evidence;
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); int id = c.IdentityId;
            Check(c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 20f),
                "routeA selected callback before the override delivers the first order");
            ObservedRouteWrite(e, f, 700, 20f); c.Applied(20f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 701, 5, 20.1f), true, 20.1f);
            Check(e.ReplacedFor(id) == 1 && c.Owns(f) && e.BuildSummary(id).Contains("edgeRetries=0"),
                "routeA armed retry is undelivered after the overwrite (runtime 101s / 0 BehaviorSelected replay)");
            bool held = true;
            for (float t = 20.6f; t <= 29.5f; t += .5f) held &= c.Owns(f) && Arm(c, f, Cps2RouteAIntent.Hold, t);
            Check(held, "routeA keeps ownership inside the delivery deadline without any selected callback");
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 30f) && !c.Owns(f) && f.Original,
                "routeA releases ownership at the delivery deadline instead of holding a placeholder");
            Check(e.BuildSummary(id).Contains("edgeRetries=0") && e.BuildSummary(id).Contains("replaced=1")
                && e.BuildSummary(id).Contains("lastSelectedAt=20.000s"),
                "routeA placeholder evidence keeps replacement/retry counts and the last selected time");
            var winner = new FormationCommandProposal { Domain = CommandDomain.Movement, Kind = CommandKind.Hold };
            var status = new FormationCommandExecutor.FormationStatus { Valid = true, FieldBattle = true,
                LeaseOk = true, RouteAMovementOwned = c.Owns(f) };
            int resumed = 0;
            Check(FormationCommandExecutor.ExecuteWinner(winner, status, default(FormationCommandExecutor.LastDomainWrite),
                5f, .2f, p => resumed++, null, false) && resumed == 1,
                "routeA placeholder release unblocks the ordinary Movement executor (no 占位不开火)");
            RouteAExit exitReason = RouteAExit.None; RouteAEvent item;
            while (e.TryRead(out item)) { if (item.Kind == RouteAEventKind.Exit) exitReason = item.Reason; }
            Check(exitReason == RouteAExit.SelectionTimeout,
                "routeA placeholder release is observable as SelectionTimeout exit reason");

            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 31f) && !c.Owns(f) && f.Original,
                "routeA does not re-acquire ownership while the situation revision is unchanged");
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 900f) && !c.Owns(f),
                "routeA delivery suspension persists across an unchanged situation (no periodic placeholder)");
            Check(Arm(c, f, Cps2RouteAIntent.Hold, 900.5f, 6) && c.Owns(f) && !f.Original,
                "routeA new situation revision re-arms ownership for one more delivery attempt");
            Check(Arm(c, f, Cps2RouteAIntent.Hold, 901f, 6) && c.Owns(f),
                "routeA re-acquired ownership opens a fresh delivery window");
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 910.5f, 6) && !c.Owns(f) && f.Original,
                "routeA second undelivered attempt is bounded by the same deadline");

            c = RouteController(); f = new RouteFake(); e = c.Evidence;
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); id = c.IdentityId;
            bool owned = true;
            for (float t = .5f; t <= 9.5f; t += .5f) owned &= c.Owns(f) && Arm(c, f, Cps2RouteAIntent.Hold, t);
            Check(owned, "routeA never-selected arming owns inside the delivery window");
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 10f) && !c.Owns(f) && f.Original
                && e.BuildSummary(id).Contains("lastSelectedAt=never"),
                "routeA never-selected arming is bounded by the same deadline (占位判定量: lastSelectedAt=never)");

            // 健康路径回归：覆盖后仍在节拍内收到选中回调 → 重试落地、所有权保持，交付上限不误触发。
            c = RouteController(); f = new RouteFake(); e = c.Evidence;
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); id = c.IdentityId;
            bool retried = false;
            for (float t = .5f; t <= 40f; t += .5f)
            {
                if (Arm(c, f, Cps2RouteAIntent.Hold, t)
                    && c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, t))
                {
                    ObservedRouteWrite(e, f, 900 + (long)(t * 2f), t); c.Applied(t, true);
                    if (t > 1f) retried = true;
                }
                if (Math.Abs(t - 20f) < .01f)
                    e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 1500, 5, 20.05f), true, 20.05f);
            }
            Check(retried && c.Owns(f) && e.BuildSummary(id).Contains("edgeRetries=1"),
                "routeA steady selected cadence lands the edge retry and keeps ownership (no false timeout)");
        }

        private static void TestRouteAEvidence()
        {
            RouteAControl c = RouteController(); RouteFake f = new RouteFake(); Arm(c, f);
            RouteAEvidence e = c.Evidence; int id = c.IdentityId;
            ObservedRouteWrite(e, f, 10, 1f); c.Applied(1f, true);
            e.SetterCompleted(e.SetterBeginning(new RouteFake(), CommandDomain.Movement, CommandSource.ExternalUnknown, 11, 9, 1.1f), true, 1.1f);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Facing, CommandSource.ExternalUnknown, 12, 9, 1.2f), true, 1.2f);
            Check(e.StableFor(id) == 1, "routeA other identity and Facing setters do not consume Movement Stable");
            RouteASetterToken skipped = e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 13, 9, 1.3f);
            e.SetterCompleted(skipped, false, 1.3f);
            Check(e.ReplacedFor(id) == 0, "routeA skipped setter is not an external replacement");
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 14, 9, 8f), true, 8f);
            Check(e.ReplacedFor(id) == 1 && e.StableFor(id) == 0, "routeA actual external write replaces outstanding Applied even beyond old one-second window");
            Check(c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 8.1f),
                "routeA first external overwrite invalidates dedupe for one edge retry");
            ObservedRouteWrite(e, f, 15, 8.1f); c.Applied(8.1f, true);
            Check(!c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 8.2f),
                "routeA edge retry is not resent on steady selected ticks");
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 16, 9, 9f), true, 9f);
            Check(!c.Owns(f) && f.Original, "routeA continued external overwrite releases ownership and restores weights immediately");
            var winner = new FormationCommandProposal { Domain = CommandDomain.Movement, Kind = CommandKind.Hold };
            var status = new FormationCommandExecutor.FormationStatus { Valid = true, FieldBattle = true,
                LeaseOk = true, RouteAMovementOwned = c.Owns(f) };
            int resumed = 0;
            Check(FormationCommandExecutor.ExecuteWinner(winner, status, default(FormationCommandExecutor.LastDomainWrite),
                5f, .2f, p => resumed++, null, false) && resumed == 1,
                "routeA persistent overwrite release restores ordinary executor immediately");
            string conflictSummary = e.BuildSummary(id);
            Check(conflictSummary.Contains("externalWhileActive=0") && conflictSummary.Contains("edgeRetries=1")
                && conflictSummary.Contains("externalDuration=1.000s"),
                "routeA summary reports active overwrite count, edge retries, and overwrite duration with unit");
            // After release, further engine writes are restoration activity, never high-frequency Route A retries.
            for (int i = 0; i < 200; i++)
                e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 17 + i, 9, 10f + i), true, 10f + i);
            Check(e.ReplacedFor(id) == 2, "routeA repeated external writes after release do not create retries or replacements");
            ObservedRouteWrite(e, f, 217, 220f); c.Applied(220f, true);
            c.Release(RouteAExit.Disabled, 221f);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 216, 9, 222f), true, 222f);
            Check(e.AppliedFor(id) == 2 && e.ReplacedFor(id) == 2 && e.StableFor(id) == 0,
                "routeA external setter after Exit is restoration, not a new replacement");

            long exit = 0, afterExit = 0, replaced = 0, prevSeq = 0; bool strict = true; RouteAEvent item;
            while (e.TryRead(out item))
            {
                strict &= item.Seq > prevSeq; prevSeq = item.Seq;
                if (item.Kind == RouteAEventKind.Exit) exit = item.Seq;
                if (item.Kind == RouteAEventKind.ExternalAfterExit) afterExit = item.Seq;
                if (item.Kind == RouteAEventKind.ExternalReplaced) replaced = item.Seq;
            }
            Check(strict && replaced < exit && exit < afterExit, "routeA recorded event order distinguishes active overwrite from post-exit restoration");
            RouteASetterToken late = e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 300, 9, 223f);
            c.BeginSession(new object(), 224f); e.SetterCompleted(late, true, 225f);
            Check(e.AppliedFor(id) == 0 && e.ReplacedFor(id) == 0 && e.Count == 0, "routeA prior-session completion cannot contaminate new session");

            // F-2 语义：重试消费前的 replaced=false 写入不算持续覆盖——不释放、不重试、仅计数。
            c = RouteController(); f = new RouteFake(); Arm(c, f); e = c.Evidence; id = c.IdentityId;
            ObservedRouteWrite(e, f, 400, 300f); c.Applied(300f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 401, 5, 301f), true, 301f);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 402, 5, 301.5f), true, 301.5f);
            Check(c.Owns(f) && !f.Original && e.BuildSummary(id).Contains("externalWhileActive=1")
                && e.BuildSummary(id).Contains("edgeRetries=0") && e.BuildSummary(id).Contains("externalDuration=0.500s"),
                "routeA replaced=false writes while active neither release nor retry (no 0.5s churn)");
            // 重试消费后再次 replaced=true → 立即 Release；此后连续外部写不再产生替换/重试。
            c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 301.6f);
            ObservedRouteWrite(e, f, 403, 301.6f); c.Applied(301.6f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 404, 5, 302f), true, 302f);
            Check(!c.Owns(f) && f.Original && e.BuildSummary(id).Contains("edgeRetries=1"),
                "routeA second replaced=true after retry fires immediate release");
            for (int i = 0; i < 200; i++)
                e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 405 + i, 5, 302f + i), true, 302f + i);
            Check(e.ReplacedFor(id) == 2 && e.BuildSummary(id).Contains("edgeRetries=1"),
                "routeA post-release external writes create no extra replacements or retries");

            // F-1 口径：Exit 落账 ExternalDuration = LastExternalAt − FirstExternalAt，与 Active 期一致。
            c = RouteController(); f = new RouteFake(); Arm(c, f); e = c.Evidence; id = c.IdentityId;
            ObservedRouteWrite(e, f, 500, 100f); c.Applied(100f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 501, 5, 110f), true, 110f);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 502, 5, 112f), true, 112f);
            c.Release(RouteAExit.Disabled, 120f);
            Check(!c.Owns(f) && e.BuildSummary(id).Contains("externalDuration=2.000s"),
                "routeA exit duration settles at last external write (Last-First), not exit time");

            // F-2 语义：独立 replaced=false 写入不 Release、不消耗重试、仅计数；重试槽仍可用。
            c = RouteController(); f = new RouteFake(); Arm(c, f); e = c.Evidence; id = c.IdentityId;
            ObservedRouteWrite(e, f, 600, 200f); c.Applied(200f, false);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 601, 5, 201f), true, 201f);
            Check(c.Owns(f) && !f.Original && e.BuildSummary(id).Contains("externalWhileActive=1")
                && e.BuildSummary(id).Contains("edgeRetries=0"),
                "routeA standalone replaced=false write does not release or consume retry");
            ObservedRouteWrite(e, f, 602, 201.5f); c.Applied(201.5f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 603, 5, 202f), true, 202f);
            Check(c.Owns(f) && c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 202.1f),
                "routeA retry slot survives standalone replaced=false writes and fires once");
            ObservedRouteWrite(e, f, 604, 202.1f); c.Applied(202.1f, true);
            Check(!c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 202.2f),
                "routeA no steady-tick resend after retry fired");

            c = RouteController(); f = new RouteFake(); Arm(c, f); e = c.Evidence;
            e.ExpectSetter(f);
            RouteASetterToken direct = e.SetterBeginning(f, CommandDomain.Movement, CommandSource.CombatBrainRouteA, 1, 7, 1f);
            RouteASetterToken nested = e.SetterBeginning(f, CommandDomain.Movement, CommandSource.CombatBrainRouteA, 2, 9, 1f);
            Check(direct.Direct && !nested.Direct && nested.Source == CommandSource.ExternalUnknown,
                "routeA nested setter cannot borrow the Adapter's direct-source token");
            c.Applied(1f, false);
            Check(e.AppliedFor(c.IdentityId) == 0 && e.StableFor(c.IdentityId) == 0, "routeA unobserved writes are excluded from verified Stable");

            e.CancelExpectedSetter();
            RouteASetterToken beforeReset = e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 900, 9, 2f);
            RouteASetterToken afterReset = e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 0, 9, 3f);
            Check(afterReset.Seq > beforeReset.Seq && afterReset.ObserverSeq == 0,
                "routeA strict setter sequence survives legacy observer counter reset");
            for (int i = 0; i <= RouteAEvidence.EventCapacity; i++) e.Selected(c.IdentityId, 1, 4f);
            Check(e.Count == RouteAEvidence.EventCapacity && e.Dropped > 0,
                "routeA bounded event overflow explicitly invalidates complete evidence");
            long dropped = e.Dropped; e.Capture = false; e.Capture = true;
            Check(e.Dropped > dropped, "routeA diagnostic interruption remains visible after re-enabling capture");
        }

        private static void TestRouteAFlapAndMode()
        {
            var c = RouteController(); var f = new RouteFake();
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); Arm(c, f, Cps2RouteAIntent.Advance, .5f); Arm(c, f, Cps2RouteAIntent.Hold, 1f);
            Check(c.Flaps == 1, "routeA unchanged situation Hold-Advance-Hold within two seconds is a flap");
            c = RouteController(); f = new RouteFake();
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); Arm(c, f, Cps2RouteAIntent.Advance, .5f, 6); Arm(c, f, Cps2RouteAIntent.Hold, 1f, 6);
            Check(c.Flaps == 0, "routeA actual situation transition is not counted as no-change oscillation");
            c = RouteController(); f = new RouteFake();
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); Arm(c, f, Cps2RouteAIntent.Advance, .5f);
            Arm(c, f, Cps2RouteAIntent.Advance, .7f, 6); Arm(c, f, Cps2RouteAIntent.Hold, 1f);
            Check(c.Flaps == 0, "routeA intervening situation change breaks no-change chain even with same intent");
            c = RouteController(); f = new RouteFake();
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); Arm(c, f, Cps2RouteAIntent.Advance, .5f); Arm(c, f, Cps2RouteAIntent.Hold, 3f);
            Check(c.Flaps == 0, "routeA return outside two-second transition window is not a flap");
            var stamp = new RouteASituationStamp(); var snapshot = new RouteASituation { Count = 100 };
            int revision = stamp.Update(f, snapshot);
            Check(stamp.Update(f, snapshot) == revision, "routeA identical situation values retain revision without allocation");
            snapshot.Count--;
            Check(stamp.Update(f, snapshot) != revision, "routeA one casualty changes situation revision, not coarse 20-unit bins");
            revision = stamp.Update(f, snapshot); snapshot.Vector.Moral = .01f;
            Check(stamp.Update(f, snapshot) != revision, "routeA real morale-vector change breaks no-change chain");
            Cps3Mode oldMode = Cps3RuntimeSettings.Mode; bool oldFlag = Cps2PoCSettings.RouteAEnabled;
            bool oldEnabled = Cps3RuntimeSettings.Enabled; bool oldObserver = Cps3RuntimeSettings.ObserverEnabled;
            Cps3RuntimeSettings.Enabled = true;
            Cps3RuntimeSettings.ObserverEnabled = true;
            Cps3RuntimeSettings.Mode = Cps3Mode.A_NativeBaseline;
            Cps3RuntimeSettings.Mode = Cps3Mode.E_RouteA;
            Check(Cps2PoCSettings.RouteAEnabled && Cps3RuntimeSettings.RouteAExecutionEnabled(), "routeA entering E automatically arms dedicated switch");
            // G-1 硬约束：E 模式无观测 → Route A 不执行（fail-safe；覆盖恢复链依赖 SetOrderObserver）。
            Cps3RuntimeSettings.ObserverEnabled = false;
            Check(Cps2PoCSettings.RouteAEnabled && !Cps3RuntimeSettings.RouteAExecutionEnabled(),
                "routeA armed in E without observer never executes (fail-safe)");
            Cps3RuntimeSettings.ObserverEnabled = true;
            Check(Cps2PoCSettings.RouteAEnabled && Cps3RuntimeSettings.RouteAExecutionEnabled(),
                "routeA observer restored re-enables execution");
            Cps2PoCSettings.RouteAEnabled = false; Cps3RuntimeSettings.Mode = Cps3Mode.E_RouteA;
            Check(!Cps2PoCSettings.RouteAEnabled && !Cps3RuntimeSettings.RouteAExecutionEnabled(), "routeA unchanged E selection cannot undo hot close");
            Cps2PoCSettings.RouteAEnabled = true; Cps3RuntimeSettings.Mode = Cps3Mode.C_ArbiterCoexist;
            Check(!Cps3RuntimeSettings.RouteAExecutionEnabled(), "routeA leaving E overrides a still-enabled dedicated switch");
            Cps3RuntimeSettings.Mode = oldMode; Cps2PoCSettings.RouteAEnabled = oldFlag;
            Cps3RuntimeSettings.Enabled = oldEnabled; Cps3RuntimeSettings.ObserverEnabled = oldObserver;
        }
    }
}
