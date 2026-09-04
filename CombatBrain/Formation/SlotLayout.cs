namespace CombatBrain.Formations
{
    using CombatBrain.Ai; // TacticsArrangement（T2-01 纯枚举，零 TaleWorlds；胶水层映射原生 ArrangementOrder）

    // X5 布局种类（第一版三件套，设计稿 §五）：
    //   ShieldWall=盾墙（盾兵 Rank0 前排居中 / 长杆 Rank1 后排架枪 / 远程与近战补位）；
    //   Line=线列（同角色按列编组，秩稳定）；
    //   RangedLoose=远程散列（无分层、间距放大参数化 + 前后错落）。
    public enum LayoutKind
    {
        Line = 0,
        ShieldWall = 1,
        RangedLoose = 2
    }

    // X5-2 布局输入（纯逻辑契约，零 TaleWorlds）：
    //   Composition=编队构成快照（胶水层 0.5s 聚合喂入，禁逐兵遍历）；
    //   Arrangement=T2-01 已选阵型（纯枚举，不写原生 ArrangementOrder——裁决 2-c）；
    //   Count=布局人数（非骑乘单位数，Cavalry 由 F8 骑兵波次管，不占位）；
    //   Center/Facing=编队中心与朝向（消费 X3/RallyPoint 输出语义）；Spacing=基础间距（默认 1.0m）；
    //   TerrainHeightBias=Terrain 输出契约（X5-3 TerrainWarp 前由胶水层喂 TerrainSampler 高度差，
    //   本阶段默认 0 = 平地，不在本类内做形变）。
    public struct SlotLayoutInput
    {
        public TroopComposition Composition;
        public TacticsArrangement Arrangement;
        public int Count;
        public float CenterX;
        public float CenterY;
        public float FacingX;
        public float FacingY;
        public float Spacing;
        public float TerrainHeightBias;

        public static SlotLayoutInput Default(TroopComposition composition, TacticsArrangement arrangement)
        {
            return new SlotLayoutInput
            {
                Composition = composition,
                Arrangement = arrangement,
                Count = composition.Total,
                CenterX = 0f,
                CenterY = 0f,
                FacingX = 1f,
                FacingY = 0f,
                Spacing = 1.0f,
                TerrainHeightBias = 0f
            };
        }
    }

    // X5-2 槽位布局生成器（SlotLayout）—— 纯逻辑（零 TaleWorlds 依赖，可离线测）。
    // 分流规则（设计稿 §五，X5 入口）：编队构成以 Ranged 为主（占比≥0.5）→ RangedLooseLayout
    //   （远程通道，不经 T2-01）；以近战为主 → 消费 T2-01 已选结果：ShieldWall → ShieldWallLayout、
    //   Line → LineLayout。
    // 角色→位置映射（§五 布局表）：
    //   ShieldWallLayout：ShieldBearer → Rank0（前排居中，列从中间块开始）；Polearm → Rank1（后排架枪）；
    //     Ranged/HeavyMelee/LightMelee → 按构成补位（Rank1 剩余）。
    //   LineLayout：同角色按列编组（连续列段），秩稳定（第一版只要求秩不抖动）。
    //   RangedLooseLayout：无分层（Rank 全 0）；间距放大（×RangedLooseSpacingFactor 参数化）；
    //     前后错落（奇偶列沿朝向 ±LooseStagger）。
    // 验收锚点（§九）：① ShieldBearer Rank0 占比 ≥90%（本实现恒 100%——盾兵全部排前排，cols 由盾兵数定宽）；
    //   ③ 相邻 slot 间距 ∈[0.8,2.0]m（列距 1.0 / 排距 1.0 / 对角 √2≈1.414 全落区间）；
    //   ⑪ RangedLoose 相邻间距（1.5m）≥ Line（1.0m）×1.3；⑬ 近战为主不进入 RangedLoose。
    // 反作弊闸：输入全是己方编队公开聚合（构成/中心/朝向/人数），无越权。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。
    public static class SlotLayout
    {
        public const float DefaultSpacing = 1.0f;          // 基础列间距（米）
        public const float RankSpacing = 1.0f;             // 排间距（米，Rank0/Rank1 纵向错开）
        public const float RangedLooseSpacingFactor = 1.5f; // 远程散列间距放大倍数（≥1.3，验收⑪）
        public const float LooseStagger = 0.5f;            // 前后错落幅度（米，奇偶列沿朝向错开）

        /// <summary>§五 分流：Ranged 为主 → RangedLoose；近战为主 → 消费 T2-01（ShieldWall/Line）。</summary>
        public static LayoutKind SelectLayout(TroopComposition composition, TacticsArrangement arrangement)
        {
            if (composition.IsRangedDominant) return LayoutKind.RangedLoose;
            return arrangement == TacticsArrangement.ShieldWall ? LayoutKind.ShieldWall : LayoutKind.Line;
        }

        /// <summary>生成槽位列表（只生成 Occupied 槽；空槽补位属 X5-4 SlotAssigner 职责）。</summary>
        public static System.Collections.Generic.List<FormationSlot> Generate(SlotLayoutInput input)
        {
            var slots = new System.Collections.Generic.List<FormationSlot>();
            if (input.Count <= 0) return slots;

            LayoutKind kind = SelectLayout(input.Composition, input.Arrangement);
            switch (kind)
            {
                case LayoutKind.ShieldWall: GenerateShieldWall(input, slots); break;
                case LayoutKind.RangedLoose: GenerateRangedLoose(input, slots); break;
                default: GenerateLine(input, slots); break;
            }
            return slots;
        }

        // ---- ShieldWall：盾兵 Rank0 前排居中（列从中间块开始），其余 Rank1 后排架枪/补位 ----

        private static void GenerateShieldWall(SlotLayoutInput input, System.Collections.Generic.List<FormationSlot> slots)
        {
            TroopComposition c = input.Composition;
            float spacing = input.Spacing > 0f ? input.Spacing : DefaultSpacing;
            int cols = System.Math.Max(c.ShieldBearer, c.Polearm + c.Ranged + c.HeavyMelee + c.LightMelee); // 宽度由盾兵定

            // Rank0：盾兵全部前排（占比恒 100%，验收①），从 (cols-S)/2 列开始居中
            int frontStart = System.Math.Max(0, (cols - c.ShieldBearer) / 2);
            for (int i = 0; i < c.ShieldBearer; i++)
            {
                slots.Add(MakeSlot(0, frontStart + i, input, spacing, TroopRole.ShieldBearer));
            }

            // Rank1：Polearm 优先（后排架枪），Ranged/HeavyMelee/LightMelee 补位
            int rearStart = System.Math.Max(0, (cols - (c.Polearm + c.Ranged + c.HeavyMelee + c.LightMelee)) / 2);
            int fi = 0;
            fi = FillRear(slots, input, spacing, rearStart, ref fi, c.Polearm, TroopRole.Polearm);
            fi = FillRear(slots, input, spacing, rearStart, ref fi, c.Ranged, TroopRole.Ranged);
            fi = FillRear(slots, input, spacing, rearStart, ref fi, c.HeavyMelee, TroopRole.HeavyMelee);
            fi = FillRear(slots, input, spacing, rearStart, ref fi, c.LightMelee, TroopRole.LightMelee);
        }

        private static int FillRear(System.Collections.Generic.List<FormationSlot> slots, SlotLayoutInput input,
            float spacing, int start, ref int fi, int count, TroopRole role)
        {
            for (int i = 0; i < count; i++)
            {
                slots.Add(MakeSlot(1, start + fi, input, spacing, role));
                fi++;
            }
            return fi;
        }

        // ---- Line：单排，同角色按列连续编组（秩稳定） ----

        private static void GenerateLine(SlotLayoutInput input, System.Collections.Generic.List<FormationSlot> slots)
        {
            TroopComposition c = input.Composition;
            float spacing = input.Spacing > 0f ? input.Spacing : DefaultSpacing;
            int fi = 0;
            fi = FillLine(slots, input, spacing, ref fi, c.ShieldBearer, TroopRole.ShieldBearer);
            fi = FillLine(slots, input, spacing, ref fi, c.Polearm, TroopRole.Polearm);
            fi = FillLine(slots, input, spacing, ref fi, c.HeavyMelee, TroopRole.HeavyMelee);
            fi = FillLine(slots, input, spacing, ref fi, c.LightMelee, TroopRole.LightMelee);
            fi = FillLine(slots, input, spacing, ref fi, c.Ranged, TroopRole.Ranged);
        }

        private static int FillLine(System.Collections.Generic.List<FormationSlot> slots, SlotLayoutInput input,
            float spacing, ref int fi, int count, TroopRole role)
        {
            for (int i = 0; i < count; i++)
            {
                slots.Add(MakeSlot(0, fi, input, spacing, role));
                fi++;
            }
            return fi;
        }

        // ---- RangedLoose：无分层（Rank0），间距放大 + 前后错落 ----

        private static void GenerateRangedLoose(SlotLayoutInput input, System.Collections.Generic.List<FormationSlot> slots)
        {
            TroopComposition c = input.Composition;
            float baseSpacing = input.Spacing > 0f ? input.Spacing : DefaultSpacing;
            float spacing = baseSpacing * RangedLooseSpacingFactor; // 散列放大（验收⑪：≥Line×1.3）
            int n = input.Count;
            for (int i = 0; i < n; i++)
            {
                // 前后错落：奇偶列沿朝向交替 ±LooseStagger/2
                float stagger = (i % 2 == 0) ? -LooseStagger * 0.5f : LooseStagger * 0.5f;
                FormationSlot slot = MakeSlot(0, i, input, spacing, TroopRole.Ranged);
                slot.WorldX += input.FacingX * stagger;
                slot.WorldY += input.FacingY * stagger;
                slots.Add(slot);
            }
        }

        // 构造槽位：列居中于编队中心，Rank0 沿朝向 -RankSpacing/2、Rank1 +RankSpacing/2（前后两排）
        private static FormationSlot MakeSlot(int rank, int file, SlotLayoutInput input,
            float spacing, TroopRole role)
        {
            float fx = input.FacingX, fy = input.FacingY;
            float len = (float)System.Math.Sqrt(fx * fx + fy * fy);
            if (len > 1e-4f) { fx /= len; fy /= len; }
            else { fx = 1f; fy = 0f; }
            // 侧向（垂直朝向）
            float px = -fy, py = fx;
            float half = ((input.Count > 0 ? input.Count : 1) - 1) * 0.5f;
            float side = (file - half) * spacing;
            float depth = rank == 0 ? -RankSpacing * 0.5f : RankSpacing * 0.5f;
            return new FormationSlot(
                rank, file,
                input.CenterX + px * side + fx * depth,
                input.CenterY + py * side + fy * depth + input.TerrainHeightBias,
                fx, fy, role, SlotState.Occupied);
        }
    }
}
