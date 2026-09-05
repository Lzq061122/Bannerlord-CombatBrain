using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain;
using CombatBrain.Intent;

namespace CombatBrain.Ai
{
    // F2 对手建模胶水（编队级，0.5s 由 CombatAiMissionLogic.TickIntent 驱动）。
    // 职责：把"可观察"的敌方编队运动学/阵型/事件聚合成 t16 §2.1 的 8 类信号 + CeaseFire，
    //       喂给纯逻辑 OpponentModel（信念更新），并把 F1 新 4 维（EnemCav/FlankEx/Gap/Skirm，
    //       t11 A1）填进 SituationVector；读不懂（置信<0.5 或熵>1.8）→ belief=null 不注入信念。
    //
    // 抗作弊读取集（t12 行19 / t16 附 L131）：
    //   只读 位置(SmoothedAverageUnitPosition)/朝向(Direction)/阵宽(ArrangementOrder)/速度(位置增量)/
    //         射击事件(MakingRangedAttackRatio)/逃跑数(ActiveAgents 存活率)/完整度(DeviationOfPositionsExcludeFarAgents)；
    //   禁读：敌编队 MovementOrder/行为权重/意图状态/士气值/精确血量/全图逐兵遍历（proposal L166）。
    //
    // 性能：0.5s 编队级聚合 O(编队数²)，无逐兵遍历、无每拍分配（复用字段）；每信号 3s 冷却由 OpponentModel 兜底。
    public sealed class OpponentModelGlue
    {
        private const float TickSeconds = 0.5f;          // 与 TickIntent 周期一致
        private const float AdvanceMeters = 2f;           // 逼近判定：0.5s 内接近 ≥2m
        private const float ReserveMoveMeters = 4f;       // 预备队位移判定：0.5s 位移 ≥4m
        private const float ChargeSpeedMps = 6f;          // 冲锋速度代理（径向速率）
        private const float ChargeAlignDot = 0.85f;       // 冲锋朝向对齐：dot ≥0.85
        private const float FlankSectorDot = 0.5f;        // 绕侧扇区：与正对己方方向夹角 >60°（dot<0.5）
        private const float IntegrityOrderly = 3f;        // 完整度上限：编队偏差 <3m 视为"有序"
        private const float CeaseFireDrop = 0.2f;         // 敌在射比例回落阈值
        private const float CeaseFireMinWasFiring = 0.15f;// 敌此前在射比例 ≥0.15 才判停火
        private const float RoutIsReal = 0.15f;           // 敌逃跑率 ≥15% → 撤退视为真退（不判佯退）

        private readonly Dictionary<int, OpponentModel> _models = new Dictionary<int, OpponentModel>();
        private readonly Dictionary<Formation, FormationTrack> _tracks = new Dictionary<Formation, FormationTrack>();
        private readonly Dictionary<int, float> _enemyFiring = new Dictionary<int, float>(); // 队 → 上一拍在射比例
        private readonly Dictionary<int, float> _enemyInitialAlive = new Dictionary<int, float>();
        private readonly Dictionary<int, Vec2> _teamCenter = new Dictionary<int, Vec2>();    // 队 → 上次中心（无一兵保持）
        private readonly List<Vec2> _enemyFcs = new List<Vec2>(); // 复用列表（零分配）
        private readonly List<Vec2> _ownFcs = new List<Vec2>();

        // N2 透出：最近一拍敌方存活衰减代理（供叙事层 E3 溃败边沿消费；值域 0..1，首拍 0）。
        // 反作弊闸：读 ActiveAgents 计数（队级公开聚合，非逐兵），零新增禁读信息。
        public float LastRoutRatio { get; private set; }

        // N2 透出：最近一拍主导敌方意图与置信（供叙事层 E4 预测冲锋边沿消费；读不懂 → 默认 None/0）。
        // 反作弊闸：读 OpponentModel 既有信念（t16 已核实只读已有决策状态），零新增禁读信息。
        public EnemyIntent LastDominantIntent { get; private set; }
        public float LastDominantConf { get; private set; }

        private sealed class FormationTrack
        {
            public float DistToUs; // 上一拍到己方中心距离（0 = 首拍未记录）
        }

