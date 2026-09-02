using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CombatBrain.Perception
{
    // 感知刷新管线：以观察者为中心，把“看得见”的 agent 写入 KnowledgeView。
    // 刷新频率：主帅 1s 一刷；个体 L0/L1 0.5s（此处骨架统一由外部控制 interval）。
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

        // P1-① 共享真实地形查表（开战构建后一次性注入转发给 VisibilityOracle；保持可逆）
        public void SetTerrain(TerrainSampler terrain)
        {
            _oracle.SetTerrain(terrain);
        }

        public void Update(float now, Vec2 observer, IEnumerable<Agent> agents)
        {
            if (now - _lastRefresh < _refreshInterval) return;
            _lastRefresh = now;

            float ox = observer.x;
            float oy = observer.y;

            foreach (Agent agent in agents)
            {
                if (agent == null || !agent.IsActive()) continue;
                Vec3 p = agent.Position;
                if (_oracle.IsVisible(ox, oy, p.x, p.y))
                {
                    _view.Observe(agent.Index, p.x, p.y, now);
                }
            }
        }
    }
}