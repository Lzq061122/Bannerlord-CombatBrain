using System;

namespace CombatBrain.Intent
{
    // 敌方意图枚举（t11 B1：OpponentModel 信念向量维度；F1 用于 C 反制矩阵列）。
    // 顺序与 C 矩阵列序一致：FrontalAttack, Flank, Probe, Skirmish, FeignRetreat, Reform。
    public enum EnemyIntent
    {
        FrontalAttack = 0,
        Flank = 1,
        Probe = 2,
        Skirmish = 3,
        FeignRetreat = 4,
        Reform = 5
    }

    // 冷启动“战术手册”权重矩阵（9 意图 × 12 维度）。
    // 权重 W = 预设战术手册(冷启动) × 运行时经验修正(F10，V3 叠加)。
    // F1 扩展：意图 7→9（新增 ProtectArcher/BraceFormation，t16 §1.2 建议值）；
    // 维度 8→12（追加 EnemCav/FlankEx/Gap/Skirm，t11 A1；旧 8 维索引不动，向后兼容）。
    public static class TacticManual
    {
        public const int IntentCount = 9;
        public const int DimensionCount = 12;
        // F2：敌方意图信念维数（t11 B1；= Counter 列数；OpponentModel 信念向量长度）
        public const int EnemyIntentCount = 6;

        public static readonly IntentType[] Order =
        {
            IntentType.Attack, IntentType.Defend, IntentType.Retreat,
            IntentType.Flank, IntentType.Harass, IntentType.Reform, IntentType.Pursue,
            IntentType.ProtectArcher, IntentType.BraceFormation
        };

        // 维度顺序：Power, Line, Cav, Height, Moral, Arc, PenB, Dist, EnemCav, FlankEx, Gap, Skirm
        // 后 4 列（EnemCav/FlankEx/Gap/Skirm）为 F2 事件采集维度的冷启动权重（建议值，可调）。
        private static readonly float[][] Matrix =
        {
            //                 Power   Line   Cav  Height Moral  Arc   PenB   Dist  EnemCav FlankEx  Gap   Skirm
            new float[] {      0.30f, 0.10f, 0.10f, 0.05f, 0.20f, 0.20f, 0.30f,-0.40f, -0.10f, -0.10f, 0.10f, 0.10f }, // Attack
            new float[] {     -0.10f, 0.40f, 0.00f, 0.30f, 0.10f, 0.10f,-0.30f, 0.20f,  0.10f,  0.10f,-0.10f, 0.05f }, // Defend
            new float[] {     -0.50f, 0.00f,-0.10f, 0.10f,-0.40f,-0.10f,-0.40f, 0.50f, -0.20f,  0.20f, 0.10f, 0.05f }, // Retreat
            new float[] {      0.10f,-0.20f, 0.50f,-0.10f, 0.05f, 0.10f, 0.10f, 0.05f, -0.10f, -0.20f, 0.15f, 0.05f }, // Flank
            new float[] {     -0.10f,-0.10f, 0.30f, 0.10f, 0.05f, 0.50f, 0.00f, 0.40f, -0.10f, -0.10f, 0.00f, 0.15f }, // Harass
            new float[] {      0.10f,-0.40f, 0.00f, 0.05f,-0.30f, 0.00f, 0.20f, 0.05f, -0.10f, -0.10f, 0.20f, 0.00f }, // Reform
            new float[] {      0.20f, 0.00f, 0.20f, 0.00f, 0.10f, 0.10f, 0.50f,-0.10f, -0.10f, -0.15f, 0.05f, 0.15f }, // Pursue
            // 新意图（t16 §1.2 建议值）：
            new float[] {      0.10f, 0.10f, 0.20f, 0.10f, 0.15f, 0.40f,-0.20f,-0.10f,  0.35f,  0.30f,-0.05f, 0.15f }, // ProtectArcher
            new float[] {      0.10f, 0.20f,-0.10f, 0.15f, 0.10f, 0.05f,-0.05f, 0.30f,  0.50f,  0.15f, 0.10f,-0.10f }, // BraceFormation
        };

