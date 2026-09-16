namespace CombatBrain.Coordination
{
    // atc4-snapshot-optimization OPT-4：Mission 状态生命周期（MissionStateLifecycle）——跨 Mission 静态/实例
    //   状态幂等清理的单一入口（有限拆分自 CombatAiMissionLogic.ClearMissionStaticState）。
    //   Mission 初始化与结束均须调用（Init + OnMissionStateFinalized/OnClearScene 双钩子，幂等）；
    //   CommandProvenance 为静态单例（串场防护）、InGameFeedback 静态去重、_suppressedAttack 静态抑制集合，
    //   以及对手模型/攻击抑制实例级一并清空——Agent/Formation 索引复用不得继承旧状态。
    public static class MissionStateLifecycle
    {
        /// <summary>跨 Mission 静态状态幂等清理（先聚合后清理；传实例级清理对象，静态单例内部处理）。
        /// ATC-5R：租约管理器纳入清理（leaseGlue.ReleaseAll 幂等释放全部 CB 租约并交还原生 AI）。</summary>
        public static void ClearStatic(CombatBrain.Ai.OpponentModelGlue opponent, CombatBrain.Ai.AttackSuppression attackSuppression,
            CombatBrain.Ai.FormationControlLeaseGlue leaseGlue = null, TaleWorlds.MountAndBlade.Mission mission = null)
        {
            CommandProvenance.Instance.Clear();
            CombatBrain.Ai.InGameFeedback.ClearAll();
            if (opponent != null) opponent.ClearAll();
            if (attackSuppression != null) attackSuppression.ClearAll(); // 联动清 _suppressedAttack（静态集合）
            if (leaseGlue != null)
            {
                // t28（用户 2026-09-16 授权 · 只读遥测，1 行）：Mission 边界租约清零**逐次可观测**。
                //   背景（t26 归因）：M6-2 边界第 2 次租约静默清零，窗口内无任何 state=Release/event=Release 行，
                //   故在此处以只读快照（HasActiveLease/ActiveCount）记录**被清零的 (team,f) 集合**与 reason。
                //   仅查询 + 1 行日志：不改清理顺序、不改任何判定、不新增命令下发或控制权变更调用点。
                string cleared = LeaseSet(leaseGlue, mission);
                int activeBefore = leaseGlue.ActiveCount;
                leaseGlue.ReleaseAll(mission, LeaseReleaseReason.MissionEnding); // ATC-5R 租约幂等释放
                CombatLog.Log(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[ControlLease] clear path=MissionStateLifecycle reason={0} activeBefore={1} activeAfter={2} cleared={3}",
                    LeaseReleaseReason.MissionEnding, activeBefore, leaseGlue.ActiveCount, cleared));
            }
        }

        // t28：只读快照——当前持有租约的 (team,f) 集合（"team:f,team:f" / "none" / "mission=null"）。
        private static string LeaseSet(CombatBrain.Ai.FormationControlLeaseGlue glue, TaleWorlds.MountAndBlade.Mission mission)
        {
            if (mission == null) return "mission=null";
            var sb = new System.Text.StringBuilder();
            try
            {
                foreach (TaleWorlds.MountAndBlade.Team team in mission.Teams)
                {
                    if (team == null || !team.MBTeam.IsValid) continue;
                    foreach (TaleWorlds.MountAndBlade.Formation f in team.FormationsIncludingEmpty)
                    {
                        if (f == null) continue;
                        if (!glue.HasActiveLease(team.TeamIndex, f.Index)) continue;
                        if (sb.Length > 0) sb.Append(',');
                        sb.Append(team.TeamIndex).Append(':').Append(f.Index);
                    }
                }
            }
            catch { return "unreadable"; }
            return sb.Length == 0 ? "none" : sb.ToString();
        }
    }
}
