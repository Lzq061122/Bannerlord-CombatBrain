namespace CombatBrain.Ai
{
    // X2 手活属性公式（纯逻辑，零 TaleWorlds 依赖，可离线测）。
    // 语义：个体执行层让 AI「格挡更准、射得更准、闪避更聪明」——改的是能力参数不是信息获取，
    //   反作弊闸不违规（观感补强派单 §2.2 明示：手活=能力非信息）。
    // 蓝本：RBM AgentAi.cs（research-rbm-ai.md §4.1）难度系数 0.7/0.85/1.0 vs 原版 0.1/0.32/0.96
    //   （低难度差近 7 倍）；ImprovedCombatAIAgentStatCalculateModel.cs（_mod_research 238 行蓝本）。
    // 设计：strength ∈ [0,1]（CombatBrainSettings.HandSkillStrength）——0=原版现值，1=满效果。
    //   上限锁死不超过原版最高难度对应值（拟人定位：重写后属性不得超原版最高难度，派单 §X2 合法性论证）。
    //   与 t19 AttackSuppression 协调：攻击概率基准在此给出，抑制时模型跳过覆盖（见 CombatBrainAgentStatCalculateModel）。
    public static class HandSkillProfile
    {
        // 原版难度系数（AgentStatCalculateModel.CalculateAILevel L162）：skill/300 × 系数
        public const float VanillaCoefLow = 0.10f;      // diff ≤ 0
        public const float VanillaCoefMid = 0.32f;      // diff ≤ 0.5
        public const float VanillaCoefHigh = 0.96f;     // diff > 0.5
        // RBM 难度系数（research-rbm-ai.md §4.1）
        public const float RbmCoefLow = 0.70f;
        public const float RbmCoefMid = 0.85f;
        public const float RbmCoefHigh = 1.00f;

        public const float SkillDenominator = 300f;     // 原版技能归一口径（skill/300）
        public const float MaxBlockAbility = 0.98f;     // 原版最高难度 AILevel=0.96 → lerp(0.5,0.99,sqrt(0.96))≈0.98（上限锁死）
        public const float MaxParryAbility = 0.95f;     // 原版最高难度 lerp(0.5,0.95,0.96)≈0.93，上限取 0.95
        public const float MinAbility = 0.30f;          // RBM clamp(base×2, 0.3, 1) 下限
        public const float VanillaShooterError = 0.008f;// 原版固定射击误差（SetAiRelatedProperties L198）
        public const float ShooterErrorBase = 0.015f;   // RBM 弓：0.015 − 0.007×等级
        public const float ShooterErrorSkillFactor = 0.007f;

        /// <summary>归一化技能等级（0~1，原版口径 skill/300）。</summary>
        public static float Skill01(float skill)
        {
            if (skill <= 0f) return 0f;
            if (skill >= SkillDenominator) return 1f;
            return skill / SkillDenominator;
        }

        /// <summary>难度系数：原版与 RBM 按 strength 线性插值（strength=0 原版、1 满 RBM）。</summary>
        public static float DifficultyCoef(float difficultyModifier, float strength)
        {
            float van = difficultyModifier <= 0f ? VanillaCoefLow
                : (difficultyModifier <= 0.5f ? VanillaCoefMid : VanillaCoefHigh);
            float rbm = difficultyModifier <= 0f ? RbmCoefLow
                : (difficultyModifier <= 0.5f ? RbmCoefMid : RbmCoefHigh);
            return van + (rbm - van) * Clamp01(strength);
        }

        /// <summary>AI 等级 = skill01 × 难度系数（0~1）。</summary>
        public static float AiLevel(float skill01, float difficultyModifier, float strength)
        {
            return Clamp01(skill01 * DifficultyCoef(difficultyModifier, strength));
        }

        /// <summary>格挡能力：clamp(base×(1+strength), 0.3, 0.98)——RBM 口径×2 顶到上限，锁死不超原版最高难度。</summary>
        public static float BlockAbility(float baseValue, float strength)
        {
            return Clamp(baseValue * (1f + strength), MinAbility, MaxBlockAbility);
        }

        /// <summary>招架能力：clamp(base×(1+strength), 0.3, 0.95)。</summary>
        public static float ParryAbility(float baseValue, float strength)
        {
            return Clamp(baseValue * (1f + strength), MinAbility, MaxParryAbility);
        }

        /// <summary>弓射击误差：lerp(原版 0.008, max(0.015−0.007×skill01, 0.001), strength)。skill01 已归一 0~1。</summary>
        public static float ShooterError(float skill01, float strength)
        {
            float s = Clamp01(skill01);
            float target = ShooterErrorBase - ShooterErrorSkillFactor * s;
            if (target < 0.001f) target = 0.001f;
            return VanillaShooterError + (target - VanillaShooterError) * Clamp01(strength);
        }

        /// <summary>攻击概率基准：clamp(base×(1+0.5×strength), 0.05, 1)——与 t19 AttackSuppression 协调（抑制时模型跳过覆盖）。</summary>
        public static float AttackChance(float baseValue, float strength)
        {
            return Clamp(baseValue * (1f + 0.5f * strength), 0.05f, 1f);
        }

        /// <summary>防守方向随机化（越低越准）：base×(1−0.5×strength) 下限 0。</summary>
        public static float DefendDirectionChance(float baseValue, float strength)
        {
            float v = baseValue * (1f - 0.5f * strength);
            return v < 0f ? 0f : v;
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }

        private static float Clamp(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }
    }
}
