using System;
using System.Collections.Generic;
using System.Globalization;

namespace CombatBrain.Intent
{
    // F2 对手建模 —— 纯逻辑层（零游戏依赖，可离线测试）。
    // 依据：t16 §2.1 八信号先验表（+CeaseFire 停火信号）、t11 B1（信念向量）、t10 佯退有序度折中、
    //       t11 L164（CeaseFire 0.4 封顶）、抗作弊读取集 t12 行19 / t16 附 L131。
    // 机制：敌方意图信念 B[6]（EnemyIntent 列序）= 加权证据累积：B[i] += Σ_s P(i|s) × weight_s；
    //       时间窗衰减（半衰期默认 8s）；每信号 3s 冷却（事件驱动 > 低频 > 每帧，防同事件反复刷分）；
    //       读取时归一化；置信度 < 0.5 或 熵 > 1.8 → "读不懂"（退化：上层不注入信念）。
    // 信号权重 weight_s ∈ [0,1]：由胶水层按"强度 × 距离折扣"算好后传入（距离折扣在胶水层完成）。
    public enum SignalKind
    {
        SkeinCharge = 0,        // 楔形阵步兵冲锋      → FrontalAttack +0.80
        ShieldWallHold = 1,     // 盾墙据守            → FrontalAttack -0.20 / Reform +0.30
        SkirmisherClose = 2,    // 散兵逼近            → FrontalAttack +0.75
        ReserveMove = 3,        // 预备队位移          → Flank +0.70
        CavalryFlankGather = 4, // 骑兵绕侧集结        → Flank +0.85
        HeavyCharge = 5,        // 重骑正面冲击        → FrontalAttack +0.80（受 CeaseFire 0.4 封顶）
        HorseArcherHarass = 6,  // 骑射袭扰            → Skirmish +0.60
        FeignedRetreat = 7,     // 有序佯退            → FeignRetreat +0.50（无序溃退=真退，不更新）
        CeaseFire = 8           // 停火（正面强压暂缓）→ FrontalAttack 信念封顶 0.4（t11 L164）
    }

    // 单个信号的先验配置（t16 §2.1）。
    public sealed class SignalPrior
    {
        public string Name;     // 与 SignalKind 同名（JSON 配置按名匹配）
        public float[] Priors;  // 6 维，列序 = EnemyIntent
        public float FrontalCap; // CeaseFire 专用：>0 时作为 FrontalAttack 信念封顶（t11 L164）
    }

    // 先验表：数据驱动配置（CL 配置化教训）+ 内置常量兜底。
    // 游戏侧从 ModuleData/opponent_priors.json 加载；解析失败/文件缺失 → BuildDefault()。
    public sealed class PriorTable
    {
        public const int SignalCount = 9;

        public SignalPrior[] Signals; // 按 SignalKind 序索引（null = 该信号未配置，忽略）

        // t16 §2.1 八信号先验 + CeaseFire 封顶（内置常量表，零依赖兜底）。
        public static PriorTable BuildDefault()
        {
            var t = new PriorTable { Signals = new SignalPrior[SignalCount] };
            t.Signals[(int)SignalKind.SkeinCharge] = new SignalPrior { Name = "SkeinCharge", Priors = new float[] { 0.80f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f } };
            t.Signals[(int)SignalKind.ShieldWallHold] = new SignalPrior { Name = "ShieldWallHold", Priors = new float[] { -0.20f, 0.00f, 0.00f, 0.00f, 0.00f, 0.30f } };
            t.Signals[(int)SignalKind.SkirmisherClose] = new SignalPrior { Name = "SkirmisherClose", Priors = new float[] { 0.75f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f } };
            t.Signals[(int)SignalKind.ReserveMove] = new SignalPrior { Name = "ReserveMove", Priors = new float[] { 0.00f, 0.70f, 0.00f, 0.00f, 0.00f, 0.00f } };
            t.Signals[(int)SignalKind.CavalryFlankGather] = new SignalPrior { Name = "CavalryFlankGather", Priors = new float[] { 0.00f, 0.85f, 0.00f, 0.00f, 0.00f, 0.00f } };
            t.Signals[(int)SignalKind.HeavyCharge] = new SignalPrior { Name = "HeavyCharge", Priors = new float[] { 0.80f, 0.00f, 0.00f, 0.00f, 0.00f, 0.00f } };
            t.Signals[(int)SignalKind.HorseArcherHarass] = new SignalPrior { Name = "HorseArcherHarass", Priors = new float[] { 0.00f, 0.00f, 0.00f, 0.60f, 0.00f, 0.00f } };
            t.Signals[(int)SignalKind.FeignedRetreat] = new SignalPrior { Name = "FeignedRetreat", Priors = new float[] { 0.00f, 0.00f, 0.00f, 0.00f, 0.50f, 0.00f } };
            t.Signals[(int)SignalKind.CeaseFire] = new SignalPrior { Name = "CeaseFire", Priors = new float[6], FrontalCap = 0.4f };
            return t;
        }

