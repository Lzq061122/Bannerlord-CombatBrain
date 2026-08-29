namespace CombatBrain
{
    public enum IntentType
    {
        Attack,
        Defend,
        Retreat,
        Flank,
        Harass,
        Reform,
        Pursue
    }

    public enum LodLevel
    {
        L0 = 0,
        L1 = 1,
        L2 = 2,
        L3 = 3
    }

    public enum AttackDir
    {
        None = -1,
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    public enum SoldierTier
    {
        Recruit = 0,
        Soldier = 1,
        Veteran = 2,
        Elite = 3
    }

    public enum KnowledgeRole
    {
        Commander = 0,
        Formation = 1,
        Unit = 2
    }

    public enum CombatEventKind
    {
        ChargeContact,
        FormationBroken,
        IntentSwitched,
        DuelStarted,
        MessengerIntercepted,
        AgentDown,
        AttackObserved,
        LodChanged
    }
}