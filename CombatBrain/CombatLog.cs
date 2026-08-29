using System;
using System.IO;

namespace CombatBrain
{
    public static class CombatLog
    {
        private static readonly object _lock = new object();
        private static readonly string _logPath;

        static CombatLog()
        {
            try { _logPath = Path.Combine(Path.GetTempPath(), "CombatBrain.log"); }
            catch { _logPath = null; }
        }

        public static void Log(string message)
        {
            if (_logPath == null) return;
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_logPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}