        /// <summary>
        /// 0.5s 编队级更新：聚合 8 信号 → 信念更新；填充新 4 维；读取主导信念。
        /// </summary>
        /// <returns>true = 信念可读（belief 为归一化 6 维信念）；false = 读不懂（belief=null，不注入）。</returns>
        public bool Update(Team own, Team enemy, float now, ref SituationVector v, out float[] belief)
        {
            belief = null;
            if (own == null || enemy == null) return false;

            OpponentModel model = ModelFor(own.TeamIndex);
            model.Tick(now);

            Vec2 oc = TeamCenter(own);
            Vec2 ec = TeamCenter(enemy);
            float dist = VecMath.Dist(oc.x, oc.y, ec.x, ec.y);

            // ---- 一次敌方编队遍历：收集信号证据 + 编队中心（读集：位置/朝向/阵宽/完整度，抗作弊 ✅）----
            _enemyFcs.Clear();
            float total = 0f, skein = 0f, wall = 0f;
            float cav = 0f, flankCav = 0f;
            float rangedAdv = 0f, rangedCavAdv = 0f;
            float reserve = 0f, charge = 0f;
            float feign = 0f;

            foreach (Formation f in enemy.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                int n = f.CountOfUnits;
                total += n;
                Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage); // 只读位置 ✅ P1-④ 统一入口
                float fd = VecMath.Dist(fc.x, fc.y, oc.x, oc.y);
                _enemyFcs.Add(fc);

                FormationTrack tr;
                if (!_tracks.TryGetValue(f, out tr)) { tr = new FormationTrack { DistToUs = fd }; _tracks[f] = tr; }
                float prev = tr.DistToUs;
                tr.DistToUs = fd;
                float radialSpeed = prev > 0f ? Math.Abs(fd - prev) / TickSeconds : 0f; // 速度代理 ✅

                // 信号1/2：阵型 ArrangementOrder（只读阵型配置 ✅，t16 §2.1）
                if (f.ArrangementOrder == ArrangementOrder.ArrangementOrderSkein) skein += n;
                if (f.ArrangementOrder == ArrangementOrder.ArrangementOrderShieldWall) wall += n;

                bool isCav = f.QuerySystem.IsCavalryFormation || f.QuerySystem.IsRangedCavalryFormation;
                bool isRanged = f.QuerySystem.IsRangedFormation;
                bool isRangedCav = f.QuerySystem.IsRangedCavalryFormation;

                // 信号5：骑兵绕侧集结（编队中心相对敌阵中心的侧向分量；读位置 ✅）
                if (isCav)
                {
                    cav += n;
                    Vec2 toF = Norm(fc.x - ec.x, fc.y - ec.y);
                    Vec2 dirToUs = Norm(oc.x - ec.x, oc.y - ec.y);
                    float align = toF.x * dirToUs.x + toF.y * dirToUs.y; // dot：1=正对我方
                    if (align < FlankSectorDot) flankCav += n;            // 侧向 >60° → 绕侧
                }

                // 信号3/7：散兵/骑射逼近（距离变化率；读位置 ✅）
                if (isRanged || isRangedCav)
                {
                    if (prev > 0f && prev - fd >= AdvanceMeters)
                    {
                        if (isRangedCav) rangedCavAdv += n; // 骑射 → HorseArcherHarass
                        else rangedAdv += n;                 // 步散兵 → SkirmisherClose
                    }
                }

                // 信号4：预备队位移（远离前线编队的大幅位移；读位置增量 ✅）
                if (fd > dist * 0.6f + 10f && prev > 0f && Math.Abs(fd - prev) >= ReserveMoveMeters)
                {
                    reserve += n;
                }

                // 信号6：重骑正面冲击（径向速率 + 阵型朝向对齐；读速度代理+朝向 ✅）
                if (radialSpeed >= ChargeSpeedMps)
                {
                    Vec2 toUs = Norm(oc.x - fc.x, oc.y - fc.y);
                    float dot = toUs.x * f.Direction.x + toUs.y * f.Direction.y;
                    if (dot > ChargeAlignDot) charge += n;
                }

                // 信号8：有序佯退（完整度代理 DeviationOfPositionsExcludeFarAgents ✅；
                //        无序=溃退真退，不更新 —— t10 折中）
                if (prev > 0f && fd - prev >= AdvanceMeters)
                {
                    float dev = f.CachedFormationIntegrityData.DeviationOfPositionsExcludeFarAgents;
                    if (dev < IntegrityOrderly) feign += n;
                }
            }

