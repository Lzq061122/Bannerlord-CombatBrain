namespace CombatBrain.Cavalry
{
    // F8 骑兵多波次调度 —— 纯状态机。
    // SCOUT → SETUP → CHARGE → RECALL → REFORM → (SETUP)。
    // 机会窗由 F2 触发；只有当 CHARGE 起始和 RECALL 是事件，其余低频。
    public enum CavalryState
    {
        Scout,
        Setup,
        Charge,
        Recall,
        Reform
    }

    public enum OpportunityType
    {
        None,
        EnemyLineGap,       // 敌方阵线缺口张开
        EnemyChargeEnded    // 敌骑冲锋刚结束的空窗
    }

    public struct CavalryInput
    {
        public bool ScoutFoundWeakness;   // SCOUT 探到弱点
        public OpportunityType Opportunity; // F2 触发的机会窗
        public bool ImpactFinished;        // CHARGE 冲击完成（事件）
        public bool RallyComplete;         // RECALL 回撤集结完成
        public bool ReformComplete;        // REFORM 再编完成
    }

    public static class CavalryWaveFSM
    {
        public static CavalryState Advance(CavalryState current, CavalryInput inp)
        {
            switch (current)
            {
                case CavalryState.Scout:
                    if (inp.ScoutFoundWeakness) return CavalryState.Setup;
                    return CavalryState.Scout;

                case CavalryState.Setup:
                    if (inp.Opportunity != OpportunityType.None) return CavalryState.Charge;
                    return CavalryState.Setup;

                case CavalryState.Charge:
                    if (inp.ImpactFinished) return CavalryState.Recall;
                    return CavalryState.Charge;

                case CavalryState.Recall:
                    if (inp.RallyComplete) return CavalryState.Reform;
                    return CavalryState.Recall;

                case CavalryState.Reform:
                    if (inp.ReformComplete) return CavalryState.Setup;
                    return CavalryState.Reform;
            }
            return current;
        }
    }
}