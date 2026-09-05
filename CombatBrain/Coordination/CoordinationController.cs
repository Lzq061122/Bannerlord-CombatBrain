namespace CombatBrain.Coordination
{
    // atc4-snapshot-optimization OPT-3/OPT-4：Army 日志适配层（CoordinationController 有限拆分）——
    //   ArmyContext 日志变化判定与格式化纯逻辑（零 TaleWorlds 可离线测）。
    //   OPT-3 降噪：坐标变化用距离阈值（≥3m）而非浮点精确不等；阶段/强度/Objective 等离散状态改变才记录；
    //   Shadow Mode 保留足够诊断字段（[ArmyCtx] 行格式不变）。
    public static class CoordinationController
    {
        /// <summary>MainBody 坐标变化距离阈值（m）：小于此值视为「无位移」不触发变化日志（OPT-3 降噪）。</summary>
        public const float MainBodyMoveLogMeters = 3f;

        /// <summary>
        /// Army 日志变化判定（OPT-3）：MainBody 位移 ≥3m 或 离散状态变化（Phase/Objective 存在性及 id/
        ///   Strength/Confidence）才返回 true（离散变化不计浮点抖动）。首拍（prev 无有效 MainBody）→ true。
        /// </summary>
        public static bool ArmyLogChanged(ArmyContextSnapshot prev, ArmyContextSnapshot snap, float mainBodyMoveMeters)
        {
            // 离散状态变化（阶段/Objective id 与存在性/强度/置信）——精确比较（离散量无抖动）
            if (prev.Phase != snap.Phase) return true;
            if (prev.HasOperationalObjective != snap.HasOperationalObjective) return true;
            if (prev.ObjectiveFormationId != snap.ObjectiveFormationId) return true;
            if (prev.ObjectiveStrength != snap.ObjectiveStrength) return true;
            if (prev.MainBodyStrength != snap.MainBodyStrength) return true;
            if (System.Math.Abs(prev.MainBodyConfidence - snap.MainBodyConfidence) > 0.01f) return true;
            // 坐标变化用距离阈值（OPT-3：浮点精确不等会因每拍中心抖动误刷）
            float dx = prev.MainBodyX - snap.MainBodyX;
            float dy = prev.MainBodyY - snap.MainBodyY;
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
            return dist >= mainBodyMoveMeters;
        }

        /// <summary>§14 [ArmyCtx] 日志行格式化（Shadow Mode 诊断字段保留：phase/MB 中心/strength/conf/objective）。</summary>
        public static string FormatArmyLog(int teamIndex, ArmyContextSnapshot snap)
        {
            return string.Format(
                "[ArmyCtx] Team {0} phase={1} MB=({2:F0},{3:F0}) str={4} conf={5:F2} | obj={6} ({7:F0},{8:F0}) str={9}",
                teamIndex, snap.Phase, snap.MainBodyX, snap.MainBodyY,
                snap.MainBodyStrength, snap.MainBodyConfidence,
                snap.HasOperationalObjective ? snap.ObjectiveFormationId.ToString() : "无",
                snap.ObjectiveX, snap.ObjectiveY, snap.ObjectiveStrength);
        }
    }
}
