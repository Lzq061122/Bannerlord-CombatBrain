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
        private sealed class RouteFake : IRouteAWeights, IRouteASilenceProbe
        {
            public readonly float[] Weights = { 0.25f, 0.5f, 0.75f, 1.25f };
            public readonly object[] Behaviors = { new object(), new object(), new object(), new object() };
            public int Writes;
            public bool Destroyed;
            // CB-REPAIR-0005 只读探针读值（默认：胜出者不是 Route A 行为、AI 对象同一且可控）。
            public int ProbeActiveIndex;
            public string ProbeActiveType = "NativeBehavior";
            public bool ProbeAiSame = true;
            public bool ProbeAiCtl = true;
            public int ProbeUnits = 200;
            public bool ProbeThrows;
            public int ProbeCalls;
            public object Identity { get { return this; } }
            public object Behavior(int i) { return Behaviors[i]; }
            public float GetWeight(int i) { return Weights[i]; }
            public void SetWeight(int i, float w)
            {
                if (Destroyed) throw new InvalidOperationException("destroyed engine object");
                Writes++; Weights[i] = w;
            }
            public int ActiveBehaviorIndex()
            {
                ProbeCalls++;
                if (ProbeThrows) throw new InvalidOperationException("probe read failed");
                return ProbeActiveIndex;
            }
            public string ActiveBehaviorType() { return ProbeActiveType; }
            public bool AiObjectSame() { return ProbeAiSame; }
            public bool AiControlled() { return ProbeAiCtl; }
            public int Units() { return ProbeUnits; }
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

            RunTest(TestRouteAEvidence);
            RunTest(TestRouteAFlapAndMode);
            RunTest(TestRouteADeliveryDeadline);
            RunTest(TestRouteARepairRound2);
            RunTest(TestRouteAReArmBounded);
            RunTest(TestRouteASilenceProbe);
            RunTest(TestRouteASilenceFailureBoundary);
            RunTest(TestReleaseDiagnosticBoundary);
        }

        // CB-REPAIR-0004 r2（review findings）：①交付暂停只能由「真实释放」终结——候选扇描失败拍的空释放
        //（ApplyRouteASingle 无候选 → Release(EmptyOrInvalid)，Target 已 null）此前会解锁暂停、占位以周期形式回归；
        // ②具名 HasExternal 取代 FirstExternalAt == 0f 哨兵（首拍 now == 0f 的首次外部写会被吞掉起点）；
        // ③edgeRetries 语义 = 授权次数而非落地次数。
        private static void TestRouteARepairRound2()
        {
            RouteAControl c = RouteController(); RouteFake f = new RouteFake(); RouteAEvidence e = c.Evidence;
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); int id = c.IdentityId;
            Check(c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 20f),
                "r2 routeA delivers before the overwrite");
            ObservedRouteWrite(e, f, 1200, 20f); c.Applied(20f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 1201, 5, 20.1f), true, 20.1f);
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 30f) && !c.Owns(f),
                "r2 routeA delivery timeout releases ownership");

            // ApplyRouteASingle 两拍等价序列（按生产调用顺序）：无候选拍 = Reconcile + Release(空释放)；
            // 候选拍 = ApplySingle → Update（同一 situationSignal）。生产侧 Target==null 时的空释放调用形态：
            // CombatAiMissionLogic.cs:2380 Release(Disabled)、:2400/:320 Release(EmptyOrInvalid)、
            // RouteAOrderAdapter.cs:161 Release(EmptyOrInvalid)、:185 Release(eligibility.Rejection)、
            // :200 Release(InitializationFailed) —— 全部是 no-op，r2 口径下均不得作废暂停。
            RouteAExit[] noOpForms = { RouteAExit.EmptyOrInvalid, RouteAExit.Disabled,
                RouteAExit.InitializationFailed, RouteAExit.EmptyOrInvalid };
            bool accepted = false, owned = false; int writes = 0, acquisitions = 0;
            for (float t = 30.5f; t <= 120f; t += .5f)
            {
                c.Release(noOpForms[(int)(t * 2f) % noOpForms.Length], t);
                var tickStatus = new FormationCommandExecutor.FormationStatus { Valid = true, FieldBattle = true,
                    LeaseOk = true, RouteAMovementOwned = c.Owns(f) };
                var tickWinner = new FormationCommandProposal { Domain = CommandDomain.Movement, Kind = CommandKind.Hold };
                FormationCommandExecutor.ExecuteWinner(tickWinner, tickStatus, default(FormationCommandExecutor.LastDomainWrite),
                    5f, .2f, p => writes++, null, false);
                accepted |= Arm(c, f, Cps2RouteAIntent.Hold, t);
                if (c.Owns(f) && !owned) acquisitions++;
                owned = c.Owns(f);
            }
            Check(!accepted && acquisitions == 0 && writes > 100 && e.BuildSummary(id).Contains("lastSelectedAt=20.000s"),
                "r2 production no-op release forms cannot unlock the suspension (executor keeps writing Movement)");
            // 验收序列①（单次可读版）：SelectionTimeout → 一次 Release(EmptyOrInvalid)（Target==null 的 no-op）
            // → 以相同 situationSignal 调 Update → 仍 suspended 且 !Owns。
            c.Release(RouteAExit.EmptyOrInvalid, 120.5f);
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 121f) && !c.Owns(f),
                "r2 no-op release then same-signal Update still refuses ownership");
            // 验收序列②尾段（r3 口径）：signal 变化不解锁；只有具名离散事件（意图真实变化 + 冷却 + 次数上限）
            // 才授予，且恰好一次。
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 121.5f, 9) && !c.Owns(f),
                "r3 raw situation revision change alone never re-acquires ownership");
            bool reacquired = Arm(c, f, Cps2RouteAIntent.Advance, 131f, 9);
            if (c.Owns(f) && !owned) acquisitions++;
            owned = c.Owns(f);
            bool steady = Arm(c, f, Cps2RouteAIntent.Advance, 131.5f, 9);
            if (c.Owns(f) && !owned) acquisitions++;
            owned = c.Owns(f);
            Check(reacquired && steady && acquisitions == 1 && owned && !f.Original,
                "r3 qualifying discrete intent change re-acquires exactly once and then stays steady");

            // 生产单例（ApplyRouteASingle 实际驱动的对象）复演同一序列：候选扇描失败拍的空释放不得解锁暂停。
            // 说明：ApplyRouteASingle 本体在 CombatBrain/Ai/CombatAiMissionLogic.cs（本轮 out of scope）且为 private、
            // 需要活 Mission/Teams，离线不可直接调用；此处用同一生产单例 + 其公开入口复演它的两拍调用序列。
            CbRouteAPoC.Control.BeginSession(new object(), 0f);
            CbRouteAPoC.Control.Evidence.Capture = true;
            var pf = new RouteFake();
            Check(CbRouteAPoC.Control.Update(pf, RouteEligible(), Cps2RouteAIntent.Hold, true, 0f, 0f, 7, 1, 0, 0f)
                && CbRouteAPoC.IsMovementOwnedByRouteA(pf), "r2 production singleton owns after arming");
            ObservedRouteWrite(CbRouteAPoC.Control.Evidence, pf, 1500, 1f);
            CbRouteAPoC.Control.Applied(1f, true);
            RouteAEvidence pe = CbRouteAPoC.Control.Evidence;
            pe.SetterCompleted(pe.SetterBeginning(pf, CommandDomain.Movement, CommandSource.ExternalUnknown, 1501, 5, 2f), true, 2f);
            Check(!CbRouteAPoC.Control.Update(pf, RouteEligible(), Cps2RouteAIntent.Hold, true, 0f, 0f, 7, 1, 0, 20f)
                && !CbRouteAPoC.IsMovementOwnedByRouteA(pf),
                "r2 production singleton releases at the delivery deadline");
            CbRouteAPoC.Control.Release(RouteAExit.EmptyOrInvalid, 21f); // ApplyRouteASingle 无候选拍的调用
            Check(!CbRouteAPoC.Control.Update(pf, RouteEligible(), Cps2RouteAIntent.Hold, true, 0f, 0f, 7, 1, 0, 22f)
                && !CbRouteAPoC.IsMovementOwnedByRouteA(pf),
                "r2 production singleton stays suspended after a scan-failure release");

            // 具名 HasExternal：now == 0f 的首次外部写入必须作为起点，而不是被 0f 哨兵当成「尚无外部写」。
            c = RouteController(); f = new RouteFake(); e = c.Evidence;
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); id = c.IdentityId;
            ObservedRouteWrite(e, f, 1300, 0f); c.Applied(0f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 1301, 5, 0f), true, 0f);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 1302, 5, 5f), true, 5f);
            Check(e.ReplacedFor(id) == 1 && e.BuildSummary(id).Contains("externalDuration=5.000s"),
                "r2 first external write at now==0 is the origin, not mistaken for none (named HasExternal)");
            c.Release(RouteAExit.Disabled, 6f);
            Check(e.BuildSummary(id).Contains("externalDuration=5.000s"),
                "r2 exit settlement keeps the now==0 origin");

            // 首拍（now == 0f）的选中回调必须如实落账，-Infinity 初值只表示「本 session 从未被选中」。
            c = RouteController(); f = new RouteFake(); e = c.Evidence;
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); id = c.IdentityId;
            Check(c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 0f)
                && e.BuildSummary(id).Contains("lastSelectedAt=0.000s"),
                "r2 selection at now==0 is a real timestamp, not 'never'");

            // edgeRetries = 授权次数（非落地次数）：重试获授权后落单失败仍是 1，applied 不增。
            c = RouteController(); f = new RouteFake(); e = c.Evidence;
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); id = c.IdentityId;
            ObservedRouteWrite(e, f, 1400, 10f); c.Applied(10f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 1401, 5, 10.5f), true, 10.5f);
            Check(c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 11f),
                "r2 edge retry authorization is granted on the next selected callback");
            c.Release(RouteAExit.AdapterFailed, 11.1f); // 落单失败：Adapter 侧释放，不产生 OrderApplied
            Check(e.BuildSummary(id).Contains("edgeRetries=1") && e.AppliedFor(id) == 1,
                "r2 edgeRetries counts authorizations, not landings");
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
                "routeA does not re-acquire ownership while no qualifying event occurs");
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 900f) && !c.Owns(f),
                "routeA delivery suspension persists across an unchanged situation (no periodic placeholder)");
            // r3：原始 signal 变化不是合格事件；具名离散事件（意图真实变化）+ 冷却才授予一次尝试。
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 900.5f, 6) && !c.Owns(f),
                "r3 raw situation revision change alone never re-arms ownership");
            Check(!Arm(c, f, Cps2RouteAIntent.Advance, 35f, 6) && !c.Owns(f),
                "r3 intent change inside the re-arm cooldown is not granted");
            Check(Arm(c, f, Cps2RouteAIntent.Advance, 45.5f, 6) && c.Owns(f) && !f.Original,
                "r3 intent change after the cooldown re-arms ownership for one delivery attempt");
            Check(Arm(c, f, Cps2RouteAIntent.Advance, 46f, 6) && c.Owns(f),
                "routeA re-acquired ownership opens a fresh delivery window");
            Check(!Arm(c, f, Cps2RouteAIntent.Advance, 56.5f, 6) && !c.Owns(f) && f.Original,
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

        // CB-REPAIR-0004 r3（Astra FAIL 复现，P1 周期重占位）：交付超时后的重新武装必须是具名、离散、
        // 可解释且有界的条件。旧逻辑把 situationSignal 任意变化当作战术变化 → 60Hz、每拍 X+=0.001、
        // 零 BehaviorSelected 时 10.000s 释放 / 10.0167s 重取，占空比 ≈99.83%，普通执行器只剩单帧窗口。
        // 本方法按派单 §4 六项组合断言锁定：连续变号不重武装 / 执行器持续可用 / 三类微变分别不解锁 /
        // 合格离散事件恰好一次且有可观测原因 / 二次失败有界 / 健康回归（选中刷新窗口 + 预算重置）。
        private static void TestRouteAReArmBounded()
        {
            // §4.1–§4.3：位置微变、兵数变化、态势向量变化在 RouteAControl 入口都表现为 situationSignal
            // 连续变化（RouteASituationStamp 精确比较 X/Y、Count、12 维向量与 Army 浮点）——三类分别验证。
            string[] changeKinds = { "position micro change", "unit count change", "situation vector change" };
            for (int k = 0; k < changeKinds.Length; k++)
            {
                RouteAControl c = RouteController(); RouteFake f = new RouteFake(); RouteAEvidence e = c.Evidence;
                Arm(c, f, Cps2RouteAIntent.Hold, 0f); int id = c.IdentityId;
                Check(!Arm(c, f, Cps2RouteAIntent.Hold, 10f) && !c.Owns(f),
                    "r3 " + changeKinds[k] + ": delivery timeout yields Movement ownership");
                bool everOwned = false, everAccepted = false; int writes = 0, ticks = 0, signal = 100 + k * 1000;
                for (float t = 10.5f; t <= 130f; t += .5f) // 240 拍 ≈ 4 个交付期限，每拍都变号
                {
                    signal++;
                    everAccepted |= Arm(c, f, Cps2RouteAIntent.Hold, t, signal);
                    everOwned |= c.Owns(f);
                    var tickStatus = new FormationCommandExecutor.FormationStatus { Valid = true, FieldBattle = true,
                        LeaseOk = true, RouteAMovementOwned = c.Owns(f) };
                    var tickWinner = new FormationCommandProposal { Domain = CommandDomain.Movement, Kind = CommandKind.Hold };
                    if (FormationCommandExecutor.ExecuteWinner(tickWinner, tickStatus,
                        default(FormationCommandExecutor.LastDomainWrite), 5f, .2f, p => writes++, null, false)) { }
                    ticks++;
                }
                Check(!everOwned && !everAccepted,
                    "r3 " + changeKinds[k] + ": never re-arms during continuous situation changes");
                Check(writes == ticks && writes > 200,
                    "r3 " + changeKinds[k] + ": every tick's Movement proposal reaches the executor (sustained window)");
                Check(e.BuildSummary(id).Contains("suspensionRetries=0"),
                    "r3 " + changeKinds[k] + ": no suspension retry granted without a qualifying event");
            }

            // §4.4 合格离散事件：意图真实变化（Hold→Advance）+ 冷却 → 恰好一次重取，且证据可观测。
            RouteAControl c4 = RouteController(); RouteFake f4 = new RouteFake(); RouteAEvidence e4 = c4.Evidence;
            Arm(c4, f4, Cps2RouteAIntent.Hold, 0f); int id4 = c4.IdentityId;
            Check(!Arm(c4, f4, Cps2RouteAIntent.Hold, 10f) && !c4.Owns(f4),
                "r3 qualifying-event case: delivery timeout yields ownership");
            Check(!Arm(c4, f4, Cps2RouteAIntent.Advance, 12f, 40) && !c4.Owns(f4),
                "r3 qualifying event inside the cooldown is not granted (bounded rate)");
            Check(Arm(c4, f4, Cps2RouteAIntent.Advance, 20.5f, 41) && c4.Owns(f4),
                "r3 qualifying discrete event after the cooldown grants exactly one new attempt");
            long suspensionRetries = 0; RouteAEvent item4;
            while (e4.TryRead(out item4)) { if (item4.Kind == RouteAEventKind.SuspensionRetry) suspensionRetries++; }
            Check(suspensionRetries == 1 && e4.BuildSummary(id4).Contains("suspensionRetries=1"),
                "r3 granted re-arm is observable (SuspensionRetry event + summary counter)");

            // §4.5 二次失败有界：仍零选中 → 再次超时释放；此后意图反复抖动 + 连续变号也不自动周期重取。
            Check(!Arm(c4, f4, Cps2RouteAIntent.Advance, 31f, 42) && !c4.Owns(f4) && f4.Original,
                "r3 second undelivered attempt is released again by the same deadline");
            Cps2RouteAIntent[] oscillate = { Cps2RouteAIntent.Hold, Cps2RouteAIntent.Advance, Cps2RouteAIntent.Retreat };
            bool owned5 = false, everOwnedTail = false; int acquisitions5 = 0, writesTail = 0, ticksTail = 0, signal5 = 500;
            for (float t = 41.5f; t <= 200f; t += .5f)
            {
                bool tail = t >= 80f; // 预算耗尽后的「持续让位」阶段：每个提案都必须到达执行器
                signal5++;
                if (Arm(c4, f4, oscillate[(int)t % oscillate.Length], t, signal5)) { }
                bool owns5 = c4.Owns(f4);
                if (owns5 && !owned5) acquisitions5++;
                owned5 = owns5;
                if (tail)
                {
                    everOwnedTail |= owns5;
                    var tickStatusTail = new FormationCommandExecutor.FormationStatus { Valid = true, FieldBattle = true,
                        LeaseOk = true, RouteAMovementOwned = c4.Owns(f4) };
                    var tickWinnerTail = new FormationCommandProposal { Domain = CommandDomain.Movement, Kind = CommandKind.Hold };
                    if (FormationCommandExecutor.ExecuteWinner(tickWinnerTail, tickStatusTail,
                        default(FormationCommandExecutor.LastDomainWrite), 5f, .2f, p => writesTail++, null, false)) { }
                    ticksTail++;
                }
            }
            Check(acquisitions5 == 1 && !everOwnedTail && !c4.Owns(f4) && writesTail == ticksTail && ticksTail > 200,
                "r3 after the attempt budget is spent the control keeps yielding (executor writes every tick)");
            Check(e4.BuildSummary(id4).Contains("suspensionRetries=2"),
                "r3 total granted re-arms are bounded by MaxSuspendedReattempts (observable: suspensionRetries=2)");

            // §4.6 健康回归（增量）：重武装后的真实选中回调刷新交付窗口并重置暂停期预算；
            // 之后的超时仍可由新的离散事件再次授权（预算不是一次性耗尽）。
            RouteAControl c6 = RouteController(); RouteFake f6 = new RouteFake(); RouteAEvidence e6 = c6.Evidence;
            Arm(c6, f6, Cps2RouteAIntent.Hold, 0f); int id6 = c6.IdentityId;
            Check(!Arm(c6, f6, Cps2RouteAIntent.Hold, 10f) && !c6.Owns(f6),
                "r3 healthy case: first timeout yields ownership");
            Check(Arm(c6, f6, Cps2RouteAIntent.Advance, 20.5f, 9) && c6.Owns(f6),
                "r3 healthy case: qualifying event re-arms once");
            Check(c6.TrySelect(f6, f6.Behaviors[1], f6.Behaviors[1], RouteEligible(), true, 21f),
                "r3 healthy case: selected callback after re-arm is accepted (Advance behavior index)");
            ObservedRouteWrite(e6, f6, 2000, 21f); c6.Applied(21f, true);
            Check(e6.BuildSummary(id6).Contains("applied=1"),
                "r3 healthy case: delivered order still lands through the adapter path");
            Check(!Arm(c6, f6, Cps2RouteAIntent.Advance, 31.5f, 9) && !c6.Owns(f6),
                "r3 healthy case: later timeout yields again after a healthy delivery");
            Check(Arm(c6, f6, Cps2RouteAIntent.Hold, 42f, 10) && c6.Owns(f6),
                "r3 healthy delivery reset the suspension budget so a new discrete event grants again");
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

        // CB-REPAIR-0005（只读诊断）：覆盖后「引擎是否仍选中 Route A 行为」的探针。
        // 断言三件事：① 只在真实覆盖后产生、节拍 ≥1s 且受交付窗口上限约束；② 字段口径正确（两个时间锚点分开）；
        // ③ 零语义差——探针读取抛异常时所有权/权重/超时释放与无探针路径完全一致。
        private static void TestRouteASilenceProbe()
        {
            RouteAControl c = RouteController(); RouteFake f = new RouteFake(); RouteAEvidence e = c.Evidence;
            var samples = new List<RouteASilenceSample>();
            e.SilenceObserved = s => samples.Add(s);
            Arm(c, f, Cps2RouteAIntent.Hold, 0f); int id = c.IdentityId;
            Check(c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 20f),
                "silence probe: RouteA delivers before the overwrite");
            ObservedRouteWrite(e, f, 900, 20f); c.Applied(20f, true);
            Check(samples.Count == 0, "silence probe stays silent while no external overwrite happened");
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 901, 5, 20.1f), true, 20.1f);
            Check(e.ReplacedFor(id) == 1 && samples.Count == 0,
                "silence probe does not sample on the overwrite tick itself (interval honouring)");
            Arm(c, f, Cps2RouteAIntent.Hold, 20.9f);
            Check(samples.Count == 0, "silence probe respects the one-second sampling interval");
            Arm(c, f, Cps2RouteAIntent.Hold, 21.1f);
            Check(samples.Count == 1, "silence probe emits one row per second after the overwrite");
            RouteASilenceSample s0 = samples[0];
            Check(s0.Final == false && s0.ProbeSeq == 1 && s0.Identity == id && s0.Session == e.Session,
                "silence probe row carries session/identity and is not marked final");
            Check(Math.Abs((s0.Time - s0.ReplacedAt) - 1.0f) < .001f
                && Math.Abs((s0.Time - s0.LastSelectedAt) - 1.1f) < .001f,
                "silence probe reports sinceReplaced and silentFor from separate anchors");
            Check(s0.ActiveIndex == 0 && s0.ActiveType == "NativeBehavior" && s0.AiObjectSame
                && s0.AiControlled && s0.Units == 200,
                "silence probe reports the foreign winner and eligibility facts verbatim");
            Check(Math.Abs(s0.Weight0 - 10f) < .001f && Math.Abs(s0.Weight1) < .001f
                && Math.Abs(s0.Weight2) < .001f && Math.Abs(s0.Weight3) < .001f,
                "silence probe reports the RouteA-side weights without touching them");
            Arm(c, f, Cps2RouteAIntent.Hold, 22.1f); Arm(c, f, Cps2RouteAIntent.Hold, 22.9f);
            Check(samples.Count == 2 && Math.Abs((samples[1].Time - samples[1].ReplacedAt) - 2.0f) < .001f,
                "silence probe keeps a one-per-second cadence without sampling every tick");
            for (float t = 23.1f; t <= 29.6f; t += 1f) Arm(c, f, Cps2RouteAIntent.Hold, t);
            Check(samples.Count == 9, "silence probe emits exactly one row per second inside the ten-second window");
            f.ProbeActiveIndex = 1; f.ProbeActiveType = "CbHoldBehavior";
            Check(!Arm(c, f, Cps2RouteAIntent.Hold, 30.1f) && !c.Owns(f) && f.Original,
                "silence probe scenario still releases ownership at the delivery deadline");
            Check(samples.Count == 10 && samples[9].Final && samples[9].ProbeSeq == 10
                && samples[9].ActiveIndex == 1 && samples[9].ActiveType == "CbHoldBehavior",
                "silence probe takes one final snapshot right before the delivery-deadline release");
            Check(e.BuildSummary(id).Contains("replaced=1") && e.BuildSummary(id).Contains("edgeRetries=0")
                && e.BuildSummary(id).Contains("lastSelectedAt=20.000s"),
                "silence probe leaves the r3 summary semantics untouched");

            // 引擎恢复选中 → armed 被消费 → 探针必须停止。
            RouteAControl c2 = RouteController(); RouteFake f2 = new RouteFake();
            var s2 = new List<RouteASilenceSample>();
            c2.Evidence.SilenceObserved = s => s2.Add(s);
            Arm(c2, f2, Cps2RouteAIntent.Hold, 0f);
            Check(c2.TrySelect(f2, f2.Behaviors[0], f2.Behaviors[0], RouteEligible(), true, 10f),
                "silence probe positive path delivers before the overwrite");
            ObservedRouteWrite(c2.Evidence, f2, 950, 10f); c2.Applied(10f, true);
            c2.Evidence.SetterCompleted(c2.Evidence.SetterBeginning(f2, CommandDomain.Movement,
                CommandSource.ExternalUnknown, 951, 5, 10.2f), true, 10.2f);
            Arm(c2, f2, Cps2RouteAIntent.Hold, 11.2f);
            Check(s2.Count == 1, "silence probe samples once inside the armed window");
            Check(c2.TrySelect(f2, f2.Behaviors[0], f2.Behaviors[0], RouteEligible(), true, 11.5f) && s2.Count == 1,
                "silence probe: a real reselection consumes the armed retry (EdgeRetry path)");
            Arm(c2, f2, Cps2RouteAIntent.Hold, 13.5f); Arm(c2, f2, Cps2RouteAIntent.Hold, 20f);
            Check(s2.Count == 1, "silence probe stops once the engine reselects the RouteA behavior");

            // 零语义差：探针读取抛异常时不得改变控制流（同 TestRouteADeliveryDeadline 的窗口与超时释放）。
            RouteAControl c3 = RouteController(); RouteFake f3 = new RouteFake();
            int rows = 0;
            c3.Evidence.SilenceObserved = s => rows++;
            Arm(c3, f3, Cps2RouteAIntent.Hold, 0f);
            Check(c3.TrySelect(f3, f3.Behaviors[0], f3.Behaviors[0], RouteEligible(), true, 20f),
                "silence probe failure scenario delivers before the overwrite");
            ObservedRouteWrite(c3.Evidence, f3, 960, 20f); c3.Applied(20f, true);
            c3.Evidence.SetterCompleted(c3.Evidence.SetterBeginning(f3, CommandDomain.Movement,
                CommandSource.ExternalUnknown, 961, 5, 20.1f), true, 20.1f);
            f3.ProbeThrows = true;
            int writesBefore = f3.Writes;
            bool heldThrowing = true;
            for (float t = 20.6f; t <= 29.5f; t += .5f) heldThrowing &= c3.Owns(f3) && Arm(c3, f3, Cps2RouteAIntent.Hold, t);
            Check(heldThrowing && f3.ProbeCalls > 0, "silence probe read failure keeps ownership inside the window");
            Check(f3.Writes == writesBefore, "silence probe read failures cause zero weight writes inside the window");
            Check(!Arm(c3, f3, Cps2RouteAIntent.Hold, 30.1f) && !c3.Owns(f3) && f3.Original,
                "silence probe read failure does not affect the delivery-deadline release");
            Check(rows == 0, "silence probe read failures are swallowed silently (zero rows)");
        }

        // CB-REPAIR-0005 r1（Astra P1）：诊断输出边界不可信——读取端与输出端异常都不得改变控制流。
        // 场景 = 覆盖后 10s 交付窗口 + 超时释放 + 暂停；三份对照（基线 / 读取端抛 / 输出端抛）逐字段比较。
        private sealed class SilenceBoundaryResult
        {
            public bool Threw, HeldInsideWindow, ReleasedAtDeadline, WeightsRestored, ImmediateReacquire;
            public bool ExitWasSelectionTimeout, SawInitializationFailed, ProbeReadCalls;
            public int ExitCount, Events, WeightWritesInsideWindow;
        }

        private static SilenceBoundaryResult RunSilenceBoundary(Action<RouteFake> configureFake,
            Action<RouteAEvidence> wireOutlet)
        {
            var r = new SilenceBoundaryResult();
            RouteAControl c = RouteController(); RouteFake f = new RouteFake(); RouteAEvidence e = c.Evidence;
            if (configureFake != null) configureFake(f);
            if (wireOutlet != null) wireOutlet(e);
            Arm(c, f, Cps2RouteAIntent.Hold, 0f);
            c.TrySelect(f, f.Behaviors[0], f.Behaviors[0], RouteEligible(), true, 20f);
            ObservedRouteWrite(e, f, 1200, 20f); c.Applied(20f, true);
            e.SetterCompleted(e.SetterBeginning(f, CommandDomain.Movement, CommandSource.ExternalUnknown, 1201, 5, 20.1f), true, 20.1f);
            int writesBefore = f.Writes;
            bool held = true;
            try
            {
                for (float t = 20.6f; t <= 29.5f; t += .5f) held &= c.Owns(f) && Arm(c, f, Cps2RouteAIntent.Hold, t);
            }
            catch (Exception) { r.Threw = true; held = false; }
            r.HeldInsideWindow = held;
            r.WeightWritesInsideWindow = f.Writes - writesBefore;
            bool acquiredAtDeadline;
            try { acquiredAtDeadline = Arm(c, f, Cps2RouteAIntent.Hold, 30.1f); }
            catch (Exception) { r.Threw = true; acquiredAtDeadline = true; }
            r.ReleasedAtDeadline = !acquiredAtDeadline && !c.Owns(f);
            r.WeightsRestored = f.Original;
            try { r.ImmediateReacquire = Arm(c, f, Cps2RouteAIntent.Hold, 30.6f); }
            catch (Exception) { r.Threw = true; r.ImmediateReacquire = true; }
            r.ProbeReadCalls = f.ProbeCalls > 0;
            RouteAEvent item;
            while (e.TryRead(out item))
            {
                r.Events++;
                if (item.Kind != RouteAEventKind.Exit) continue;
                r.ExitCount++;
                if (item.Reason == RouteAExit.SelectionTimeout) r.ExitWasSelectionTimeout = true;
                if (item.Reason == RouteAExit.InitializationFailed) r.SawInitializationFailed = true;
            }
            return r;
        }

        private static bool SameBoundary(SilenceBoundaryResult a, SilenceBoundaryResult b)
        {
            return a.HeldInsideWindow == b.HeldInsideWindow
                && a.ReleasedAtDeadline == b.ReleasedAtDeadline
                && a.WeightsRestored == b.WeightsRestored
                && a.ImmediateReacquire == b.ImmediateReacquire
                && a.ExitWasSelectionTimeout == b.ExitWasSelectionTimeout
                && a.SawInitializationFailed == b.SawInitializationFailed
                && a.ExitCount == b.ExitCount
                && a.Events == b.Events
                && a.WeightWritesInsideWindow == b.WeightWritesInsideWindow;
        }

        private static void TestRouteASilenceFailureBoundary()
        {
            SilenceBoundaryResult baseline = RunSilenceBoundary(null, null);
            Check(!baseline.Threw && baseline.HeldInsideWindow && baseline.ReleasedAtDeadline
                && baseline.WeightsRestored && !baseline.ImmediateReacquire
                && baseline.ExitWasSelectionTimeout && !baseline.SawInitializationFailed
                && baseline.WeightWritesInsideWindow == 0 && baseline.ProbeReadCalls,
                "silence boundary baseline: unplugged outlet keeps the plain delivery-deadline semantics");

            SilenceBoundaryResult readFault = RunSilenceBoundary(f => f.ProbeThrows = true, null);
            SilenceBoundaryResult outletFault = RunSilenceBoundary(null,
                e => e.SilenceObserved = s => { throw new InvalidOperationException("diagnostic outlet failed"); });

            Check(!readFault.Threw && SameBoundary(baseline, readFault),
                "silence boundary: probe read failures never escape the diagnostic boundary");
            Check(!outletFault.Threw && SameBoundary(baseline, outletFault),
                "silence boundary: outlet (log callback) failures never escape the diagnostic boundary");
            Check(readFault.WeightWritesInsideWindow == 0 && outletFault.WeightWritesInsideWindow == 0,
                "silence boundary: failing diagnostics cause zero weight writes inside the window");
            Check(outletFault.ExitWasSelectionTimeout && !outletFault.SawInitializationFailed
                && !outletFault.ImmediateReacquire && outletFault.WeightsRestored,
                "silence boundary: final-tick outlet failure still releases as SelectionTimeout and stays suspended");
            Check(outletFault.Events == baseline.Events && outletFault.ExitCount == baseline.ExitCount,
                "silence boundary: repeated outlet failures add no event storm and consume no extra retry budget");
            Check(baseline.Events == readFault.Events && baseline.Events == outletFault.Events,
                "silence boundary: diagnostic faults leave the recorded event stream identical to baseline");
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
