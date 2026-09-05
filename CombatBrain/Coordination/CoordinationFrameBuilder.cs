using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain.Ai;
using CombatBrain.Formations;

namespace CombatBrain.Coordination
{
    // atc4-snapshot-optimization OPT-1/OPT-4：单拍协调帧快照构建器（CoordinationFrameBuilder）。
    // 职责：从 Team 的公开编队聚合构建 FormationCoordSnapshot 列表（己方全量 + 敌迷雾过滤），
    //   供 ArmyContext / Mission / Commitment 在同一 0.5s 协调周期内复用（禁止同拍重复构建 ownSnaps/enemySnaps）。
    // FogOfWar 标记在快照构建阶段统一确定（isKnown 委托=IsEnemyFormationKnown，未知敌 IsKnown=false）。
    // 反作弊闸：只读编队公开聚合（CountOfUnits/QuerySystem 兵种旗标/CachedMedianPosition/
    //   CachedFormationIntegrityData）+ 迷雾过滤，零新增禁读、零逐兵扫描。
    // 注意：本文件依赖 TaleWorlds，属胶水侧（OfflineTests 不链接；纯判定逻辑见 CoordinationController）。
    public static class CoordinationFrameBuilder
    {
        /// <summary>构建己方全部非空编队快照（O(F)）。outList 由调用方清空复用（拍内值列表，非枚举器缓存）。</summary>
        public static void BuildOwn(Team own, List<FormationCoordSnapshot> outList)
        {
            if (own == null || outList == null) return;
            foreach (Formation f in own.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                outList.Add(BuildSnapshot(f, true));
            }
        }

        /// <summary>构建敌编队快照（O(F)，迷雾过滤统一在此阶段确定：isKnown=false 的编队 IsKnown=false）。</summary>
        public static void BuildEnemy(Team own, Team enemy,
            System.Func<Team, Formation, bool> isEnemyFormationKnown, List<FormationCoordSnapshot> outList)
        {
            if (enemy == null || outList == null) return;
            foreach (Formation f in enemy.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                bool known = isEnemyFormationKnown != null && isEnemyFormationKnown(own, f);
                outList.Add(BuildSnapshot(f, known));
            }
        }

        /// <summary>编队快照（QuerySystem 聚合；Integrity 用 CachedFormationIntegrityData 只读）。</summary>
        public static FormationCoordSnapshot BuildSnapshot(Formation f, bool isKnown)
        {
            var kind = f.QuerySystem.IsRangedCavalryFormation ? FormationCoordKind.RangedCavalry
                : f.QuerySystem.IsCavalryFormation ? FormationCoordKind.MeleeCavalry
                : f.QuerySystem.IsRangedFormation ? FormationCoordKind.Ranged
                : f.QuerySystem.IsInfantryFormation ? FormationCoordKind.Infantry
                : FormationCoordKind.Other;
            Vec2 pos = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
            var ig = f.CachedFormationIntegrityData;
            float integrity = FormationIntegrity.Compute(
                ig.DeviationOfPositionsExcludeFarAgents, ig.MaxDeviationOfPositionExcludeFarAgents,
                VecMath.Dist(0f, 0f, ig.AverageVelocityExcludeFarAgents.x, ig.AverageVelocityExcludeFarAgents.y),
                ig.AverageMaxUnlimitedSpeedExcludeFarAgents, f.CountOfUnits);
            return new FormationCoordSnapshot
            {
                FormationId = f.Index,
                Count = f.CountOfUnits,
                X = pos.x, Y = pos.y,
                Integrity = integrity,
                NominalSpeed = ig.AverageMaxUnlimitedSpeedExcludeFarAgents,
                Kind = kind,
                IsKnown = isKnown
            };
        }
    }
}
