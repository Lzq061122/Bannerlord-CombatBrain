namespace OfflineTests
{
    using System;
    using System.IO;
    using CombatBrain;
    using CombatBrain.Coordination;

    internal static partial class Program
    {
        private static string RemoveSourceTrivia(string source)
        {
            return System.Text.RegularExpressions.Regex.Replace(source,
                "(?s)/\\*.*?\\*/|//[^\\r\\n]*|@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"", " ");
        }

        private static void TestReleaseDiagnosticBoundary()
        {
            for (int fault = 0; fault < 4; fault++)
            {
                var c = RouteController(); var f = new RouteFake(); Arm(c, f);
                int gateCalls = 0, liveCalls = 0;
                c.ReleaseGateText = () =>
                {
                    gateCalls++;
                    if ((fault & 1) != 0) throw new InvalidOperationException("gate diagnostic failed");
                    return "auditGate=ok";
                };
                c.ReleaseLiveText = identity =>
                {
                    liveCalls++;
                    if ((fault & 2) != 0) throw new InvalidOperationException("live diagnostic failed");
                    return "auditLive=ok";
                };
                bool threw = false;
                try { c.Release(RouteAExit.Disabled, 1f); }
                catch (InvalidOperationException) { threw = true; }
                Check(!threw, "release diagnostics: callback faults do not escape, mask=" + fault);
                Check(!c.Owns(f) && f.Original && c.RestoreFailures == 0,
                    "release diagnostics: callback faults preserve cleanup, mask=" + fault);
                Check(gateCalls == 1 && liveCalls == 1,
                    "release diagnostics: each outlet runs once independently, mask=" + fault);
                RouteAEvent item; int exits = 0, restores = 0;
                while (c.Evidence.TryRead(out item))
                {
                    if (item.Kind == RouteAEventKind.Exit && item.Reason == RouteAExit.Disabled) exits++;
                    if (item.Kind == RouteAEventKind.Restore) restores++;
                }
                Check(exits == 1 && restores == 1,
                    "release diagnostics: failures retain one Exit/Restore pair, mask=" + fault);
                if (!threw)
                {
                    int weightWrites = f.Writes;
                    c.Release(RouteAExit.Disabled, 2f);
                    Check(gateCalls == 1 && liveCalls == 1 && f.Writes == weightWrites,
                        "release diagnostics: repeated release has no callbacks or writes, mask=" + fault);
                }
            }

            var quiet = RouteController(); var quietFake = new RouteFake(); Arm(quiet, quietFake);
            int quietCalls = 0;
            quiet.ReleaseGateText = () => { quietCalls++; return "unexpected"; };
            quiet.ReleaseLiveText = identity => { quietCalls++; return "unexpected"; };
            quiet.Evidence.Capture = false;
            quiet.Release(RouteAExit.Disabled, 1f);
            Check(quietCalls == 0 && !quiet.Owns(quietFake) && quietFake.Original,
                "release diagnostics: capture off skips both outlets and still restores weights");

            var session = RouteController(); var sessionFake = new RouteFake(); Arm(session, sessionFake);
            session.ReleaseGateText = () => { throw new InvalidOperationException("session diagnostic failed"); };
            session.ReleaseLiveText = identity => { throw new InvalidOperationException("session diagnostic failed"); };
            bool sessionThrew = false;
            try { session.BeginSession(new object(), 2f); }
            catch (InvalidOperationException) { sessionThrew = true; }
            Check(!sessionThrew && session.Target == null && sessionFake.Original && session.Evidence.Identities == 0,
                "release diagnostics: Mission change completes cleanup even when both callbacks fail");

            var deadline = RouteController(); var deadlineFake = new RouteFake(); Arm(deadline, deadlineFake);
            deadline.ReleaseGateText = () => { throw new InvalidOperationException("deadline diagnostic failed"); };
            deadline.ReleaseLiveText = identity => { throw new InvalidOperationException("deadline diagnostic failed"); };
            bool deadlineThrew = false, acquiredAtDeadline = true, reacquired = true;
            try
            {
                acquiredAtDeadline = Arm(deadline, deadlineFake, now: 10f);
                reacquired = Arm(deadline, deadlineFake, now: 10.5f);
            }
            catch (InvalidOperationException) { deadlineThrew = true; }
            Check(!deadlineThrew && !acquiredAtDeadline && !deadline.Owns(deadlineFake) && deadlineFake.Original,
                "release diagnostics: failing callbacks preserve delivery-timeout cleanup");
            Check(!reacquired, "release diagnostics: failing callbacks do not unlock suspended re-acquisition");
            RouteAEvent deadlineEvent; bool selectionTimeout = false;
            while (deadline.Evidence.TryRead(out deadlineEvent))
                selectionTimeout |= deadlineEvent.Kind == RouteAEventKind.Exit && deadlineEvent.Reason == RouteAExit.SelectionTimeout;
            Check(selectionTimeout, "release diagnostics: failing callbacks retain the SelectionTimeout reason");

            TestReleaseDiagnosticOutput();
            TestReleaseLiveIdentity();
            TestTrySelectOwnershipOutput();
            TestReplacementOwnershipOutput();
        }

        private static string CaptureReleaseOutput(Action action)
        {
            string previousPath = CombatLog.OverridePath;
            bool previousDisabled = CombatLog.Disabled;
            string path = Path.Combine(Path.GetTempPath(), "CombatBrain-ownership-test-" + Guid.NewGuid().ToString("N") + ".log");
            CombatLog.Flush();
            try
            {
                CombatLog.OverridePath = path; CombatLog.Disabled = false;
                action();
                CombatLog.Flush();
                return File.ReadAllText(path);
            }
            finally
            {
                CombatLog.Flush();
                CombatLog.OverridePath = previousPath; CombatLog.Disabled = previousDisabled;
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private sealed class ReleaseIdentityWeights : IRouteAWeights
        {
            private readonly object _identity = new object();
            public readonly RouteFake Weights = new RouteFake();
            public object Identity { get { return _identity; } }
            public object Behavior(int index) { return Weights.Behavior(index); }
            public float GetWeight(int index) { return Weights.GetWeight(index); }
            public void SetWeight(int index, float value) { Weights.SetWeight(index, value); }
        }

        private static void TestReleaseLiveIdentity()
        {
            var a = new ReleaseIdentityWeights(); var b = new ReleaseIdentityWeights();
            object probed = null;
            int liveCalls = 0, ownerId = 0;
            string output = CaptureReleaseOutput(() =>
            {
                var c = RouteController();
                c.Update(a, RouteEligible(), Cps2RouteAIntent.Hold, true, 0f, 0f, 5, 1, 0, 0f);
                ownerId = c.IdentityId;
                c.ReleaseLiveText = identity =>
                {
                    probed = identity; liveCalls++;
                    return ReferenceEquals(identity, a.Identity) ? "auditTarget=A" : "auditTarget=wrong";
                };
                var unrelated = RouteEligible();
                unrelated.PlayerOwned = unrelated.PlayerOwnerSet = unrelated.PlayerTeam = true;
                Check(!c.TrySelect(b.Identity, b.Behavior(0), b.Behavior(0), unrelated, true, .5f),
                    "release identity: unrelated formation selection is rejected");
                Check(c.Owns(a.Identity) && liveCalls == 0 && b.Weights.Original,
                    "release identity: unrelated callback keeps A owned and does not probe or change B");
                c.Release(RouteAExit.Disabled, 1f);
                Check(ReferenceEquals(probed, a.Identity) && liveCalls == 1,
                    "release identity: live callback receives released A after rejected B");
                Check(!c.Owns(a.Identity) && a.Weights.Original,
                    "release identity: A is still released and its weights restored");
            });
            string live = Array.Find(output.Split('\n'), line => line.Contains("where=ReleaseLive"));
            string cached = Array.Find(output.Split('\n'), line => line.Contains("where=Release:"));
            Check(live != null && live.Contains("auditTarget=A") && live.Contains("identity=" + ownerId),
                "release identity: live log is bound to A's evidence identity");
            Check(cached != null && cached.Contains("identity=" + ownerId)
                && cached.Contains("playerOwned=0") && cached.Contains("playerOwnerSet=0")
                && cached.Contains("playerTeam=0") && cached.Contains("eligibilityFresh=1"),
                "release identity: rejected B cannot replace A's cached ownership inputs");
        }

        private static void TestTrySelectOwnershipOutput()
        {
            for (int fault = 0; fault < 4; fault++)
            {
                var a = new RouteFake(); var latest = RouteEligible();
                if (fault == 0)
                    latest.PlayerOwned = latest.PlayerOwnerSet = latest.HasPlayerControlledTroop
                        = latest.IsPlayerTroopInFormation = latest.PlayerTeam = true;
                if (fault == 1) latest.AiControlled = false;
                RouteAExit reason = fault == 0 ? RouteAExit.PlayerOwned
                    : fault == 1 ? RouteAExit.LostAIControl : RouteAExit.InvalidCommand;
                int liveCalls = 0, ownerId = 0;
                object probed = null;
                string fields = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "playerOwned={0} playerOwnerSet={1} hasPlayerControlledTroop={2} isPlayerTroopInFormation={3} playerTeam={4} isAIControlled={5} reject={6}",
                    latest.PlayerOwned ? 1 : 0, latest.PlayerOwnerSet ? 1 : 0,
                    latest.HasPlayerControlledTroop ? 1 : 0, latest.IsPlayerTroopInFormation ? 1 : 0,
                    latest.PlayerTeam ? 1 : 0, latest.AiControlled ? 1 : 0, (int)latest.Rejection);
                string output = CaptureReleaseOutput(() =>
                {
                    var c = RouteController(); Arm(c, a); ownerId = c.IdentityId;
                    c.ReleaseLiveText = identity =>
                    {
                        probed = identity; liveCalls++;
                        return ReferenceEquals(identity, a) ? fields : "auditTarget=wrong";
                    };
                    object active = fault == 3 ? new object() : a.Behaviors[0];
                    Check(!c.TrySelect(a, a.Behaviors[0], active, latest, fault != 2, 1.5f)
                        && !c.Owns(a) && a.Original,
                        "release ownership: TrySelect rejection restores A, case=" + fault);
                    Check(ReferenceEquals(probed, a.Identity) && liveCalls == 1,
                        "release ownership: TrySelect probes released A exactly once, case=" + fault);
                    RouteAEvent item; int exits = 0, restores = 0;
                    while (c.Evidence.TryRead(out item))
                    {
                        if (item.Kind == RouteAEventKind.Exit && item.Reason == reason && item.Identity == ownerId) exits++;
                        if (item.Kind == RouteAEventKind.Restore && item.Reason == reason && item.Identity == ownerId) restores++;
                    }
                    Check(exits == 1 && restores == 1,
                        "release ownership: Exit/Restore retain reason and identity, case=" + fault);
                });
                string live = Array.Find(output.Split('\n'), line => line.Contains("where=ReleaseLive"));
                string cached = Array.Find(output.Split('\n'), line => line.Contains("where=Release:"));
                Check(cached != null && cached.Contains(fields) && cached.Contains("eligibilityFresh=1")
                    && cached.Contains("identity=" + ownerId) && cached.Contains("missionTime=1.500"),
                    "release ownership: fresh cache contains this TrySelect's inputs, case=" + fault);
                Check(live != null && live.Contains(fields) && live.Contains("identity=" + ownerId)
                    && live.Contains("reason=" + (int)reason) && live.Contains("missionTime=1.500"),
                    "release ownership: live and cached logs describe the same release, case=" + fault);
            }
        }

        private static void TestReplacementOwnershipOutput()
        {
            var a = new RouteFake(); var b = new RouteFake();
            object probed = null;
            int ownerId = 0;
            string output = CaptureReleaseOutput(() =>
            {
                var c = RouteController(); Arm(c, a); ownerId = c.IdentityId;
                c.ReleaseLiveText = identity =>
                {
                    probed = identity;
                    return ReferenceEquals(identity, a) ? "auditTarget=A" : "auditTarget=wrong";
                };
                var candidate = RouteEligible(); candidate.PlayerOwned = candidate.PlayerTeam = true;
                Check(!c.Update(b, candidate, Cps2RouteAIntent.Hold, true, 0f, 0f, 5, 1, 1, 1f),
                    "replacement ownership: ineligible B is not acquired");
                Check(c.Target == null && a.Original && b.Original && ReferenceEquals(probed, a.Identity),
                    "replacement ownership: replacing A probes and restores A without changing B");
            });
            string live = Array.Find(output.Split('\n'), line => line.Contains("where=ReleaseLive"));
            string cached = Array.Find(output.Split('\n'), line => line.Contains("where=Release:"));
            Check(live != null && live.Contains("auditTarget=A") && live.Contains("identity=" + ownerId),
                "replacement ownership: live log describes replaced A");
            Check(cached != null && cached.Contains("identity=" + ownerId)
                && cached.Contains("playerOwned=0") && cached.Contains("playerTeam=0")
                && cached.Contains("reject=0") && cached.Contains("eligibilityFresh=1"),
                "replacement ownership: B's eligibility cannot replace A's cached snapshot");
        }

        private static void TestReleaseDiagnosticOutput()
        {
            string previousPath = CombatLog.OverridePath;
            bool previousDisabled = CombatLog.Disabled;
            string path = Path.Combine(Path.GetTempPath(), "CombatBrain-release-test-" + Guid.NewGuid().ToString("N") + ".log");
            CombatLog.Flush();
            try
            {
                CombatLog.OverridePath = path; CombatLog.Disabled = false;
                var c = RouteController(); var f = new RouteFake(); Arm(c, f);
                c.ReleaseGateText = () => "auditGate=ok";
                c.ReleaseLiveText = identity => "auditLive=ok";
                c.Release(RouteAExit.Disabled, 1.25f);
                c.Release(RouteAExit.Disabled, 2f);
                CombatLog.Flush();
                string output = File.ReadAllText(path);
                Check(output.Contains("where=ReleaseEntry") && output.Contains("auditGate=ok"),
                    "release diagnostics: real gate callback text reaches the log");
                Check(output.Contains("where=ReleaseLive") && output.Contains("auditLive=ok"),
                    "release diagnostics: real live callback text reaches the log");
                Check(output.Split(new[] { "where=ReleaseLive" }, StringSplitOptions.None).Length == 2
                    && output.Contains("missionTime=1.250") && output.Contains("eligibilityFresh=1"),
                    "release diagnostics: log has one live line with release time and cached freshness");
            }
            finally
            {
                CombatLog.Flush();
                CombatLog.OverridePath = previousPath; CombatLog.Disabled = previousDisabled;
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
