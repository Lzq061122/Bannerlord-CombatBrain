namespace CombatBrain.Formations
{
    // X5 槽位状态：Occupied=该槽已指派士兵；Empty=空槽（补位/降级时使用）。
    public enum SlotState
    {
        Empty = 0,
        Occupied = 1
    }

    // X5 槽位（FormationSlot）—— 纯逻辑数据结构（零 TaleWorlds 依赖，可离线测）。
    // 语义：一个"这兵站哪、朝哪、用什么打"的抽象位置。位置轴与角色轴分离（设计稿 §二 裁决 2）：
    //   Role 由 X5-1 TroopClassifier 产出（武器角色轴，怎么打）；Rank/File/WorldPos 由 X5-2
    //   SlotLayout 生成（位置轴，站哪）；Facing 由 X3 FacingCorrection 输出（朝向）。
    // 字段：Rank=第几排（0=最前排，越大越靠后）；File=第几列（横向索引）；
    //   WorldX/WorldY=世界坐标（编队中心 + 朝向推导，纯数值）；FacingX/FacingY=朝向（归一化）；
    //   Role=该槽位要求的角色；State=槽位占用状态。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。
    public struct FormationSlot
    {
        public int Rank;        // 第几排（0=最前排）
        public int File;        // 第几列（横向索引）
        public float WorldX;    // 世界坐标 X（米）
        public float WorldY;    // 世界坐标 Y（米）
        public float FacingX;   // 朝向 X（归一化）
        public float FacingY;   // 朝向 Y（归一化）
        public TroopRole Role;  // 槽位要求的角色（武器角色轴）
        public SlotState State; // Occupied/Empty

        public FormationSlot(int rank, int file, float worldX, float worldY,
            float facingX, float facingY, TroopRole role, SlotState state)
        {
            Rank = rank;
            File = file;
            WorldX = worldX;
            WorldY = worldY;
            FacingX = facingX;
            FacingY = facingY;
            Role = role;
            State = state;
        }
    }
}
