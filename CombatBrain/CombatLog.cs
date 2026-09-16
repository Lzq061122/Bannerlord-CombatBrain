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
        private static readonly UTF8Encoding _utf8Bom = new UTF8Encoding(true); // 带 BOM，复用实例零分配
        private static Timer _flushTimer;
        private static bool _hooksInstalled;

        // t28（用户 2026-09-16 授权 · 仅日志出口 plumbing）：输出路径**可覆盖 / 可关闭**。
        //   背景（t26 归因）：本类静态构造把路径硬编码为 %TEMP%\CombatBrain.log，游戏与离线测试进程
        //   共用同一 append-only 文件 ⇒ 离线用例产出（如 [RouteAOwnership] 夹具行）混入取证日志。
        //   OverridePath：非空 ⇒ 写入该路径（离线 harness 指向 CombatBrain-offline.log）；
        //   Disabled：true ⇒ 完全关闭输出（含 flush 钩子）；
        //   环境变量 COMBATBRAIN_LOG_PATH：进程启动级覆盖（不改默认行为）。
        //   注意：默认路径与既有行为**不变**；本改动不触碰任何判定逻辑。
        public static string OverridePath;
        public static bool Disabled;

        static CombatLog()
        {
            try { _logPath = Path.Combine(Path.GetTempPath(), "CombatBrain.log"); }
            catch { _logPath = null; }
            try
            {
                string env = Environment.GetEnvironmentVariable("COMBATBRAIN_LOG_PATH");
                if (!string.IsNullOrEmpty(env)) OverridePath = env;
            }
            catch { }
        }

        // 有效输出路径：关闭 ⇒ null（调用方直接返回，零 IO）；覆盖优先于默认。
        private static string EffectivePath()
        {
            if (Disabled) return null;
            return string.IsNullOrEmpty(OverridePath) ? _logPath : OverridePath;
        }

        public static void Log(string message)
        {
            if (EffectivePath() == null) return;
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
            if (EffectivePath() == null) return;
            try { lock (_lock) { FlushInternal(); } }
            catch { }
        }

        private static void FlushInternal()
        {
            if (_buffer.Length == 0) return;
            string path = EffectivePath();
            if (path == null) { _buffer.Clear(); return; } // 已关闭：丢弃缓冲，避免下次误写到默认路径
            // 日志增强：UTF-8 with BOM 落盘——PowerShell Get-Content 默认按 BOM 识别编码，
            // 消除中文乱码（楠戝叺缂栭槦/澹皵 等，原为无 BOM 被 ANSI/GBK 误读）。
            // 注：BOM 仅在文件新建/为空时写入一次（AppendAllText 追加模式不重复写 BOM）；
            // 旧的无 BOM 日志文件删除后即以新编码重建。
            File.AppendAllText(path, _buffer.ToString(), _utf8Bom);
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
