namespace CombatBrain.Ai
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using TaleWorlds.Library;

    // 游戏内左下角反馈层（解决「决策只写 %TEMP%\CombatBrain.log、游戏画面看不到」的实测痛点）。
    // 底层 API（v1.4.8 反编译源已核实）：InformationManager.DisplayMessage(InformationMessage)
    // （InformationManager.cs L54-56，无监听者时事件空转发=天然安全）+ InformationMessage(string, Color)
    // （InformationMessage.cs L21），颜色经 Color.FromUint(uint)（Color.cs L110）。
    //
    // 两档反馈模式（用户裁决，分级显示）：
    //   - 正常模式（DebugMode=false，默认）：左下角只显示战术级 + 编队级反馈（意图切换 /
    //     骑兵 FSM 跃迁 / 士气撤退阶段 / 架枪触发释放 / 保护激活释放）；个体级完全静默。
    //   - Debug 模式（DebugMode=true）：额外显示个体级反馈（走位/格挡/求生撤退/远程后撤，
    //     仍走限频聚合防 500 人刷屏）。
    // 底层 API（v1.4.8 反编译源已核实）：InformationManager.DisplayMessage(InformationMessage)
    // （InformationManager.cs L54-56，无监听者时事件空转发=天然安全）+ InformationMessage(string, Color)
    // （InformationMessage.cs L21），颜色经 Color.FromUint(uint)（Color.cs L110）。
    // 反作弊闸：只读已有决策状态，不新增任何禁读信息；性能：事件触发调用，无每帧轮询。
    // 与 CombatLog 文件日志并存：本层是游戏内 HUD 附加输出，不替代、不影响文件日志。
    // 线程：调用方均为主线程（MissionBehavior 事件/低频拍）；_sync 锁为防御性（与 CombatLog 同款）。
    public static class InGameFeedback
    {
        // 总开关：默认开（便于用户实测）；置 false 后仅剩 CombatLog 文件日志。静态字段可热改。
        public static bool EnableInGameFeedback = true;

        // Debug 模式开关：默认 false（正常模式个体级完全静默）；开启后额外显示个体级聚合反馈。
        public static bool DebugMode = false;

        private const float NotifyDedupSeconds = 3f;      // 编队级同类消息去重窗口（N 秒内最多 1 条）
        private const float AggregatePeriodSeconds = 3f;  // 个体级聚合输出周期
        private const int MaxMessagesPerSecond = 4;       // 全局总速率上限（防异常刷屏）

        public const uint TacticalColor  = 0xFF81D4FA; // 浅蓝：战术级（意图切换）
        public const uint FormationColor = 0xFFFFD54F; // 琥珀：编队级（骑兵/士气/架枪/保护）
        public const uint AggregateColor = 0xFFB0BEC5; // 灰蓝：个体级聚合统计

        private static readonly object _sync = new object();
        private static readonly Dictionary<string, float> _lastNotify = new Dictionary<string, float>(); // per 类别上次输出时间
        private static readonly Dictionary<string, int> _counts = new Dictionary<string, int>();         // 个体级类别累计计数
        private static readonly Dictionary<string, float> _lastFlush = new Dictionary<string, float>();  // 个体级类别上次汇总时间
        private static long _rateSecond = -1;
        private static int _rateCount;

        private static double NowSeconds()
        {
            return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        }

        // 全量反馈入口（战术/编队级）：正常模式即显示。category 用于限频去重（同名类别 N 秒最多 1 条）。
        public static void NotifyTactical(string category, string text, uint color = AggregateColor)
        {
            if (!EnableInGameFeedback || string.IsNullOrEmpty(text)) return;
            try
            {
                double now = NowSeconds();
                lock (_sync)
                {
                    float last;
                    if (_lastNotify.TryGetValue(category, out last) && now - last < NotifyDedupSeconds) return;
                    if (!AllowRateLocked(now)) return;
                    _lastNotify[category] = (float)now;
                }
                InformationManager.DisplayMessage(new InformationMessage(text, Color.FromUint(color)));
            }
            catch { } // HUD 未就绪等异常绝不外泄影响战斗逻辑
        }

        // 个体级反馈入口：仅 DebugMode=true 时显示，正常模式完全静默；仍走同类去重 + 总速率上限。
        public static void NotifyIndividual(string category, string text, uint color = AggregateColor)
        {
            if (!DebugMode) return;
            NotifyTactical(category, text, color);
        }

        // 个体级事件计数（Debug 模式专用聚合）：逐兵事件只计数，该类别距上次汇总
        // ≥ AggregatePeriodSeconds 时由本次调用触发输出「近3秒：类别 N 次」（纯中文通俗文案）。
        // 正常模式（DebugMode=false）直接 return：零计数零输出零开销。
        public static void Count(string category)
        {
            if (!EnableInGameFeedback || !DebugMode) return;
            int n = 0;
            bool due = false;
            double now = NowSeconds();
            lock (_sync)
            {
                int c;
                _counts.TryGetValue(category, out c);
                c++;
                _counts[category] = c;
                float last;
                _lastFlush.TryGetValue(category, out last);
                if (now - last >= AggregatePeriodSeconds)
                {
                    due = true;
                    n = c;
                    _counts[category] = 0;
                    _lastFlush[category] = (float)now;
                }
            }
            if (!due) return;
            try
            {
                bool allowed;
                lock (_sync) { allowed = AllowRateLocked(now); }
                if (allowed)
                    InformationManager.DisplayMessage(new InformationMessage(
                        string.Format("近{1:F0}秒：{0} {2} 次", category, AggregatePeriodSeconds, n),
                        Color.FromUint(AggregateColor)));
            }
            catch { }
        }

        // 总速率上限（须持 _sync）：每自然秒最多 MaxMessagesPerSecond 条，超限丢弃（文件日志仍有全量）
        private static bool AllowRateLocked(double now)
        {
            long sec = (long)now;
            if (sec != _rateSecond) { _rateSecond = sec; _rateCount = 0; }
            if (_rateCount >= MaxMessagesPerSecond) return false;
            _rateCount++;
            return true;
        }
    }
}
