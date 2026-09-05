namespace CombatBrain.Formations
{
    // X5-3 地形形变（TerrainWarp）—— 纯逻辑（零 TaleWorlds / TerrainSampler 依赖，可离线测）。
    // 消费 Terrain 输出契约（坡度/高度/障碍）形变 X5-2 生成的 Slot 列表（设计稿 §六 TerrainWarp.cs）：
    //   * 高地适配：坡顶方向 Ranged slot 后移↑（Ranged 平均高度 ≥ 全队平均，§九 场景2⑤）；
    //   * 坡度闸：坡度 >15° 处无 Rank=0 slot（⑥——陡坡前排会被拖离阵线，后移为 Rank1）；
    //   * 障碍切割：禁入区后无 slot 落入（⑦——障碍 Frontline bend：落入禁入区的 slot 沿阵线侧向移出）。
    // 输入契约（本文件定义，胶水层从 TerrainSampler/场景映射注入，先例 TerrainSampler.cs HeightQuery 委托风格）：
    //   TerrainContext.Height(x,y)=高度采样、Slope(x,y)=坡度角（度）、UphillX/Y=坡顶方向（归一化）、
    //   ForbiddenZones=禁入区列表（轴对齐矩形）、RangedUphillStep=Ranged 后移步长（米）。
    // 边界：委托为 null → 该维度不形变（优雅降级，无 Terrain 时行为=原 Slot 列表）；空输入返回空列表。
    // 反作弊闸：只消费己方编队槽位 + 公开地形采样（Scene 高度/坡度肉眼可辨），无越权。
    // 约束：不改 X5-2 三布局生成规则（SlotLayout 零改动）；不改 FormationIntegrity；不选型不解阵（红线 1/2）。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。

    /// <summary>禁入区（轴对齐矩形，含边界；胶水层从场景障碍/禁行网格映射）。</summary>
    public struct ForbiddenZone
    {
        public float MinX, MinY, MaxX, MaxY;

        public ForbiddenZone(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;
        }

        public bool Contains(float x, float y)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }
    }

    /// <summary>Terrain 输出契约（纯逻辑输入结构；胶水层从 TerrainSampler/场景映射喂入，本类不引用其本体）。</summary>
    public sealed class TerrainContext
    {
        public const float SlopeThresholdDegrees = 15f; // §九⑥：坡度 >15° 处无 Rank=0 slot

        public delegate float HeightQuery(float x, float y);
        public delegate float SlopeQuery(float x, float y);

        public HeightQuery Height;           // 高度采样（米）——预留：本类 Warp 不直接消费（高地适配用 Uphill 方向 + 坡度闸用 Slope 梯度近似表达）；由胶水层映射 Uphill/Slope 时已隐含高度语义，字段保留供未来高度形变/断言契约
        public SlopeQuery Slope;             // 坡度角（度）；null=不做坡度闸
        public float UphillX;                // 坡顶方向 X（归一化；胶水层从高度梯度映射）
        public float UphillY;                // 坡顶方向 Y（归一化）
        public float RangedUphillStep = 1.0f; // Ranged 后移步长（米，沿坡顶方向）
        public System.Collections.Generic.List<ForbiddenZone> ForbiddenZones; // 禁入区；null/空=无障碍
    }

    public static class TerrainWarp
    {
        /// <summary>
        /// 形变 Slot 列表（返回新列表，不改入参容器）：①障碍切割（禁入区→侧向移出）；
        /// ②坡度闸（>15° 的 Rank0 → Rank1 后移）；③高地适配（Ranged 沿坡顶方向后移↑）。
        /// 委托/障碍为 null 的维度跳过（优雅降级）。空输入返回空列表。
        /// </summary>
        public static System.Collections.Generic.List<FormationSlot> Warp(
            System.Collections.Generic.List<FormationSlot> slots, TerrainContext terrain)
        {
            var result = new System.Collections.Generic.List<FormationSlot>();
            if (slots == null || slots.Count == 0) return result;
            if (terrain == null) return new System.Collections.Generic.List<FormationSlot>(slots);

            for (int i = 0; i < slots.Count; i++)
            {
                FormationSlot s = slots[i];

                // ① 障碍切割（⑦）：落入禁入区 → 沿阵线侧向（垂直朝向）移出到区域外
                if (terrain.ForbiddenZones != null && terrain.ForbiddenZones.Count > 0)
                {
                    s = PushOutOfZones(s, terrain);
                }

                // ② 坡度闸（⑥）：>15° 处 Rank0 → Rank1（前排退出陡坡，后移一排）
                if (s.Rank == 0 && terrain.Slope != null && terrain.Slope(s.WorldX, s.WorldY) > TerrainContext.SlopeThresholdDegrees)
                {
                    s.Rank = 1; // 陡坡上无前排（后移为后排架枪位）
                }

                // ③ 高地适配（⑤）：Ranged 沿坡顶方向后移↑（坡顶方向 Ranged 高度 ≥ 全队平均）
                if (s.Role == TroopRole.Ranged && (terrain.UphillX != 0f || terrain.UphillY != 0f))
                {
                    float step = terrain.RangedUphillStep > 0f ? terrain.RangedUphillStep : 1.0f;
                    float len = (float)System.Math.Sqrt(terrain.UphillX * terrain.UphillX + terrain.UphillY * terrain.UphillY);
                    if (len > 1e-4f)
                    {
                        s.WorldX += terrain.UphillX / len * step;
                        s.WorldY += terrain.UphillY / len * step;
                    }
                }

                result.Add(s);
            }
            return result;
        }

        // 禁入区移出：沿阵线侧向（垂直 facing）把 slot 平移出第一个命中的禁入区
        private static FormationSlot PushOutOfZones(FormationSlot s, TerrainContext terrain)
        {
            for (int z = 0; z < terrain.ForbiddenZones.Count; z++)
            {
                ForbiddenZone zone = terrain.ForbiddenZones[z];
                if (!zone.Contains(s.WorldX, s.WorldY)) continue;

                // 侧向（垂直朝向）：px = -fy, py = fx
                float fx = s.FacingX, fy = s.FacingY;
                float len = (float)System.Math.Sqrt(fx * fx + fy * fy);
                if (len > 1e-4f) { fx /= len; fy /= len; } else { fx = 1f; fy = 0f; }
                float px = -fy, py = fx;

                // 槽位相对区域中心 → 选移出方向（向左/右侧移出最近边）
                float zoneCx = (zone.MinX + zone.MaxX) * 0.5f;
                float zoneCy = (zone.MinY + zone.MaxY) * 0.5f;
                float sideX = s.WorldX - zoneCx, sideY = s.WorldY - zoneCy;
                float sideAlong = sideX * px + sideY * py; // 槽位在侧向上的偏移符号
                float pushDir = sideAlong >= 0f ? 1f : -1f;

                // 移到区域边缘外（半宽 + 一个基础间距，保证不含边界）
                float halfW = ((zone.MaxX - zone.MinX) + (zone.MaxY - zone.MinY)) * 0.5f;
                float push = halfW + 1.0f;
                s.WorldX += px * pushDir * push;
                s.WorldY += py * pushDir * push;
                break; // 一次移出后重新检查（若又入其它区由下一次 Warp 拍处理；本拍只保证出第一个区）
            }
            return s;
        }
    }
}