            // ---- 己方编队中心（Gap 维用）----
            _ownFcs.Clear();
            foreach (Formation f in own.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                _ownFcs.Add(TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage)); // P1-④ 统一入口
            }

            // ---- 逃跑数（存活率代理 ✅）：≥15% → 撤退视为真退，不判佯退 ----
            float routRatio = EnemyRoutRatio(enemy);
            LastRoutRatio = routRatio; // N2 透出给叙事层（E3 溃败边沿）

            // ---- 射击事件（MakingRangedAttackRatio 队级代理 ✅）：回落 → CeaseFire（t11 L164 0.4 封顶）----
            float firing = TeamFiringRatio(enemy);
            float prevFiring;
            if (!_enemyFiring.TryGetValue(enemy.TeamIndex, out prevFiring)) { prevFiring = firing; }
            float firingDrop = prevFiring - firing;
            _enemyFiring[enemy.TeamIndex] = firing;

            // ---- 信号应用（强度 = 占比 × 距离折扣；权重由模型冷却兜底）----
            if (skein > 0f) model.ApplySignal(SignalKind.SkeinCharge, W(skein, total, dist), now);
            if (wall > 0f) model.ApplySignal(SignalKind.ShieldWallHold, W(wall, total, dist), now);
            if (rangedAdv > 0f) model.ApplySignal(SignalKind.SkirmisherClose, W(rangedAdv, total, dist), now);
            if (rangedCavAdv > 0f) model.ApplySignal(SignalKind.HorseArcherHarass, W(rangedCavAdv, total, dist), now);
            if (reserve > 0f) model.ApplySignal(SignalKind.ReserveMove, W(reserve, total, dist), now);
            if (flankCav > 0f) model.ApplySignal(SignalKind.CavalryFlankGather, W(flankCav, cav > 0f ? cav : 1f, dist), now);
            if (charge > 0f) model.ApplySignal(SignalKind.HeavyCharge, W(charge, total, dist), now);
            if (feign > 0f && routRatio < RoutIsReal) model.ApplySignal(SignalKind.FeignedRetreat, W(feign, total, dist), now);
            if (firingDrop >= CeaseFireDrop && prevFiring >= CeaseFireMinWasFiring)
                model.ApplySignal(SignalKind.CeaseFire, Clamp01(firingDrop / 0.5f), now);

            // ---- 新 4 维填充（t11 A1）----
            FillDimensions(ref v, enemy, total, cav, flankCav, rangedAdv + rangedCavAdv, reserve, dist);