        // 最小手写 JSON 解析（零依赖，供 ModuleData/opponent_priors.json）。
        // 合法 schema：{ "signals": [ { "name": "SkeinCharge", "priors": [0.8,0,0,0,0,0], "frontalCap": 0.4 }, ... ] }
        // 任何解析错误/未知信号名/priors 长度≠6 → 返回 null（上层回退 BuildDefault）。
        public static PriorTable ParseJson(string json)
        {
            if (json == null) return null;
            var signals = new SignalPrior[SignalCount];
            try
            {
                int i = 0;
                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != '{') return null;
                i++;
                while (true)
                {
                    SkipWs(json, ref i);
                    if (i >= json.Length) return null;
                    if (json[i] == '}') { i++; break; }
                    if (json[i] != '"') return null;
                    string key = ReadString(json, ref i);
                    SkipWs(json, ref i);
                    if (i >= json.Length || json[i] != ':') return null;
                    i++;
                    SkipWs(json, ref i);
                    if (key == "signals")
                    {
                        if (i >= json.Length || json[i] != '[') return null;
                        i++;
                        while (true)
                        {
                            SkipWs(json, ref i);
                            if (i >= json.Length) return null;
                            if (json[i] == ']') { i++; break; }
                            if (json[i] != '{') return null;
                            i++;
                            string name = null;
                            float[] priors = null;
                            float frontalCap = 0f;
                            bool capSet = false;
                            while (true)
                            {
                                SkipWs(json, ref i);
                                if (i >= json.Length) return null;
                                if (json[i] == '}') { i++; break; }
                                if (json[i] != '"') return null;
                                string fk = ReadString(json, ref i);
                                SkipWs(json, ref i);
                                if (i >= json.Length || json[i] != ':') return null;
                                i++;
                                SkipWs(json, ref i);
                                if (fk == "name") name = ReadString(json, ref i);
                                else if (fk == "priors") priors = ReadFloatArray(json, ref i);
                                else if (fk == "frontalCap") { frontalCap = ReadNumber(json, ref i); capSet = true; }
                                else SkipValue(json, ref i);
                                SkipWs(json, ref i);
                                if (i < json.Length && json[i] == ',') i++;
                            }
                            if (name == null || priors == null || priors.Length != 6) return null;
                            SignalKind kind;
                            if (!Enum.TryParse<SignalKind>(name, true, out kind)) return null;
                            signals[(int)kind] = new SignalPrior
                            {
                                Name = name,
                                Priors = priors,
                                FrontalCap = capSet ? frontalCap : 0f
                            };
                            SkipWs(json, ref i);
                            if (i < json.Length && json[i] == ',') i++;
                        }
                    }
                    else
                    {
                        SkipValue(json, ref i);
                    }
                    SkipWs(json, ref i);
                    if (i < json.Length && json[i] == ',') i++;
                }
            }
            catch (FormatException)
            {
                return null;
            }
            return new PriorTable { Signals = signals };
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException();
            i++;
            var sb = new System.Text.StringBuilder();
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\')
                {
                    if (i + 1 >= s.Length) throw new FormatException();
                    sb.Append(s[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '"') { i++; return sb.ToString(); }
                sb.Append(c);
                i++;
            }
            throw new FormatException();
        }

        private static float[] ReadFloatArray(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '[') throw new FormatException();
            i++;
            var list = new List<float>();
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException();
                if (s[i] == ']') { i++; break; }
                list.Add(ReadNumber(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') i++;
            }
            return list.ToArray();
        }

