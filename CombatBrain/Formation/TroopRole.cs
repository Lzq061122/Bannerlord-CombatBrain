namespace CombatBrain.Formations
{
    // X5 武器角色轴（TroopRole）—— 描述"这兵怎么打"。
    // 两轴分离（设计稿 §二 裁决 2）：TroopRole 只描述角色（用什么打），位置由 SlotLayout 决定（站哪），
    // 防绑定战术（ShieldWall: ShieldBearer→前、Polearm→后；Wedge: HeavyMelee→尖端、Cavalry→两翼）。
    // 命名空间用复数 CombatBrain.Formations（t65 教训：CombatBrain.Formation 遮蔽原生类型 CS0118×42，
    //   与 FormationIntegrity.cs:1-8 同惯例）；目录仍为 CombatBrain/Formation/。
    // Support 预留：Bannerlord 无旗手/乐手单位，第一版归类无输出规则 = 死代码，不入枚举（设计稿 §四 裁决 2-b）。
    public enum TroopRole
    {
        ShieldBearer, // 单手 + 盾（盾墙前排核心）
        Polearm,      // 长杆步战（反骑/后排架枪；骑乘长杆归 Cavalry）
        Ranged,       // 弓/弩（近战武器不改变该角色）
        HeavyMelee,   // 板甲重装 + 双手/单手（破阵/尖端）
        LightMelee,   // 轻甲近战（侧翼/补位/散兵侧卫）
        Cavalry       // 骑乘（含骑射手——胶水层按既有 F10 归并惯例，Enums.cs:44）
    }
}
