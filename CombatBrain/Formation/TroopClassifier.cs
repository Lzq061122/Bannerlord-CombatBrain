namespace CombatBrain.Formations
{
    using CombatBrain.Combat;

    // X5-1 兵种角色归类（TroopClassifier）—— 纯函数规则表（零 TaleWorlds 依赖，可离线测）。
    // 输入 = SoldierInput 扩展（设计稿 §二 裁决 2-a：装备 + ArmorGrade + 等级复用现有结构，
    //   不另起平行输入）：Kind（兵种，骑乘由胶水层归并 Cavalry）/ HasShield / PrimaryWeaponClass /
    //   Armor（板甲）/ Level（预留，角色归类不直接用等级）/ IsVeteran（英雄折算沿用既有语义）。
    // 规则表（设计稿 §四，逐条照抄；顺序即优先级）：
    //   ① Kind==Cavalry（骑乘，含骑射手）              → Cavalry（骑枪/骑射/近战骑全归入，
    //      细分是二阶段文化化的事；Enums.cs:44 胶水层归并惯例）
    //   ② 非骑乘 + 持盾 + 单手                        → ShieldBearer（盾墙前排核心）
    //   ③ 非骑乘 + 长杆主武器                          → Polearm（反骑/后排架枪）
    //   ④ 非骑乘 + 弓/弩/投掷主武器                    → Ranged（投掷按远程惯例并入；近战武器不改变该角色）
    //   ⑤ 非骑乘 + 双手重武器 或 板甲+单手             → HeavyMelee（破阵/尖端）
    //   ⑥ 其余近战                                    → LightMelee（侧翼/补位/散兵侧卫）
    // 边界对齐：骑射手归 Cavalry（Enums.cs:44 注释先例）；英雄/特殊单位沿用既有折算
    //   （IsVeteran 只影响 FighterProfileFactory.Classify 档位，不改变角色——角色由装备决定）。
    // 反作弊闸：输入全部是士兵自身公开属性（装备/骑乘/护甲肉眼可辨），无越权。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。
    public static class TroopClassifier
    {
        /// <summary>
        /// 归类：SoldierInput → TroopRole。规则表顺序即优先级（盾+单手在板甲+单手之前，
        /// 所以持盾者即使穿板甲也归 ShieldBearer——盾是角色轴第一特征）。
        /// </summary>
        public static TroopRole Classify(SoldierInput input)
        {
            // ① 骑乘（含骑射手）→ Cavalry
            if (input.Kind == SoldierKind.Cavalry) return TroopRole.Cavalry;

            // ② 持盾 + 单手 → ShieldBearer
            if (input.HasShield && input.PrimaryWeaponClass == PrimaryWeaponClass.OneHanded)
                return TroopRole.ShieldBearer;

            // ③ 长杆主武器 → Polearm
            if (input.PrimaryWeaponClass == PrimaryWeaponClass.Polearm) return TroopRole.Polearm;

            // ④ 弓/弩/投掷 → Ranged
            if (input.PrimaryWeaponClass == PrimaryWeaponClass.Bow
                || input.PrimaryWeaponClass == PrimaryWeaponClass.Crossbow
                || input.PrimaryWeaponClass == PrimaryWeaponClass.Thrown)
                return TroopRole.Ranged;

            // ⑤ 双手重武器，或板甲+单手 → HeavyMelee
            if (input.PrimaryWeaponClass == PrimaryWeaponClass.TwoHanded) return TroopRole.HeavyMelee;
            if (input.Armor == ArmorGrade.Plate && input.PrimaryWeaponClass == PrimaryWeaponClass.OneHanded)
                return TroopRole.HeavyMelee;

            // ⑥ 其余近战（含徒手/单手轻甲）→ LightMelee
            return TroopRole.LightMelee;
        }
    }
}
