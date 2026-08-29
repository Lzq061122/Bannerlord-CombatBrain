namespace CombatBrain.Combat
{
    // 预测式格挡：只吃攻击历史统计 + 可见姿态，绝不读玩家下一帧输入。
    // “带置信度的赌博”：永远正确的防守是无趣的。被虚招诱导则破绽大开。
    public static class PredictiveBlock
    {
        private const float Threshold = 0.5f;

        // defender 的素质决定预测置信度下限；attacker 的虚招频率越高越难被预测。
        public static bool TryPredict(
            AttackStats attackerStats,
            FighterProfile defender,
            float attackerFeintRate,
            out AttackDir predictedAttack,
            out float confidence)
        {
            AttackDir p = attackerStats.Predict(out float raw);
            confidence = raw * defender.PredictConf * (1f - attackerFeintRate);

            if (confidence >= Threshold)
            {
                predictedAttack = p;
                return true;
            }

            predictedAttack = AttackDir.None;
            return false;
        }

        // 攻击方向 → 防守方向（同向对应：见左刀则左挡）
        public static AttackDir ToBlockDir(AttackDir attack)
        {
            return attack;
        }
    }
}