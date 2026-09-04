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
        // t60 夹击/绕后触发修正（用户实测 P0：整场只有 Defend/Brace/Protect，从不 Flank/Harass）：
        //   * Flank 行原 FlankEx=-0.20 为反向权重 bug（敌侧翼暴露=包抄核心触发信号，却在扣分）；
        //     修正 FlankEx=+0.60、Gap=+0.45——敌侧翼暴露/阵线缺口达标时 Flank 可靠胜出（配合
        //     防抖 hold 6s 与 15% 切换余量，需持续暴露才会真正切换，不抖动）。
        //   * Harass 行重定标为「骑兵优骚扰消耗」：Cav↑/EnemCav↓（敌骑多不骚扰）/FlankEx/Gap/Skirm↑
        //     （侧翼暴露、缺口、敌散兵压上=骚扰目标多）；Arc↓（不再以我方射手为主，骑兵为主）。
        //   * 其余行（含 ProtectArcher/BraceFormation t16 建议值）不动，C 反制矩阵不动。
        private static readonly float[][] Matrix =
        {
            //                 Power   Line   Cav  Height Moral  Arc   PenB   Dist  EnemCav FlankEx  Gap   Skirm
            new float[] {      0.30f, 0.10f, 0.10f, 0.05f, 0.20f, 0.20f, 0.30f,-0.40f, -0.10f, -0.10f, 0.10f, 0.10f }, // Attack
            new float[] {     -0.10f, 0.40f, 0.00f, 0.30f, 0.10f, 0.10f,-0.30f, 0.20f,  0.10f,  0.10f,-0.10f, 0.05f }, // Defend
            new float[] {     -0.50f, 0.00f,-0.10f, 0.10f,-0.40f,-0.10f,-0.40f, 0.50f, -0.20f,  0.20f, 0.10f, 0.05f }, // Retreat
            new float[] {      0.05f,-0.25f, 0.50f,-0.10f, 0.05f, 0.10f, 0.10f, 0.05f, -0.10f,  0.60f, 0.45f, 0.05f }, // Flank（t60）
            new float[] {     -0.10f,-0.10f, 0.45f, 0.10f, 0.05f, 0.35f, 0.00f, 0.25f, -0.25f,  0.30f, 0.25f, 0.35f }, // Harass（t60）
            new float[] {      0.10f,-0.40f, 0.00f, 0.05f,-0.30f, 0.00f, 0.20f, 0.05f, -0.10f, -0.10f, 0.20f, 0.00f }, // Reform
            new float[] {      0.20f, 0.00f, 0.20f, 0.00f, 0.10f, 0.10f, 0.50f,-0.10f, -0.10f, -0.15f, 0.05f, 0.15f }, // Pursue
            // 新意图（t16 §1.2 建议值）：
            new float[] {      0.10f, 0.10f, 0.20f, 0.10f, 0.15f, 0.40f,-0.20f,-0.10f,  0.35f,  0.30f,-0.05f, 0.15f }, // ProtectArcher
            // t71 修正（用户实测 P0：开局 Dist=1.00 就切 BraceFormation，步兵撑盾墙走不动，70% 切换是 Brace）：
            //   Dist +0.30 为反向权重 bug（越远越加分=敌骑还远就架枪站定）→ 改 -0.30（越近越架枪，
            //   枪阵是迎敌动作，敌骑进入 55m 内才有意义）。EnemCav 0.50 评估保留：敌骑少(0.1)时仅
            //   +0.05 不足以单独触发，需与 Dist 近叠加；执行层另有 55m 距离守卫（BraceGlue）。
            new float[] {      0.10f, 0.20f,-0.10f, 0.15f, 0.10f, 0.05f,-0.05f,-0.30f,  0.50f,  0.15f, 0.10f,-0.10f }, // BraceFormation（t71）
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

        // ---------------- T26-01 文化战术模板（六文化 9×12，源文档 §2.2） ----------------
        // 增量 helper：从默认表深拷贝后按 (row, dim, value) 打补丁——保证「相对默认表的增量」
        // 可读可回溯，未 patch 的格子永远与默认表一致。
        private static float[][] FromDefault((int row, int dim, float val)[] patches)
        {
            var rows = new float[Matrix.Length][];
            for (int r = 0; r < Matrix.Length; r++) rows[r] = (float[])Matrix[r].Clone();
            foreach (var p in patches) rows[p.row][p.dim] = p.val;
            return rows;
        }

        // 行序（Order）：0 Attack / 1 Defend / 2 Retreat / 3 Flank / 4 Harass / 5 Reform / 6 Pursue / 7 ProtectArcher / 8 Brace
        // 维序：0 Power / 1 Line / 2 Cav / 3 Height / 4 Moral / 5 Arc / 6 PenB / 7 Dist / 8 EnemCav / 9 FlankEx / 10 Gap / 11 Skirm

        // 瓦兰迪亚（§7）：弩兵消耗 → 重骑择机冲锋 → 脱离 → 再冲 → 高追击
        private static readonly float[][] MatrixVlandia = FromDefault(new (int, int, float)[]
        {
            (0, 7, -0.55f),              // Attack Dist -0.40→-0.55 §7.4 远不让步兵先上，让弩兵消耗
            (1, 5, 0.35f), (1, 3, 0.35f),// Defend Arc 0.10→0.35 / Height 0.30→0.35 §7.4 建立弩兵射击阵地
            (3, 2, 0.70f), (3, 9, 0.70f), (3, 10, 0.55f), // Flank Cav/FlankEx 0.70 / Gap 0.55 §7.5 侧翼暴露/阵型混乱=极高冲锋优先级
            (4, 2, 0.20f), (4, 11, 0.10f), // Harass Cav 0.20 / Skirm 0.10 §7 不骚扰，只等一次决定性冲锋
            (5, 1, -0.55f), (5, 4, -0.20f), // Reform Line -0.55 / Moral -0.20 §7.6 冲后脱离再集结，重整门槛放低
            (6, 0, 0.40f), (6, 6, 0.70f), (6, 2, 0.35f), // Pursue Power 0.40 / PenB 0.70 / Cav 0.35 §21 高追击
            (7, 5, 0.55f),               // ProtectArcher Arc 0.55 §7.4 步兵保护弩兵
            (8, 8, 0.35f),               // Brace EnemCav 0.35 §7 靠骑兵对冲，不靠步兵架枪
        });

        // 斯特吉亚（§8）：盾墙推进、主动贴身、近战解决、不追骑兵
        private static readonly float[][] MatrixSturgia = FromDefault(new (int, int, float)[]
        {
            (0, 0, 0.40f), (0, 7, -0.70f), (0, 4, 0.30f), // Attack Power 0.40 / Dist -0.70 / Moral 0.30 §8.5 越近越强，主动缩短距离
            (2, 0, -0.70f), (2, 7, 0.30f), // Retreat Power -0.70 / Dist 0.30 §8.8 低撤退倾向
            (3, 2, 0.25f), (3, 9, 0.35f), // Flank Cav 0.25 / FlankEx 0.35 §8.7 骑兵只护侧翼/拦截/打远程
            (4, 2, 0.15f),               // Harass Cav 0.15 §8.8 低复杂骑兵机动
            (5, 1, -0.55f),              // Reform Line -0.55 §18 盾墙被破→重组盾墙
            (6, 2, 0.10f), (6, 7, -0.25f), // Pursue Cav 0.10 / Dist -0.25 §33 禁止重步兵长距离追击骑兵
            (7, 5, 0.15f),               // ProtectArcher Arc 0.15 §8.2 远程仅 15%
            (8, 8, 0.70f), (8, 1, 0.40f), // Brace EnemCav 0.70 / Line 0.40 §8.4/§8.6 盾墙抗远程+抗骑；Dist 继承默认 -0.30（t71 删原 0.45 越远越架枪，改越近才架枪）
        });

        // 巴旦尼亚（§9）：高地/森林弓兵阵地、集中火力、反骑射、低追击、守阵地
        private static readonly float[][] MatrixBattania = FromDefault(new (int, int, float)[]
        {
            (0, 0, 0.15f), (0, 7, -0.20f), // Attack Power 0.15 / Dist -0.20 §9.9 低主动冲锋
            (1, 1, 0.60f), (1, 3, 0.55f), (1, 5, 0.35f), (1, 7, 0.35f), // Defend Line 0.60 / Height 0.55 / Arc 0.35 / Dist 0.35 §9.3 占高地建立阵地
            (2, 5, 0.10f), (2, 7, 0.35f), // Retreat Arc 0.10 / Dist 0.35 §9.8 边撤边射可接受
            (3, 2, 0.30f), (3, 5, 0.25f), // Flank Cav 0.30 / Arc 0.25 §9
            (4, 5, 0.55f), (4, 7, 0.40f), // Harass Arc 0.55 / Dist 0.40 §9 远程骚扰为主
            (6, 0, 0.05f), (6, 6, 0.20f), (6, 7, -0.35f), // Pursue Power 0.05 / PenB 0.20 / Dist -0.35 §21/§33 低追击，禁止弃高地追击
            (7, 5, 0.70f), (7, 8, 0.55f), (7, 7, 0.20f), // ProtectArcher Arc 0.70 / EnemCav 0.55 / Dist 0.20 §9.6 反骑射：护弓+优先打骑射
            (8, 8, 0.60f),               // Brace EnemCav 0.60 §9.7 对重骑防御
        });

        // 库赛特（§10）：骑射风筝、包抄、诱敌、避免正面、极高追击
        private static readonly float[][] MatrixKhuzait = FromDefault(new (int, int, float)[]
        {
            (0, 0, 0.10f), (0, 7, -0.15f), (0, 2, 0.25f), // Attack Power 0.10 / Dist -0.15 / Cav 0.25 §10.1 绝不正面步战
            (1, 1, 0.15f), (1, 5, 0.20f), (1, 7, 0.40f), // Defend Line 0.15 / Arc 0.20 / Dist 0.40 §10 不守阵地
            (2, 7, 0.60f), (2, 0, -0.35f), (2, 2, 0.10f), // Retreat Dist 0.60 / Power -0.35 / Cav 0.10 §10.5 敌近即脱离，骑兵撤退不亏
            (3, 2, 0.75f), (3, 9, 0.80f), (3, 10, 0.60f), (3, 7, 0.25f), // Flank Cav 0.75 / FlankEx 0.80 / Gap 0.60 / Dist 0.25 §10.8 包抄优先
            (4, 2, 0.75f), (4, 5, 0.60f), (4, 11, 0.55f), (4, 7, 0.45f), // Harass Cav 0.75 / Arc 0.60 / Skirm 0.55 / Dist 0.45 §10.4 骑射游击（SkirmishGlue 同向）
            (5, 2, 0.20f), (5, 7, 0.25f), // Reform Cav 0.20 / Dist 0.25 §10.7 脱离后重整
            (6, 2, 0.50f), (6, 6, 0.75f), (6, 7, -0.30f), // Pursue Cav 0.50 / PenB 0.75 / Dist -0.30 §21 极高追击
            (7, 5, 0.20f),               // ProtectArcher Arc 0.20 骑射自保，不需要步兵护
            (8, 8, 0.25f),               // Brace EnemCav 0.25 §10.10 不正面接战，架枪无意义
        });

        // 阿塞莱（§11）：骑兵机动骚扰、侧翼、多方向攻击、中近距骑兵战
        private static readonly float[][] MatrixAserai = FromDefault(new (int, int, float)[]
        {
            (0, 2, 0.30f), (0, 7, -0.25f), // Attack Cav 0.30 / Dist -0.25
            (1, 1, 0.25f), (1, 5, 0.15f), // Defend Line 0.25 / Arc 0.15 §11.4 步骑混合，不固守
            (3, 2, 0.65f), (3, 9, 0.75f), (3, 10, 0.55f), // Flank Cav 0.65 / FlankEx 0.75 / Gap 0.55 §11.6 多方向攻击
            (4, 2, 0.65f), (4, 5, 0.45f), (4, 11, 0.50f), (4, 7, 0.35f), // Harass Cav 0.65 / Arc 0.45 / Skirm 0.50 / Dist 0.35 §11.4 骚扰消耗
            (2, 7, 0.55f), (2, 2, 0.05f), // Retreat Dist 0.55 / Cav 0.05
            (6, 2, 0.40f), (6, 6, 0.65f), // Pursue Cav 0.40 / PenB 0.65 §21 高追击
            (7, 5, 0.30f),               // ProtectArcher Arc 0.30
            (8, 8, 0.40f),               // Brace EnemCav 0.40
        });

        /// <summary>
        /// t69 按文化取 9×12 权重表：vlandia/sturgia/battania/khuzait/aserai → 文化分表；
        /// empire/looter/未知/null/空 → 默认表（=帝国=现有 Matrix，一个数字不改，零回归）。
        /// </summary>
        public static float[][] MatrixFor(string cultureId)
        {
            switch (cultureId)
            {
                case "vlandia": return MatrixVlandia;
                case "sturgia": return MatrixSturgia;
                case "battania": return MatrixBattania;
                case "khuzait": return MatrixKhuzait;
                case "aserai": return MatrixAserai;
                default: return Matrix;
            }
        }

        /// <summary>t69 文化表版综合分 = ΣV·MatrixFor(cultureId) + ΣB·C（C 反制矩阵文化克制留第二期，全文化共用）。</summary>
        public static float CombinedScoreFor(string cultureId, SituationVector v, float[] belief, int intentIndex)
        {
            float[][] w = MatrixFor(cultureId);
            if (intentIndex < 0 || intentIndex >= IntentCount || w.Length != IntentCount) return CombinedScore(v, belief, intentIndex);
            float s = 0f;
            for (int d = 0; d < DimensionCount; d++) s += v[d] * w[intentIndex][d];
            return s + BeliefScore(belief, intentIndex);
        }
    }
}
