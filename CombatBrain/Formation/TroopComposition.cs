namespace CombatBrain.Formations
{
    // X5 编队构成统计（TroopComposition）—— 纯逻辑数据结构（零 TaleWorlds 依赖，可离线测）。
    // 语义：编队里各角色轴兵力的低频聚合快照。X5 的布局入口（设计稿 §五 分流规则）：
    //   * 以 Ranged 为主 → RangedLooseLayout（远程通道，间距受交战压力调制，不经 T2-01）；
    //   * 以近战为主 → 消费 T2-01 已选结果：ShieldWall → ShieldWallLayout、Line → LineLayout。
    // 聚合语义：本结构只是「输入参数容器」，不做逐兵遍历（禁逐帧逐兵，铁律②）——
    //   由胶水层 0.5s 编队级节拍把 TroopClassifier 逐兵结果聚合成一份快照喂入（X5-6 挂接）。
    // 分流阈值：RangedRatio ≥ 0.5 = 以 Ranged 为主（Ranged 数量过半才走远程通道；
    //   混编近战为主时不误入 RangedLoose——验收断言⑬）。
    // 命名空间用 CombatBrain.Formations（复数，t65 CS0118 教训，与 FormationIntegrity.cs 同惯例）。
    public struct TroopComposition
    {
        public const float RangedDominantRatio = 0.5f; // §五：以 Ranged 为主的阈值（过半）

        public int ShieldBearer; // 盾兵（盾墙前排核心）
        public int Polearm;      // 长杆步战（反骑/后排架枪）
        public int Ranged;       // 弓/弩
        public int HeavyMelee;   // 板甲重装近战
        public int LightMelee;   // 轻甲近战
        public int Cavalry;      // 骑乘（X5 布局阶段不参与近战阵站位）

        public int Total { get { return ShieldBearer + Polearm + Ranged + HeavyMelee + LightMelee + Cavalry; } }

        /// <summary>Ranged 占比（0..1；Total==0 时返回 0，避免除零）。</summary>
        public float RangedRatio
        {
            get
            {
                int total = Total;
                return total > 0 ? (float)Ranged / total : 0f;
            }
        }

        /// <summary>§五 分流判定：以 Ranged 为主（占比 ≥ 0.5）→ 走远程通道。</summary>
        public bool IsRangedDominant
        {
            get { return RangedRatio >= RangedDominantRatio; }
        }

        /// <summary>近战合计（ShieldBearer+Polearm+HeavyMelee+LightMelee；Cavalry 不计——骑乘走骑兵波次 F8）。</summary>
        public int MeleeTotal { get { return ShieldBearer + Polearm + HeavyMelee + LightMelee; } }
    }
}
