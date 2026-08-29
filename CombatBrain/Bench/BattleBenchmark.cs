using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CombatBrain.Bench
{
    // 帧预算基准：统计 AI 各子系统耗时并周期性汇总。
    // 红线：500 人战场、四核中端、锁 30 帧（帧预算 33.3ms，AI 占比应远低于此）。
    // fps/avgFrame 依据游戏 dt 累计计算（真实游戏帧率）；avgAi/peakAi 依据 Stopwatch 测 AI 逻辑自身耗时。
    public sealed class BattleBenchmark
    {
        private const float ReportInterval = 10f;

        private readonly Stopwatch _sectionWatch = new Stopwatch();
        private readonly Dictionary<string, float> _sectionAcc = new Dictionary<string, float>();

        private float _sinceReport;
        private float _totalGameSeconds;
        private long _frameCount;
        private double _frameAiMs;
        private double _totalAiMs;
        private double _peakAiMs;

        public void BeginFrame()
        {
            _frameAiMs = 0.0;
        }

        public void BeginSection(string name)
        {
            _sectionWatch.Restart();
        }

        public void EndSection(string name)
        {
            _sectionWatch.Stop();
            double ms = _sectionWatch.Elapsed.TotalMilliseconds;
            _frameAiMs += ms;
            _totalAiMs += ms;

            float acc;
            if (_sectionAcc.TryGetValue(name, out acc)) _sectionAcc[name] = acc + (float)ms;
            else _sectionAcc[name] = (float)ms;
        }

        public void EndFrame(float dt, int agentCount)
        {
            _frameCount++;
            _totalGameSeconds += dt;
            if (_frameAiMs > _peakAiMs) _peakAiMs = _frameAiMs;

            _sinceReport += dt;
            if (_sinceReport < ReportInterval) return;

            double fps = _frameCount / Math.Max(1.0, _totalGameSeconds);
            double avgFrameMs = _totalGameSeconds * 1000.0 / Math.Max(1, _frameCount);
            double avgAi = _totalAiMs / Math.Max(1, _frameCount);

            CombatLog.Log(string.Format(
                "[Bench] agents={0} fps={1:F1} avgFrame={2:F2}ms avgAi={3:F2}ms peakAi={4:F2}ms",
                agentCount, fps, avgFrameMs, avgAi, _peakAiMs));

            var names = new List<string>(_sectionAcc.Keys);
            names.Sort();
            foreach (string name in names)
            {
                CombatLog.Log(string.Format(
                    "  [Bench]   {0}={1:F3}ms/frame",
                    name, _sectionAcc[name] / Math.Max(1, _frameCount)));
            }

            _sectionAcc.Clear();
            _sinceReport = 0f;
            _frameCount = 0;
            _totalGameSeconds = 0f;
            _totalAiMs = 0.0;
            _peakAiMs = 0.0;
        }
    }
}