            // ---- 信念读取：读不懂 → belief=null（t16 §2.1 不注入）----
            EnemyIntent intent;
            float conf;
            if (model.TryReadDominant(out intent, out conf))
            {
                LastDominantIntent = intent;
                LastDominantConf = conf;
                belief = model.SnapshotBelief();
                return true;
            }
            LastDominantIntent = EnemyIntent.FrontalAttack; // 读不懂 → 非冲锋（不触发 E4）
            LastDominantConf = 0f;
            return false;
        }

        private void FillDimensions(ref SituationVector v, Team enemy, float total,
            float cav, float flankCav, float rangedAdv, float reserve, float dist)
        {
            float totalSafe = Math.Max(1f, total);
            TeamQuerySystem eqs = enemy.QuerySystem;
            float cavRatio = eqs != null ? Math.Max(0f, eqs.CavalryRatio + eqs.RangedCavalryRatio) : 0f;
            float flankFactor = cav > 0f ? flankCav / cav : 0f;

            // EnemCav：敌方骑兵占比 × 侧翼展开修正（t11 A1）
            v.EnemCav = Clamp01(cavRatio * (0.7f + 0.6f * flankFactor));

            // FlankEx：绕侧编队暴露 + 预备队调动（t11 A1）
            v.FlankEx = Clamp01(1.2f * flankCav / totalSafe + 0.4f * reserve / totalSafe);

            // Gap：敌方/己方编队间距对比（阵宽间隙代理；t11 A1）
            float enemyGap = AvgSpacing(_enemyFcs);
            float ownGap = AvgSpacing(_ownFcs);
            v.Gap = enemyGap + ownGap < 1e-3f ? 0.5f : Clamp01(enemyGap / (enemyGap + ownGap));

            // Skirm：散兵威胁 = 远程推进占比 + 敌我距离反比（t11 A1 距离曲线简化）
            v.Skirm = Clamp01(0.7f * rangedAdv / totalSafe + 0.3f * (1f - Clamp01(dist / 80f)));
        }

        private OpponentModel ModelFor(int teamIndex)
        {
            OpponentModel m;
            if (!_models.TryGetValue(teamIndex, out m))
            {
                m = new OpponentModel(LoadPriors());
                _models[teamIndex] = m;
            }
            return m;
        }

        // 数据驱动配置（CL 配置化教训）：ModuleData/opponent_priors.json 加载，失败回退内置常量表。
        // 注：deploy.ps1 只拷 dll+SubModule.xml（不拷 ModuleData），JSON 缺失时自动走 BuildDefault()。
        // 路径：以程序集所在目录（bin）向上两级定位模块根（开发=CombatBrain\，部署=Modules\CombatBrain\），
        // 不依赖任何外部路径 API。
        private static PriorTable LoadPriors()
        {
            try
            {
                string moduleRoot = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
                string path = System.IO.Path.Combine(moduleRoot, "ModuleData", "opponent_priors.json");
                if (System.IO.File.Exists(path))
                {
                    PriorTable t = PriorTable.ParseJson(System.IO.File.ReadAllText(path));
                    if (t != null) return t;
                }
            }
            catch (Exception)
            {
                // 任何 IO/解析失败 → 内置常量表兜底
            }
            return PriorTable.BuildDefault();
        }

        private Vec2 TeamCenter(Team team)
        {
            // P1-④ 口径收敛：几何聚合进 TeamGeometry（Smoothed 口径单一实现）；
            // 此处仅保留「无一兵保持上次中心」胶水状态回退
            Vec2 c;
            if (!TeamGeometry.TryGetTeamCenter(team, TeamPosKind.SmoothedAverage, out c))
                return _teamCenter.TryGetValue(team.TeamIndex, out c) ? c : Vec2.Zero;
            _teamCenter[team.TeamIndex] = c;
            return c;
        }

        // 编队平均两两间距（阵宽/间隙代理；O(n²)，n 为个位数编队）。
        private static float AvgSpacing(List<Vec2> pts)
        {
            int n = pts.Count;
            if (n < 2) return 0f;
            float sum = 0f;
            int pairs = 0;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    sum += VecMath.Dist(pts[i].x, pts[i].y, pts[j].x, pts[j].y);
                    pairs++;
                }
            }
            return pairs > 0 ? sum / pairs : 0f;
        }

        // 逃跑数代理：敌方存活率衰减（读 ActiveAgents 计数 ✅，非逐兵）。
        private float EnemyRoutRatio(Team enemy)
        {
            float init;
            if (!_enemyInitialAlive.TryGetValue(enemy.TeamIndex, out init))
            {
                _enemyInitialAlive[enemy.TeamIndex] = enemy.ActiveAgents.Count;
                return 0f;
            }
            if (init <= 0f) return 0f;
            return 1f - enemy.ActiveAgents.Count / init;
        }

        // 队级"实际在射"比例：MakingRangedAttackRatio 按人数加权均值（引擎缓存，非逐兵 ✅）。
        private static float TeamFiringRatio(Team team)
        {
            float sum = 0f;
            int total = 0;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                sum += f.QuerySystem.MakingRangedAttackRatio * f.CountOfUnits;
                total += f.CountOfUnits;
            }
            return total > 0 ? sum / total : 0f;
        }

        // 信号强度 = 单位占比 × 距离折扣（t16 B1：距离折扣在胶水层完成）。
        private static float W(float units, float total, float dist)
        {
            float strength = total > 0f ? units / total : 0f;
            return Clamp01(strength * Clamp01(1.15f - dist / 130f));
        }

        private static float Clamp01(float x)
        {
            return x < 0f ? 0f : (x > 1f ? 1f : x);
        }

        private static Vec2 Norm(float x, float y)
        {
            float l = (float)Math.Sqrt(x * x + y * y);
            return l > 1e-4f ? new Vec2(x / l, y / l) : new Vec2(0f, 0f);
        }
    }
}
