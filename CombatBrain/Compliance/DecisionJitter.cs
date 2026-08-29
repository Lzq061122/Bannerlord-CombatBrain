using System;

namespace CombatBrain.Compliance
{
    // F12 决策抖动&服从度 —— 纯逻辑，随机源注入以便离线回归。
    // 拟人的最后一块：疲劳迟疑、士气崩溃走形、低纪律可能犹豫。
    // discipline/fatigue/morale 均 0..1（纪律越高越精锐，疲劳越高越累，士气越低越接近崩溃）。
    public struct ComplianceProfile
    {
        public float DelayBase;     // 0..1 指令起始迟疑程度
        public float HesitateProb;  // 0..1 本轮犹豫(跳过一次执行)的概率
        public float Wobble;        // 0..1 走形扰动幅度
    }

    public struct JitterResult
    {
        public float DelaySeconds; // 本次实际延迟(秒)
        public bool Hesitate;      // 本轮是否犹豫
        public float Wobble;       // 走形幅度(0..1)
    }

    public static class DecisionJitter
    {
        // 确定性核心：把纪律/疲劳/士气映射成迟疑、犹豫、走形三个系数。系数和 ≤1，结果天然 0..1。
        public static ComplianceProfile ComputeProfile(float discipline, float fatigue, float morale)
        {
            float d = Clamp01(discipline);
            float f = Clamp01(fatigue);
            float m = Clamp01(morale);

            return new ComplianceProfile
            {
                DelayBase = (1f - d) * 0.6f + f * 0.4f,
                HesitateProb = (1f - d) * 0.35f + f * 0.25f + (1f - m) * 0.40f,
                Wobble = (1f - m) * 0.7f + f * 0.3f
            };
        }

        public static JitterResult Apply(Random rng, ComplianceProfile p)
        {
            double roll = rng.NextDouble();
            return new JitterResult
            {
                DelaySeconds = p.DelayBase * (0.5f + 0.5f * (float)roll),
                Hesitate = roll < p.HesitateProb,
                Wobble = p.Wobble
            };
        }

        public static JitterResult Compute(float discipline, float fatigue, float morale, Random rng)
        {
            return Apply(rng, ComputeProfile(discipline, fatigue, morale));
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}