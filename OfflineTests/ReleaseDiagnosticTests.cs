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
                c.ReleaseLiveText = () =>
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
            quiet.ReleaseGateText = quiet.ReleaseLiveText = () => { quietCalls++; return "unexpected"; };
            quiet.Evidence.Capture = false;
            quiet.Release(RouteAExit.Disabled, 1f);
            Check(quietCalls == 0 && !quiet.Owns(quietFake) && quietFake.Original,
                "release diagnostics: capture off skips both outlets and still restores weights");

            var session = RouteController(); var sessionFake = new RouteFake(); Arm(session, sessionFake);
            session.ReleaseGateText = session.ReleaseLiveText = () => { throw new InvalidOperationException("session diagnostic failed"); };
            bool sessionThrew = false;
            try { session.BeginSession(new object(), 2f); }
            catch (InvalidOperationException) { sessionThrew = true; }
            Check(!sessionThrew && session.Target == null && sessionFake.Original && session.Evidence.Identities == 0,
                "release diagnostics: Mission change completes cleanup even when both callbacks fail");

            var deadline = RouteController(); var deadlineFake = new RouteFake(); Arm(deadline, deadlineFake);
            deadline.ReleaseGateText = deadline.ReleaseLiveText = () => { throw new InvalidOperationException("deadline diagnostic failed"); };
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
                c.ReleaseLiveText = () => "auditLive=ok";
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
