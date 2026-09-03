namespace CombatBrain.Intent
{
    // F2 信念增强意图引擎：score(i) = ΣV·W + ΣB·C。
    // 与 F1 IntentEngine 同构（同一防抖协议：新意图需超当前 15% 且维持 holdTime 才切换），
    // 仅评分多一项信念修正（t11 B1/B2：C 矩阵锚点）。
    // 信念注入纪律（t16 §2.1）：仅当 OpponentModel 可读（置信度≥0.5 且熵≤1.8）才传非空 belief；
    // belief == null（读不懂/无信念）→ 退化为纯反应式，评分与 IntentEngine 完全一致（向后兼容）。
    // 防抖状态在读/读不懂切换间连续保持：读不懂时按反应式评分续走同一 hold 流程，不重置。
    public sealed class BeliefIntentEngine
    {
        private readonly float[][] _weights;
        private readonly string _cultureId;    // t69：文化战术模板（null=默认表=帝国）
        private readonly float _switchMargin;  // 15%
        private readonly float _holdTime;      // 6s（与 F1 一致）
        private IntentType _current;
        private float _currentScore;
        private float _holdTimer;

        // P2-④：删除死参数 counter（review-findings-2026-09-01 P2）——原参数从未存储/使用：
        // 评分经 TacticManual.CombinedScore 统一走静态 Counter 矩阵（与 weights 同源，均来自
        // TacticManual 静态矩阵），传入的 BuildCounter() 实参无任何效果。构造调用点已同步
        // （CombatAiMissionLogic L28、OfflineTests F2 段）。
        // t69：新增可选 cultureId（null=默认表，向后兼容）——非空时 Evaluate 走
        // TacticManual.CombinedScoreFor(cultureId,...)（文化分表 §2.2 六文化权重），
        // 防抖/C 矩阵/信念注入语义不变。
        public BeliefIntentEngine(float[][] weights, string cultureId = null, float switchMargin = 0.15f, float holdTime = 6f)
        {
            _weights = weights;
            _cultureId = cultureId;
            _switchMargin = switchMargin;
            _holdTime = holdTime;
            _current = IntentType.Defend;
            _currentScore = 0f;
            _holdTimer = 0f;
        }

        public IntentType Current { get { return _current; } }

        // 返回 true 表示意图发生切换（out newIntent 为新意图）。
        // belief == null → 纯反应式（ΣV·W，与 F1 一致）。
        public bool Evaluate(SituationVector v, float[] belief, float dt, out IntentType newIntent)
        {
            newIntent = _current;

            int bestIndex = 0;
            float bestScore = float.MinValue;
            for (int i = 0; i < TacticManual.IntentCount; i++)
            {
                // t69：文化分表评分（cultureId=null 走默认表=帝国）
                float s = _cultureId == null
                    ? TacticManual.CombinedScore(v, belief, i)
                    : TacticManual.CombinedScoreFor(_cultureId, v, belief, i);
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
