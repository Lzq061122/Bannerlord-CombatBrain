namespace CombatBrain.Coordination
{
    // ATC-2 编队坐标快照（FormationCoordSnapshot）—— 纯数据结构（§3.2 字段全），零 TaleWorlds 可离线测。
    // 用途：MainBodySelector / OperationalObjectiveSelector 的输入；胶水层从 Formation.QuerySystem 聚合
    // 填充（反作弊闸：只读编队级公开聚合 Count/位置/完整度/速度，零新增禁读、零逐兵扫描）。
    public enum FormationCoordKind
    {
        Infantry = 0,
        Ranged = 1,
        MeleeCavalry = 2,
        RangedCavalry = 3,
        Other = 4
    }

    public struct FormationCoordSnapshot
    {
        public int FormationId;        // Formation.Index
        public int Count;              // 存活兵力（CountOfUnits）
        public float X, Y;             // 编队中心（CachedMedianPosition 口径）
        public float Integrity;        // 完整度 0..1（FormationIntegrity 输出，未算=1 中性）
        public float NominalSpeed;     // 标称速度 m/s（编队级聚合；0=未知）
        public FormationCoordKind Kind; // 兵种类别（QuerySystem 旗标聚合）
        public bool IsKnown;           // 迷雾一致：Known=KnowledgeView 可见未过期（X6 IsEnemyFormationKnown）

        public static readonly FormationCoordSnapshot Empty = new FormationCoordSnapshot { FormationId = -1 };
    }
}
