using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CombatBrain.Events;
using CombatBrain.Experience;

namespace CombatBrain.Ai
{
    // F10 反制矩阵 + 套路记忆 —— 胶水层（纯事件驱动）。
    // 职责：
    //   * CounterMatrix（反制换招）：订阅 IntentSwitched 事件；每次意图切换时对比敌队存活数变化——
    //     上升 → OnCountered(上一意图)（套路被反制，置信度 ×0.7，连续 3 次打入冷宫）；
    //     下降 → OnSuccess（置信度回补 +0.15）。新意图若已被冷宫 → PickBest 换招（AdjustIntent）。
    //   * CampaignHabits（跨战役套路记忆）：记录玩家队每次意图切换（惯用开局/打法）；战役开始载入历史
    //     并预测玩家惯用开局 → 按反制表预设我方开局（每队仅一次，不覆盖玩家队）；战役结束落盘
    //     %TEMP%\CombatBrain_habits.json（手动 JSON 序列化，零第三方依赖）——绝不做每帧 IO。
    // 性能预算：纯事件驱动（事件驱动 > 低频周期 > 每帧）——无轮询、无每帧逻辑；
    //   载入仅战役开始一次、落盘仅 OnMissionEnd 一次。
    // API 核实（BannerlordSage，游戏 v1.4.8）：
    //   * MissionBehavior.OnMissionStateFinalized() / OnClearScene() —— Mission.cs L2209/L1767 逐个调用
    //   * Team.ActiveAgents —— CombatAiMissionLogic.TickIntent 已在用（敌队存活数），无新增游戏 API
    //   * 事件源全部复用本 Mod CombatEventBus（IntentSwitched 由 TickIntent 发布），持久化用 BCL File/Path
    // 说明：OnAgentHit 的 AttackObserved 事件暂不接入（意图级反制已覆盖"换招"语义，攻击级留作后续增强）。
    public sealed class ExperienceGlue
    {
        private const string HabitsFileName = "CombatBrain_habits.json";

        private static readonly int[] AllTacticIntents =
        {
            (int)IntentType.Attack, (int)IntentType.Defend, (int)IntentType.Retreat,
            (int)IntentType.Flank, (int)IntentType.Harass, (int)IntentType.Reform,
            (int)IntentType.Pursue
        };

        private readonly Dictionary<int, CounterMatrix> _matrices = new Dictionary<int, CounterMatrix>();
        private readonly CampaignHabits _habits = new CampaignHabits();
        private readonly Dictionary<int, int> _playCounts = new Dictionary<int, int>(); // 落盘镜像
        private readonly HashSet<int> _openingApplied = new HashSet<int>();

        // 每队：上一意图 + 切换时敌队存活数（反制判定用）
        private readonly Dictionary<int, int> _lastIntent = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _lastEnemyAlive = new Dictionary<int, int>();

        private int _playerTeamIndex = -1;
        private Func<int, int> _enemyAliveOf;
        private bool _attached;
        private bool _saved; // 落盘幂等守卫（OnMissionStateFinalized / OnClearScene 双钩子只写一次）

        /// <summary>
        /// 战役开始接入：订阅事件总线 + 载入跨战役套路记忆（一次 IO）。
        /// </summary>
        /// <param name="bus">CombatEventBus（事件驱动源）</param>
        /// <param name="playerTeamIndex">玩家队索引（-1 无玩家队）</param>
        /// <param name="enemyAliveOf">给定队索引 → 敌队存活数（意图反制判定输入）</param>
        public void Attach(CombatEventBus bus, int playerTeamIndex, Func<int, int> enemyAliveOf)
        {
            if (_attached) return;
            _attached = true;
            _playerTeamIndex = playerTeamIndex;
            _enemyAliveOf = enemyAliveOf;
            bus.Subscribe(CombatEventKind.IntentSwitched, OnIntentSwitched);
            LoadHabits();
        }

        // 反制换招：新意图已被打入冷宫 → 挑置信度最高者；全冷宫/无可选 → 保持原意图
        public int AdjustIntent(int teamIndex, int proposedIntent)
        {
            CounterMatrix m = GetMatrix(teamIndex);
            if (!m.IsShunned(proposedIntent)) return proposedIntent;
            int best = m.PickBest(AllTacticIntents);
            return best >= 0 ? best : proposedIntent;
        }

