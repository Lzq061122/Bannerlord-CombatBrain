using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using CombatBrain.Combat;
using CombatBrain.Compliance;

namespace CombatBrain.Ai
{
    // F12 决策抖动&服从度 —— 胶水层（横切包装器）。
    // 职责：把纯逻辑 DecisionJitter（CombatBrain/Compliance/DecisionJitter.cs，只读不侵入）
    // 接到个体层决策点（走位/格挡等）之前，产生拟人化行为：
    //   * 犹豫(Hesitate)   → 跳过本轮决策执行（低纪律偶发迟疑）
    //   * 延迟(DelaySeconds) → 决策提交后进入维持期，期间不翻转决策（指令起始迟疑，防抽搐）
    //   * 走形(Wobble)     → 扰动移动方向（士气崩/疲劳导致的动作走形）
    //
    // 性能预算（三条铁律落地）：
    //   * 仅在决策点调用（走位 0.2s / 格挡 0.1s 低频），绝不每帧重计算；
    //   * 每 agent 状态（Random/延迟计时/累计战斗时间）常驻字典，零每拍分配；
    //   * 剧场效应天然继承：只挂在已过 LOD 闸门的个体决策路径内（L2+/L3），可见区外不生效。
    // 输入映射（总指挥 Phase 3）：
    //   * discipline：FighterProfile.PredictConf 归一 → 0.2(新兵) .. 1.0(精锐)
    //   * fatigue  ：每 agent 累计战斗时间（活跃决策拍 × dt）/ 180s 封顶
    //   * morale   ：TeamState.LastVector.Moral（意图引擎 0.5s 一刷，非逐帧读取）
    // 反作弊闸：不新增任何游戏状态读取——输入全部来自已有 Context（FighterProfile/TeamState）。
    public sealed class DecisionJitterGlue
    {
        public const float FatigueCapSeconds = 180f;  // 连续战斗 3 分钟后疲劳满
        private const float MaxWobbleRadians = 0.6f;  // 走形最大转角 ≈ 34°

        private sealed class AgentState
        {
            public readonly Random Rng;
            public float DelayRemaining; // 决策维持期剩余秒数
            public float ActiveTime;     // 累计战斗时间（疲劳源）

            public AgentState(int seed)
            {
                Rng = new Random(seed); // 确定性种子：同 agent 行为可复现（离线回归友好）
            }
        }

        private readonly Dictionary<int, AgentState> _states = new Dictionary<int, AgentState>();

        // 纪律映射：PredictConf 0.35(新兵) ~ 0.85(精锐) → discipline 0.2 ~ 1.0
        public static float DisciplineFrom(FighterProfile profile)
        {
            float normalized = Clamp01((profile.PredictConf - 0.35f) / 0.5f);
            return 0.2f + 0.8f * normalized;
        }

        /// <summary>
        /// 决策执行前调用。返回 true=允许执行本轮决策；false=跳过（犹豫中/延迟维持期）。
        /// signedWobble：带符号走形幅度（-1..1），仅用于扰动非零移动向量。
        /// dt 语义：该 agent 距上次调用 ShouldAct 的「真实间隔（秒）」，由节流+跳帧的调用方
        /// （MovementGlue/SurvivalGlue/RangedGlue/TickBlock）按实际经过时间计算传入，不得传帧 dt——
        /// 否则维持期扣减（DelayRemaining）与疲劳累计（ActiveTime）按帧步进，慢 6~20 倍（P0-①）。
        /// </summary>
        public bool ShouldAct(int agentIndex, float dt, float discipline, float morale, out float signedWobble)
        {
            AgentState s = GetOrCreate(agentIndex);

            if (s.DelayRemaining > 0f)
            {
                s.DelayRemaining -= dt; // 维持期：不翻转决策
                signedWobble = 0f;
                return false;
            }

            s.ActiveTime += dt; // 活跃战斗时间累计（疲劳源）
            float fatigue = Math.Min(1f, s.ActiveTime / FatigueCapSeconds);

            ComplianceProfile profile = DecisionJitter.ComputeProfile(discipline, fatigue, morale);
            // X0 决策延迟开关：强度 0=果断（无犹豫/无延迟/无走形），1=现状，中间线性
            profile = DecisionDelay.ScaleProfile(profile, CombatBrainSettings.DecisionDelayStrength);
            JitterResult result = DecisionJitter.Apply(s.Rng, profile);

            if (result.Hesitate)
            {
                signedWobble = 0f;
                return false; // 本轮犹豫：跳过执行
            }

            s.DelayRemaining = result.DelaySeconds; // 提交决策并进入维持期
            signedWobble = result.Wobble * (s.Rng.NextDouble() < 0.5 ? -1f : 1f);
            return true;
        }

        // 把带符号走形（-1..1）旋转应用到移动方向（仅对非零向量扰，避免原地漂移）
        public static Vec2 ApplyWobble(Vec2 dir, float signedWobble)
        {
            float angle = signedWobble * MaxWobbleRadians;
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);
            return new Vec2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
        }

        public void Remove(int agentIndex)
        {
            _states.Remove(agentIndex);
        }

        private AgentState GetOrCreate(int agentIndex)
        {
            AgentState s;
            if (!_states.TryGetValue(agentIndex, out s))
            {
                s = new AgentState(agentIndex * 7919 + 12345);
                _states[agentIndex] = s;
            }
            return s;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