        private static float ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '.' || s[i] == '+' || s[i] == 'e' || s[i] == 'E')) i++;
            if (i == start) throw new FormatException();
            return float.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static void SkipValue(string s, ref int i)
        {
            if (i >= s.Length) throw new FormatException();
            if (s[i] == '"') { ReadString(s, ref i); return; }
            if (s[i] == '{' || s[i] == '[')
            {
                char open = s[i];
                char close = open == '{' ? '}' : ']';
                i++;
                int depth = 1;
                while (i < s.Length && depth > 0)
                {
                    char c = s[i];
                    if (c == '"') { ReadString(s, ref i); continue; }
                    if (c == open) depth++;
                    else if (c == close) depth--;
                    i++;
                }
                if (depth != 0) throw new FormatException();
                return;
            }
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '.' || s[i] == '+' || s[i] == 'e' || s[i] == 'E')) i++;
        }
    }

    // 信念累积器：B[i] += Σ_s P(i|s) × weight_s；Tick 时间窗衰减；读取归一化 + 退化判定。
    public sealed class OpponentModel
    {
        private readonly SignalPrior[] _priors;          // 按 SignalKind 序（null = 未配置）
        private readonly float[] _belief = new float[TacticManual.EnemyIntentCount]; // 未归一化累积权重
        private readonly float[] _lastSignalTime = new float[PriorTable.SignalCount]; // 每信号最近触发时刻
        private readonly float _confidenceThreshold;     // 0.5
        private readonly float _entropyThreshold;        // 1.8
        private readonly float _decayHalfLife;           // 8s
        private readonly float _signalCooldown;          // 3s
        private float _lastTickTime;

        public OpponentModel(PriorTable table,
            float confidenceThreshold = 0.5f,
            float entropyThreshold = 1.8f,
            float decayHalfLife = 8f,
            float signalCooldown = 3f)
        {
            _priors = (table != null && table.Signals != null) ? table.Signals : PriorTable.BuildDefault().Signals;
            _confidenceThreshold = confidenceThreshold;
            _entropyThreshold = entropyThreshold;
            _decayHalfLife = decayHalfLife;
            _signalCooldown = signalCooldown;
            for (int i = 0; i < _lastSignalTime.Length; i++) _lastSignalTime[i] = float.NegativeInfinity;
            _lastTickTime = float.MinValue;
        }

        // 证据累积：CeaseFire 特殊处理为"封顶"而非加分（t11 L164）；同信号 3s 冷却防刷分。
        public void ApplySignal(SignalKind kind, float weight, float now)
        {
            int k = (int)kind;
            if (weight <= 0f || _priors == null || k < 0 || k >= _priors.Length) return;
            SignalPrior p = _priors[k];
            if (p == null) return;
            if (now - _lastSignalTime[k] < _signalCooldown) return; // 冷却：同信号 3s 内不重复
            _lastSignalTime[k] = now;

            if (p.FrontalCap > 0f)
            {
                // CeaseFire：正面强压暂缓 → FrontalAttack 信念封顶（t11 L164 0.4）
                _belief[(int)EnemyIntent.FrontalAttack] = Math.Min(_belief[(int)EnemyIntent.FrontalAttack], p.FrontalCap);
                return;
            }
            for (int i = 0; i < _belief.Length && i < p.Priors.Length; i++)
            {
                _belief[i] += p.Priors[i] * weight; // 距离折扣已由胶水层计入 weight
            }
        }

        // 时间窗衰减：半衰期指数衰减；间隔 >60s（读档/开场）不清零防误判。
        public void Tick(float now)
        {
            float dt = now - _lastTickTime;
            _lastTickTime = now;
            if (dt <= 0f || dt >= 60f) return;
            float scale = (float)Math.Pow(0.5, dt / _decayHalfLife);
            for (int i = 0; i < _belief.Length; i++) _belief[i] *= scale;
        }

        // 正钳位后的原始累积权重（诊断/测试用）。
        public float[] BeliefRaw()
        {
            var r = new float[_belief.Length];
            for (int i = 0; i < _belief.Length; i++) r[i] = _belief[i] > 0f ? _belief[i] : 0f;
            return r;
        }

        // 归一化信念（和为 1；无证据 → 均匀分布 → 熵最大 → 读不懂）。
        public float[] SnapshotBelief()
        {
            float[] raw = BeliefRaw();
            float sum = 0f;
            for (int i = 0; i < raw.Length; i++) sum += raw[i];
            var b = new float[raw.Length];
            if (sum > 1e-6f)
            {
                for (int i = 0; i < b.Length; i++) b[i] = raw[i] / sum;
            }
            else
            {
                for (int i = 0; i < b.Length; i++) b[i] = 1f / b.Length;
            }
            return b;
        }

        // 可读判定：置信度（最大信念）≥ 阈值 且 熵 ≤ 阈值；否则"读不懂"→ 上层不注入信念。
        public bool TryReadDominant(out EnemyIntent intent, out float confidence, out float entropy)
        {
            float[] b = SnapshotBelief();
            int best = 0;
            for (int i = 1; i < b.Length; i++) if (b[i] > b[best]) best = i;
            confidence = b[best];
            entropy = 0f;
            for (int i = 0; i < b.Length; i++)
            {
                if (b[i] <= 0f) continue;
                entropy -= b[i] * (float)Math.Log(b[i], 2.0);
            }
            intent = (EnemyIntent)best;
            return confidence >= _confidenceThreshold && entropy <= _entropyThreshold;
        }

        public bool TryReadDominant(out EnemyIntent intent, out float confidence)
        {
            float entropy;
            return TryReadDominant(out intent, out confidence, out entropy);
        }
    }
}