        // C 反制系数矩阵（9 意图 × 6 敌方意图，t11 B2 + t16 §1.3）。
        // 列序 = EnemyIntent：FrontalAttack, Flank, Probe, Skirmish, FeignRetreat, Reform。
        // 语义：C[我方意图][敌方意图] = 识别敌方意图后对我方该意图评分的修正（>0 克制，<0 下策）。
        // 既有行锚点值取自 t11 B2（Defend×Flank=+0.3、Defend×Frontal=+0.2、Flank×Flank=-0.1、
        // Harass×Probe=+0.2、Reform×Reform=+0.2），其余为按语义补全的建议值；新意图行取 t16 §1.3 原文。
        private static readonly float[][] Counter =
        {
            //                 Front   Flank  Probe  Skirm  Feign  Reform
            new float[] {      0.20f, -0.20f, 0.00f, 0.10f,-0.20f, 0.20f }, // Attack
            new float[] {      0.20f,  0.30f, 0.00f, 0.10f, 0.10f, 0.10f }, // Defend
            new float[] {     -0.30f,  0.30f, 0.00f, 0.00f, 0.00f, 0.10f }, // Retreat
            new float[] {      0.10f, -0.10f, 0.00f, 0.10f, 0.10f, 0.00f }, // Flank
            new float[] {     -0.10f, -0.10f, 0.20f, 0.10f, 0.00f, 0.10f }, // Harass
            new float[] {     -0.20f, -0.10f, 0.10f, 0.00f, 0.00f, 0.20f }, // Reform
            new float[] {     -0.10f, -0.20f, 0.10f, 0.20f,-0.30f, 0.10f }, // Pursue
            new float[] {      0.10f,  0.30f, 0.00f, 0.20f,-0.10f, 0.00f }, // ProtectArcher（t16 §1.3 原文）
            new float[] {      0.40f, -0.20f, 0.00f, 0.00f, 0.00f, 0.10f }, // BraceFormation（t16 §1.3 原文）
        };

        public static float[][] BuildMatrix()
        {
            return Matrix;
        }

        public static float[][] BuildCounter()
        {
            return Counter;
        }

        // F2 综合评分：score(i) = ΣV·W + ΣB·C（t11 B1/B2）。
        // 反应式部分 = ΣV·W（纯态势，不注入信念）；
        // 信念项 = ΣB·C（B 为 6 维归一化敌方意图信念，来自 OpponentModel；
        //           null/长度不足 → 按 0 处理，即不注入信念 → 向后兼容 F1 纯反应式）。

        /// <summary>反应式评分 score(i) = ΣV·W。</summary>
        public static float Score(SituationVector v, int intentIndex)
        {
            if (intentIndex < 0 || intentIndex >= IntentCount) return 0f;
            float[] w = Matrix[intentIndex];
            float s = 0f;
            for (int d = 0; d < DimensionCount; d++) s += v[d] * w[d];
            return s;
        }

        /// <summary>信念修正项 beliefScore(i) = ΣB·C；belief 为 null/长度不足 → 0。</summary>
        public static float BeliefScore(float[] belief, int intentIndex)
        {
            if (belief == null || intentIndex < 0 || intentIndex >= IntentCount) return 0f;
            float[] c = Counter[intentIndex];
            int n = Math.Min(c.Length, Math.Min(belief.Length, EnemyIntentCount));
            float s = 0f;
            for (int i = 0; i < n; i++) s += belief[i] * c[i];
            return s;
        }

        /// <summary>综合分 = ΣV·W + ΣB·C（belief == null → 纯反应式，与 F1 IntentEngine 语义一致）。</summary>
        public static float CombinedScore(SituationVector v, float[] belief, int intentIndex)
        {
            return Score(v, intentIndex) + BeliefScore(belief, intentIndex);
        }
    }
}
