namespace CombatBrain.Lod
{
    // 行为 LOD 调度器：剧场效应落地。玩家聚焦球内才爬 L2/L3，球外 L0/L1。
    public static class BehaviorLodScheduler
    {
        public const float L3MeleeRadius = 3.5f;
        public const float L2CombatRadius = 25f;
        public const float PlayerFocusRadius = 70f;

        public static LodLevel Evaluate(float distToPlayerFocus, float distToNearestEnemy)
        {
            if (distToNearestEnemy <= L3MeleeRadius) return LodLevel.L3;
            if (distToPlayerFocus <= PlayerFocusRadius && distToNearestEnemy <= L2CombatRadius) return LodLevel.L2;
            if (distToNearestEnemy <= L2CombatRadius * 2f) return LodLevel.L1;
            return LodLevel.L0;
        }

        // 决策步长(帧)：人数越多，L2 步长越大（500 人战场 L2 拉到 6~8 帧）。
        public static int StepFor(LodLevel level, int agentCount)
        {
            switch (level)
            {
                case LodLevel.L0: return 12 + agentCount / 100;
                case LodLevel.L1: return 8 + agentCount / 150;
                case LodLevel.L2: return 6 + agentCount / 80;
                case LodLevel.L3: return 1; // 纯事件驱动，几乎不轮询
            }
            return 6;
        }

        public static float DecisionInterval(LodLevel level)
        {
            switch (level)
            {
                case LodLevel.L0: return 0.75f;
                case LodLevel.L1: return 0.25f;
                case LodLevel.L2: return 0.25f;
                case LodLevel.L3: return 0.1f;
            }
            return 0.5f;
        }
    }
}