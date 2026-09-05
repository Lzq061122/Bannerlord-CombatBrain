namespace CombatBrain.Coordination
{
    using CombatBrain.Battle;

    // ATC-2 Army Context 快照（ArmyContextSnapshot）—— 纯数据结构（§3.2 字段全），零 TaleWorlds 可离线测。
    // TeamState 保存最新快照（0.5s 更新）；MainBody 与 Operational Objective 均为影子上下文，
    // 本卡不改变任何 Formation 行为（ATC 规格 §8 不得做）。
    public struct ArmyContextSnapshot
    {
        public BattlePhase Phase;              // 当前战局阶段（ATC-1 输出）
        public float MainBodyX, MainBodyY;     // Main Body 加权中心
        public int MainBodyStrength;           // Main Body 兵力和
        public float MainBodyConfidence;       // Main Body 置信（簇兵力占己方总兵力比例 0..1）
        public bool HasOperationalObjective;   // 是否存在有效 Operational Objective
        public int ObjectiveFormationId;       // 敌目标簇代表编队（最大编队 id，-1=无）
        public float ObjectiveX, ObjectiveY;   // 敌目标簇中心
        public int ObjectiveStrength;          // 敌目标簇兵力

        public static readonly ArmyContextSnapshot None = new ArmyContextSnapshot
        {
            MainBodyX = 0f, MainBodyY = 0f, MainBodyStrength = 0, MainBodyConfidence = 0f,
            HasOperationalObjective = false, ObjectiveFormationId = -1,
            ObjectiveX = 0f, ObjectiveY = 0f, ObjectiveStrength = 0
        };
    }
}
