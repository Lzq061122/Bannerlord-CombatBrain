namespace OfflineTests
{
    using System;
    using System.Collections.Generic;

    internal static partial class Program
    {
        private static int _checks;
        private static readonly HashSet<string> _completedGroups = new HashSet<string>();

        private static void RunTest(Action test)
        {
            string name = test.Method.Name;
            if (!_completedGroups.Add(name))
            {
                Check(false, "duplicate test group: " + name);
                return;
            }
            int checksBefore = _checks;
            int failuresBefore = _failures;
            test();
            if (_checks == checksBefore) Check(false, "empty test group: " + name);
            Console.WriteLine((_failures == failuresBefore ? "[GROUP PASS] " : "[GROUP FAIL] ")
                + name + " checks=" + (_checks - checksBefore));
        }
    }
}
