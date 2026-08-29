namespace CombatBrain.Intent
{
    // 意图引擎：score(i) = Σ V[k] × W[i][k]。
    // 防抖是生命线：新意图需超当前意图 15% 且维持 hold_time 才切换。
    public sealed class IntentEngine
    {
        private readonly float[][] _weights;
        private readonly float _switchMargin;  // 15%
        private readonly float _holdTime;      // 5~10s
        private IntentType _current;
        private float _currentScore;
        private float _holdTimer;

        public IntentEngine(float[][] weights, float switchMargin = 0.15f, float holdTime = 6f)
        {
            _weights = weights;
            _switchMargin = switchMargin;
            _holdTime = holdTime;
            _current = IntentType.Defend;
            _currentScore = 0f;
            _holdTimer = 0f;
        }

        public IntentType Current { get { return _current; } }

        // 返回 true 表示意图发生切换（out newIntent 为新意图）
        public bool Evaluate(SituationVector v, float dt, out IntentType newIntent)
        {
            newIntent = _current;

            int bestIndex = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < TacticManual.IntentCount; i++)
            {
                float s = 0f;
                float[] w = _weights[i];
                for (int d = 0; d < SituationVector.DimensionCount; d++)
                {
                    s += v[d] * w[d];
                }
                if (s > bestScore)
                {
                    bestScore = s;
                    bestIndex = i;
                }
            }

            IntentType candidate = TacticManual.Order[bestIndex];

            if (candidate != _current)
            {
                if (bestScore > _currentScore * (1f + _switchMargin))
                {
                    _holdTimer += dt;
                    if (_holdTimer >= _holdTime)
                    {
                        _current = candidate;
                        _currentScore = bestScore;
                        _holdTimer = 0f;
                        newIntent = _current;
                        return true;
                    }
                }
                else
                {
                    _holdTimer = 0f;
                }
            }
            else
            {
                _holdTimer = 0f;
                _currentScore = bestScore;
            }
            return false;
        }
    }
}