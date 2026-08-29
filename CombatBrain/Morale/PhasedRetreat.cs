namespace CombatBrain.Morale
{
    // F11b 分阶段撤退 —— 纯状态机。
    // 崩溃不是瞬间蒸发：Hold → RequestRetreat(请求后撤) → Disengage(批准后分阶段脱离) → Reform → Hold。
    public enum RetreatState
    {
        Hold,
        RequestRetreat,
        Disengage,
        Reform
    }

    public struct RetreatInput
    {
        public bool Collapse;      // 本队已到崩溃临界
        public bool Approved;      // 指挥官批准后撤
        public bool DisengageDone; // 脱离完成(事件)
        public bool ReformDone;    // 重整完成
    }

    public static class PhasedRetreatFSM
    {
        public static RetreatState Advance(RetreatState current, RetreatInput inp)
        {
            switch (current)
            {
                case RetreatState.Hold:
                    if (inp.Collapse) return RetreatState.RequestRetreat;
                    return RetreatState.Hold;

                case RetreatState.RequestRetreat:
                    if (inp.Approved) return RetreatState.Disengage;
                    if (!inp.Collapse) return RetreatState.Hold; // 危机解除，撤回请求
                    return RetreatState.RequestRetreat;

                case RetreatState.Disengage:
                    if (inp.DisengageDone) return RetreatState.Reform;
                    return RetreatState.Disengage;

                case RetreatState.Reform:
                    if (inp.ReformDone) return RetreatState.Hold;
                    return RetreatState.Reform;
            }
            return current;
        }
    }
}