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
        Pursue,

        // F1 枚举层扩展（H1）：追加于末尾，索引 7/8；旧 0-6 映射不变，向后兼容。
        // 设计来源：combatbrain_historical_tactics_design.md §1.2（ProtectArcher/BraceFormation）。
        ProtectArcher,
        BraceFormation
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

    // CB-PROMPT-0038 R3 / F-7·M12：`public enum SoldierTier { Recruit/Soldier/Veteran/Elite }`
    //   已退役删除——其唯一消费者 `FighterProfileFactory.Classify` 随个体控制面退役（t9）归零，
    //   全仓零代码消费者；不得以「未来兵种分级预留」为由保留（无消费者状态不留、类型不得暗示旧功能仍有效）。

    // 兵种（X5-1 兵种分类输入；消费链 Ai/FormationSlotGlue.BuildSoldierInput → Formation/TroopClassifier.Classify；
    //   骑射手由胶水层归并到 Cavalry）
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
        LodChanged,
        BattlePhaseChanged // ATC-1 追加（只追加不重排，旧索引不变）
    }
}