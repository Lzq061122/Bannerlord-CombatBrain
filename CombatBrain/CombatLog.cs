using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace CombatBrain
{
    // P2-③ 缓冲日志（review-findings-2026-09-01 P2）：Log() 只写内存缓冲，消除每行
    // File.AppendAllText（open/write/close）的 IO 抖动（事件高峰如 AttackObserved 密集期）。
    // 落盘时机三路：①缓冲 ≥8KB（事件高峰按块批量写出）；②1s 周期 Timer（低频日志新鲜度，
    // 空缓冲直接返回不产生 IO）；③进程退出 ProcessExit 兜底 flush 残余缓冲（崩溃丢最近 ≤1s 日志，可接受）。
    // 顺序语义：单缓冲按调用序追加、整块按序落盘；时间戳取 Log 调用时刻——日志时序不变。
    // 线程：Log 主线程调用（现状契约），Timer 回调线程负责落盘——沿用原 _lock 串行化缓冲与文件访问。
    public static class CombatLog
    {
        private const int FlushThresholdChars = 8 * 1024; // 容量阈值：高峰期按块落盘上限
        private const int FlushIntervalMs = 1000;         // 新鲜度：缓冲最长滞留 1s

        private static readonly object _lock = new object();
        private static readonly string _logPath;
        private static readonly StringBuilder _buffer = new StringBuilder(FlushThresholdChars + 256);
        private static Timer _flushTimer;
        private static bool _hooksInstalled;

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
                    EnsureLifecycleHooks();
                    _buffer.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("  ")
                           .Append(message).AppendLine();
                    if (_buffer.Length >= FlushThresholdChars) FlushInternal(); // 高峰批量落盘
                }
            }
            catch { }
        }

        // 落盘残余缓冲（幂等，空缓冲无 IO；供进程退出钩子与未来显式调用点使用）
        public static void Flush()
        {
            if (_logPath == null) return;
            try { lock (_lock) { FlushInternal(); } }
            catch { }
        }

        private static void FlushInternal()
        {
            if (_buffer.Length == 0) return;
            File.AppendAllText(_logPath, _buffer.ToString());
            _buffer.Clear();
        }

        private static void EnsureLifecycleHooks()
        {
            if (_hooksInstalled) return;
            _hooksInstalled = true;
            // 低频新鲜度：1s 周期落盘（Timer 回调与 Log 共用 _lock，串行无竞争）
            _flushTimer = new Timer(s =>
            {
                try { lock (_lock) { FlushInternal(); } }
                catch { }
            }, null, FlushIntervalMs, FlushIntervalMs);
            // 关停兜底：正常退出时 flush 残余缓冲并停表
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try { Flush(); }
                finally { if (_flushTimer != null) _flushTimer.Dispose(); }
            };
        }
    }
}
