namespace CombatBrain.Perception
{
    // 感知刷新管线（atc4-perception-fix 重构，P0-1）——以观察者为中心，把「看得见的敌编队中心」写入
    // KnowledgeView，供下游 Glue 经查询过滤（未见/超期 → 视为不存在，保守降级）。
    // P0-1 修复：
    //   ①「本拍是否允许刷新」与「观察单个目标」解耦——TryBeginRefresh(now) 一次性批量刷新门控 +
    //     ObserveFormation 无内部节流（调用方在 TryBeginRefresh 成功后批量调用，同一批次可写多个编队）；
    //   ②删除无下游消费者的全场 Agent 感知扫描（原 Update(agents) 的 agent.Index 观察经 grep 核实零
    //     消费者——KnowledgeView 唯一消费点为 FormationKey 编队级查询，CombatAiMissionLogic.cs
    //     IsEnemyFormationKnown L431）；
    //   ③Agent 观察删除后 KnowledgeView ID 域只剩 FormationKey（teamIndex*100+formationIndex），
    //     无 Agent/Formation 整数 ID 碰撞（原 agent.Index 与 FormationKey 同 int 域可能撞号）。
    // 节流：refreshInterval 内 TryBeginRefresh 返回 false（调用方跳过整段观察）；FogOfWar 关闭时胶水层
    //   跳过感知段（现状直通行为不变，本层不感知 Fog 开关——关闭不新增信息）。
    // 反作弊闸：只写「观察者可见」的编队中心（VisibilityOracle 距离门+LoS），零新增禁读；
    //   零 TaleWorlds 依赖（可离线测完整 PerceptionPipeline 流程，非只测 KnowledgeView/PerceptionFilter）。
    public sealed class PerceptionPipeline
    {
        private readonly KnowledgeView _view;
        private readonly VisibilityOracle _oracle;
        private readonly float _refreshInterval;
        private float _lastRefresh = -1000f;

        public PerceptionPipeline(KnowledgeView view, float range, float refreshInterval)
        {
            _view = view;
            _oracle = new VisibilityOracle(range);
            _refreshInterval = refreshInterval;
        }

        // P1-① 共享真实地形查表（开战构建后一次性注入转发给 VisibilityOracle；保持可逆——不注入即平地降级）
        public void SetTerrain(TerrainSampler terrain)
        {
            _oracle.SetTerrain(terrain);
        }

        /// <summary>
        /// 本拍是否允许刷新（一次性批量门控，P0-1）：距上次刷新 ≥ refreshInterval 才返回 true 并推进
        /// _lastRefresh；返回 false 时调用方应跳过本拍全部观察（周期未到不刷新）。与观察方法解耦——
        /// 刷新成功后可批量调用无内部节流的 ObserveFormation，互不干扰。
        /// </summary>
        public bool TryBeginRefresh(float now)
        {
            if (now - _lastRefresh < _refreshInterval) return false;
            _lastRefresh = now;
            return true;
        }

        /// <summary>
        /// 编队级观察（无内部节流，P0-1）：可见敌编队中心写入 KnowledgeView。
        /// 必须在 TryBeginRefresh 成功后调用；可见性=距离门+LoS（VisibilityOracle.IsVisible）。
        /// </summary>
        public void ObserveFormation(float now, float ox, float oy, int formationId, float x, float y)
        {
            if (_oracle.IsVisible(ox, oy, x, y))
            {
                _view.Observe(formationId, x, y, now);
            }
        }
    }
}
