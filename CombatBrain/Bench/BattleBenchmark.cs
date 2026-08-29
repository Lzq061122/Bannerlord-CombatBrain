using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CombatBrain.Bench
{
    // 帧预算基准：每帧测 AI 各子系统耗时，周期性汇总。
    // 红线：500 人战场、四核中端、锁 30 帧（帧预算 33.3ms，AI 占比应远低于此）。
    public sealed class BattleBenchmark
    {
        private const float ReportInterval = 10f;

        private readonly Stopwatch _frameWatch = new Stopwatch();
        private readonly Stopwatch _sectionWatch = new Stopwatch();
        private readonly Dictionary<string, float> _sectionAcc = new Dictionary<string, float>();

        private float _sinceReport;
        private long _frameCount;
        private double _totalAiMs;
        private double _totalFrameMs;
        private double _peakAiMs;

        public void BeginFrame()
        {
            _frameWatch.Restart();
        }

        public void EndFrame(float dt, int agentCount)
        {
            _frameWatch.Stop();
            _frameCount++;
            double frameMs = _frameWatch.Elapsed.TotalMilliseconds;
            double aiMs = _totalAiMs;
            if (aiMs > _peakAiMs) _peakAiMs = aiMs;
            _totalFrameMs += frameMs;

            _sinceReport += dt;
            if (_sinceReport < ReportInterval) return;

            double avgFrame = _totalFrameMs / Math.Max(1, _frameCount);
            double avgAi = _totalAiMs / Math.Max(1, _frameCount);
            double fps = 1000.0 / Math.Max(1.0, avgFrame);

            CombatLog.Log(string.Format(
                "[Bench] agents={0} fps={1:F1} avgFrame={2:F2}ms avgAi={3:F2}ms peakAi={4:F2}ms",
                agentCount, fps, avgFrame, avgAi, _peakAiMs));

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
            _totalAiMs = 0;
            _totalFrameMs = 0;
            _peakAiMs = 0;
        }

        public void BeginSection(string name)
        {
            _sectionWatch.Restart();
        }

        public void EndSection(string name)
        {
            _sectionWatch.Stop();
            double ms = _sectionWatch.Elapsed.TotalMilliseconds;
            _totalAiMs += ms;

            float acc;
            if (_sectionAcc.TryGetValue(name, out acc)) _sectionAcc[name] = acc + (float)ms;
            else _sectionAcc[name] = (float)ms;
        }
    }
}