        // 开局预判（每队仅一次，不覆盖玩家队）：预测玩家惯用开局 → 反制表给出我方开局意图
        public bool TryGetOpening(int teamIndex, out int intent)
        {
            intent = -1;
            if (_playerTeamIndex >= 0 && teamIndex == _playerTeamIndex) return false;
            if (_openingApplied.Contains(teamIndex)) return false;
            _openingApplied.Add(teamIndex);
            int predicted = _habits.Predict();
            if (predicted < 0) return false;
            intent = CounterFor(predicted);
            CombatLog.Log(string.Format(
                "[F10] 预判玩家开局套路 {0} → 我方开局 {1}", (IntentType)predicted, (IntentType)intent));
            return true;
        }

        // 战役结束落盘（幂等：双钩子只写一次，非每帧 IO）
        public void OnMissionEnd()
        {
            if (_saved) return;
            _saved = true;
            SaveHabits();
        }

        // ---------- 事件处理 ----------

        private void OnIntentSwitched(CombatEvent e)
        {
            if (e == null) return;
            int team = e.Id;

            // 玩家队：记录惯用打法（每次意图切换 = 一次套路）
            if (_playerTeamIndex >= 0 && team == _playerTeamIndex)
            {
                _habits.Record(e.IntValue);
                int c;
                _playCounts.TryGetValue(e.IntValue, out c);
                _playCounts[e.IntValue] = c + 1;
            }

            // 己方反制判定：对比上一意图切换时的敌队存活数
            int prev;
            int aliveNow = _enemyAliveOf != null ? Math.Max(0, _enemyAliveOf(team)) : 0;
            if (_lastIntent.TryGetValue(team, out prev) && prev >= 0)
            {
                int alivePrev;
                if (_lastEnemyAlive.TryGetValue(team, out alivePrev) && alivePrev >= 0 && aliveNow != alivePrev)
                {
                    CounterMatrix m = GetMatrix(team);
                    if (aliveNow > alivePrev) m.OnCountered(prev); // 敌队存活上升：套路被反制
                    else m.OnSuccess(prev);                        // 敌队存活下降：套路见效
                }
            }
            _lastIntent[team] = e.IntValue;
            _lastEnemyAlive[team] = aliveNow;
        }

        // ---------- 反制表（开局预判 → 我方开局） ----------

        private static int CounterFor(int predictedPlay)
        {
            switch ((IntentType)predictedPlay)
            {
                case IntentType.Attack: return (int)IntentType.Defend;  // 吸收冲锋
                case IntentType.Flank: return (int)IntentType.Defend;   // 护侧
                case IntentType.Harass: return (int)IntentType.Attack;  // 反压骚扰
                case IntentType.Defend: return (int)IntentType.Attack;  // 破防
                case IntentType.Retreat: return (int)IntentType.Pursue; // 追逃
                case IntentType.Reform: return (int)IntentType.Harass;  // 骚扰重整
                case IntentType.Pursue: return (int)IntentType.Reform;  // 避其锋芒
                default: return (int)IntentType.Attack;
            }
        }

        // ---------- 持久化（%TEMP%\CombatBrain_habits.json，手动 JSON 零依赖） ----------

        private string HabitsPath
        {
            get { return Path.Combine(Path.GetTempPath(), HabitsFileName); }
        }

        private void SaveHabits()
        {
            try
            {
                if (_playCounts.Count == 0) return; // 无记录不覆盖历史
                var sb = new StringBuilder();
                sb.Append("{\"counts\":{");
                bool first = true;
                foreach (KeyValuePair<int, int> kv in _playCounts)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
                }
                sb.Append("}}");
                File.WriteAllText(HabitsPath, sb.ToString());
                CombatLog.Log("[F10] 套路记忆已落盘: " + HabitsPath);
            }
            catch (Exception ex)
            {
                CombatLog.Log("[F10] 套路记忆落盘失败: " + ex.Message);
            }
        }

        private void LoadHabits()
        {
            try
            {
                if (!File.Exists(HabitsPath)) return;
                string json = File.ReadAllText(HabitsPath);
                foreach (Match m in Regex.Matches(json, "\"(\\d+)\":(\\d+)"))
                {
                    int id = int.Parse(m.Groups[1].Value);
                    int count = int.Parse(m.Groups[2].Value);
                    if (count <= 0) continue;
                    _playCounts[id] = count;
                    for (int i = 0; i < count; i++) _habits.Record(id);
                }
                CombatLog.Log(string.Format("[F10] 套路记忆已载入: {0} 种打法", _playCounts.Count));
            }
            catch (Exception ex)
            {
                CombatLog.Log("[F10] 套路记忆载入失败: " + ex.Message);
            }
        }

        private CounterMatrix GetMatrix(int teamIndex)
        {
            CounterMatrix m;
            if (!_matrices.TryGetValue(teamIndex, out m))
            {
                m = new CounterMatrix();
                _matrices[teamIndex] = m;
            }
            return m;
        }
    }
}
