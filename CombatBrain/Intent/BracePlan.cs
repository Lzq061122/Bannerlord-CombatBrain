using System;

namespace CombatBrain.Intent
{
    // F4 BracePlan 架枪反骑纯逻辑（pipeline §三 F4：触发阈值/架枪优先/无长杆降级）。
    // 语义：敌骑逼近（≤55m 且净接近 ≥1m/s）时步兵架枪站定；接战(<8m)不触发（已缠斗，
    //   架枪动画会被打断）；无长杆（架枪可用兵占比 <30%）降级为不架枪（交回正常防御 AI，
    //   不浪费站定命令）；退出距离 70m（回交正常 AI）。
    // 纯函数、零游戏 API 依赖，OfflineTests 直连；胶水层在 CombatBrain/Ai/BraceGlue.cs。
    public static class BracePlan
    {
        public const float TriggerRange = 55f;   // 敌骑触发距离（RBM 同量级：OverrideAgent 在敌骑距己 <35m 内判定，本层提前到 55m 准备）
        public const float MinMeleeDist = 8f;    // 接战距离下限（<8m 已缠斗，架枪动画会被打断）
        public const float ClosingSpeed = 1.0f;  // 净接近速度阈值 (m/s)
        public const float ReleaseRange = 70f;   // 退出距离（架枪剧集结束，交回正常 AI）
        public const float MinBraceRatio = 0.3f; // 架枪可用兵占比下限（无长杆降级阈值）
        public const int SampleSize = 12;        // 占比采样上限（多数兵零扫描：武装沿一次统计的预算参考）

        /// <summary>
        /// 触发判定：敌骑最近距离在 (minMelee, range] 内且本拍净接近（上拍距离 - 本拍 ≥ closing）。
        /// prevDist &lt; 0（首拍未知）→ 只要在范围内即触发（初始武装，宁可早站定）。
        /// </summary>
        public static bool ShouldBrace(float dist, float prevDist,
            float range = TriggerRange, float minMelee = MinMeleeDist, float closing = ClosingSpeed)
        {
            if (float.IsNaN(dist) || dist <= minMelee || dist > range) return false;
            if (prevDist < 0f) return true; // 首拍
            return prevDist - dist >= closing;
        }

        /// <summary>
        /// 退出判定：敌骑超出释放距离或已无敌骑（dist ≤ 0 / NaN）→ 架枪剧集结束，交回正常 AI。
        /// </summary>
        public static bool ShouldRelease(float dist, float release = ReleaseRange)
        {
            return float.IsNaN(dist) || dist <= 0f || dist > release;
        }

        /// <summary>
        /// 无长杆降级：架枪可用兵占比 ≥ minRatio 才架枪；占比不足或零样本 → 降级（不架枪）。
        /// </summary>
        public static bool HasEnoughBraceUnits(int braceCapable, int sampled, float minRatio = MinBraceRatio)
        {
            if (sampled <= 0) return false;
            return braceCapable >= Math.Ceiling(sampled * minRatio);
        }

        /// <summary>
        /// 架枪优先：架枪剧集中且占比达标 → 站定命令优先于机动命令（殿后档/走位等）。
        /// survivalRetreating：F7 个体层求生撤退走 agent.SetMovementDirection 直接驱动个体，
        /// 编队级站定命令不覆盖个体输入，故编队层传 false 即可（个体撤退不受影响）。
        /// </summary>
        public static bool ShouldIssueBraceOrder(bool episodeActive, bool enoughBraceUnits, bool survivalRetreating)
        {
            return episodeActive && enoughBraceUnits && !survivalRetreating;
        }
    }
}
