using System;

namespace CombatBrain.Intent
{
    // F3 保护计划 —— 纯逻辑（零依赖，可离线测试）。
    // ProtectArcher 意图执行层的目标选择数学（pipeline §三 F3）：
    //   远程退向步兵阵后（ChooseRetreatTarget）、步兵殿后档（ChooseScreenTarget）、
    //   骑兵回援阈值（ShouldReturnCavalry）、弓手受压判定（IsUnderPressure）、下令防抖（ShouldIssueOrder）。
    // 语义（冷兵器-顾问：退后≠溃逃）：退后目标是"步兵阵后方的受保护射击位"，不是脱离战场。
    public static class ProtectPlan
    {
        public const float DefaultClearanceMargin = 8f; // 退后间隙裕量（米）
        public const float ScreenFraction = 0.6f;       // 殿后档位置：敌→弓连线 60% 处
        public const float MinScreenDistance = 15f;     // 殿后档距敌下限（米）
        public const float ReturnThreshold = 40f;       // 骑兵回援阈值：敌距弓 ≤40m
        public const float PressureRange = 45f;         // 弓手受压判定：敌距弓 ≤45m
        public const float OrderHysteresis = 3f;        // 下令防抖：距目标 >3m 才重新下令（防命令风暴）

        // 退后目标：步兵中心沿"敌→己"方向退 clearDist 米（阵后射击位）。
        // 敌/步重合（零向量）→ 退化向正上退避，避免 NaN。
        public static void ChooseRetreatTarget(
            float ex, float ey, float ix, float iy, float clearDist, out float tx, out float ty)
        {
            float dx = ix - ex;
            float dy = iy - ey;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f)
            {
                tx = ix;
                ty = iy + clearDist;
                return;
            }
            tx = ix + dx / len * clearDist;
            ty = iy + dy / len * clearDist;
        }

        // 殿后档目标：敌→弓连线上距敌 ScreenFraction×总距处；夹 [MinScreenDistance, 总距]。
        public static void ChooseScreenTarget(
            float ex, float ey, float rx, float ry, out float tx, out float ty)
        {
            float dx = rx - ex;
            float dy = ry - ey;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-4f)
            {
                tx = rx;
                ty = ry;
                return;
            }
            float d = len * ScreenFraction;
            if (d < MinScreenDistance) d = MinScreenDistance;
            if (d > len) d = len; // 不越过弓手
            tx = ex + dx / len * d;
            ty = ey + dy / len * d;
        }

        // 退后间隙：步兵阵半宽 + 裕量（阵宽来自编队离散度代理）。
        public static float RetreatClearance(float infantrySpread, float margin = DefaultClearanceMargin)
        {
            return infantrySpread * 0.5f + margin;
        }

        // 骑兵回援阈值：敌距弓 ≤ threshold 且非负 → 回援。
        public static bool ShouldReturnCavalry(float enemyToRangedDist, float threshold = ReturnThreshold)
        {
            return enemyToRangedDist >= 0f && enemyToRangedDist <= threshold;
        }

        // 弓手受压判定：敌距弓 ≤ pressureRange 且非负。
        public static bool IsUnderPressure(float enemyToRangedDist, float pressureRange = PressureRange)
        {
            return enemyToRangedDist >= 0f && enemyToRangedDist <= pressureRange;
        }

        // 下令防抖：当前距目标 > hysteresis 才需要重新下令。
        public static bool ShouldIssueOrder(float distToTarget, float hysteresis = OrderHysteresis)
        {
            return distToTarget > hysteresis;
        }
    }
}
