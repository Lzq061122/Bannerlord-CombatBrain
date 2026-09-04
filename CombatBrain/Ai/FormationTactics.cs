namespace CombatBrain.Ai
{
    // T2-01 行军/战斗阵型三态切换（March/Approach/Threatened）—— 纯逻辑判定（可离线测）。
    // 纯枚举 + 纯函数，零 TaleWorlds 依赖；胶水层（CombatAiMissionLogic.TickFormationTactics）负责
    //   编队遍历 + SetArrangementOrder 映射（只切阵型，不动移动命令）。
    // 三态语义（阈值源文档《骑马与砍杀 II》战术 AI Mod.md §T2-01）：
    //   March      >80m：保持 Line 推进（不提前举盾，节约体力）；
    //   Approach   40~80m：Line；盾墙文化（斯特吉亚）或完整度低（<50，t65 重组）提前 ShieldWall；
    //   Threatened ≤40m 或敌方进入射程：ShieldWall（敌逼近/被射，举盾防御）。
    public enum FormationTacticsState
    {
        March = 0,
        Approach = 1,
        Threatened = 2
    }

    // 阵型决策结果（纯枚举，避免离线测试依赖 TaleWorlds ArrangementOrder；胶水层映射到 ArrangementOrderLine/ShieldWall）。
    public enum TacticsArrangement
    {
        Line = 0,
        ShieldWall = 1
    }

    public static class FormationTactics
    {
        public const float MarchDistance = 80f;        // >80m = March
        public const float ThreatenedDistance = 40f;   // ≤40m = Threatened
        public const float ReformIntegrity = 50f;      // 完整度 <50 提前 ShieldWall 重组（t65 FormationIntegrity）
        public const string ShieldWallCulture = "sturgia"; // 斯特吉亚：Approach 即举盾（盾墙文化）

        /// <summary>三态判定：敌进入射程或 ≤40m → Threatened；>80m → March；否则 Approach。</summary>
        public static FormationTacticsState Classify(float distToEnemy, bool enemyInRange)
        {
            if (enemyInRange || distToEnemy <= ThreatenedDistance) return FormationTacticsState.Threatened;
            if (distToEnemy > MarchDistance) return FormationTacticsState.March;
            return FormationTacticsState.Approach;
        }

        /// <summary>
        /// 阵型选择：Threatened=ShieldWall；March=Line；Approach=盾墙文化或完整度低（<50 重组）才 ShieldWall。
        /// </summary>
        public static TacticsArrangement SelectArrangement(FormationTacticsState state, string cultureId, float integrity)
        {
            if (state == FormationTacticsState.Threatened) return TacticsArrangement.ShieldWall;
            if (state == FormationTacticsState.March) return TacticsArrangement.Line;
            // Approach：斯特吉亚提前举盾，或完整度 <50 提前 ShieldWall 重组（t65）
            if (cultureId == ShieldWallCulture || integrity < ReformIntegrity) return TacticsArrangement.ShieldWall;
            return TacticsArrangement.Line;
        }
    }
}
