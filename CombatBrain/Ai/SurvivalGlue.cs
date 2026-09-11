using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using CombatBrain.Events;
using CombatBrain.Lod;
using CombatBrain.Survival;

namespace CombatBrain.Ai
{
    // F7 编队级重整信号胶水 —— CB-PROMPT-0038 §五.5「F7 拆分」后的保留半边。
    //
    // 保留（编队级）：统计「编队成员新跌破濒死线（HP/上限 < 30%）」事件，5s 窗内 ≥8 次 →
    //   上报 IntentType.Reform 事件（编队重整信号），供意图/士气/命令层消费。
    // 退役（个体级，已删除）：单兵低血量自行寻找友军并调用 Agent.SetMovementDirection 后撤
    //   （原 SurvivalInstinct.Decide + ApplyRetreat + _retreating/_lastJitterTime 个体状态、
    //    F12 抖动闸门、旧逐兵敌情/友军扫描）。本类**不再有任何 Agent 写入**。
    //
    // 性能预算（三条铁律落地）：
    //   * 0.3s 低频编队级节拍；每拍只做一次编队遍历 + O(1) 事件累计入队（无位置/敌情扫描）；
    //   * 剧场效应：只统计 L2+（玩家可见战斗区）成员的事件，L0/L1 交原版；
    //   * 反作弊闸：只读己方成员的公开血量（Health/BaseHealthLimit），零读指针/全图/隐藏态。
    // 玩家队豁免：本类无任何写入面；编队级信号不改变玩家队行为。
    // 与 F6 的关系：旧「求生撤退优先于走位」的耦合随 F6/F7 个体腿退役一并消失。
    public sealed class SurvivalGlue
    {
        public const float TickInterval = 0.3f;

        // 濒死线：与退役前 SurvivalInstinct.HpThreshold 同值（0.30），保持事件口径不变
        private const float HpThreshold = 0.30f;

        private float _sinceTick;
        private int _phase;

        // 每 agent 上一拍「是否已在濒死态」（仅用于取"新跌破"边沿；非战术状态，不驱动任何动作）
        private readonly Dictionary<int, bool> _wasCritical = new Dictionary<int, bool>();

        // 编队层：每队一个重伤窗口 + 触发武装（单发触发，窗内事件数回落后自动重新武装）
        private readonly Dictionary<int, CasualtyReformSignal> _reformByTeam = new Dictionary<int, CasualtyReformSignal>();
        private readonly Dictionary<int, bool> _reformArmed = new Dictionary<int, bool>();

        /// <summary>
        /// 低频驱动入口：CombatAiMissionLogic.OnMissionTick 每帧调用，内部按 TickInterval 节流。
        /// 只统计编队级重伤事件并发布重整信号；**不调用任何 Agent 写入 API**。
        /// </summary>
        public void Tick(Mission mission, float dt, float now,
            Func<int, LodLevel> getLod,
            Action<CombatEvent> publish)
        {
            _sinceTick += dt;
            if (_sinceTick < TickInterval) return;
            _sinceTick = 0f;
            if (mission == null) return;

            int agentCount = mission.Agents.Count;
            _phase = (_phase + 1) % 64;

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive() || !agent.IsAIControlled) continue;
                if (!agent.IsHuman) continue;
                Team team = agent.Team;
                if (team == null) continue;

                // 剧场闸门：L0/L1 不参与统计（可见区外优雅降级，交原版）
                LodLevel lod = getLod(agent.Index);
                if (lod < LodLevel.L2) continue;

                // 跳帧聚合：只摊薄簿记成本，不改变「新跌破」事件累计口径
                int step = BehaviorLodScheduler.StepFor(lod, agentCount);
                if (((agent.Index + _phase) % step) != 0) continue;

                TrackCritical(agent.Index, team, ComputeHpRatio(agent), now, publish);
            }
        }

        /// <summary>Agent 生命周期清理（防每 Agent 簿记字典只增不减）。</summary>
        public void Remove(int agentIndex)
        {
            _wasCritical.Remove(agentIndex);
        }

        /// <summary>队/编队清理（Mission 收尾）。</summary>
        public void RemoveTeam(int teamIndex)
        {
            _reformByTeam.Remove(teamIndex);
            _reformArmed.Remove(teamIndex);
        }

        // ---------- 编队层：重伤事件窗口 → 重整信号 ----------

        // 读公开血量比（Health/BaseHealthLimit）；无上限兜底视为濒死（与退役前同口径）
        private static float ComputeHpRatio(Agent agent)
        {
            float limit = agent.BaseHealthLimit;
            if (limit <= 0f) return 0f;
            float ratio = agent.Health / limit;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }

        private void TrackCritical(int agentIndex, Team team, float hpRatio, float now, Action<CombatEvent> publish)
        {
            bool was;
            if (!_wasCritical.TryGetValue(agentIndex, out was)) was = false;
            bool isCritical = hpRatio < HpThreshold;
            _wasCritical[agentIndex] = isCritical;

            if (!isCritical || was) return; // 只统计"新跌破濒死线"事件

            CasualtyReformSignal signal;
            if (!_reformByTeam.TryGetValue(team.TeamIndex, out signal))
            {
                signal = new CasualtyReformSignal();
                _reformByTeam[team.TeamIndex] = signal;
            }

            bool windowFull = signal.OnCriticalCasualty(now); // 入队 + 5s 窗判断（纯逻辑内部阈值=8）

            bool armed;
            if (!_reformArmed.TryGetValue(team.TeamIndex, out armed)) armed = true;

            if (armed && windowFull)
            {
                if (publish != null)
                {
                    publish(new CombatEvent
                    {
                        Kind = CombatEventKind.IntentSwitched,
                        Time = now,
                        Id = team.TeamIndex,
                        IntValue = (int)IntentType.Reform
                    });
                }
                CombatLog.Log(string.Format("[F7] Team {0} 编队重伤触发重整信号", team.TeamIndex));
                DecisionObservatory.Observe(DecisionObservatory.BSurvReform); // T1 决策分支计数
                _reformArmed[team.TeamIndex] = false; // 单发触发
            }
            else if (!windowFull)
            {
                _reformArmed[team.TeamIndex] = true; // 窗内事件数回落到阈值下 → 重新武装
            }
        }
    }
}
