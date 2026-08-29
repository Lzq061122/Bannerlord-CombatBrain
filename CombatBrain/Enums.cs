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

    // 兵种（F10/F6 FighterProfile.Classify 输入；骑射手由胶水层归并到 Cavalry）
    public enum SoldierKind
    {
        Infantry = 0,
        Ranged = 1,
        Cavalry = 2
    }

    // 护甲材质（对齐游戏 ArmorMaterialTypes 视觉可辨：Cloth/Leather/Chainmail/Plate；None 视为 Cloth）
    public enum ArmorGrade
    {
        Cloth = 0,
        Leather = 1,
        Chainmail = 2,
        Plate = 3
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