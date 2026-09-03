namespace CombatBrain.Formations
{
    using System;

    // 命名空间注意（t65 踩坑）：不用 CombatBrain.Formation——子命名空间名会遮蔽
    // TaleWorlds.MountAndBlade.Formation 类型（C# 解析：外层命名空间成员优先于 using 导入类型，
    // CS0118「Formation 是命名空间」×42）。目录仍为 CombatBrain/Formation/，仅命名空间复数化。

    // 编队阵型完整度分档（T1-01，阈值表勿改）：
    //   80~100 完整 / 60~79 轻微散乱 / 40~59 中度散乱 / 20~39 严重散乱 / 0~19 崩溃。
    public enum FormationIntegrityGrade
    {
        Collapsed = 0,
        Severe = 1,
        Moderate = 2,
        Slight = 3,
        Full = 4
    }

    // T1-01 编队阵型完整度（0~100）—— 纯逻辑零依赖（可离线回归），被 T2/T19/T31/T39 共用。
    // 数据源（反作弊闸：只用己方官方聚合值，官方预计算 O(1)，铁律禁遍历个体单位）：
    //   Formation.CachedFormationIntegrityData（Formation.cs L243；内嵌 struct L36-45）——
    //     DeviationOfPositionsExcludeFarAgents（平均位置偏差米）、
    //     MaxDeviationOfPositionExcludeFarAgents（最大位置偏差米）、
    //     AverageVelocityExcludeFarAgents（Vec2 速度向量，胶水层取模长传入 avgSpeed）、
    //     AverageMaxUnlimitedSpeedExcludeFarAgents（平均最大不限速）。
    // 映射设计：
    //   * 偏差分：≤3m=100（沿用 OpponentModelGlue.cs:29 IntegrityOrderly=3f 口径）、≥15m=0、中间线性；
    //   * 合并 = 0.6*平均 + 0.4*最大——平均偏差为主（整体散乱），最大偏差只占四成：
    //     单兵掉队拉高最大值时不应把整队打崩（例：平均 3m 完美 + 一个 15m 掉队 → 60 而非 0）；
    //   * 速度归一：全员同速移动（ratio≈1）不额外扣分（骑兵编队机动天然散开=有意机动不误判）；
    //     掉队/混乱时平均速度远低于平均最大速（ratio 低）额外衰减；驻停（平均最大速<0.1）不归一；
    //   * 空编队（unitCount<=0）= 100：无事可测，按完整处理（非 0 防下游误报崩溃）。
    // 消费约定（T2/T19/T31/T39）：编队级查询入口建议 0.5s 缓存一次（与现有 Glue 同频），
    // 本类为纯计算不内建缓存。
    public static class FormationIntegrity
    {
        public const float OrderlyDeviation = 3f;   // 有序上限（米），沿用 IntegrityOrderly 口径
        public const float BrokenDeviation = 15f;   // 崩坏下限（米）
        public const float AvgWeight = 0.6f;        // 平均偏差权重
        public const float MaxWeight = 0.4f;        // 最大偏差权重（防单兵掉队拉爆）
        public const float FullThreshold = 80f;     // 分档阈值（勿改）
        public const float SlightThreshold = 60f;
        public const float ModerateThreshold = 40f;
        public const float SevereThreshold = 20f;

        private const float MovingMinSpeed = 0.1f;    // 平均最大速低于此视为未移动（跳过速度归一）
        private const float VelocityInfluence = 0.3f; // 速度归一影响上限（30%）

        // 完整度 0~100：avgDeviation/maxDeviation=平均与最大位置偏差（米）；avgSpeed/avgMaxSpeed=
        // 平均速度模长与平均最大不限速（米/秒）；unitCount=编队人数（<=0 → 100）。
        public static float Compute(float avgDeviation, float maxDeviation, float avgSpeed, float avgMaxSpeed, int unitCount)
        {
            if (unitCount <= 0) return 100f;
            float combined = AvgWeight * LinearScore(avgDeviation) + MaxWeight * LinearScore(maxDeviation);
            float ratio = avgMaxSpeed < MovingMinSpeed ? 1f : Clamp01(avgSpeed / avgMaxSpeed);
            return combined * (1f - VelocityInfluence * (1f - ratio));
        }

        // 偏差 → 线性分 0~100：≤3m=100、≥15m=0、中间线性递减。
        public static float LinearScore(float deviation)
        {
            float x = (deviation - OrderlyDeviation) / (BrokenDeviation - OrderlyDeviation);
            if (x < 0f) x = 0f; else if (x > 1f) x = 1f;
            return 100f * (1f - x);
        }

        // 分档（阈值表勿改）：>=80 完整、>=60 轻微、>=40 中度、>=20 严重、<20 崩溃。
        public static FormationIntegrityGrade Grade(float integrity)
        {
            if (integrity >= FullThreshold) return FormationIntegrityGrade.Full;
            if (integrity >= SlightThreshold) return FormationIntegrityGrade.Slight;
            if (integrity >= ModerateThreshold) return FormationIntegrityGrade.Moderate;
            if (integrity >= SevereThreshold) return FormationIntegrityGrade.Severe;
            return FormationIntegrityGrade.Collapsed;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
