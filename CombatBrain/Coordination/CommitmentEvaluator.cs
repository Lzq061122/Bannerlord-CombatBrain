namespace CombatBrain.Coordination
{
    using CombatBrain.Battle;
    using System.Collections.Generic;

    // ATC-4 CommitmentEvaluator 影子模式（CommitmentEvaluator + CommitmentEstimator）—— 纯逻辑（零 TaleWorlds 可离线测）。
    // 职责：对 Formation 的候选投入做统一许可判断，但只记录「若执行层接线将允许/拒绝什么」，
    //   不改变任何命令（ATC 规格 §10 objective：影子模式，零行为变化）。
    // §3.4 输出契约：Permission 五值 + Reasons flags（完整，不只留最后一个）+ 明细字段。
    // §10 判定纪律 6 条（逐条落实，见 Evaluate 内注释）：
    //   1) Retreat/Emergency 直接获得对应 override，不走普通进攻评分；
    //   2) 已贴身接战时不得突然 Denied 后原地不动——返回 Limited，允许脱困/后撤/维持接触；
    //   3) Reserve 默认不得主动 Charge，仅 Intercept/Emergency 或 Army Plan 释放后可投入；
    //   4) TacticalTarget 与 Objective 不一致不是单独死刑，Screen/Intercept 可以处理次要威胁；
    //   5) Support 不足 + LocalInferiority + UnauthorizedIsolation 至少两项同时成立才允许 Denied（防单阈值误杀）；
    //   6) Reasons 必须完整输出（flags 逐位保留）。
    // §4.3 编队级近似（CommitmentEstimator）：ContactPoint/ETA/FriendlySupport/EnemyLocalForce/LocalForceRatio，
    //   EffectiveStrength = Count × KindCoefficient × IntegrityCoefficient（系数集中 public const/只读表并有测试覆盖）；
    //   禁逐兵构造局部兵力场/邻接图/ETA；全部军级计算 O(F²)。
    // 迷雾一致（§2.2.2）：EnemyLocalForce 只消费 IsKnown=true 的敌编队快照（胶水层已按 IsEnemyFormationKnown 过滤，
    //   本层对混入的未知编队防御性跳过）。
    // 反作弊闸：输入=既有公开聚合（编队中心/Count/QuerySystem 兵种/完整度/标称速度/阶段/任务/分离意图），零新增禁读。

    /// <summary>§3.4 Commitment 许可五值（影子输出，只记录不下命令）。</summary>
    public enum CommitmentPermission
    {
        Denied = 0,     // 拒绝新发起的主动推进/冲锋/追击（不得否决撤退/自保/贴身脱困，§4.5）
        Hold = 1,       // 保持/列阵（等待；不主动投入）
        ScreenOnly = 2, // 允许横向屏护与退让，禁止向 Objective 深入
        Limited = 3,    // 允许脱困/后撤/维持接触/防御性投入（Limited 授权）
        Granted = 4     // 允许投入（候选发起被许可）
    }

    /// <summary>§3.4 Commitment 原因 flags（九位；完整输出，不只留最后一个）。</summary>
    [System.Flags]
    public enum CommitmentReason
    {
        None = 0,
        NoArmyObjective = 1,          // 无有效 Operational Objective
        NotAlignedWithMainEffort = 2, // TacticalTarget 与 Objective 不一致（推进型任务提示，非单独死刑）
        InsufficientSupport = 4,      // 支援不足（SupportScore < 阈值）
        LocalInferiority = 8,         // 局部劣势（LocalForceRatio < 阈值）
        ArrivalGap = 16,              // 敌先到达（EnemyContactEta < FriendlyArrivalEta）
        UnauthorizedIsolation = 32,   // 未经授权的孤立（ATC-3 §4.4 六因子）
        ReserveProtected = 64,        // Reserve 保护（默认不得主动投入）
        DefensiveOverride = 128,      // Immediate Defensive / Retreat（Brace/Protect/脱困/撤退，§4.5 高优先）
        EmergencyOverride = 256       // Emergency/Survival（最高优先，直接授权脱困）
    }

    /// <summary>§10 Commitment 输入契约（胶水层从既有聚合填充，全部纯数值）。</summary>
    public struct CommitmentInput
    {
        public BattlePhase Phase;             // 当前战局阶段（ATC-1）
        public FormationMission Mission;      // 编队任务（ATC-3 §9 映射输出）
        public SeparationIntent Separation;   // 分离意图（ATC-3 §3.3 八值）
        public bool HasOperationalObjective;  // Army 是否存在有效 Operational Objective（ATC-2）
        public bool TargetAlignedWithObjective; // TacticalTarget 与 OperationalObjective 对齐（推进型任务语义）
        public float MainBodyDistance;        // 距 MainBody 距离（m）
        public bool DistanceIncreasing;       // 距离正在增加（趋势）
        public float FriendlySupport;         // 支援充足度 0..1（SupportRadius 内且及时到达的己方有效兵力占比）
        public float LocalForceRatio;         // 局部兵力比（己方局部有效兵力 / max(敌局部,1)；>1 有利）
        public float FriendlyArrivalEta;      // 己方（支援）预计到达（s）
        public float EnemyContactEta;         // 敌预计接触（s）
        public float Integrity;               // 编队完整度 0..1
        public bool IsReserve;                // 是否 Reserve（未投入且靠近 MainBody）
        public bool InContact;                // 是否已贴身接战（近战接触中）
        public bool DefensiveOverride;        // Immediate Defensive（Brace/Protect/脱困，§4.5 高优先）
        public bool EmergencyOverride;        // Emergency/Survival（最高优先）
    }

    /// <summary>§3.4 Commitment 输出契约（影子判定结果，不下命令）。</summary>
    public struct CommitmentDecision
    {
        public CommitmentPermission Permission; // 许可（Denied..Granted）
        public CommitmentReason Reasons;        // flags 完整（不只留最后一个原因）
        public float LocalForceRatio;           // 局部兵力比（镜像输入，供日志/观测）
        public float SupportScore;              // 支援充足度（镜像输入）
        public bool IsolationRisk;              // UnauthorizedIsolation 风险（ATC-3 §4.4 判定）
        public float FriendlyArrivalEta;        // 己方（支援）预计到达（s）
        public float EnemyContactEta;           // 敌预计接触（s）
    }

    /// <summary>
    /// 编队接敌判定（atc4-fog-contact-fix P0-2 纯函数）：己方编队中心与任一已知敌编队中心距离 ≤
    /// contactMeters 视为贴身接战。只消费 IsKnown=true 的敌编队——未知敌（迷雾未观测/超期）不得参与
    /// 接敌状态/局部兵力/接触 ETA/Commitment 等级及原因码（统一 IsKnown 迷雾契约，规格 §2.2.2）。
    /// FogOfWar 关闭时胶水层 IsEnemyFormationKnown 直通 true（快照 IsKnown=true），同一敌按已知参与——
    /// 本函数不做 Fog 开关判断，语义由快照构建层保证。
    /// </summary>
    public static class CommitmentContact
    {
        /// <summary>是否贴身接战（O(F)，只查已知敌编队）。enemySnaps 为 null/空 → false。</summary>
        public static bool IsInContact(float fx, float fy, IList<FormationCoordSnapshot> enemySnaps, float contactMeters)
        {
            if (enemySnaps == null || contactMeters <= 0f) return false;
            float sq = contactMeters * contactMeters;
            for (int i = 0; i < enemySnaps.Count; i++)
            {
                FormationCoordSnapshot e = enemySnaps[i];
                if (e.FormationId < 0) continue;
                if (!e.IsKnown) continue; // P0-2：未知敌不参与接敌判定（迷雾契约）
                float dx = e.X - fx, dy = e.Y - fy;
                if (dx * dx + dy * dy <= sq) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 编队距离趋势判定（atc4-fix finding1 纯函数）：per-formation 距离基线的读/判时序辅助。
    /// 用途：ATC-3（TickFormationMissions）与 ATC-4（TickCommitment）各自维护独立基线字典，
    ///   避免共享字典同拍「先写后读」导致 ATC-4 读到本拍已更新值、distIncreasing 恒 false
    ///   （Shadow Gate 审计失真）。语义：prevDist &gt; 0（首拍缺省 0=无趋势）且本拍距离大于上拍 → 距离增加。
    /// </summary>
    public static class FormationDistanceTrend
    {
        /// <summary>判定距离趋势：上拍基线有效（&gt;0）且本拍距离更大。</summary>
        public static bool IsIncreasing(float prevDist, float dist)
        {
            return prevDist > 0f && dist > prevDist;
        }
    }

    /// <summary>§10 Commitment 统一许可判定（纯函数）。判定纪律 6 条逐条落实，详见各分支注释。</summary>
    public static class CommitmentEvaluator
    {
        // 阈值（§10 判定纪律 5 + IsolationDetector 同口径；集中 public const 可离线断言）
        public const float SupportThreshold = 0.3f;  // SupportScore <0.3 = 支援不足
        public const float RatioThreshold = 1.0f;    // LocalForceRatio <1.0 = 局部劣势
        public const int RiskDenyCount = 2;          // 纪律5：Support/Local/Isolation 至少 2 项才 Denied
        public const float AlignMeters = 60f;        // TacticalTarget 与 Objective 对齐半径（m，与连通半径同口径）

        /// <summary>统一许可判定（只输出判定，不改变命令——影子模式）。</summary>
        public static CommitmentDecision Evaluate(CommitmentInput input)
        {
            var d = new CommitmentDecision();
            d.LocalForceRatio = input.LocalForceRatio;
            d.SupportScore = input.FriendlySupport;
            d.FriendlyArrivalEta = input.FriendlyArrivalEta;
            d.EnemyContactEta = input.EnemyContactEta;

            // ---- 纪律1：Retreat/Emergency 直接获得对应 override，不走普通进攻评分（§4.5 最高优先） ----
            if (input.EmergencyOverride)
            {
                d.Reasons |= CommitmentReason.EmergencyOverride;
                d.Permission = CommitmentPermission.Limited; // 允许脱困/后撤/维持接触（Emergency 不得被 Denied 锁死）
                return d;
            }
            if (input.Mission == FormationMission.Retreat || input.Phase == BattlePhase.Retreat)
            {
                // Retreat/Morale 高于 Army Commitment（§4.5）；DefensiveOverride 标识撤退/防御性行为
                d.Reasons |= CommitmentReason.DefensiveOverride;
                d.Permission = CommitmentPermission.Limited; // 允许撤退/脱离
                return d;
            }
            if (input.DefensiveOverride)
            {
                // Immediate Defensive（Brace/Protect/脱困）高于 Army Commitment（§4.5）
                d.Reasons |= CommitmentReason.DefensiveOverride;
                d.Permission = CommitmentPermission.Limited;
                return d;
            }

            // ---- 纪律2：已贴身接战 → Limited（不得突然 Denied 后原地不动，允许脱困/后撤/维持接触） ----
            if (input.InContact)
            {
                d.Reasons |= CommitmentReason.DefensiveOverride; // 贴身脱困属 Immediate Defensive（§4.5）
                d.Permission = CommitmentPermission.Limited;
                return d;
            }

            // ---- 风险因子收集（纪律6：Reasons flags 完整输出，不只留最后一个） ----
            bool insufficientSupport = input.FriendlySupport < SupportThreshold;
            bool localInferiority = input.LocalForceRatio < RatioThreshold;
            bool enemyArrivesFirst = input.EnemyContactEta < input.FriendlyArrivalEta;

            // UnauthorizedIsolation 复用 ATC-3 §4.4 六因子判定（同口径，Exploit+Pursuit/合法分离/Retreat/Emergency 均豁免）
            var iso = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = input.MainBodyDistance,
                DistanceIncreasing = input.DistanceIncreasing,
                SupportScore = input.FriendlySupport,
                LocalForceRatio = input.LocalForceRatio,
                EnemyArrivesFirst = enemyArrivesFirst,
                Phase = input.Phase,
                Separation = input.Separation,
                Emergency = input.EmergencyOverride || input.DefensiveOverride,
                InRetreat = input.Mission == FormationMission.Retreat || input.Phase == BattlePhase.Retreat
            });
            d.IsolationRisk = iso.UnauthorizedIsolation;

            if (insufficientSupport) d.Reasons |= CommitmentReason.InsufficientSupport;
            if (localInferiority) d.Reasons |= CommitmentReason.LocalInferiority;
            if (enemyArrivesFirst) d.Reasons |= CommitmentReason.ArrivalGap;
            if (d.IsolationRisk) d.Reasons |= CommitmentReason.UnauthorizedIsolation;
            if (!input.HasOperationalObjective) d.Reasons |= CommitmentReason.NoArmyObjective;
            // ReserveProtected 不在收集阶段设置：由下方守卫分支精确设置（Intercept/释放时保护不生效，纪律3）

            int riskCount = (insufficientSupport ? 1 : 0) + (localInferiority ? 1 : 0) + (d.IsolationRisk ? 1 : 0);
            bool armyReleased = input.HasOperationalObjective && input.TargetAlignedWithObjective; // Army Plan 释放近似

            // ---- 纪律4：TacticalTarget 与 Objective 不一致不是单独死刑；Screen/Intercept 可处理次要威胁 ----
            switch (input.Mission)
            {
                case FormationMission.Screen:
                    // Screen：允许横向屏护与退让（ScreenOnly），禁止向 Objective 深入；不因目标不一致 Denied
                    d.Permission = CommitmentPermission.ScreenOnly;
                    return d;
                case FormationMission.Intercept:
                    // Intercept：防御性投入（Limited）；合法拦截次要威胁，不因目标不一致/远离 Denied（纪律3 例外：可投入）
                    d.Permission = CommitmentPermission.Limited;
                    return d;
                case FormationMission.Reserve:
                    // ---- 纪律3：Reserve 默认不得主动 Charge ----
                    if (!armyReleased)
                    {
                        d.Reasons |= CommitmentReason.ReserveProtected;
                        d.Permission = CommitmentPermission.Hold;
                        return d;
                    }
                    break; // Army Plan 释放 → 落入推进评分
                default:
                    break;
            }

            // ---- 推进型任务评分（SupportMainEffort/Flank/Occupy/Pursuit） ----
            // 纪律3（兜底）：Reserve 未释放 → Hold（ReserveProtected 精确置位）
            if (input.IsReserve && !armyReleased)
            {
                d.Reasons |= CommitmentReason.ReserveProtected;
                d.Permission = CommitmentPermission.Hold;
                return d;
            }
            // 无 Army Objective → Hold（不主动推进；NoArmyObjective 已置位，不得用真实敌位置补齐）
            if (!input.HasOperationalObjective)
            {
                d.Permission = CommitmentPermission.Hold;
                return d;
            }
            // 纪律4：未对齐仅记 flag，不单独构成拒绝（避免单阈值误杀）
            if (!input.TargetAlignedWithObjective) d.Reasons |= CommitmentReason.NotAlignedWithMainEffort;

            // ---- 纪律5：Support 不足 + LocalInferiority + UnauthorizedIsolation 至少 2 项才 Denied ----
            if (riskCount >= RiskDenyCount)
            {
                d.Permission = CommitmentPermission.Denied;
                return d;
            }
            d.Permission = CommitmentPermission.Granted;
            return d;
        }
    }

    /// <summary>§4.3 编队级局部兵力/ETA 近似（纯函数，零 TaleWorlds；O(F²) 禁逐兵）。</summary>
    public struct LocalForceEstimate
    {
        public float ContactX, ContactY;   // 接敌点（己方与候选目标连线上的预计接敌点）
        public float DistanceToContact;    // 到接敌点距离（m）
        public float SelfEta;              // 自身预计到达接敌点（s）
        public float FriendlyEta;          // 己方支援预计到达（s；无支援 → 自身 ETA）
        public float EnemyContactEta;      // 敌局部预计接触（s；无及时敌 → float.MaxValue）
        public float FriendlySupport;      // 支援充足度 0..1（支援有效兵力 / 全体己方有效兵力）
        public float EnemyLocalForce;      // 敌局部有效兵力（LocalRadius 内、已知且及时到达）
        public float LocalForceRatio;      // (自身+支援有效兵力) / max(敌局部有效兵力, 1)
    }

    /// <summary>§4.3 编队级估计器：ContactPoint/ETA/FriendlySupport/EnemyLocalForce/LocalForceRatio。</summary>
    public static class CommitmentEstimator
    {
        // 系数集中为 public const / 只读表（§4.3 约束；测试覆盖见 OfflineTests TestCommitmentEvaluator）
        public const float MinSpeed = 1.0f;         // ETA 分母下限（标称速度过低防 ETA 爆炸）
        public const float SupportRadius = 60f;     // 支援半径（m）：在半径内且及时到达的己方编队才算支援
        public const float ArrivalSlack = 10f;      // 到达容差（s）：支援 ETA <= 自身 ETA + Slack
        public const float LocalRadius = 100f;      // ContactPoint 局部半径（m）：敌局部编队收集窗
        public const float ContactClampMeters = 40f; // 接敌点截断：距己方 ≤ 40m（不过冲到敌编队中心）
        public const float IntegrityCoeffMin = 0.5f; // 完整度系数下限（Integrity=0 → 0.5）
        public const float IntegrityCoeffMax = 1.0f; // 完整度系数上限（Integrity=1 → 1.0）

        // 兵种战力系数（Index 与 FormationCoordKind 枚举值对齐：Infantry/Ranged/MeleeCavalry/RangedCavalry/Other）
        public static readonly float[] KindCoefficient = { 1.0f, 1.0f, 1.2f, 1.1f, 1.0f };

        /// <summary>完整度系数：0..1 线性映射到 [IntegrityCoeffMin, IntegrityCoeffMax]。</summary>
        public static float IntegrityCoefficient(float integrity)
        {
            float t = integrity < 0f ? 0f : (integrity > 1f ? 1f : integrity);
            return IntegrityCoeffMin + (IntegrityCoeffMax - IntegrityCoeffMin) * t;
        }

        /// <summary>§4.3 有效兵力：Count × KindCoefficient × IntegrityCoefficient（唯一允许的公式）。</summary>
        public static float EffectiveStrength(int count, FormationCoordKind kind, float integrity)
        {
            int k = (int)kind;
            float coeff = k >= 0 && k < KindCoefficient.Length ? KindCoefficient[k] : 1.0f;
            return count * coeff * IntegrityCoefficient(integrity);
        }

        /// <summary>§4.3 ETA = DistanceToContact / max(NominalSpeed, MinSpeed)。</summary>
        public static float Eta(float distance, float nominalSpeed)
        {
            return distance / (nominalSpeed > MinSpeed ? nominalSpeed : MinSpeed);
        }

        /// <summary>ContactPoint：己方编队与候选目标连线上的预计接敌点（距己方 ≤ ContactClampMeters 截断）。</summary>
        public static void ContactPoint(float fx, float fy, float tx, float ty,
            out float cx, out float cy, out float distToContact)
        {
            float dx = tx - fx, dy = ty - fy;
            float dist = (float)System.Math.Sqrt(dx * dx + dy * dy);
            float d = dist < ContactClampMeters ? dist : ContactClampMeters;
            if (dist > 1e-4f)
            {
                cx = fx + dx / dist * d;
                cy = fy + dy / dist * d;
            }
            else
            {
                cx = fx;
                cy = fy;
            }
            distToContact = d;
        }

        /// <summary>
        /// §4.3 编队级估计（O(F²)：friendlies/enemies 各一次线性扫描）。
        /// FriendlySupport：SupportRadius 内 且 支援 ETA <= 自身 ETA + ArrivalSlack 的己方编队有效兵力 / 全体己方有效兵力；
        /// EnemyLocalForce：ContactPoint LocalRadius 内、IsKnown=true 且预计可及时到达（eta <= 自身 ETA + Slack）的敌编队有效兵力；
        /// LocalForceRatio = (自身有效兵力 + 支援有效兵力) / max(敌局部有效兵力, 1)。
        /// 迷雾一致：IsKnown=false 的敌编队不参与（§2.2.2）。
        /// </summary>
        public static LocalForceEstimate Estimate(
            float fx, float fy, float tx, float ty, float nominalSpeed,
            int selfCount, FormationCoordKind selfKind, float selfIntegrity,
            IList<FormationCoordSnapshot> friendlies, IList<FormationCoordSnapshot> enemies)
        {
            var r = new LocalForceEstimate();
            ContactPoint(fx, fy, tx, ty, out r.ContactX, out r.ContactY, out r.DistanceToContact);
            r.SelfEta = Eta(r.DistanceToContact, nominalSpeed);

            // 己方：自身 + 支援（SupportRadius 内且及时到达）
            float ownEff = EffectiveStrength(selfCount, selfKind, selfIntegrity);
            float supportEff = 0f, allFriendlyEff = ownEff;
            float minSupportEta = float.MaxValue;
            if (friendlies != null)
            {
                for (int i = 0; i < friendlies.Count; i++)
                {
                    FormationCoordSnapshot f = friendlies[i];
                    if (f.FormationId < 0) continue;
                    float df = Dist(f.X, f.Y, fx, fy);
                    float eff = EffectiveStrength(f.Count, f.Kind, f.Integrity);
                    allFriendlyEff += eff;
                    if (df <= SupportRadius)
                    {
                        float eta = Eta(df, f.NominalSpeed);
                        if (eta <= r.SelfEta + ArrivalSlack)
                        {
                            supportEff += eff;
                            if (eta < minSupportEta) minSupportEta = eta;
                        }
                    }
                }
            }
            r.FriendlySupport = allFriendlyEff > 0f ? System.Math.Min(1f, supportEff / allFriendlyEff) : 0f;
            r.FriendlyEta = minSupportEta < float.MaxValue ? minSupportEta : r.SelfEta;

            // 敌方：LocalRadius 内、已知且预计可及时到达
            float enemyEff = 0f;
            float minEnemyEta = float.MaxValue;
            if (enemies != null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    FormationCoordSnapshot e = enemies[i];
                    if (e.FormationId < 0) continue;
                    if (!e.IsKnown) continue; // Fog 一致：未知敌不参与（§2.2.2）
                    float de = Dist(e.X, e.Y, r.ContactX, r.ContactY);
                    if (de > LocalRadius) continue;
                    float eta = Eta(de, e.NominalSpeed);
                    if (eta > r.SelfEta + ArrivalSlack) continue; // 预计无法及时到达 → 不计入局部
                    enemyEff += EffectiveStrength(e.Count, e.Kind, e.Integrity);
                    if (eta < minEnemyEta) minEnemyEta = eta;
                }
            }
            r.EnemyLocalForce = enemyEff;
            r.EnemyContactEta = minEnemyEta < float.MaxValue ? minEnemyEta : float.MaxValue;
            r.LocalForceRatio = (ownEff + supportEff) > 0f
                ? (ownEff + supportEff) / System.Math.Max(enemyEff, 1f)
                : 0f;
            return r;
        }

        private static float Dist(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
