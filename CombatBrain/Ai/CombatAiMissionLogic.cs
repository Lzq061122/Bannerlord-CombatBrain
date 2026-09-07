using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using CombatBrain;
using CombatBrain.Bench;
using CombatBrain.Combat;
using CombatBrain.Coordination;
using CombatBrain.Battle;
using CombatBrain.Events;
using CombatBrain.Formations;
using CombatBrain.Intent;
using CombatBrain.Lod;
using CombatBrain.Perception;
using CombatBrain.Telemetry;

namespace CombatBrain.Ai
{
    // 战斗AI系统本体 —— 一号项目的使命逻辑胶水层。
    // 把游戏对象喂给各子系统，驱动感知管线 / LOD / 意图引擎 / 预测格挡 / 基准。
    public sealed class CombatAiMissionLogic : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType { get { return MissionBehaviorType.Other; } }

        private sealed class TeamState
        {
            // t69：cultureId 开战缓存一次（FeedbackText.CultureId 是 O(兵数) 遍历，严禁放 0.5s TickIntent）
            public TeamState(Team team, string cultureId)
            {
                Team = team;
                CultureId = cultureId;
                // X0 决策延迟开关：hold 6s→1.5s、切换余量 15%→0% 按强度线性缩放（果断~拟人）
                Engine = new BeliefIntentEngine(TacticManual.BuildMatrix(), cultureId,
                    DecisionDelay.SwitchMargin(CombatBrainSettings.DecisionDelayStrength),
                    DecisionDelay.HoldTime(CombatBrainSettings.DecisionDelayStrength));
            }

            public Team Team;
            public PerceptionPipeline Perception;
            public readonly KnowledgeView View = new KnowledgeView(25f, 0.08f, 0.15f);
            public readonly string CultureId; // t69：开战缓存文化（日志/未来消费可读）
            // F2：信念增强引擎（score = ΣV·W + ΣB·C；belief==null 时退化为 F1 纯反应式）
            public readonly BeliefIntentEngine Engine;
            public SituationVector LastVector;
            public IntentType LastIntent = IntentType.Defend;
            public float InitialCount = -1f;
            public readonly Queue<float> EnemyCountHistory = new Queue<float>();
            public Vec2 Center = Vec2.Zero;   // 上次非空编队中心(无一兵时保持)
            public float Spread = 0f;         // 上次编队离散度(无一兵时保持)

            // N2 L0 战场事件边沿锁存（E1-E4；进/出双阈值+上次状态，NarrativeEvents 纯逻辑消费）
            public NarrativeLatch LatchE1, LatchE2a, LatchE2b, LatchE3;
            public bool LatchE4;              // E4 预测冲锋边沿锁存
            public string LastStrategicClass = null; // E5 战术转折：上拍战略大类（进攻/防守/撤退/机动）
            public NarrativeEventBuffer EventBuffer; // N3 L1 事件缓冲（ring 2，TTL 3.5s；意图切换后读原因）

            // ATC-1 战局阶段（队级五态上下文；只更新状态+发事件/播报，不下命令）
            public BattlePhase Phase = BattlePhase.Deployment; // 当前战局阶段
            public float PhaseEnteredAt = 0f; // 进入当前阶段的时刻（相位内时间基准，强制修订4）
            // P1-2：稳定敌军初始兵力基线（开战首拍缓存最近敌队存活数；不再用滚动队列 Peek 代替开战初始值。
            //   多敌队场景口径：GetEnemyTeam 取最近敌队，基线=该敌队开战存活数；-1=未初始化）
            public float EnemyInitialCount = -1f;

            // ATC-2 Army Context 影子快照（0.5s 更新；MainBody/Objective 只算不改行为）
            public ArmyContextSnapshot Army = ArmyContextSnapshot.None;
            // OPT-3：Army 日志节流每 Team 独立（原实例级全局计时器多队互相抑制；OnMissionTick 按 dt 递减）
            public float ArmyLogTimer;

            // OPT-1：单拍协调帧——每个 0.5s 协调周期只采集一次编队快照（己方全量 + 敌迷雾过滤），
            //   ArmyContext（构建帧）/Mission/Commitment（复用帧）禁止同拍重复构建 ownSnaps/enemySnaps。
            //   帧只在单个逻辑拍内有效（TickArmyContext 每拍重建，FrameValid 防异常路径复用过期帧）；
            //   值为 FormationCoordSnapshot 值列表（非 TaleWorlds 可变集合枚举器缓存，无悬挂枚举风险）。
            public readonly List<FormationCoordSnapshot> FrameOwn = new List<FormationCoordSnapshot>();
            public readonly List<FormationCoordSnapshot> FrameEnemy = new List<FormationCoordSnapshot>();
            public bool FrameValid;

            // ATC-3 Formation Mission / Separation / Isolation 影子判定（0.5s；只判断不下召回命令）。
            // atc4-state-lifecycle P1-1：输出按 FormationId 保存（每支编队结果独立）——原单值字段被
            //   编队循环覆盖、仅残留最后编队的结果（影子数据错位）；改 per-formation 字典后变化日志按
            //   编队比较、编队解散清理对应记录、FormationId 复用不继承旧状态（OnFormationRemoved 清理）。
            public readonly Dictionary<int, FormationMission> Missions = new Dictionary<int, FormationMission>();
            public readonly Dictionary<int, SeparationIntent> Separations = new Dictionary<int, SeparationIntent>();
            public readonly Dictionary<int, IsolationDecision> Isolations = new Dictionary<int, IsolationDecision>();
            // atc4-fix finding1：距离趋势基线拆分为 ATC-3/ATC-4 两个独立 per-formation 字典（键=Formation.Index、
            //   值=上拍该编队距 MainBody 距离，首拍缺省 0）。共享单字典会「同拍先写后读」（TickFormationMissions
            //   先写本拍 → TickCommitment 后读本拍值）→ ATC-4 distIncreasing 恒 false（Shadow Gate 审计失真）。
            //   各层按自身编队历史判趋势（FormationDistanceTrend.IsIncreasing 纯函数），零命令/阈值变化。
            public readonly Dictionary<int, float> MissionPrevDist = new Dictionary<int, float>();      // ATC-3 距离趋势基线
            public readonly Dictionary<int, float> CommitmentPrevDist = new Dictionary<int, float>();   // ATC-4 距离趋势基线（独立）
            // atc4-reserve-classifier P0-4：Reserve 判定迟滞状态（ReserveClassifier 唯一判定源；
            //   ATC-3/ATC-4 各自维护防同拍先写后读——同 prevdist 教训）
            public readonly Dictionary<int, bool> MissionReserveLast = new Dictionary<int, bool>();       // ATC-3 Reserve 上拍结果
            public readonly Dictionary<int, bool> CommitmentReserveLast = new Dictionary<int, bool>();    // ATC-4 Reserve 上拍结果

            // ATC-4 Commitment 影子判定（0.5s；每编队一条，只记录「若执行层接线将允许/拒绝什么」，零行为变化）。
            // 键=Formation.Index；日志变化才写（Permission/Reasons 边沿），禁每拍每队刷。
            public readonly Dictionary<int, CommitmentDecision> Commitments = new Dictionary<int, CommitmentDecision>();
        }

        private bool _initialized;
        private float _elapsed;

        private readonly Dictionary<int, TeamState> _states = new Dictionary<int, TeamState>();
        private readonly Dictionary<int, LodLevel> _agentLod = new Dictionary<int, LodLevel>();
        private readonly Dictionary<int, FighterProfile> _profiles = new Dictionary<int, FighterProfile>();
        private readonly Dictionary<int, AttackStats> _attackStats = new Dictionary<int, AttackStats>();

        private readonly CombatEventBus _bus = new CombatEventBus();
        private ReplayRecorder _replay;
        private readonly BattleBenchmark _bench = new BattleBenchmark();
        private readonly BattleTelemetry _telemetry = new BattleTelemetry(); // T38-01 战斗遥测计数器（事件驱动累加，战斗结束一次落盘）
        private bool _telemetrySaved; // 落盘幂等守卫（OnMissionStateFinalized/OnClearScene 双钩子只写一次）
        private readonly AnomalyDetector _anomaly = new AnomalyDetector(); // T39-01 战术异常检测（6 类反模式，0.5s 低频判定）

        private float _lodTimer;
        private float _intentTimer;
        private float _blockTimer;
        private float _formationLogTimer; // 日志增强：编队明细快照 5s 节拍
        private float _provenanceTimer;   // ATC-0 命令来源 5s 汇总节拍（只输出覆盖/命令变化 Formation）
        // OPT-3：_armyLogTimer 已改 per-team（TeamState.ArmyLogTimer，OnMissionTick 统一 dt 递减）——删除实例字段
        private float _anomalyTimer;       // T39-01 异常检测节拍（0.5s 低频，O(队数×编队数)）
        private float _tacticsTimer;       // T2-01 阵型三态节拍（0.5s 编队级）
        private float _formationSlotsTimer; // X5-6 槽位布局节拍（0.5s 编队级，TickFormationTactics 之后）
        private float _rangedPosTimer;     // T3-01 射手射击阵地节拍（0.5s 编队级）
        private readonly Dictionary<Formation, RangedPositionReason> _rangedPosReason = new Dictionary<Formation, RangedPositionReason>(); // T3-01 每编队上次转移原因（防抖）
        private float _unitPreservationTimer; // T8-01 骑射保存有生力量节拍（0.5s 编队级）
        private readonly Dictionary<Formation, int> _rangedCavInitial = new Dictionary<Formation, int>(); // T8-01 骑射编队初始兵力（分档基准）
        private readonly Dictionary<Formation, PreservationBand> _unitPreservationBand = new Dictionary<Formation, PreservationBand>(); // T8-01 每编队上次分档（边沿）
        private float _stalemateTimer;            // T18-01 僵局判定慢拍（2s）
        private float _frontlineMovement;         // T18-01 战线累计位移（米）
        private Vec2 _lastFrontCenter;            // T18-01 上次敌中心（战线移动基准）
        private bool _hasFrontCenter;             // T18-01 已有战线基准
        private bool _stalemateActive;            // T18-01 僵局中（供意图引擎消费）
        private StalematePlan _stalematePlan = StalematePlan.None; // T18-01 打破方案建议

        // X1 编队级目标选择：0.5s 编队级节拍（契约③缓存）+ 每编队目标缓存字典（FormationTargeting.cs 纯逻辑）。
        // 只产出选择供下游消费（X3 朝向/X4 解阵框/骑兵冲锋目标），本层不下任何命令（避免与
        // Protect/Brace/Cavalry/Skirmish/Morale 命令冲突，契约②）。
        private float _targetingTimer; // X1 目标选择节拍（0.5s 编队级）
        private readonly Dictionary<Formation, TargetSelection> _formationTargets = new Dictionary<Formation, TargetSelection>(); // X1 每编队上次选择（缓存+迟滞基准）
        private readonly TargetCandidate[] _targetCandidates = new TargetCandidate[32]; // X1 候选复用缓冲（编队数上限防御，零每拍分配）

        // X3 编队朝向修正：0.5s 编队级节拍（FacingCorrection.cs 纯逻辑，只产出朝向不下移动/阵型命令）。
        // 朝向源：t93 TryGetFormationTarget 目标编队方向优先（敌射手优先），无目标 → 最近敌队中心（TeamGeometry）。
        // 剧场半径内修正、玩家队豁免（MCM）保留；每编队记录上次方向（迟滞防抖基准）。
        private float _facingTimer; // X3 朝向修正节拍（0.5s 编队级）
        private readonly Dictionary<Formation, Vec2> _facingLastDir = new Dictionary<Formation, Vec2>(); // X3 每编队上次朝向方向（迟滞）
        private readonly Dictionary<Formation, float> _facingLastTime = new Dictionary<Formation, float>(); // X3 每编队上次朝向时刻（0.5s 缓存）

        // X4 接敌解阵框：0.5s 编队级节拍（FrameReleaseGlue.cs 纯逻辑判定）+ 逐 agent 执行
        // （Agent.SetFormationFrameDisabled，Agent.cs:2306-2308 已核实）。触发=编队 Charge/ChargeToTarget
        // 且距 X1 t93 最显著敌编队 ≤25m；剧场闸门（硬性）仅 L3；编队 ≤25 人不启用；F7/F9/逃跑态跳过；
        // 逐 agent 冷却字典限频（p3-1 设计，键=agent.Index，OnAgentRemoved 清理）。
        private float _frameReleaseTimer; // X4 解阵框节拍
        private readonly FrameReleaseCooldown _frameReleaseCooldown = new FrameReleaseCooldown();

        // X6 感知管线开关（信息迷雾接线后置回 true）：KnowledgeView 已接 4 个编队级 Glue 消费者
        // （Protect/Brace/Skirmish/Cavalry 敌编队位置/分类经 KnowledgeView 过滤；PerceptionFilter
        //   折叠：开关关=现状全可见，开=未见/超期目标视为不存在→保守降级）。
        // TickPerception 每 1s 对每队以 observer 为中心的 VisibilityOracle 距离门+LoS 采样（非全图逐兵
        //   白烧——观察结果被 Glue 消费）；500 人 [Bench] perception 段不劣化为验收闸。
        // 用 static readonly 而非 const：const false 会让编译器报 CS0162 不可达代码警告，质量门要求 0 警。
        private static readonly bool PerceptionEnabled = true;

        // F6 走位/绕侧胶水：0.2s 低频周期 + LOD 剧场效应 + 跳帧聚合（见 MovementGlue.cs）
        private readonly MovementGlue _movement = new MovementGlue();

        // F7 求生/重整胶水：0.3s 低频周期，濒死靠拢友军 + 编队重伤重整信号（见 SurvivalGlue.cs）
        private readonly SurvivalGlue _survival = new SurvivalGlue();

        // F9 远程目标/弹药取舍胶水：0.3s 低频周期，远程选目标 + 弹药告罄回退（见 RangedGlue.cs）
        private readonly RangedGlue _ranged = new RangedGlue();

        // F8 骑兵波次胶水：0.5s 编队级周期，冲锋→回撤→重整→再冲（见 CavalryGlue.cs）
        private readonly CavalryGlue _cavalry = new CavalryGlue();

        // X5-6 槽位布局胶水：0.5s 编队级，串联 X5-1..5 全链（归类→分流→布局→地形→纪律→指派→L3 个体补位）
        private readonly FormationSlotGlue _formationSlots = new FormationSlotGlue();

        // T5 骑射游击胶水：0.5s 编队级周期，骑射消耗/游击/夹击（绝不冲锋；见 SkirmishGlue.cs）
        private readonly SkirmishGlue _skirmish = new SkirmishGlue();

        // ATC-5R 编队控制权租约胶水（默认关闭：CombatBrainSettings.FormationControlLeasePocEnabled=false 时零
        //   SetControlledByAI 调用零行为变化；PoC 验证未完成不得 GO——运行期 R1-R8 留集成轮）
        private readonly FormationControlLeaseGlue _leaseGlue = new FormationControlLeaseGlue();

        // F11 士气传导/分阶段撤退胶水：0.5s 编队级周期，邻队崩盘传染 + 请求→批准→脱离（见 MoraleGlue.cs）
        private readonly MoraleGlue _morale = new MoraleGlue();

        // F10 反制矩阵/套路记忆胶水：纯事件驱动（IntentSwitched 反制换招 + 跨战役套路落盘，见 ExperienceGlue.cs）
        private readonly ExperienceGlue _experience = new ExperienceGlue();

        // F12 决策抖动横切：格挡决策点（L3）独立实例，避免与走位共用延迟计时
        private readonly DecisionJitterGlue _jitterBlock = new DecisionJitterGlue();
        // P0-① 抖动时间基准：每 agent「上次调用 _jitterBlock.ShouldAct 的时刻」（now = _elapsed），
        // 真实决策间隔 = now - last；传帧 dt 会让维持期/疲劳慢 6~20 倍。
        private readonly Dictionary<int, float> _lastBlockJitter = new Dictionary<int, float>();

        // F2 对手建模胶水：0.5s 编队级事件聚合（8 信号 → 敌方意图信念）+ 新 4 维填充
        // （EnemCav/FlankEx/Gap/Skirm，t11 A1）；读不懂 → belief=null 不注入（见 OpponentModelGlue.cs）
        private readonly OpponentModelGlue _opponent = new OpponentModelGlue();

        // F3 保护核心输出胶水：ProtectArcher 意图执行层——远程退向步兵阵后 + 步兵殿后档 + 骑兵回援
        // （0.5s 编队级低频下令，见 ProtectGlue.cs）
        private readonly ProtectGlue _protect = new ProtectGlue();

        // F4 架枪反骑阵地胶水：BraceFormation 意图执行层——敌骑逼近时步兵架枪站定
        // （0.5s 编队级低频下令，无长杆降级；见 BraceGlue.cs）
        private readonly BraceGlue _brace = new BraceGlue();

        // T3 攻击抑制/恢复模式工具：供 H3/H4 复用（存原值→置 0→每帧恢复重判，见 AttackSuppression.cs）。
        // 当前仅建立 OnAgentRemoved 生命周期清理挂点；实际抑制/恢复由 H3/H4 决策点调用。
        private readonly AttackSuppression _attackSuppression = new AttackSuppression();

        // T4 性能工具箱：编队层敌中心聚合复用缓冲（零每帧分配，BufferPool 池化；随 LOD tick 重建复用）
        private readonly Dictionary<int, List<Vec2>> _enemyCenters = new Dictionary<int, List<Vec2>>();

        // T4 性能工具箱：0.5s 窗口缓存（带锁）——缓存编队实射比等编队层统计聚合（WindowCache.cs）
        private readonly WindowCache<float> _firingRatioCache = new WindowCache<float>();

        // CPS-1 唯一命令执行器（§5.3）：每协调拍在全部 producer 之后调用（本拍提案由迁移卡接入，
        //   当前为空列表 → 近 O(1) 空转；不迁移任何现有 Glue 写入点，cps1-migrate-* 后续卡）。
        private readonly FormationCommandExecutor _commandExecutor = new FormationCommandExecutor();
        // 本拍提案收集（迁移卡填充；当前恒空——静态空列表零分配）
        private readonly List<FormationCommandProposal> _commandProposals = new List<FormationCommandProposal>();
        // CPS-2 路线 B 观测：5s 汇总计时（Cps2LeaseObserver 输出 [Cps2Observation]，默认关闭）
        private float _cps2ObservationTimer;

        // P1-① 真实地形预缓存：开战一次性采样为只读查表，战中 O(1) 查询（禁实时射线/逐兵采样）。
        // TerrainStub 合成正弦假地形已删除（review-findings-2026-09-01 #7「假数据比没数据更糟」）。
        // null = 尚未构建或构建失败（不可用）→ Height 维降级中性 0.5（宁缺毋假）。
        private TerrainSampler _terrain;
        private bool _terrainBuilt;

        // 采样预算：己方中心 ±300m、8m 网格 → 76×76≈5.8k 点，开战一次性完成（战前填好，战中只读=T0 线程安全）
        private const float TerrainHalfExtent = 300f;
        private const float TerrainCellSize = 8f;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            Init();
        }

        private void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // atc4-state-lifecycle P1-4 / OPT-4：Mission 初始化幂等清理入口——CommandProvenance 为静态单例，
            //   跨战斗残留会串场；InGameFeedback 静态去重/计数同理；_opponent/_attackSuppression 实例级也一并清
            //   （统一走 MissionStateLifecycle 单一入口，双保险；ATC-5R 租约 ReleaseAll 一并纳入）。
            MissionStateLifecycle.ClearStatic(_opponent, _attackSuppression, _leaseGlue, Mission);

            // T1 观测管线：挂载决策分支计数器 + 补丁/管线活性自检（单一 CombatLog 渠道）
            DecisionObservatory.Attach();
            if (DecisionObservatory.Instance != null) DecisionObservatory.Instance.CheckHygiene();

            _replay = new ReplayRecorder(_bus);
            _bus.SubscribeAll(OnTelemetryEvent); // T38-01 遥测：事件驱动累加（勿轮询勿每帧）

            var mission = Mission;
            if (mission == null) return;

            foreach (Team team in mission.Teams)
            {
                if (team == null) continue;
                // t69：cultureId 开战缓存一次（O(兵数) 士兵多数决，Init 仅跑一次；严禁放 0.5s TickIntent）
                var state = new TeamState(team, FeedbackText.CultureId(team));
                state.Perception = new PerceptionPipeline(state.View, 60f, 1.0f);
                _states[team.TeamIndex] = state;
            }

            // F10 事件驱动接入：订阅意图切换事件 + 载入跨战役套路记忆（一次 IO）
            _experience.Attach(_bus,
                mission.PlayerTeam != null ? mission.PlayerTeam.TeamIndex : -1,
                GetEnemyAliveCount);

            CombatLog.Log("CombatBrain 战斗AI已初始化, teams=" + _states.Count);

            // 日志增强：阵营归属（消除 team 归属歧义——玩家队 index + 各队 Side/阵营/与玩家关系）
            CombatLog.Log(string.Format("阵营归属: PlayerTeam=Team {0}",
                mission.PlayerTeam != null ? mission.PlayerTeam.TeamIndex.ToString() : "无"));
            foreach (TeamState st in _states.Values)
            {
                LogTeamIdentity(st.Team);
            }
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            _bench.BeginFrame();

            if (!_initialized) Init();

            var mission = Mission;
            if (mission == null || !mission.IsLoadingFinished)
            {
                _bench.EndFrame(dt, 0);
                return;
            }

            // ATC-0 命令来源观测：逻辑拍起点（同拍覆盖链判定基准；只记录，不改命令）
            CommandProvenance.Instance.BeginLogicalTick(_elapsed);

            _elapsed += dt;
            int agentCount = mission.Agents.Count;

            // CPS-1：本拍提案收集重置（producers 本拍提交 → 拍末执行器统一 Resolve+落地）
            _commandProposals.Clear();

            // T1 观测管线：低频周期导出（3s 窗内聚合，随帧累计耗时可忽略；单一 CombatLog 渠道）
            if (DecisionObservatory.Instance != null) DecisionObservatory.Instance.ReportIfDue(_elapsed);

            // 日志增强：编队级明细快照（5s 低频，看清兵种构成/位置/FSM 状态；拍内拼接、无每帧 IO）
            _formationLogTimer -= dt;
            if (_formationLogTimer <= 0f)
            {
                _formationLogTimer = 5f;
                LogFormationDetails(mission);
            }

            // ATC-0 输出层：5s 汇总——只输出有覆盖/命令变化的 Formation（规格 §14，禁每拍每队刷日志）。
            _provenanceTimer -= dt;
            if (_provenanceTimer <= 0f)
            {
                _provenanceTimer = 5f;
                LogProvenanceSummary();
            }

            // OPT-3：ATC-2 Army 日志 5s 兜底节拍每 Team 独立按真实 dt 递减（原实例级全局计时器多队互相
            //   抑制——一队日志触发重置会推迟其他队兜底；per-team 后各队独立 5s 周期）。
            foreach (TeamState st in _states.Values)
            {
                st.ArmyLogTimer -= dt;
            }

            _bench.BeginSection("perception");
            // X7：FogOfWarEnabled=false 时直接跳过感知计算（关闭降开销，开启才增开销——派单④方向）；
            // PerceptionEnabled 为 X6 接线开关（static true），FogOfWarEnabled 为玩家可关迷雾。
            if (PerceptionEnabled && CombatBrainSettings.FogOfWarEnabled)
            {
                TickPerception(mission);
            }
            _bench.EndSection("perception");

            _bench.BeginSection("lod");
            TickLod(mission, dt);
            _bench.EndSection("lod");

            _bench.BeginSection("intent");
            TickIntent(mission, dt);
            _bench.EndSection("intent");

            _bench.BeginSection("anomaly");
            TickAnomaly(mission, dt);
            _bench.EndSection("anomaly");

            _bench.BeginSection("tactics");
            TickFormationTactics(mission, dt);
            _bench.EndSection("tactics");

            _bench.BeginSection("slotlayout");
            TickFormationSlots(mission, dt);
            _bench.EndSection("slotlayout");

            // ATC-5R 租约胶水（默认关闭零动作；watchdog 每帧轻量，无租约 O(1)；producer 0.5s 评估）
            _bench.BeginSection("lease");
            _leaseGlue.Tick(mission, dt, _elapsed,
                (team, formation) =>
                {
                    TeamState st;
                    CommitmentDecision cd;
                    if (_states.TryGetValue(team.TeamIndex, out st)
                        && st.Commitments.TryGetValue(formation.Index, out cd)) return cd;
                    return (CommitmentDecision?)null;
                },
                (team) =>
                {
                    TeamState st;
                    return _states.TryGetValue(team.TeamIndex, out st) ? st.Army : ArmyContextSnapshot.None;
                },
                _commandProposals); // cps2-lease-integration：租约 fallback 提案提交（唯一执行器落地）
            _bench.EndSection("lease");

            _bench.BeginSection("rangedpos");
            TickRangedPosition(mission, dt);
            _bench.EndSection("rangedpos");

            _bench.BeginSection("unitpreserve");
            TickUnitPreservation(mission, dt);
            _bench.EndSection("unitpreserve");

            _bench.BeginSection("stalemate");
            TickStalemate(mission, dt);
            _bench.EndSection("stalemate");

            _bench.BeginSection("targeting");
            TickTargeting(mission, dt);
            _bench.EndSection("targeting");

            _bench.BeginSection("facing");
            TickFacing(mission, dt);
            _bench.EndSection("facing");

            _bench.BeginSection("framerelease");
            TickFrameRelease(mission, dt);
            _bench.EndSection("framerelease");

            _bench.BeginSection("movement");
            TickMovement(mission, dt);
            _bench.EndSection("movement");

            _bench.BeginSection("survival");
            TickSurvival(mission, dt);
            _bench.EndSection("survival");

            _bench.BeginSection("ranged");
            TickRanged(mission, dt);
            _bench.EndSection("ranged");

            _bench.BeginSection("cavalry");
            TickCavalry(mission, dt);
            _bench.EndSection("cavalry");

            _bench.BeginSection("skirmish");
            TickSkirmish(mission, dt);
            _bench.EndSection("skirmish");

            _bench.BeginSection("morale");
            TickMorale(mission, dt);
            _bench.EndSection("morale");

            _bench.BeginSection("protect");
            TickProtect(mission, dt);
            _bench.EndSection("protect");

            // F4 架枪反骑：在 protect 之后运行（后写者胜=架枪优先，步兵就地站定不参与殿后机动）
            _bench.BeginSection("brace");
            TickBrace(mission, dt);
            _bench.EndSection("brace");

            _bench.BeginSection("block");
            TickBlock(mission, dt);
            _bench.EndSection("block");

            // CPS-1 唯一执行器挂点（§5.3.2 执行器位于本拍所有 proposal producer 之后）：
            // 先收集（_commandProposals，迁移卡填充）→ 统一 Resolve → 再执行；
            // cps2-lease-integration：LeaseOk 接入真实租约状态（_leaseGlue.HasActiveLease）
            _bench.BeginSection("executor");
            _commandExecutor.Tick(mission, _elapsed, _commandProposals, _leaseGlue.HasActiveLease);
            _bench.EndSection("executor");

            // CPS-2 路线 A PoC 挂点（§6.1，默认关闭零 AddAiBehavior/SetBehaviorWeight 调用安全态；
            //   CPS-3 运行期验证开启）：自定义 BehaviorComponent 权重表达 Movement 域意图
            if (CombatBrain.Settings.Cps2PoCSettings.RouteAEnabled)
            {
                CbRouteAPoC.Apply(mission, team => Cps2RouteAPolicy.MapIntent(GetTeamIntent(team)),
                    team => GetTeamCenter(team)); // Rejoin 目标=队中心（MainBody 近似，§5.2）
            }
            // CPS-2 路线 B 观测：5s 汇总（Cps2LeaseObserver Acquire/Release/Expired/ExternalOwner +
            //   ExternalUnknown 三域覆盖数，供 CPS-3 A/B；默认关闭无日志；喂数据点属运行期验证钩子）
            _cps2ObservationTimer -= dt;
            if (_cps2ObservationTimer <= 0f)
            {
                _cps2ObservationTimer = 5f;
                if (CombatBrain.Settings.Cps2PoCSettings.RouteBObservationEnabled)
                {
                    CombatLog.Log("[Cps2Observation] " + Cps2LeaseObserver.BuildSummary());
                }
            }

            _bench.EndFrame(dt, agentCount);
        }

        // ---------- F1 感知 ----------

        private void TickPerception(Mission mission)
        {
            float now = _elapsed;
            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null) continue;
                Vec2 observer = GetTeamCenter(own);
                // 每拍超时清理（独立于刷新门控：周期未到也清超时，幂等）
                state.View.Tick(now);
                // atc4-perception-fix P0-1：一次性批量刷新门控——周期未到 → 跳过本拍全部观察（不刷新不写）；
                // 到达 → 下方批量 ObserveFormation 无内部节流（同一批次可写多个可见敌编队）。
                if (!state.Perception.TryBeginRefresh(now)) continue;
                // X6：观察敌编队中心（编队级聚合，非逐兵）——每个可见敌编队中心写进 KnowledgeView，
                // 下游 Glue 查询时过滤（未见/超期 → 视为不存在，保守降级）。id = 敌队索引*100 + 编队索引
                // （Agent 观察已删，ID 域无碰撞，见 PerceptionPipeline.cs 头注）。
                foreach (Team et in mission.Teams)
                {
                    if (et == null || !et.MBTeam.IsValid || !et.IsEnemyOf(own)) continue; // P1-③ 真敌队判定
                    foreach (Formation ef in et.FormationsIncludingEmpty)
                    {
                        if (ef == null || ef.CountOfUnits == 0 || ef.QuerySystem == null) continue;
                        Vec2 c = TeamGeometry.GetFormationPosition(ef, TeamPosKind.CachedMedian); // P1-④ 统一口径
                        state.Perception.ObserveFormation(now, observer.x, observer.y, FormationKey(et, ef), c.x, c.y);
                    }
                }
            }
        }

        // X6/X7：敌编队 KnowledgeView 查询（供 4 个编队级 Glue 过滤）——
        //   FogOfWarEnabled=false（迷雾关=全知）→ 直通 true（现状全可见，不新增信息——Glue 仍只读
        //   己方聚合/公开编队位置，关闭不改变信息源，只去掉过滤）；
        //   开启 → 查询 own 队 KnowledgeView，未见/超期 → false（视为不存在，Glue 保守降级）。
        private bool IsEnemyFormationKnown(Team own, Formation enemyFormation)
        {
            if (enemyFormation == null) return false;
            if (!CombatBrainSettings.FogOfWarEnabled) return true; // X7 迷雾关=直通真实值（现状等价）
            TeamState state;
            if (!_states.TryGetValue(own.TeamIndex, out state)) return false;
            KnowledgeView.SightedInfo info;
            bool visible = state.View.TryGet(FormationKey(enemyFormation.Team, enemyFormation), _elapsed, out info);
            return PerceptionFilter.IsKnown(PerceptionEnabled, visible);
        }

        // N1 叙事关系提取（Team→NarrationInfo，供 Narrator.Notify 统一出口）：
        // 反作弊闸：只读公开阵营关系（IsPlayerTeam@Team.cs:40/IsPlayerAlly@Team.cs:52/IsEnemyOf@Team.cs:629）。
        public static Narrator.NarrationInfo NarrateInfo(Team team, Team playerTeam)
        {
            return new Narrator.NarrationInfo
            {
                IsPlayerTeam = team != null && team.IsPlayerTeam,
                IsPlayerAlly = team != null && team.IsPlayerAlly,
                IsEnemyOfPlayer = team != null && playerTeam != null && playerTeam.IsEnemyOf(team),
                HasPlayerTeam = playerTeam != null,
                FactionName = team != null ? FeedbackText.Faction(team) : string.Empty
            };
        }

        // atc4-state-lifecycle P1-4：叙事类别按 Team 区分（InGameFeedback 3s 去重键=category——
        //   多队同拍用通用类别会互相压制第二条提示；拼 teamIndex 后各队独立去重，语义不变）
        private static string NarrateCategory(Team own, string category)
        {
            return category + "_t" + (own != null ? own.TeamIndex.ToString() : "?");
        }

        // 编队 Key：敌队索引*100 + 编队索引（跨队不冲突；编队索引 <100 上限防御）
        private static int FormationKey(Team team, Formation formation)
        {
            return team.TeamIndex * 100 + (formation.Index >= 0 ? formation.Index : 0);
        }

        // ---------- F3 行为 LOD ----------

        private void TickLod(Mission mission, float dt)
        {
            _lodTimer -= dt;
            if (_lodTimer > 0f) return;
            _lodTimer = 0.5f;

            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;

            // T4 零每帧分配：复用模块域字典 + BufferPool 池化 List<Vec2>，不新建 Dictionary/List
            _enemyCenters.Clear();
            foreach (TeamState state in _states.Values)
            {
                _enemyCenters[state.Team.TeamIndex] = CollectEnemyCenters(state.Team, state.Team.TeamIndex & (BufferPool.SlotCount - 1));
            }

            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive()) continue;
                Team t = agent.Team;
                if (t == null) continue;

                List<Vec2> enemies;
                if (!_enemyCenters.TryGetValue(t.TeamIndex, out enemies)) enemies = null;

                float dp = VecMath.Dist(agent.Position.x, agent.Position.y, playerCenter.x, playerCenter.y);
                float de = NearestDistance(agent.Position.x, agent.Position.y, enemies);
                _agentLod[agent.Index] = BehaviorLodScheduler.Evaluate(dp, de);
            }
        }

        // ---------- F2 态势向量 + 意图引擎 ----------

        private void TickIntent(Mission mission, float dt)
        {
            _intentTimer -= dt;
            if (_intentTimer > 0f) return;
            _intentTimer = 0.5f;

            // P1-① 开战一次性预采样真实地形（战前填好；失败不重试，降级中性 0.5）
            EnsureTerrainCache(mission);

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                Team enemy = GetEnemyTeam(own);
                if (enemy == null) continue;

                int aliveSelf = own.ActiveAgents.Count;
                int aliveEnemy = enemy.ActiveAgents.Count;

                if (state.InitialCount < 0f) state.InitialCount = aliveSelf;

                // PenB 历史窗口：5 秒 @0.5s = 10 样本
                state.EnemyCountHistory.Enqueue(aliveEnemy);
                while (state.EnemyCountHistory.Count > 10) state.EnemyCountHistory.Dequeue();

                SituationVector v;
                ComposeVector(state, own, enemy, aliveSelf, aliveEnemy, out v);

                // F2：对手建模事件聚合（8 信号 → 信念）+ 填充新 4 维（EnemCav/FlankEx/Gap/Skirm）。
                // 读不懂（置信<0.5 或 熵>1.8）→ belief=null，不注入信念（t16 §2.1）
                float[] belief;
                _opponent.Update(own, enemy, _elapsed, ref v, out belief);
                state.LastVector = v;

                // N2 L0 战场事件 E1-E4（边沿检测，消费既有态势/信念/存活代理，不加新 tick）：
                //   E1 骑兵威胁 / E2a 缺口机会 / E2b 侧翼暴露 / E3 敌溃败 / E4 预测冲锋。
                //   全部输入=既有公开态势（反作弊闸不变）；只播无→有跨越（NarrativeEvents 迟滞带）。
                TickNarrativeEvents(state, own, enemy, v, belief);

                // ATC-1 战局阶段（队级五态上下文，0.5s 拍内；只更新状态+发事件/播报，不下命令）。
                // OwnSurvival=aliveSelf/InitialCount 真实存活比（强制修订2，绝不用 v.Moral）；
                // TimeInPhase=_elapsed-PhaseEnteredAt（强制修订4）；Stalemate=_stalemateActive（修订5）。
                TickBattlePhase(state, own, enemy, aliveSelf, aliveEnemy);

                // ATC-2 Army Context 影子计算（0.5s 拍内；MainBody/Objective 只算不改行为，迷雾一致）。
                _bench.BeginSection("coordination");
                TickArmyContext(state, own, enemy);
                TickFormationMissions(state, own, enemy); // ATC-3：Mission/Separation/Isolation 影子判定（不下命令）
                TickCommitment(state, own, enemy);        // ATC-4：Commitment 影子判定（只记录，零行为变化）
                _bench.EndSection("coordination");

                // P0-④ 玩家队豁免意图决策：意图是玩家指挥的领域——玩家队不 Evaluate/不发布
                // IntentSwitched（否则 F10 把自家引擎输出当玩家套路，循环自证；玩家队也不消费本层
                // 意图，4 个编队层 Glue 已同步豁免）。态势向量/士气照算（LastVector 仍供 GetMorale
                // 消费），仅跳过决策与事件发布。IsPlayerTeam @ Team.cs L40（v1.4.8 已核实）。
                if (CombatBrainSettings.PlayerTeamExemptionEnabled && own.IsPlayerTeam) continue; // P0-④（MCM 可开关 t66）：豁免关闭=意图决策也接管玩家队

                IntentType newIntent;
                if (state.Engine.Evaluate(v, belief, 0.5f, out newIntent))
                {
                    // F10 反制换招：被冷宫意图 → PickBest 换招；开局预判（仅一次、非玩家队）
                    int adjusted = _experience.AdjustIntent(own.TeamIndex, (int)newIntent);
                    int opening;
                    if ((mission.PlayerTeam == null || own != mission.PlayerTeam)
                        && _experience.TryGetOpening(own.TeamIndex, out opening))
                    {
                        adjusted = opening;
                    }
                    newIntent = (IntentType)adjusted;

                    state.LastIntent = newIntent;
                    DecisionObservatory.Observe(DecisionObservatory.BIntentSwitch); // T1 决策分支计数
                    _bus.Publish(new CombatEvent
                    {
                        Kind = CombatEventKind.IntentSwitched,
                        Time = _elapsed,
                        Id = own.TeamIndex,
                        IntValue = (int)newIntent
                    });
                    CombatLog.Log(string.Format(
                        "Team {0} 意图切换 → {1}  [{2}]",
                        own.TeamIndex, newIntent, v.ToString()));
                    Narrator.Notify(NarrateInfo(own, Mission.PlayerTeam), NarrateCategory(own, "意图"), // N1 统一出口（P1-4：category 按 Team 区分，多队同拍不互相压制）
                        // t62 战术层播报：{S}+战术名+态势摘要（正常模式显示；hold 6s 天然限频 +
                        // NotifyTactical 3s 去重/速率上限防刷屏；编队级跃迁仍为细节行）。
                        // narrate-fix P0：模板含 {S} 占位符不得经 string.Format（{S} 非合法格式项→FormatException
                        // 崩溃）；动态值用拼接，{S} 保留给 Narrator.ComposeText 代填。文案逐字保持。
                        "{S}战术：" + FeedbackText.Intent(newIntent) + "——" + FeedbackText.TacticBrief(newIntent),
                        InGameFeedback.TacticalColor, InGameFeedback.NotifyTactical);

                    // N3 L1 动态原因（§4）：缓冲内最近未过期 L0 事件 → 追加第二条 toast。
                    // category 用新 key "意图.原因"（不得用 "意图"——否则撞 3s 同类去重被吞）。
                    // 无事件/过期 → 不发（回退=现状，固定 TacticBrief 已够）。反作弊闸：只读既有事件缓冲。
                    NarrativeEventSlot cause = state.EventBuffer.PeekFresh(_elapsed);
                    if (cause.Kind != NarrativeEventKind.None)
                    {
                        string causeText = FeedbackText.CauseOf((int)cause.Kind);
                        if (!string.IsNullOrEmpty(causeText))
                        {
                            Narrator.Notify(NarrateInfo(own, Mission.PlayerTeam), NarrateCategory(own, "意图.原因"),
                                "{S}——" + causeText, // narrate-fix P0：模板含 {S} 不经 string.Format（防 FormatException），拼接保 {S} 代填
                                InGameFeedback.TacticalColor, InGameFeedback.NotifyTactical);
                        }
                    }

                    // N2 E5 战术转折（特殊紫）：仅跨战略大类跳变时播（攻/守/撤/机动，§3 映射表）。
                    string newClass = FeedbackText.StrategicClass(newIntent);
                    if (state.LastStrategicClass != null && state.LastStrategicClass != newClass)
                    {
                        Narrator.Notify(NarrateInfo(own, Mission.PlayerTeam), NarrateCategory(own, "事件"),
                            "战局变化：{S}由" + state.LastStrategicClass + "转入" + newClass, // narrate-fix P0：{S} 不经 string.Format
                            InGameFeedback.SpecialColor, InGameFeedback.NotifyTactical);
                        state.EventBuffer.Push(NarrativeEventKind.E5TacticalShift, _elapsed); // E5 也入缓冲（原因）
                    }
                    state.LastStrategicClass = newClass;
                }
            }
        }

        // ATC-2 Army Context 影子计算（0.5s 队级拍；MainBody/Objective 只算不改行为）。
        // 输入=既有公开聚合（Formation.QuerySystem 兵种旗标/CountOfUnits/CachedMedianPosition/
        //   CachedFormationIntegrityData）+ X6 迷雾（IsEnemyFormationKnown 过滤敌编队，不绕过）。
        // OPT-1：单拍协调帧——本方法构建 Frame（CoordinationFrameBuilder 一次采集，FogOfWar 标记统一确定），
        //   Mission/Commitment 同拍复用（禁止重复构建 ownSnaps/enemySnaps）；帧每拍重建，仅拍内有效。
        // OPT-3：Army 日志节流每 Team 独立（state.ArmyLogTimer，OnMissionTick dt 递减）；坐标变化用距离阈值
        //   ≥3m（CoordinationController.ArmyLogChanged）而非浮点精确不等；离散状态改变才记录；诊断字段保留。
        // 反作弊闸：零新增禁读；O(编队数²)；coordination Bench 段可单独观测。
        private void TickArmyContext(TeamState state, Team own, Team enemy)
        {
            // 单拍协调帧：清空复用值列表后一次采集（己方全量 + 敌迷雾过滤；FrameValid 标记拍内有效）
            state.FrameOwn.Clear();
            state.FrameEnemy.Clear();
            CoordinationFrameBuilder.BuildOwn(own, state.FrameOwn);
            CoordinationFrameBuilder.BuildEnemy(own, enemy, IsEnemyFormationKnown, state.FrameEnemy);
            state.FrameValid = true;

            // Main Body（最大连通簇，§4.1）复用帧
            var main = MainBodySelector.Select(state.FrameOwn);
            // Operational Objective（已知敌最大有效兵力簇，§4.2；迷雾一致）复用帧
            var obj = OperationalObjectiveSelector.Select(state.FrameEnemy);

            var snap = new ArmyContextSnapshot
            {
                Phase = state.Phase,
                MainBodyX = main.X, MainBodyY = main.Y,
                MainBodyStrength = main.Strength,
                MainBodyConfidence = main.Confidence,
                HasOperationalObjective = obj.HasObjective,
                ObjectiveFormationId = obj.FormationId,
                ObjectiveX = obj.X, ObjectiveY = obj.Y,
                ObjectiveStrength = obj.Strength
            };

            // 日志：变化（距离阈值/离散状态）或 5s 兜底才写（OPT-3 降噪；每 Team 独立节流）
            bool changed = CoordinationController.ArmyLogChanged(state.Army, snap,
                CoordinationController.MainBodyMoveLogMeters);
            state.Army = snap;
            if (changed || state.ArmyLogTimer <= 0f)
            {
                state.ArmyLogTimer = 5f;
                CombatLog.Log(CoordinationController.FormatArmyLog(own.TeamIndex, snap));
            }
        }

        // 编队快照已迁至 CoordinationFrameBuilder.BuildSnapshot（OPT-4 有限拆分；单拍协调帧复用）

        // ATC-3 Formation Mission / Separation / Isolation 影子判定（0.5s 队级拍）。
        // 只产出判断与 Why 日志，不下召回命令（ATC 规格 §4.4/§9）；Isolation 输出仅作 CommitmentReason
        // 供 ATC-4 消费。输入=既有公开聚合（兵种/阶段/意图/Morale 撤退态/距 MainBody 距离/存活比）。
        // 反作弊闸：零新增禁读；O(编队数²)。
        private void TickFormationMissions(TeamState state, Team own, Team enemy)
        {
            // atc4-state-lifecycle P1-1/P1-4：编队解散/清空惰性清理（Bannerlord 编队不解散对象，
            //   CountOfUnits==0 即空——FormationsIncludingEmpty 枚举含空编队；非空集合外的字典键为残留，
            //   清理防 FormationId 复用继承旧状态，同时联动 CommandProvenance.RemoveFormation）
            CleanupFormationState(state, own);
            float mbX = state.Army.MainBodyX, mbY = state.Army.MainBodyY;
            float aliveSelf = own.ActiveAgents.Count;
            int aliveEnemy = enemy != null ? enemy.ActiveAgents.Count : 0;
            float localRatio = aliveEnemy > 0 ? aliveSelf / (float)aliveEnemy : 1f; // 局部兵力比近似（队级存活比）
            // atc4-reserve-classifier P0-4：ATC-3 接敌近似（队级口径：编队距敌队中心 ≤ ReserveClassifier.ContactMeters；
            //   ATC-4 用 CommitmentContact 编队级精确口径——Reserve 判定源唯一，输入近似层级不同，注释说明）
            Vec2 enemyCenter = enemy != null ? GetTeamCenter(enemy) : Vec2.Zero;
            bool hasEnemy = enemy != null;

            foreach (Formation f in own.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                Vec2 pos = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                float dist = (float)Math.Sqrt((pos.x - mbX) * (pos.x - mbX) + (pos.y - mbY) * (pos.y - mbY));
                // atc4-fix finding1：ATC-3 用独立 MissionPrevDist 基线（per-formation；首拍缺省 0=无趋势）
                float prevDist;
                if (!state.MissionPrevDist.TryGetValue(f.Index, out prevDist)) prevDist = 0f;
                bool distIncreasing = FormationDistanceTrend.IsIncreasing(prevDist, dist);

                // §9 任务映射输入（P0-4：Reserve 判定走 ReserveClassifier 唯一源——主动任务/接敌硬否决 +
                //   距离辅助 + 迟滞防抖，替代旧「距 MainBody<30m 即 Reserve」单条件误判）
                FormationCoordKind kind = MapKind(f);
                string moraleState = _morale.GetStateName(f); // F11 撤退 FSM（"-"=无记录）
                bool inRetreat = moraleState != "-" && moraleState != "Hold"; // 非 Hold 即撤退中
                bool inContact = hasEnemy
                    && VecMath.Dist(pos.x, pos.y, enemyCenter.x, enemyCenter.y) <= ReserveClassifier.ContactMeters;
                bool lastReserve;
                if (!state.MissionReserveLast.TryGetValue(f.Index, out lastReserve)) lastReserve = false;
                bool isReserve = ReserveClassifier.IsReserve(new ReserveInput
                {
                    IsActiveTask = ReserveClassifier.IsActiveIntent(state.LastIntent),
                    InContact = inContact,
                    ExplicitReserve = false, // 第一版无显式预备队命令源，预留
                    MainBodyDistance = dist
                }, lastReserve);
                state.MissionReserveLast[f.Index] = isReserve;
                var mission = FormationMissionAssigner.Assign(new MissionInput
                {
                    Kind = kind,
                    Phase = state.Phase,
                    Intent = state.LastIntent,
                    InRetreat = inRetreat,
                    IsReserve = isReserve && state.Army.MainBodyStrength > 0 // 无主力存在时不标 Reserve
                });

                // §4.4 隔离判定（SupportScore 第一版近似：MainBody 兵力占比；ArrivalGap 缺省=false）
                var iso = IsolationDetector.Evaluate(new IsolationInput
                {
                    MainBodyDistance = dist,
                    DistanceIncreasing = distIncreasing,
                    SupportScore = state.Army.MainBodyStrength > 0
                        ? Math.Min(1f, f.CountOfUnits / (float)state.Army.MainBodyStrength) : 0f,
                    LocalForceRatio = localRatio,
                    EnemyArrivesFirst = false,
                    Phase = state.Phase,
                    Separation = mission.Separation,
                    Emergency = false,
                    InRetreat = inRetreat
                });

                state.MissionPrevDist[f.Index] = dist; // ATC-3 per-formation 基线更新（atc4-fix finding1 独立字典）
                // atc4-state-lifecycle P1-1：per-formation 输出——变化日志按编队比较（单值残留 bug 修复）
                bool changed;
                FormationMission lastMission;
                if (!state.Missions.TryGetValue(f.Index, out lastMission)) { changed = true; }
                else
                {
                    SeparationIntent lastSep;
                    IsolationDecision lastIso;
                    bool sepOk = state.Separations.TryGetValue(f.Index, out lastSep) && mission.Separation == lastSep;
                    bool isoOk = state.Isolations.TryGetValue(f.Index, out lastIso)
                        && iso.UnauthorizedIsolation == lastIso.UnauthorizedIsolation;
                    changed = mission.Mission != lastMission || !sepOk || !isoOk;
                }
                state.Missions[f.Index] = mission.Mission;
                state.Separations[f.Index] = mission.Separation;
                state.Isolations[f.Index] = iso;
                if (changed)
                {
                    CombatLog.Log(string.Format(
                        "[Mission] Team {0} f={1} mission={2} sep={3} iso={4} dist={5:F0} mb=({6:F0},{7:F0})",
                        own.TeamIndex, f.Index, mission.Mission, mission.Separation,
                        iso.UnauthorizedIsolation ? "UnauthorizedIsolation" : "OK",
                        dist, mbX, mbY));
                }
            }
        }

        // atc4 nits 收口：贴身接战阈值（编队级近似）——己方编队中心与最近已知敌编队中心 ≤20m 视为近战接触中
        private const float CommitmentContactMeters = 20f;

        // ATC-4 Commitment 影子判定（0.5s 队级拍，零行为变化）：对每个己方 Formation 的候选投入做统一
        // 许可判断，只记录「若执行层接线将允许/拒绝什么」，不 SetMovementOrder / 不下召回 / 不动
        // FormationTargeting 优先级（规格 §10：影子模式）。消费：ATC-1 Phase + ATC-2 Army（Objective/
        // MainBody）+ ATC-3 任务映射纯函数（per-formation 重算，不触碰 state.Mission 队级近似）+ X1 目标缓存
        // （TacticalTarget 对齐度）。§4.3 编队级近似经 CommitmentEstimator 纯函数（O(F²)，禁逐兵）。
        // Fog 一致：敌快照经 IsEnemyFormationKnown 过滤后传入（未知敌不参与 EnemyLocalForce，§2.2.2）。
        // 日志：Permission/Reasons 变化才写（[Commit] 行 §14 固定字段顺序），禁每拍每队刷。
        private void TickCommitment(TeamState state, Team own, Team enemy)
        {
            // OPT-1：复用单拍协调帧（TickArmyContext 本拍已构建，禁止重复构建 ownSnaps/enemySnaps）。
            // 防御：FrameValid=false（异常路径：ArmyContext 未跑/失败）→ 本拍跳过判定（影子层零命令，安全）
            if (!state.FrameValid) return;
            var ownSnaps = state.FrameOwn;
            var enemySnaps = state.FrameEnemy;

            float mbX = state.Army.MainBodyX, mbY = state.Army.MainBodyY;
            // atc4-fix finding2：DefensiveOverride 为队级布尔（全队统一），设计理由（保持现状，非 per-formation）：
            //   BraceGlue.IsBracing/BraceGlue.cs:50 与 ProtectGlue.IsProtecting/ProtectGlue.cs:106 的公开状态
            //   均为队级（键=TeamIndex，_episode/_protecting 队级字典）——BraceFormation/ProtectArcher 是 F2 意图
            //   引擎的队级意图，Brace/Protect 以队级剧集状态驱动全队防御；队级防御激活时全队编队均处
            //   Immediate Defensive 语境（§4.5 高优先），影子判定统一 Limited 授权是保守正确近似（不误杀防御动作、
            //   不被普通 Denied 锁死）。per-formation 化需先给 BraceGlue/ProtectGlue 增编队级查询接口（涉及
            //   Ai/ 其它文件，超出本卡范围），后续 feature 再做。
            bool defensiveActive = _brace.IsBracing(own.TeamIndex) || _protect.IsProtecting(own.TeamIndex);

            foreach (Formation f in own.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // 编队层下令系口径
                FormationCoordSnapshot self = FormationCoordSnapshot.Empty;
                for (int i = 0; i < ownSnaps.Count; i++)
                {
                    if (ownSnaps[i].FormationId == f.Index) { self = ownSnaps[i]; break; }
                }
                if (self.FormationId < 0) continue;

                float dist = (float)Math.Sqrt((fc.x - mbX) * (fc.x - mbX) + (fc.y - mbY) * (fc.y - mbY));
                // atc4-fix finding1：ATC-4 用独立 CommitmentPrevDist 基线（per-formation；首拍缺省 0=无趋势）。
                // 与 ATC-3 的 MissionPrevDist 互不读写——TickFormationMissions 先写本拍不影响本层读到上一拍基线，
                // distIncreasing 真实反映距离趋势（FormationDistanceTrend.IsIncreasing 纯函数）。
                float prevDist;
                if (!state.CommitmentPrevDist.TryGetValue(f.Index, out prevDist)) prevDist = 0f;
                bool distIncreasing = FormationDistanceTrend.IsIncreasing(prevDist, dist);

                // atc4-fog-contact-fix P0-2：InContact 判定走 CommitmentContact.IsInContact 纯函数——
                //   只消费 IsKnown=true 的敌编队（未知敌不参与接敌状态/局部兵力/接触 ETA/Commitment 等级及原因码，
                //   统一 IsKnown 迷雾契约）；TeamGeometry CachedMedian 口径；纪律2 由此真实生效：
                //   接战中返回 Limited 而非突然 Denied 原地不动。
                bool inContact = CommitmentContact.IsInContact(fc.x, fc.y, enemySnaps, CommitmentContactMeters);

                // 候选目标：X1 目标缓存优先（TacticalTarget）；无 → Operational Objective 中心；再无 → 无候选（跳评）
                TargetSelection sel;
                float tx, ty;
                bool hasTarget;
                if (TryGetFormationTarget(f, out sel) && sel.HasTarget)
                {
                    tx = sel.X; ty = sel.Y; hasTarget = true;
                }
                else if (state.Army.HasOperationalObjective)
                {
                    tx = state.Army.ObjectiveX; ty = state.Army.ObjectiveY; hasTarget = true;
                }
                else
                {
                    tx = 0f; ty = 0f; hasTarget = false;
                }
                if (!hasTarget)
                {
                    // atc4-state-lifecycle P1-5：无候选目标（无 Army Objective 且无 X1 目标）不得 continue
                    //   残留旧 Commitment——写入明确 NoObjective/Hold 决策并产生一次状态变化记录，
                    //   后续日志/消费者不读取过期 Commitment。
                    var noTarget = new CommitmentDecision
                    {
                        Permission = CommitmentPermission.Hold,
                        Reasons = CommitmentReason.NoArmyObjective,
                        LocalForceRatio = 0f, SupportScore = 0f, IsolationRisk = false,
                        FriendlyArrivalEta = 0f, EnemyContactEta = 0f
                    };
                    CommitmentDecision old;
                    bool changedNoTarget = !state.Commitments.TryGetValue(f.Index, out old)
                        || old.Permission != noTarget.Permission || old.Reasons != noTarget.Reasons;
                    state.Commitments[f.Index] = noTarget;
                    if (changedNoTarget)
                    {
                        CombatLog.Log(string.Format(
                            "[Commit] Team {0} f={1} permission={2} reasons={3} (no objective, stale cleared)",
                            own.TeamIndex, f.Index, noTarget.Permission, noTarget.Reasons));
                    }
                    continue;
                }

                // §4.3 编队级近似（含 TacticalTarget 对齐度；P0-3：传 selfFormationId 由估算器跳过自身，
                //   ownSnaps 含自身不重复计入 SupportEff/AllFriendlyEff/LocalForceRatio）
                LocalForceEstimate est = CommitmentEstimator.Estimate(
                    self.FormationId, fc.x, fc.y, tx, ty, self.NominalSpeed,
                    self.Count, self.Kind, self.Integrity, ownSnaps, enemySnaps);
                bool aligned = VecMath.Dist(tx, ty, state.Army.ObjectiveX, state.Army.ObjectiveY) <= CommitmentEvaluator.AlignMeters;

                // ATC-3 任务映射（per-formation 重算：影子判定独立，不触碰 state.Mission 队级近似）
                FormationCoordKind kind = self.Kind;
                string moraleState = _morale.GetStateName(f); // F11 撤退 FSM（"-"=无记录）
                bool inRetreat = moraleState != "-" && moraleState != "Hold";
                // atc4-reserve-classifier P0-4：Reserve 判定走 ReserveClassifier 唯一源（与 ATC-3 同判定逻辑，
                //   InContact 用编队级 CommitmentContact 精确口径；迟滞状态独立防同拍先写后读）
                bool lastReserve;
                if (!state.CommitmentReserveLast.TryGetValue(f.Index, out lastReserve)) lastReserve = false;
                bool isReserve = ReserveClassifier.IsReserve(new ReserveInput
                {
                    IsActiveTask = ReserveClassifier.IsActiveIntent(state.LastIntent),
                    InContact = inContact,
                    ExplicitReserve = false,
                    MainBodyDistance = dist
                }, lastReserve);
                state.CommitmentReserveLast[f.Index] = isReserve;
                var mission = FormationMissionAssigner.Assign(new MissionInput
                {
                    Kind = kind,
                    Phase = state.Phase,
                    Intent = state.LastIntent,
                    InRetreat = inRetreat,
                    IsReserve = isReserve && state.Army.MainBodyStrength > 0
                });

                CommitmentDecision decision = CommitmentEvaluator.Evaluate(new CommitmentInput
                {
                    Phase = state.Phase,
                    Mission = mission.Mission,
                    Separation = mission.Separation,
                    HasOperationalObjective = state.Army.HasOperationalObjective,
                    TargetAlignedWithObjective = aligned,
                    MainBodyDistance = dist,
                    DistanceIncreasing = distIncreasing,
                    FriendlySupport = est.FriendlySupport,
                    LocalForceRatio = est.LocalForceRatio,
                    FriendlyArrivalEta = est.FriendlyEta,
                    EnemyContactEta = est.EnemyContactEta,
                    Integrity = self.Integrity,
                    IsReserve = isReserve && state.Army.MainBodyStrength > 0, // 与 ATC-3 同口径（ReserveClassifier 唯一源）
                    InContact = inContact,           // ④ 接线：距最近已知敌编队 ≤20m（编队级近似，纪律2 生效）
                    DefensiveOverride = defensiveActive, // ④ 接线：Brace/Protect 激活（既有公开状态，§4.5 优先）
                    EmergencyOverride = false // ④ 收口：编队级无既有 Emergency 聚合——个体级 SurvivalGlue 属 LOD
                                              //   逐 agent 层（F7），不宜提升为队级 Emergency 信号；保持 false
                                              //   预留，未来接入队级存活骤降/全队溃逃信号时再接线
                });

                // 日志：Permission/Reasons 变化才写（限频；§14 [Commit] 固定字段顺序）
                state.CommitmentPrevDist[f.Index] = dist; // ATC-4 per-formation 基线更新（atc4-fix finding1 独立字典）
                CommitmentDecision last;
                bool changed = !state.Commitments.TryGetValue(f.Index, out last)
                    || last.Permission != decision.Permission || last.Reasons != decision.Reasons;
                state.Commitments[f.Index] = decision;
                if (changed)
                {
                    CombatLog.Log(string.Format(
                        "[Commit] Team {0} f={1} mission={2} sep={3} aligned={4} support={5:F2} ratio={6:F2} " +
                        "friendlyEta={7:F1} enemyEta={8:F1} isolation={9} permission={10} reasons={11}",
                        own.TeamIndex, f.Index, mission.Mission, mission.Separation, aligned ? 1 : 0,
                        decision.SupportScore, decision.LocalForceRatio,
                        decision.FriendlyArrivalEta, decision.EnemyContactEta,
                        decision.IsolationRisk ? 1 : 0, decision.Permission, decision.Reasons));
                }
            }
        }

        // atc4-state-lifecycle P1-1/P1-4：编队解散/清空惰性清理——清理非空编队集合外的 ATC 状态字典键
        //   （Missions/Separations/Isolations/Commitments/距离基线/Reserve 迟滞），并联动
        //   CommandProvenance.RemoveFormation（P1-3④ 编队解散挂点：Bannerlord 无 OnFormationRemoved 钩子，
        //   以 CountOfUnits==0 惰性判定）。FormationId 复用不得继承旧状态。
        private void CleanupFormationState(TeamState state, Team own)
        {
            var valid = new HashSet<int>();
            foreach (Formation f in own.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                valid.Add(f.Index);
            }
            var stale = new List<int>();
            CollectStale(state.Missions, valid, stale);
            CollectStale(state.Separations, valid, stale);
            CollectStale(state.Isolations, valid, stale);
            CollectStale(state.Commitments, valid, stale);
            CollectStale(state.MissionPrevDist, valid, stale);
            CollectStale(state.CommitmentPrevDist, valid, stale);
            CollectStale(state.MissionReserveLast, valid, stale);
            CollectStale(state.CommitmentReserveLast, valid, stale);
            for (int i = 0; i < stale.Count; i++)
            {
                CommandProvenance.Instance.RemoveFormation(stale[i]); // 幂等：ring 无该编队记录也安全
            }
        }

        private static void CollectStale<T>(Dictionary<int, T> map, HashSet<int> valid, List<int> stale)
        {
            var removed = new List<int>();
            foreach (int k in map.Keys)
            {
                if (!valid.Contains(k)) removed.Add(k);
            }
            for (int i = 0; i < removed.Count; i++)
            {
                map.Remove(removed[i]);
                if (!stale.Contains(removed[i])) stale.Add(removed[i]); // 去重（CommandProvenance 清理用，小列表 O(n)）
            }
        }

        // 编队兵种映射（QuerySystem 旗标 → ATC-2 五类；与 BuildSnapshot 同口径）
        private static FormationCoordKind MapKind(Formation f)
        {
            return f.QuerySystem.IsRangedCavalryFormation ? FormationCoordKind.RangedCavalry
                : f.QuerySystem.IsCavalryFormation ? FormationCoordKind.MeleeCavalry
                : f.QuerySystem.IsRangedFormation ? FormationCoordKind.Ranged
                : f.QuerySystem.IsInfantryFormation ? FormationCoordKind.Infantry
                : FormationCoordKind.Other;
        }

        // N2 L0 战场事件 E1-E4（§3）：边沿检测 + Narrator.Notify 播报。消费既有态势/信念/存活代理，
        // 不加新 tick（挂在 TickIntent 0.5s 拍内）。反作弊闸：全部输入=既有公开态势与既有信念。
        private void TickNarrativeEvents(TeamState state, Team own, Team enemy, SituationVector v, float[] belief)
        {
            bool dominantFrontal = _opponent.LastDominantIntent == EnemyIntent.FrontalAttack;
            float dominantConf = _opponent.LastDominantConf;
            float routRatio = _opponent.LastRoutRatio;

            NarrativeEventKind evt = NarrativeEvents.Tick(
                v.EnemCav, v.FlankEx, v.Gap, routRatio,
                dominantFrontal, dominantConf,
                ref state.LatchE1, ref state.LatchE2a, ref state.LatchE2b, ref state.LatchE3, ref state.LatchE4);
            if (evt == NarrativeEventKind.None) return;

            uint color;
            switch (evt)
            {
                case NarrativeEventKind.E1CavalryThreat: color = InGameFeedback.CrisisColor; break;
                case NarrativeEventKind.E2aGapOpportunity: color = InGameFeedback.AdvantageColor; break;
                case NarrativeEventKind.E2bFlankExposed: color = InGameFeedback.CrisisColor; break;
                case NarrativeEventKind.E3EnemyRout: color = InGameFeedback.AdvantageColor; break;
                default: color = InGameFeedback.SpecialColor; break; // E4PredictCharge
            }
            Narrator.Notify(NarrateInfo(own, Mission.PlayerTeam), NarrateCategory(own, "事件"),
                FeedbackText.BattlefieldEvent((int)evt), color, InGameFeedback.NotifyTactical,
                NarrateInfo(enemy, Mission.PlayerTeam)); // {O}=敌队（T 的敌队）

            // N3 L1 事件缓冲写入（§4）：L0 事件触发时记入 ring 2，供意图切换后原因第二条 toast 消费。
            state.EventBuffer.Push(evt, _elapsed);
        }

        // ATC-1 战局阶段推进（0.5s 队级拍，只更新状态+发事件/播报，不下命令）。
        // 强制修订落实：OwnSurvival=aliveSelf/InitialCount 真实存活比（修订2）；Retreat 终态（修订3）；
        //   TimeInPhase=_elapsed-PhaseEnteredAt（修订4）；Stalemate 不触发转移（修订5）；
        //   EnemyWeakening 需配合敌存活<阈值或 Pursue 才进 Exploit（修订6）。
        // P1-2：①稳定敌军初始基线=state.EnemyInitialCount（开战首拍缓存最近敌队存活数，绝不用滚动队列
        //   Peek 代替开战初始值；多敌队口径：GetEnemyTeam 取最近敌队，基线=该敌队开战存活数）；
        //   ②Advance→EnemyContact 接入真实且符合迷雾约束的接触信号（己方编队与已知敌编队中心 ≤20m，
        //   未知敌不触发）——无接触不得仅因计时结束进入 Engage；最短驻留保留。
        // 反作弊闸：输入=己方/敌方存活聚合（ActiveAgents 公开）+ 编队中心（TeamGeometry）+ 既有僵局判定，
        //   零新增禁读；O(编队数²) 0.5s 拍。
        private void TickBattlePhase(TeamState state, Team own, Team enemy, int aliveSelf, int aliveEnemy)
        {
            float ownSurvival = state.InitialCount > 0f ? (float)aliveSelf / state.InitialCount : 1f; // 修订2
            // P1-2①：稳定敌军初始基线（首拍缓存；滚动队列 Peek 已弃用）
            if (state.EnemyInitialCount < 0f) state.EnemyInitialCount = aliveEnemy;
            float enemySurvival = state.EnemyInitialCount > 0f ? aliveEnemy / state.EnemyInitialCount : 1f;

            // P1-2②：真实且符合迷雾约束的敌接触（己方编队 vs 已知敌编队 ≤20m；未知敌不触发）
            bool enemyContact = false;
            if (enemy != null)
            {
                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                    foreach (Formation ef in enemy.FormationsIncludingEmpty)
                    {
                        if (ef == null || ef.CountOfUnits == 0 || ef.QuerySystem == null) continue;
                        if (!IsEnemyFormationKnown(own, ef)) continue; // 迷雾约束：未知敌不触发接触
                        Vec2 ec2 = TeamGeometry.GetFormationPosition(ef, TeamPosKind.CachedMedian);
                        if (VecMath.Dist(fc.x, fc.y, ec2.x, ec2.y) <= ReserveClassifier.ContactMeters)
                        {
                            enemyContact = true;
                            break;
                        }
                    }
                    if (enemyContact) break;
                }
            }

            var input = new BattlePhaseInput
            {
                OwnSurvival = ownSurvival,
                EnemySurvival = enemySurvival,
                Stalemate = _stalemateActive,          // 修订5：不触发转移
                EnemyContact = enemyContact,           // P1-2②：真实接触才 Advance→Engage
                EnemyWeakening = enemySurvival < 0.4f,  // 敌弱化信号（修订6 条件之一）
                Intent = state.LastIntent,             // Pursue 直接触发 Exploit（修订6）
                TimeInPhase = _elapsed - state.PhaseEnteredAt // 修订4：相位内时间
            };

            BattlePhaseResult r = BattlePhaseFSM.Advance(state.Phase, input);
            if (!r.Changed) return; // 同态不重播（修订5：不发事件/播报）

            BattlePhase prev = state.Phase;
            state.Phase = r.Phase;
            state.PhaseEnteredAt = _elapsed;

            // 事件发布（BattlePhaseChanged；只发状态迁移，同态不发）
            _bus.Publish(new CombatEvent
            {
                Kind = CombatEventKind.BattlePhaseChanged,
                Time = _elapsed,
                Id = own.TeamIndex,
                IntValue = (int)r.Phase
            });
            CombatLog.Log(string.Format(
                "[BattlePhase] Team {0} {1} → {2} (ownSurv={3:F2} enemSurv={4:F2} intent={5})",
                own.TeamIndex, prev, r.Phase, ownSurvival, enemySurvival, state.LastIntent));

            // 播报（Narrator 统一出口，阶段短文案；只播迁移瞬间）
            string phaseText = FeedbackText.BattlePhaseText((int)r.Phase);
            if (!string.IsNullOrEmpty(phaseText))
            {
                Narrator.Notify(NarrateInfo(own, Mission.PlayerTeam), NarrateCategory(own, "战局"),
                    "{S}进入" + phaseText, // narrate-fix P0：{S} 不经 string.Format（防 FormatException），拼接保 {S} 代填
                    InGameFeedback.TacticalColor, InGameFeedback.NotifyTactical);
            }
        }

        private void ComposeVector(
            TeamState state, Team own, Team enemy,
            int aliveSelf, int aliveEnemy, out SituationVector v)
        {
            v = new SituationVector();

            int total = aliveSelf + aliveEnemy;
            float ratio = total > 0 ? (float)aliveSelf / (float)total : 0.5f;
            v.Power = VecMath.LogMapRatio(ratio);

            Vec2 oc = GetTeamCenter(own);
            Vec2 ec = GetTeamCenter(enemy);

            // Dist: 0 = 贴身, 1 = 很远
            v.Dist = VecMath.Clamp01(VecMath.Dist(oc.x, oc.y, ec.x, ec.y) / 80f);

            // Height: 地形优劣 = 己方中心相对敌方中心的高度优势
            // P1-①：真实地形查表（开战预缓存，O(1)）；无表（未就绪/构建失败）→ hDiff=0 → 中性 0.5，宁缺毋假
            float hDiff = 0f;
            if (_terrain != null)
            {
                hDiff = _terrain.Height(oc.x, oc.y) - _terrain.Height(ec.x, ec.y);
            }
            v.Height = VecMath.Clamp01(0.5f + hDiff * 0.15f);

            // Line: 阵线宽度对比——己方是否被拉散（编队离散度 vs 敌方）
            float ownSpread = FormationSpread(own);
            float enemySpread = FormationSpread(enemy);
            float spreadSum = ownSpread + enemySpread;
            v.Line = spreadSum > 1e-4f ? 1f - VecMath.Clamp01(ownSpread / spreadSum) : 0.5f;

            // Moral: 存活率衰减（士气代理）
            float survival = state.InitialCount > 0f ? aliveSelf / state.InitialCount : 1f;
            v.Moral = VecMath.Clamp01(survival);

            // PenB: 敌方虚弱度 = 过去 5 秒敌方兵力下降斜率
            v.PenB = ComputePenB(state);

            // Cav / Arc: 编队兵种聚合（Team.QuerySystem 引擎缓存，可观察、非全图逐兵）
            // Cav = 己方骑兵占比（含骑射）；Arc = 有效射手占比 × 弹药/在射代理
            TeamQuerySystem ownQs = own.QuerySystem;
            if (ownQs != null)
            {
                v.Cav = VecMath.Clamp01(ownQs.CavalryRatio + ownQs.RangedCavalryRatio);
                float ranged = ownQs.RangedRatio;                 // 有效射手占比
                float firing = FormationFiringRatio(own);         // 实际在射比例（弹药余量代理）
                v.Arc = VecMath.Clamp01(ranged * (0.35f + 0.65f * firing));
            }
            else
            {
                v.Cav = 0.5f;
                v.Arc = 0.5f;
            }
        }

        // P1-① 真实地形预缓存（开战一次性，主线程）：
        // v1.4.8 API 已核实（官方反编译源 Scene.cs L1089-1092）：
        //   public float GetTerrainHeight(Vec2 position, bool checkHoles = true)
        // 引用证据：Team.cs L561 `Mission.Scene.GetTerrainHeight(averagePositionOfUnits)`（编队均值位置查地形，
        // 与本模块同型）；Mission.cs L1089 `public Scene Scene { get; private set; }`。
        // 采样：锚点=玩家队（无玩家→第一个队）编队中心 ±300m、8m 网格 ≈5.8k 点，一次性 <10ms 量级；
        // 战中只读查表 O(1)，无实时射线。T0：数组战前填好后只读，无锁共享线程安全。
        private void EnsureTerrainCache(Mission mission)
        {
            if (_terrainBuilt) return;

            Team anchor = mission.PlayerTeam;
            if (anchor == null)
            {
                foreach (TeamState st in _states.Values)
                {
                    anchor = st.Team;
                    break;
                }
            }
            if (anchor == null)
            {
                _terrainBuilt = true; // 无队可锚定：不再重试，Height 维降级中性 0.5
                return;
            }

            Vec2 c = GetTeamCenter(anchor);
            if (c == Vec2.Zero && _elapsed < 5f) return; // 编队未就绪（部署中）：延迟重试，5s 后按当前值兜底
            _terrainBuilt = true;

            var scene = mission.Scene;
            if (scene == null)
            {
                CombatLog.Log("TerrainSampler: Mission.Scene 不可用，Height 维降级中性 0.5");
                return;
            }

            try
            {
                _terrain = TerrainSampler.Build(
                    c.x, c.y, TerrainHalfExtent, TerrainCellSize,
                    (x, y) => scene.GetTerrainHeight(new Vec2(x, y)));
                foreach (TeamState st in _states.Values)
                {
                    st.Perception.SetTerrain(_terrain); // 与态势向量共用同一查表（感知停用中，接线保持可逆）
                }
                CombatLog.Log(string.Format(
                    "TerrainSampler 就绪: center=({0:F0},{1:F0}) extent={2}m cell={3:F1}m points={4}",
                    c.x, c.y, TerrainHalfExtent, _terrain.Step, _terrain.Points));
            }
            catch (Exception e)
            {
                _terrain = null; // 宁缺毋假：构建失败 → Height 维中性 0.5
                CombatLog.Log("TerrainSampler 构建失败（Height 维降级中性 0.5）: " + e.Message);
            }
        }

        // 日志增强：单队身份行（Side/阵营名/与玩家关系），开战一次。
        // 阵营名 = FeedbackText.CultureId（t54 士兵文化多数决；GeneralAgent 文化自定义战斗不可靠——
        // 实测选帝国读空/误读）。StringId 来自 MBObjectBase（TaleWorlds.ObjectSystem 已引用；
        // 不用 TextObject/Localization）。
        private void LogTeamIdentity(Team team)
        {
            if (team == null) return;
            string culture = FeedbackText.CultureId(team) ?? "-";
            string relation = Mission.PlayerTeam == null ? "-"
                : (team.IsPlayerTeam ? "玩家" : team.IsPlayerAlly ? "玩家盟友" : "敌方");
            CombatLog.Log(string.Format("  Team {0}: Side={1} 阵营={2} 关系={3}",
                team.TeamIndex, team.Side, culture, relation));
        }

        // 日志增强：编队级明细快照（5s 一拍，OnMissionTick 挂点）。构成按 QuerySystem 兵种旗标
        // 分类（骑射优先于骑兵/射手判定），位置用 TeamGeometry CachedMedian 口径（与编队层下令一致），
        // FSM 状态从各 Glue 只读 getter 读取，架枪/保护为队级（IsBracing/IsProtecting）。
        // 低频（每 5s 一拍、每编队一条）+ 字符串拼接仅发生在拍内，无每帧 IO/分配。
        private void LogFormationDetails(Mission mission)
        {
            foreach (TeamState state in _states.Values)
            {
                Team team = state.Team;
                if (team == null) continue;
                CombatLog.Log(string.Format("[编队明细] Team {0} 意图={1} 保护={2} 架枪={3}",
                    team.TeamIndex, state.LastIntent,
                    _protect.IsProtecting(team.TeamIndex) ? "是" : "否",
                    _brace.IsBracing(team.TeamIndex) ? "是" : "否"));
                foreach (Formation f in team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    FormationQuerySystem qs = f.QuerySystem;
                    string kind = qs.IsRangedCavalryFormation ? "骑射"
                        : qs.IsCavalryFormation ? "骑兵"
                        : qs.IsRangedFormation ? "射手"
                        : "步兵";
                    Vec2 pos = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                    CombatLog.Log(string.Format("  └ {0}x{1}@({2:F0},{3:F0}) 骑FSM={4} 士FSM={5} 游={6}",
                        kind, f.CountOfUnits, pos.x, pos.y,
                        _cavalry.GetStateName(f), _morale.GetStateName(f), _skirmish.GetModeName(f)));
                }
            }
        }

        // ATC-0 输出层：5s 汇总——只输出窗口内有覆盖/命令变化的 Formation（规格 §14 OrderTrace 格式：
        //   [OrderTrace] team= f= tick= Source:Kind → Source:Kind(overrode) → final=Kind 固定字段顺序）。
        // 反作弊闸：只读既有 CommandProvenance 记录数据，零新增游戏读取；5s 低频无逐帧分配。
        private void LogProvenanceSummary()
        {
            float start = _elapsed - 5f;
            var dirty = CommandProvenance.Instance.GetDirtyFormations(start, _elapsed);
            if (dirty.Count == 0) return; // 无覆盖/命令变化 → 不刷日志
            for (int i = 0; i < dirty.Count; i++)
            {
                int fid = dirty[i];
                string trace = CommandProvenance.Instance.BuildOrderTrace(fid);
                if (string.IsNullOrEmpty(trace)) continue;
                CombatLog.Log(string.Format("[OrderTrace] f={0} tick={1:F1} {2}",
                    fid, _elapsed, trace));
            }
        }

        // ATC-0 战斗结束输出：按 CommandSource 聚合命令数/覆盖数（CombatLog 单渠道，规格 §14）。
        // 反作弊闸：只读既有记录数据；OnMissionStateFinalized/OnClearScene 双钩子幂等调用。
        private void LogProvenanceAggregate()
        {
            string agg = CommandProvenance.Instance.BuildSourceAggregate();
            if (!string.IsNullOrEmpty(agg))
                CombatLog.Log("[OrderTraceAgg] " + agg);
        }

        // 编队"实际在射"比例：FormationQuerySystem.MakingRangedAttackRatio 按人数加权均值（引擎缓存）。
        // 弹药告罄的射手不再射击 → 该比例自然衰减，作为弹药余量的可观察代理（非逐兵读取）。
        // T4：0.5s 窗口缓存（带锁 WindowCache，键=队索引），避免同窗口内对同一队重复迭代编队聚合。
        private float FormationFiringRatio(Team team)
        {
            int teamIndex = team.TeamIndex;
            float cached;
            if (_firingRatioCache.TryGet(teamIndex, _elapsed, out cached)) return cached;

            float sum = 0f;
            int total = 0;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                if (f.QuerySystem == null) continue;
                sum += f.QuerySystem.MakingRangedAttackRatio * f.CountOfUnits;
                total += f.CountOfUnits;
            }
            float ratio = total > 0 ? sum / total : 0f;
            _firingRatioCache.Set(teamIndex, ratio);
            return ratio;
        }

        private static float ComputePenB(TeamState state)
        {
            if (state.EnemyCountHistory.Count < 2) return 0f;
            float first = 0f;
            float last = 0f;
            int i = 0;
            foreach (float f in state.EnemyCountHistory)
            {
                if (i == 0) first = f;
                last = f;
                i++;
            }
            float drop = first - last;
            if (drop <= 0f) return 0f;
            float rate = drop / Math.Max(1f, first);
            return VecMath.Clamp01(rate * 2f);
        }

        // ---------- F6 走位/绕侧 ----------

        private void TickMovement(Mission mission, float dt)
        {
            // F7 求生撤退 / F9 远程后撤中的兵跳过走位（求生/后撤优先级高于走位）
            _movement.Tick(mission, dt, _elapsed, GetLod, GetProfile, GetMorale,
                i => _survival.IsRetreating(i) || _ranged.IsFallingBack(i));
        }

        // ---------- F7 求生本能 + 重整信号 ----------

        private void TickSurvival(Mission mission, float dt)
        {
            _survival.Tick(mission, dt, _elapsed, GetLod, GetProfile, GetMorale, _bus.Publish);
        }

        // ---------- F9 远程目标/弹药取舍 ----------

        private void TickRanged(Mission mission, float dt)
        {
            _ranged.Tick(mission, dt, _elapsed, GetLod, GetProfile, GetMorale, _survival.IsRetreating);
        }

        // ---------- F8 骑兵波次（编队级） ----------

        private void TickCavalry(Mission mission, float dt)
        {
            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;
            _cavalry.Tick(mission, dt, _elapsed, playerCenter, GetTeamIntent, _commandProposals, IsEnemyFormationKnown); // X6：敌编队 KnowledgeView 过滤
        }

        // ---------- T5 骑射游击（编队级） ----------

        private void TickSkirmish(Mission mission, float dt)
        {
            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;
            _skirmish.Tick(mission, dt, _elapsed, playerCenter, i => _protect.IsProtecting(i), _commandProposals, IsEnemyFormationKnown); // X6：敌编队 KnowledgeView 过滤
        }

        // ---------- F11 士气传导 + 分阶段撤退（编队级） ----------

        private void TickMorale(Mission mission, float dt)
        {
            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;
            _morale.Tick(mission, dt, _elapsed, playerCenter, GetTeamIntent, GetMorale, _commandProposals);
        }

        // ---------- F3 保护核心输出（ProtectArcher 执行层，编队级） ----------

        private void TickProtect(Mission mission, float dt)
        {
            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;
            _protect.Tick(mission, dt, _elapsed, playerCenter, GetTeamIntent, _commandProposals, IsEnemyFormationKnown); // X6：敌编队 KnowledgeView 过滤
        }

        // ---------- F4 架枪反骑阵地（BraceFormation 执行层，编队级） ----------

        private void TickBrace(Mission mission, float dt)
        {
            Team playerTeam = mission.PlayerTeam;
            Vec2 playerCenter = playerTeam != null ? GetTeamCenter(playerTeam) : Vec2.Zero;
            _brace.Tick(mission, dt, _elapsed, playerCenter, GetTeamIntent, _commandProposals, IsEnemyFormationKnown); // X6：敌编队 KnowledgeView 过滤
        }

        private IntentType GetTeamIntent(Team team)
        {
            TeamState state;
            if (team != null && _states.TryGetValue(team.TeamIndex, out state))
                return state.LastIntent;
            return IntentType.Attack;
        }

        private LodLevel GetLod(int agentIndex)
        {
            LodLevel lod;
            return _agentLod.TryGetValue(agentIndex, out lod) ? lod : LodLevel.L0;
        }

        // F12 输入：队士气（意图引擎 0.5s 一刷的 LastVector.Moral，非逐帧重算）
        private float GetMorale(Team team)
        {
            TeamState state;
            if (team != null && _states.TryGetValue(team.TeamIndex, out state))
                return state.LastVector.Moral;
            return 0.5f;
        }

        // ---------- F4 预测式格挡 ----------

        private void TickBlock(Mission mission, float dt)
        {
            _blockTimer -= dt;
            if (_blockTimer > 0f) return;
            _blockTimer = 0.1f;

            // T4 零每帧分配：L3 候选列表从 [ThreadStatic] BufferPool 复用（仅本方法内使用）
            List<Agent> l3 = BufferPool.GetAgentList(0);
            foreach (Agent agent in mission.Agents)
            {
                if (agent == null || !agent.IsActive()) continue;
                LodLevel lod;
                if (_agentLod.TryGetValue(agent.Index, out lod) && lod == LodLevel.L3)
                    l3.Add(agent);
            }

            for (int i = 0; i < l3.Count; i++)
            {
                Agent defender = l3[i];
                // P2-② 攻击者查找改引擎空间查询：Mission.GetNearbyEnemyAgents(Vec2,float,Team,MBList<Agent>)
                // （v1.4.8 已核实：Mission.cs L6582-6586；GetNearbyAgentsAux L2312-2333 只 Add 不清空，
                //   故经 BufferPool.GetMBList 清空复用，与 F6/F7/F9 同款）。引擎空间哈希 O(邻域)
                //   替代原 L3 候选列表内 O(n²) 扫描；且不再要求攻击者也是 L3——近战半径内任意敌人均
                //   可成为预测源，L2 攻击者不再漏判。
                if (defender.Team == null) continue; // 无队防御（与原实现等效：同队判定永远找不到攻击者）
                MBList<Agent> nearby = BufferPool.GetMBList(1);
                mission.GetNearbyEnemyAgents(defender.Position.AsVec2, BehaviorLodScheduler.L3MeleeRadius, defender.Team, nearby);

                Agent attacker = null;
                float bestD = float.MaxValue;
                for (int j = 0; j < nearby.Count; j++)
                {
                    Agent e = nearby[j];
                    if (e == null || !e.IsActive()) continue;
                    float d = VecMath.DistSq(defender.Position.x, defender.Position.y, e.Position.x, e.Position.y);
                    if (d < bestD)
                    {
                        bestD = d;
                        attacker = e;
                    }
                }
                if (attacker == null) continue;

                AttackStats stats = GetAttackStats(attacker.Index);
                FighterProfile defenderProfile = GetProfile(defender);
                FighterProfile attackerProfile = GetProfile(attacker);

                AttackDir predicted;
                float confidence;
                if (PredictiveBlock.TryPredict(stats, defenderProfile, attackerProfile.FeintRate, out predicted, out confidence))
                {
                    // T1 决策分支计数：产生格挡预测 = 决策分支命中
                    DecisionObservatory.Observe(DecisionObservatory.BBlockPred);
                    InGameFeedback.Count("格挡预测"); // 游戏内反馈：个体级聚合（防 500 人逐兵刷屏）

                    // P0-① 真实决策间隔：now(_elapsed) - last（跨 0.1s 节流+跳帧空拍也在内）；
                    // 首次近似=节流周期 0.1s；时钟异常兜底 0.1s。
                    float now = _elapsed;
                    float last;
                    if (!_lastBlockJitter.TryGetValue(defender.Index, out last)) last = now - 0.1f;
                    float elapsed = now - last;
                    if (elapsed <= 0f) elapsed = 0.1f;
                    _lastBlockJitter[defender.Index] = now;

                    // F12 横切：犹豫/延迟维持期跳过本轮举盾（拟人化反应迟滞，不侵入纯逻辑）
                    float signedWobble;
                    if (_jitterBlock.ShouldAct(
                        defender.Index, elapsed,
                        DecisionJitterGlue.DisciplineFrom(defenderProfile),
                        GetMorale(defender.Team), out signedWobble))
                    {
                        defender.EnforceShieldUsage(ToUsageDefend(predicted));
                    }
                    else
                    {
                        // T1 决策分支计数：F12 犹豫/维持期让过本轮举盾
                        DecisionObservatory.Observe(DecisionObservatory.BBlockSkip);
                    }
                }
            }
        }

        public override void OnAgentHit(
            Agent affectedAgent, Agent affectorAgent,
            in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);

            if (affectorAgent == null || affectedAgent == null) return;

            // P2-⑤ 方向源修复（review-findings P2）：改读入参 attackCollisionData.AttackDirection
            // （碰撞时刻的方向快照）。v1.4.8 已核实：AttackCollisionData.cs L120 定义、私有 ctor L194
            // 在碰撞数据构造时赋值；引擎同款消费 MissionCombatMechanicsHelper.cs L143（读
            // collisionData.AttackDirection 判定方向类特性）。原读 affectorAgent.AttackDirection 是
            // 命中回调时刻的实时原生状态（Agent.cs L714 → IMBAgent.GetAttackDirectionUsage），
            // 攻击动作命中结算/下一拍可复位或推进 → PredictiveBlock 攻击方向统计源存在系统性
            // 时机噪声；改用入参快照后统计源可信（评估结论：修复，不接受噪声）。
            AttackDir dir = MapAttackDir(attackCollisionData.AttackDirection);
            if (dir == AttackDir.None) return;

            GetAttackStats(affectorAgent.Index).Record(dir);
            _bus.Publish(new CombatEvent
            {
                Kind = CombatEventKind.AttackObserved,
                Time = _elapsed,
                Id = affectorAgent.Index,
                Id2 = affectedAgent.Index,
                IntValue = (int)dir
            });
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
        {
            base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);

            if (affectedAgent != null)
            {
                _bus.Publish(new CombatEvent
                {
                    Kind = CombatEventKind.AgentDown,
                    Time = _elapsed,
                    Id = affectedAgent.Index,
                    Id2 = affectorAgent != null ? affectorAgent.Index : -1
                });
                _agentLod.Remove(affectedAgent.Index);
                _profiles.Remove(affectedAgent.Index);
                _movement.RemoveAgent(affectedAgent.Index);
                _survival.Remove(affectedAgent.Index);
                _ranged.Remove(affectedAgent.Index);
                _jitterBlock.Remove(affectedAgent.Index);
                _lastBlockJitter.Remove(affectedAgent.Index); // P0-① 清理时间基准
                _attackStats.Remove(affectedAgent.Index); // P2-① 预测格挡统计随 Agent 生命周期清理（否则字典只增不减，GetAttackStats 会重建）
                _attackSuppression.Remove(affectedAgent.Index); // T3 攻击抑制 Agent 生命周期清理
                _frameReleaseCooldown.Remove(affectedAgent.Index); // X4 解阵框冷却随 Agent 生命周期清理
                _formationSlots.RemoveAgent(affectedAgent.Index); // X5-6 个体补位冷却随 Agent 生命周期清理
                _morale.RecordCasualty(affectedAgent.Formation, _elapsed); // F11 脱阵窗口（事件驱动）
            }
        }

        // F10 跨战役套路记忆落盘（战役结束一次 IO，不做每帧 IO）
        // v1.4.8 无 OnMissionEnded；OnMissionStateFinalized 为战役收尾标准钩子，OnClearScene 兜底（幂等）
        public override void OnMissionStateFinalized()
        {
            base.OnMissionStateFinalized();
            _experience.OnMissionEnd();
            LogProvenanceAggregate(); // ATC-0 战斗结束按 CommandSource 聚合（幂等：只在收尾钩子输出一次）
            SaveTelemetry();
            ClearMissionStaticState(); // atc4-state-lifecycle P1-4：跨 Mission 静态状态幂等清理（先聚合后清理）
        }

        public override void OnClearScene()
        {
            base.OnClearScene();
            _experience.OnMissionEnd();
            LogProvenanceAggregate(); // ATC-0 战斗结束聚合（OnClearScene 兜底，幂等）
            SaveTelemetry();
            ClearMissionStaticState(); // atc4-state-lifecycle P1-4（双钩子幂等，OnClearScene 兜底）
        }

        // atc4-state-lifecycle P1-4 / atc4-snapshot-optimization OPT-4：跨 Mission 静态状态幂等清理入口
        //   （OnMissionStateFinalized/OnClearScene 双钩子共用，先聚合后清理；逻辑已拆至 MissionStateLifecycle；
        //   ATC-5R 租约 ReleaseAll 一并纳入——Mission 结束幂等交还原生 AI）
        private void ClearMissionStaticState()
        {
            MissionStateLifecycle.ClearStatic(_opponent, _attackSuppression, _leaseGlue, Mission);
        }

        // T38-01 遥测事件分发：仅映射已有事件 → 计数（各计数点的详细调用留给后续 feature 各自补，
        // 勿在此堆砌每帧/复杂逻辑）。FormationBroken 等事件发布点由后续 feature 补，届时在此增补映射。
        private void OnTelemetryEvent(CombatEvent e)
        {
            if (e == null) return;
            switch (e.Kind)
            {
                case CombatEventKind.IntentSwitched:
                    if (e.IntValue == (int)IntentType.Retreat) _telemetry.Increment(TelemetryKind.RetreatCount);
                    break;
                case CombatEventKind.FormationBroken:
                    _telemetry.Increment(TelemetryKind.FormationBreakCount);
                    break;
                // AgentDown/AttackObserved/ChargeContact 等 → 待后续 feature 定义语义后在此增补
            }
        }

        // T2-01 行军/战斗阵型三态切换（0.5s 编队级，O(队数×编队数)）：只切 ArrangementOrder，不动移动命令。
        // 分域：骑兵/骑射归 F8/Skirmish（跳过）；Protect/Brace 激活时跳过（不覆盖其阵型）；玩家队豁免（MCM）保留。
        private void TickFormationTactics(Mission mission, float dt)
        {
            _tacticsTimer -= dt;
            if (_tacticsTimer > 0f) return;
            _tacticsTimer = 0.5f;

            const float RangedThreatRange = 150f; // 敌方远程威胁射程代理（弓矢量级，源文档「进入射程」）

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && own.IsPlayerTeam)) continue; // P0-④ 玩家队豁免
                if (_protect.IsProtecting(own.TeamIndex) || _brace.IsBracing(own.TeamIndex)) continue; // 分域：Protect/Brace 激活不覆盖
                Team enemy = GetEnemyTeam(own);
                if (enemy == null) continue;

                Vec2 ec = GetTeamCenter(enemy);
                bool enemyRanged = enemy.QuerySystem != null
                    && (enemy.QuerySystem.RangedRatio > 0.05f || enemy.QuerySystem.RangedCavalryRatio > 0.05f);

                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    if (f.QuerySystem.IsCavalryFormation || f.QuerySystem.IsRangedCavalryFormation) continue; // 骑兵/骑射归 F8/Skirmish

                    Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage);
                    float dist = VecMath.Dist(fc.x, fc.y, ec.x, ec.y);
                    bool enemyInRange = enemyRanged && dist <= RangedThreatRange;

                    FormationTacticsState st = FormationTactics.Classify(dist, enemyInRange);

                    var ig = f.CachedFormationIntegrityData;
                    float avgSpeed = VecMath.Dist(0f, 0f, ig.AverageVelocityExcludeFarAgents.x, ig.AverageVelocityExcludeFarAgents.y);
                    float integrity = FormationIntegrity.Compute(ig.DeviationOfPositionsExcludeFarAgents, ig.MaxDeviationOfPositionExcludeFarAgents, avgSpeed, ig.AverageMaxUnlimitedSpeedExcludeFarAgents, f.CountOfUnits);

                    TacticsArrangement want = FormationTactics.SelectArrangement(st, state.CultureId, integrity);

                    // 只切阵型，不动移动命令（避免与 Protect/Brace/Cavalry/Morale 命令互刷）
                    // t89 全面接管：检测原版 Circle 防御环（BehaviorDefensiveRing 下发）→ 强制改写（ShouldOverride 幂等）
                    if (FormationTactics.ShouldOverride(MapArrangementKind(f.ArrangementOrder), want))
                    {
                        // CPS-1 迁移（cps1-migrate-specialists）：直接 SetArrangementOrder → P40 Presentation 层提案
                        //（Source=FormationTactics 真实来源 t3；ReasonCode=ArrangementKind 阵型种类；执行器映射落地）
                        _commandProposals.Add(FormationCommandProposal.PresentationProposal(own.TeamIndex, f.Index,
                            RuntimeHelpers.GetHashCode(f), _elapsed,
                            CommandSource.FormationTactics, CommandKind.Arrangement, CommandDomain.Arrangement,
                            (int)(want == TacticsArrangement.ShieldWall ? ArrangementKind.ShieldWall : ArrangementKind.Line),
                            -1, 0f, 0f));
                    }
                }
            }
        }

        // t89 当前实际阵型 → 纯枚举（Circle=原版防御环，用于全面接管检测）
        private static FormationArrangementKind MapArrangementKind(ArrangementOrder o)
        {
            if (o == ArrangementOrder.ArrangementOrderCircle) return FormationArrangementKind.Circle;
            if (o == ArrangementOrder.ArrangementOrderShieldWall) return FormationArrangementKind.ShieldWall;
            if (o == ArrangementOrder.ArrangementOrderLine) return FormationArrangementKind.Line;
            return FormationArrangementKind.Other;
        }

        // X5-6 槽位布局胶水（0.5s 编队级，TickFormationTactics 之后）：X5 全链落地——TroopClassifier 归类
        // → TroopComposition 分流 → SlotLayout 生成 → TerrainWarp 形变 → Discipline 四态门控 →
        // SlotAssigner 指派 → L3 个体补位（X4 冷却模式）。MCM 总开关 + 玩家豁免 + Bench 回退闸在
        // FormationSlotGlue 内（X5Enabled/IndividualLegEnabled 静态开关，无 MCM 静默默认开）。
        // 反作弊闸：只读己方编队聚合 + 公开地形；不 SetArrangementOrder（红线 1）/不新增解阵（红线 2）。
        private void TickFormationSlots(Mission mission, float dt)
        {
            _formationSlotsTimer -= dt;
            if (_formationSlotsTimer > 0f) return;
            _formationSlotsTimer = 0.5f;

            _formationSlots.Tick(mission, dt, _elapsed, GetLod, GetFormationIntegrity, _terrain);
        }

        // 编队完整度（Discipline 门控输入；与 TickFormationTactics L949-951 同口径，纯聚合）
        private float GetFormationIntegrity(Formation f)
        {
            if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) return 100f;
            var ig = f.CachedFormationIntegrityData;
            float avgSpeed = VecMath.Dist(0f, 0f, ig.AverageVelocityExcludeFarAgents.x, ig.AverageVelocityExcludeFarAgents.y);
            return FormationIntegrity.Compute(ig.DeviationOfPositionsExcludeFarAgents, ig.MaxDeviationOfPositionExcludeFarAgents,
                avgSpeed, ig.AverageMaxUnlimitedSpeedExcludeFarAgents, f.CountOfUnits);
        }

        // T3-01 射手射击阵地转移（0.5s 编队级，O(队数×编队数)）：仅在 7 种允许原因下转移，只发 Move 不动阵型。
        // 分域：Protect 激活让位（不覆盖退后/殿后）；玩家队豁免（MCM）保留；骑射归 Skirmish（只步弓手）。
        private void TickRangedPosition(Mission mission, float dt)
        {
            _rangedPosTimer -= dt;
            if (_rangedPosTimer > 0f) return;
            _rangedPosTimer = 0.5f;

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && own.IsPlayerTeam)) continue; // P0-④ 玩家队豁免
                if (_protect.IsProtecting(own.TeamIndex)) continue; // 分域：Protect 激活让位

                Team enemy = GetEnemyTeam(own);
                bool hasTarget = enemy != null;

                // 己方步兵线中心（射手射击阵地 = 步兵线后受保护位）
                Vec2 ic = Vec2.Zero; int infCount = 0;
                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    if (!f.QuerySystem.IsInfantryFormation) continue;
                    Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                    ic.x += p.x; ic.y += p.y; infCount++;
                }
                if (infCount == 0) ic = GetTeamCenter(own);
                else { ic.x /= infCount; ic.y /= infCount; }

                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    if (!f.QuerySystem.IsRangedFormation) continue; // 只步弓手（骑射归 Skirmish）

                    // 敌骑最近距离/数量（聚合可观察，禁全图逐兵）
                    float cavDist = -1f; int cavCount = 0;
                    foreach (Team et in mission.Teams)
                    {
                        if (et == null || et == own || (CombatBrainSettings.PlayerTeamExemptionEnabled && et.IsPlayerTeam)) continue;
                        foreach (Formation ef in et.FormationsIncludingEmpty)
                        {
                            if (ef == null || ef.CountOfUnits == 0 || ef.QuerySystem == null) continue;
                            if (!ef.QuerySystem.IsCavalryFormation && !ef.QuerySystem.IsRangedCavalryFormation) continue;
                            cavCount += ef.CountOfUnits;
                            Vec2 ep = TeamGeometry.GetFormationPosition(ef, TeamPosKind.CachedMedian);
                            Vec2 rp = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                            float d = VecMath.Dist(ep.x, ep.y, rp.x, rp.y);
                            if (cavDist < 0f || d < cavDist) cavDist = d;
                        }
                    }

                    var ig = f.CachedFormationIntegrityData;
                    float avgSpeed = VecMath.Dist(0f, 0f, ig.AverageVelocityExcludeFarAgents.x, ig.AverageVelocityExcludeFarAgents.y);
                    float integrity = FormationIntegrity.Compute(ig.DeviationOfPositionsExcludeFarAgents, ig.MaxDeviationOfPositionExcludeFarAgents, avgSpeed, ig.AverageMaxUnlimitedSpeedExcludeFarAgents, f.CountOfUnits);

                    var input = new RangedPositionInput();
                    input.EnemyCavalryDistance = cavDist;
                    input.EnemyCavalryCount = cavCount;
                    input.FiringArcRatio = 1f;   // 射界计算后续 feature 接线（默认充足）
                    input.HasTarget = hasTarget;
                    input.FormationIntegrity = integrity;
                    // CurrentPositionValue/TargetPositionValue/LineAdvanceDistance/TerrainValueDelta 后续 feature 接线（默认 0）

                    RangedPositionReason reason = RangedPosition.Decide(input);
                    if (reason == RangedPositionReason.None) continue;

                    Vec2 cur = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian);
                    float drift = VecMath.Dist(cur.x, cur.y, ic.x, ic.y);

                    RangedPositionReason last;
                    if (!_rangedPosReason.TryGetValue(f, out last)) last = RangedPositionReason.None;
                    if (!RangedPosition.ShouldIssueMove(reason, last, drift)) { _rangedPosReason[f] = reason; continue; }
                    _rangedPosReason[f] = reason;

                    IssueMoveToPosition(f, ic.x, ic.y, mission); // 只发 Move，不动 ArrangementOrder
                }
            }
        }

        // T3-01 下发阵地 Move（ProtectGlue.IssueMove 同款已核实模式：界内校验→导航点校验→提案）。
        // CPS-1 迁移：static→实例（访问 _commandProposals），直接 SetMovementOrder → P50 Specialist 层提案。
        private void IssueMoveToPosition(Formation f, float tx, float ty, Mission mission)
        {
            Vec2 target = new Vec2(tx, ty);
            if (!mission.IsPositionInsideBoundaries(target)) return;
            WorldPosition wp = f.CachedMedianPosition;
            wp.SetVec2(target);
            if (wp.GetNavMesh() == UIntPtr.Zero) return;
            _commandProposals.Add(FormationCommandProposal.SpecialistProposal(f.Team.TeamIndex, f.Index,
                RuntimeHelpers.GetHashCode(f), _elapsed,
                CommandSource.RangedPosition, CommandKind.Move, CommandDomain.Movement, 0, -1, tx, ty));
        }

        // T8-01 骑射编队兵力保存（0.5s 编队级）：按当前/初始兵力比例五档分档，撤离档(<20%)下发 FallBack。
        // 分域：只骑射编队（IsRangedCavalryFormation，与 Skirmish 同域）；个体级 SurvivalGlue 不管本层；玩家队豁免保留。
        private void TickUnitPreservation(Mission mission, float dt)
        {
            _unitPreservationTimer -= dt;
            if (_unitPreservationTimer > 0f) return;
            _unitPreservationTimer = 0.5f;

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && own.IsPlayerTeam)) continue; // P0-④ 玩家队豁免

                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    if (!f.QuerySystem.IsRangedCavalryFormation) continue; // 只骑射（反作弊闸：CountOfUnits 可观察）

                    int initial;
                    if (!_rangedCavInitial.TryGetValue(f, out initial))
                    {
                        initial = f.CountOfUnits; // 首次观测记初始兵力（分档基准）
                        _rangedCavInitial[f] = initial;
                    }
                    if (initial <= 0) continue;
                    float ratio = (float)f.CountOfUnits / initial;
                    PreservationBand band = UnitPreservation.ClassifyBand(ratio);

                    PreservationBand last;
                    if (!_unitPreservationBand.TryGetValue(f, out last)) last = PreservationBand.Normal;
                    if (band != last)
                    {
                        _unitPreservationBand[f] = band; // 分档边沿才动作（防每拍刷命令）
                        if (band == PreservationBand.Withdraw)
                        {
                            // CPS-1 迁移：直接 SetMovementOrder → P50 Specialist 层提案（撤离档：脱离战场）
                            _commandProposals.Add(FormationCommandProposal.SpecialistProposal(own.TeamIndex, f.Index,
                                RuntimeHelpers.GetHashCode(f), _elapsed,
                                CommandSource.UnitPreservation, CommandKind.FallBack, CommandDomain.Movement,
                                (int)band, -1, 0f, 0f));
                        }
                        // 中间档（谨慎/减接战/优先保存）行为建议由 UnitPreservation.ActionFor 提供，SkirmishGlue 后续接线消费
                    }
                }
            }
        }

        // T18-01 僵局检测 + 打破方案建议（2s 慢拍，勿每帧）：只产出建议意图（不干预命令层，供意图引擎消费）。
        private void TickStalemate(Mission mission, float dt)
        {
            _stalemateTimer -= dt;
            if (_stalemateTimer > 0f) return;
            _stalemateTimer = 2f;

            // 代表队（首个非玩家队）与敌
            Team own = null, enemy = null;
            foreach (TeamState st in _states.Values)
            {
                if (st.Team == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && st.Team.IsPlayerTeam)) continue;
                own = st.Team;
                enemy = GetEnemyTeam(own);
                if (enemy != null) break;
            }
            if (own == null || enemy == null) { _stalemateActive = false; _stalematePlan = StalematePlan.None; return; }

            // 伤亡率（非玩家队聚合；反作弊闸：存活数公开可观察）
            int aliveSelf = 0, initialSelf = 0;
            foreach (TeamState st in _states.Values)
            {
                if (st.Team == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && st.Team.IsPlayerTeam)) continue;
                aliveSelf += st.Team.ActiveAgents.Count;
                initialSelf += st.InitialCount > 0f ? (int)st.InitialCount : st.Team.ActiveAgents.Count;
            }
            float casualty = initialSelf > 0 ? 1f - (float)aliveSelf / initialSelf : 0f;

            // 战线移动（敌中心位移累计）
            Vec2 ec = GetTeamCenter(enemy);
            if (_hasFrontCenter)
                _frontlineMovement += VecMath.Dist(_lastFrontCenter.x, _lastFrontCenter.y, ec.x, ec.y);
            _lastFrontCenter = ec;
            _hasFrontCenter = true;

            var input = new StalemateInput();
            input.CombatDuration = _elapsed;
            input.CasualtyRate = casualty;
            input.FrontlineMovement = _frontlineMovement;
            // 7 类态势
            input.PowerRatio = enemy.ActiveAgents.Count > 0 ? (float)aliveSelf / enemy.ActiveAgents.Count : 1f;
            TeamQuerySystem qs = own.QuerySystem;
            input.CavalryRatio = qs != null ? qs.CavalryRatio + qs.RangedCavalryRatio : 0f;
            input.RangedRatio = qs != null ? qs.RangedRatio : 0f;
            Vec2 oc = GetTeamCenter(own);
            input.TerrainAdvantage = _terrain != null ? _terrain.Height(oc.x, oc.y) - _terrain.Height(ec.x, ec.y) : 0f;
            input.Morale = initialSelf > 0 ? (float)aliveSelf / initialSelf : 1f;
            input.LineIntegrity = 100f; // 阵线完整度后续接线（t65 FormationIntegrity）
            input.Distance = VecMath.Dist(oc.x, oc.y, ec.x, ec.y);

            _stalemateActive = StalemateDetector.IsStalemate(input);
            _stalematePlan = _stalemateActive ? StalemateDetector.ChoosePlan(input) : StalematePlan.None;
        }

        // X1 编队级目标选择（0.5s 编队级，只产出选择不下令）：每编队从剧场半径内可见敌方编队选目标。
        // 优先级链 敌射手>敌骑射>敌骑兵>敌步兵 + 距离/威胁加权 + 7m 迟滞（FormationTargeting 纯逻辑，可离线测）。
        // 反作弊闸：只用引擎公开聚合（QuerySystem 兵种旗标 / CountOfUnits / TeamGeometry 编队中心），
        //   禁全图、禁个体遍历；敌队判定与 P1-③ 一致（IsEnemyOf）；玩家队豁免（MCM 开关）保留。
        // 结果入 _formationTargets 字典，下游经 TryGetFormationTarget 消费（契约②：本层不下令）。
        private void TickTargeting(Mission mission, float dt)
        {
            _targetingTimer -= dt;
            if (_targetingTimer > 0f) return;
            _targetingTimer = 0.5f;

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && own.IsPlayerTeam)) continue; // P0-④ 玩家队豁免（MCM t66）

                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // 编队层下令系口径（P1-④）

                    int n = 0;
                    foreach (Team et in mission.Teams)
                    {
                        // P1-③ 真敌队判定（IsEnemyOf，Team.cs L629 已核实）；多队混战不把盟友当敌人
                        if (et == null || et == own || !et.MBTeam.IsValid || !et.IsEnemyOf(own)) continue;
                        if (CombatBrainSettings.PlayerTeamExemptionEnabled && et.IsPlayerTeam) continue; // 玩家队豁免
                        foreach (Formation ef in et.FormationsIncludingEmpty)
                        {
                            if (ef == null || ef.CountOfUnits == 0 || ef.QuerySystem == null) continue;
                            Vec2 ep = TeamGeometry.GetFormationPosition(ef, TeamPosKind.CachedMedian);
                            float dist = VecMath.Dist(fc.x, fc.y, ep.x, ep.y);
                            if (dist > FormationTargeting.TheaterRadius) continue; // 剧场半径内可见（禁全图）

                            var c = new TargetCandidate
                            {
                                Index = ef.Index,
                                X = ep.x,
                                Y = ep.y,
                                Count = ef.CountOfUnits,
                                Kind = ClassifyFormation(ef)
                            };
                            if (n < _targetCandidates.Length) _targetCandidates[n++] = c; // 编队数上限防御（复用缓冲零分配）
                        }
                    }

                    TargetSelection last;
                    if (!_formationTargets.TryGetValue(f, out last)) last = TargetSelection.None;
                    TargetSelection sel = FormationTargeting.Select(fc.x, fc.y, f.CountOfUnits,
                        n > 0 ? _targetCandidates : null, n, last, _elapsed);
                    _formationTargets[f] = sel; // 缓存：0.5s 节拍 + 本层时间缓存双保险
                }
            }
        }

        /// <summary>X1 编队级目标选择查询（供 X3/X4 等下游消费；无目标返回 false）。</summary>
        public bool TryGetFormationTarget(Formation f, out TargetSelection selection)
        {
            if (f != null && _formationTargets.TryGetValue(f, out selection) && selection.HasTarget) return true;
            selection = TargetSelection.None;
            return false;
        }

        // X1 编队分类：QuerySystem 四类旗标按兵力比互斥（FormationQuerySystem.cs L434-439 已核实），
        // 优先级映射 射手>骑射>骑兵>步兵 与 FormationTargetKind 枚举一致。
        private static FormationTargetKind ClassifyFormation(Formation ef)
        {
            if (ef.QuerySystem.IsRangedFormation) return FormationTargetKind.Ranged;
            if (ef.QuerySystem.IsRangedCavalryFormation) return FormationTargetKind.RangedCavalry;
            if (ef.QuerySystem.IsCavalryFormation) return FormationTargetKind.Cavalry;
            return FormationTargetKind.Infantry;
        }

        // X3 编队朝向修正（0.5s 编队级，只 SetFacingOrder 不动 Movement/Arrangement——与 t79 三态/t89 接管分域独立）。
        // 朝向源：①t93 TryGetFormationTarget 目标编队方向（敌射手优先，7m 迟滞）；②无目标 → GetEnemyTeam 最近敌队中心。
        // FacingCorrection 纯逻辑判定（0.5s 缓存 + 1m 目标无效 + 150m 剧场半径 + 0.2rad 方向迟滞防抖），
        // 只产出方向供 SetFacingOrder(FacingOrderLookAtDirection)（Formation.cs:717 / FacingOrder.cs:32 已核实）。
        // 反作弊闸：编队中心/敌队中心为引擎公开可观察位置；玩家队豁免（MCM）保留。
        private void TickFacing(Mission mission, float dt)
        {
            _facingTimer -= dt;
            if (_facingTimer > 0f) return;
            _facingTimer = 0.5f;

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null || FacingCorrection.ShouldExempt(own.IsPlayerTeam, CombatBrainSettings.PlayerTeamExemptionEnabled)) continue; // P0-④ 玩家队豁免

                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                    Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // 编队层下令系口径

                    // 朝向源优先：t93 目标编队方向；无目标 → 最近敌队中心
                    TargetSelection sel;
                    Vec2 target;
                    if (TryGetFormationTarget(f, out sel))
                    {
                        target = new Vec2(sel.X, sel.Y);
                    }
                    else
                    {
                        Team enemy = GetEnemyTeam(own);
                        if (enemy == null) continue;
                        target = GetTeamCenter(enemy);
                    }

                    Vec2 lastDir;
                    if (!_facingLastDir.TryGetValue(f, out lastDir)) lastDir = Vec2.Zero;
                    float lastTime;
                    if (!_facingLastTime.TryGetValue(f, out lastTime)) lastTime = -1f;

                    float dx, dy, dist;
                    if (FacingCorrection.DecideFacing(fc.x, fc.y, target.x, target.y,
                        lastDir.x, lastDir.y, _elapsed, lastTime, out dx, out dy, out dist))
                    {
                        // CPS-1 迁移（cps1-migrate-specialists）：直接 SetFacingOrder → P40 Presentation 层提案
                        //（Source=FacingCorrection 真实来源 t3；Facing 目标=方向向量）
                        _commandProposals.Add(FormationCommandProposal.PresentationProposal(own.TeamIndex, f.Index,
                            RuntimeHelpers.GetHashCode(f), _elapsed,
                            CommandSource.FacingCorrection, CommandKind.Facing, CommandDomain.Facing,
                            0, -1, dx, dy));
                        _facingLastDir[f] = new Vec2(dx, dy);
                        _facingLastTime[f] = _elapsed;
                    }
                }
            }
        }

        // X4 接敌解阵框（0.5s 编队级，逐 agent 执行）：编队 MovementOrder 为 Charge/ChargeToTarget（
        //   MovementOrder.cs:16-17 已核实）且距 X1 t93 最显著敌编队 ≤25m 时，对 L3（玩家可见区）的
        //   士兵调 Agent.SetFormationFrameDisabled()（Agent.cs:2306-2308 已核实，Agent 级 API）——
        //   脱离刚性阵型槽位自由接敌，战线从直线变犬牙交错（RBM OverrideFormation 同款）。
        // 判定走 FrameReleaseGlue.cs 纯逻辑（可离线测）：命令/距离/规模三闸 + L3 剧场闸 + F7/F9/逃跑
        //   跳过态 + 逐 agent 冷却字典限频（p3-1 设计，键=agent.Index，OnAgentRemoved 清理）。
        // 反作弊闸：只解除自身阵框约束（能力非信息），不改信息获取；玩家队豁免（MCM）。
        // 分域：不动 Formation 排列/阵深（T2-01 领地）、不动 ArrangementOrder、零 Harmony。
        private void TickFrameRelease(Mission mission, float dt)
        {
            _frameReleaseTimer -= dt;
            if (_frameReleaseTimer > 0f) return;
            _frameReleaseTimer = 0.5f;

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null || FrameReleaseRules.ShouldExempt(own.IsPlayerTeam, CombatBrainSettings.PlayerTeamExemptionEnabled)) continue; // 玩家队豁免（P0-④）

                foreach (Formation f in own.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;

                    // ① 命令判定：Charge/ChargeToTarget（GetReadonlyMovementOrderReference @ Formation.cs:932 已核实）
                    int orderEnum = (int)f.GetReadonlyMovementOrderReference().OrderEnum;
                    if (!FrameReleaseRules.IsChargeOrder(orderEnum)) continue;

                    // ② 距最显著敌编队（X1 t93 目标源；无目标 → 不触发，交原版）
                    TargetSelection sel;
                    if (!TryGetFormationTarget(f, out sel)) continue;
                    Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.CachedMedian); // 编队层下令系口径（P1-④）
                    float dist = VecMath.Dist(fc.x, fc.y, sel.X, sel.Y);

                    // ③ 规模闸门 + 距离闸门（纯逻辑）
                    if (!FrameReleaseRules.ShouldReleaseFormation(true, dist, f.CountOfUnits)) continue;

                    // 逐 agent 执行（本项为个体遍历例外：L3 闸门 + 冷却字典压规模）
                    foreach (IFormationUnit unit in f.Arrangement.GetAllUnits())
                    {
                        Agent agent = unit as Agent;
                        if (agent == null || !agent.IsActive() || !agent.IsAIControlled || !agent.IsHuman) continue;

                        // 剧场闸门（硬性）：仅 L3（玩家可见区），L0-L2 交原版
                        if (!FrameReleaseRules.IsReleaseLod((int)GetLod(agent.Index))) continue;

                        // F7 求生/F9 后撤/原版逃跑态跳过（求生优先于散开）
                        if (FrameReleaseRules.ShouldSkipAgent(
                            _survival.IsRetreating(agent.Index),
                            _ranged.IsFallingBack(agent.Index),
                            agent.IsRunningAway)) continue;

                        // 逐 agent 冷却限频（SetFormationFrameDisabled 一次性状态切换，冷却窗内不重发）
                        if (!_frameReleaseCooldown.CanRelease(agent.Index, _elapsed)) continue;

                        agent.SetFormationFrameDisabled(); // Agent.cs:2306-2308 已核实
                        _frameReleaseCooldown.MarkReleased(agent.Index, _elapsed);
                    }
                }
            }
        }

        /// <summary>T18-01 僵局打破建议（供意图引擎消费；非僵局返回 false）。</summary>
        public bool TryGetStalemateIntent(out IntentType intent)
        {
            intent = _stalemateActive ? StalemateDetector.MapIntent(_stalematePlan) : IntentType.Attack;
            return _stalemateActive;
        }

        // T38-01 战斗结束一次性落盘 %TEMP%\CombatBrain.telemetry.json + 汇总日志（幂等，非每帧 IO）。
        // T39-01：合并 6 类异常计数（20 项遥测 + 6 项异常，扁平 JSON 对象）。
        private void SaveTelemetry()
        {
            if (_telemetrySaved) return;
            _telemetrySaved = true;
            _telemetry.SetBattleDuration(_elapsed);
            try
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CombatBrain.telemetry.json");
                string telemetryJson = _telemetry.ToJson();
                string anomalyJson = _anomaly.ToJson();
                string combined = telemetryJson.Substring(0, telemetryJson.Length - 1) + "," + anomalyJson.Substring(1);
                File.WriteAllText(path, combined);
                CombatLog.Log("[遥测] 落盘: " + path + " | " + _telemetry.Summary() + " | " + _anomaly.Summary());
            }
            catch (Exception ex)
            {
                CombatLog.Log("[遥测] 落盘失败: " + ex.Message);
            }
        }

        // T39-01 异常检测低频驱动（0.5s，O(队数×编队数)，禁全图逐兵）：采集聚合喂入 AnomalyDetector。
        private void TickAnomaly(Mission mission, float dt)
        {
            _anomalyTimer -= dt;
            if (_anomalyTimer > 0f) return;
            _anomalyTimer = 0.5f;

            foreach (TeamState state in _states.Values)
            {
                Team own = state.Team;
                if (own == null || (CombatBrainSettings.PlayerTeamExemptionEnabled && own.IsPlayerTeam)) continue; // P0-④ 玩家队豁免
                Team enemy = GetEnemyTeam(own);
                if (enemy == null) continue;
                _anomaly.Tick(BuildAnomalyInput(own, enemy, state));
            }
        }

        // T39-01 聚合输入采集：编队中心距(TeamGeometry) + QuerySystem 兵种 + CachedFormationIntegrityData(t65)
        // + 存活数/真实地形高度。禁全图/禁个体遍历（铁律）；阈值见 AnomalyDetector.cs §T39-01。
        private AnomalyInput BuildAnomalyInput(Team own, Team enemy, TeamState state)
        {
            Vec2 oc = GetTeamCenter(own);
            Vec2 ec = GetTeamCenter(enemy);
            int aliveSelf = own.ActiveAgents.Count;
            int aliveEnemy = enemy.ActiveAgents.Count;
            int total = aliveSelf + aliveEnemy;

            var input = new AnomalyInput();
            input.Now = _elapsed;
            input.BattleAdvantage = total > 0 ? (aliveSelf - aliveEnemy) * 100f / total : 0f;
            input.CasualtyRate = state.InitialCount > 0f ? 1f - aliveSelf / state.InitialCount : 0f;
            input.TerrainAdvantage = _terrain != null ? _terrain.Height(oc.x, oc.y) - _terrain.Height(ec.x, ec.y) : 0f;

            float infDist = -1f, archDist = -1f, cavDist = -1f;
            float integrity = 100f;
            foreach (Formation f in own.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0 || f.QuerySystem == null) continue;
                Vec2 fc = TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage);
                float d = VecMath.Dist(fc.x, fc.y, ec.x, ec.y);
                if (f.QuerySystem.IsRangedFormation || f.QuerySystem.IsRangedCavalryFormation) { if (archDist < 0f || d < archDist) archDist = d; }
                else if (f.QuerySystem.IsInfantryFormation) { if (infDist < 0f || d < infDist) infDist = d; }
                else if (f.QuerySystem.IsCavalryFormation) { if (cavDist < 0f || d < cavDist) cavDist = d; }
                var ig = f.CachedFormationIntegrityData;
                float avgSpeed = VecMath.Dist(0f, 0f, ig.AverageVelocityExcludeFarAgents.x, ig.AverageVelocityExcludeFarAgents.y);
                float fi = FormationIntegrity.Compute(ig.DeviationOfPositionsExcludeFarAgents, ig.MaxDeviationOfPositionExcludeFarAgents, avgSpeed, ig.AverageMaxUnlimitedSpeedExcludeFarAgents, f.CountOfUnits);
                if (fi < integrity) integrity = fi;
            }
            input.InfantryDistToEnemy = infDist;
            input.ArcherDistToEnemy = archDist;
            input.CavalryDistFromMain = cavDist;
            input.FormationIntegrity = integrity;

            // 骑兵是否有目标：近似=意图非 Reform/Retreat（无独立「无任务」信号，后续 feature 接入）
            input.CavalryHasObjective = state.LastIntent != IntentType.Reform && state.LastIntent != IntentType.Retreat;

            // 骑射正面/侧翼累计：来自遥测（t72 只埋点未接线，未接线前=0，异常②不触发）
            input.HorseArcherFrontalCount = _telemetry.Count(TelemetryKind.HorseArcherFrontalAttackCount);
            input.HorseArcherFlankCount = _telemetry.Count(TelemetryKind.HorseArcherFlankCount);

            // 强制冲锋/新目标/战线移动：无现成事件源，后续 feature 接入（当前默认 false/0，异常④⑤⑥依赖项不触发）
            input.ForcedCharge = false;
            input.HasNewObjective = false;
            input.FrontlineMovement = 0f;

            return input;
        }

        // ---------- helpers ----------

        // P1-③ 敌队识别：按引擎真实敌对关系判定，不再取「第一个非己队」（3+ 队混战会把盟友当敌人）。
        // v1.4.8 API 已核实（官方反编译源 Team.cs）：
        //   public bool IsEnemyOf(Team otherTeam)（L629-631，转 MBTeam.IsEnemyOf）；
        //   引擎惯例：遍历 Teams 过滤 MBTeam.IsValid && IsEnemyOf（同 L783-785 GetAveragePositionOfEnemies）。
        // 多敌队时取编队中心距己最近者（拟人：打最近的敌人）；无敌队返回 null（调用方已有防御）。
        // O(队数×编队数)，仅 0.5s 意图拍/事件驱动调用，与原实现同阶；敌队关系为引擎公开 API（反作弊闸不变）。
        private Team GetEnemyTeam(Team own)
        {
            if (own == null) return null;
            Team best = null;
            float bestDist = float.MaxValue;
            Vec2 oc = GetTeamCenter(own);
            foreach (TeamState state in _states.Values)
            {
                Team t = state.Team;
                if (t == own || !t.MBTeam.IsValid || !t.IsEnemyOf(own)) continue;
                Vec2 tc = GetTeamCenter(t);
                float d = VecMath.Dist(tc.x, tc.y, oc.x, oc.y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }
            return best;
        }

        // F10 输入：给定队索引 → 其敌队存活数（意图反制判定）
        private int GetEnemyAliveCount(int teamIndex)
        {
            foreach (TeamState state in _states.Values)
            {
                if (state.Team.TeamIndex != teamIndex) continue;
                Team enemy = GetEnemyTeam(state.Team);
                return enemy != null ? enemy.ActiveAgents.Count : 0;
            }
            return 0;
        }

        private List<Vec2> CollectEnemyCenters(Team own, int poolSlot)
        {
            // T4 零每帧分配：从 [ThreadStatic] BufferPool 取 List<Vec2> 复用，不清空复用
            List<Vec2> list = BufferPool.GetVec2List(poolSlot);
            foreach (TeamState state in _states.Values)
            {
                // P1-③ 只收集真实敌队（IsEnemyOf，Team.cs L629 已核实）：多队混战不再把盟友中心当敌中心
                if (state.Team == own || !state.Team.MBTeam.IsValid || !state.Team.IsEnemyOf(own)) continue;
                foreach (Formation f in state.Team.FormationsIncludingEmpty)
                {
                    if (f == null || f.CountOfUnits == 0) continue;
                    list.Add(TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage)); // P1-④ 统一入口
                }
            }
            return list;
        }

        private Vec2 GetTeamCenter(Team team)
        {
            // P1-④ 口径收敛：几何聚合进 TeamGeometry（Smoothed 口径单一实现）；
            // 此处仅保留胶水状态回退（无一兵保持上次中心，避免跳回原点把 Dist 打成 1.0 假象）
            Vec2 result;
            if (!TeamGeometry.TryGetTeamCenter(team, TeamPosKind.SmoothedAverage, out result))
            {
                TeamState state;
                return _states.TryGetValue(team.TeamIndex, out state) ? state.Center : Vec2.Zero;
            }

            TeamState st;
            if (_states.TryGetValue(team.TeamIndex, out st))
                st.Center = result;
            return result;
        }

        private float FormationSpread(Team team)
        {
            Vec2 c = GetTeamCenter(team);
            float sum = 0f;
            int n = 0;
            foreach (Formation f in team.FormationsIncludingEmpty)
            {
                if (f == null || f.CountOfUnits == 0) continue;
                Vec2 p = TeamGeometry.GetFormationPosition(f, TeamPosKind.SmoothedAverage); // P1-④ 统一入口
                sum += VecMath.Dist(p.x, p.y, c.x, c.y);
                n++;
            }

            TeamState state;
            if (n == 0)
            {
                // 无一兵：保持上次离散度
                return _states.TryGetValue(team.TeamIndex, out state) ? state.Spread : 0f;
            }

            float spread = sum / n;
            if (_states.TryGetValue(team.TeamIndex, out state))
                state.Spread = spread;
            return spread;
        }

        private static float NearestDistance(float x, float y, List<Vec2> points)
        {
            if (points == null || points.Count == 0) return 1000f;
            float best = float.MaxValue;
            foreach (Vec2 p in points)
            {
                float d = VecMath.Dist(x, y, p.x, p.y);
                if (d < best) best = d;
            }
            return best;
        }

        // P2-② 原 FindNearestEnemyAgent（L3 列表内 O(n²) 扫描，攻击者须为 L3）已删除，
        // 由 TickBlock 内 Mission.GetNearbyEnemyAgents 引擎空间查询取代（见该方法注释）。

        private FighterProfile GetProfile(Agent agent)
        {
            if (agent == null) return FighterProfileFactory.Create(SoldierTier.Soldier); // 兜底
            int agentIndex = agent.Index;
            FighterProfile p;
            if (!_profiles.TryGetValue(agentIndex, out p))
            {
                // F5 分类：真实兵种数据（等级/护甲/老兵旗标），不再用 agentIndex 伪随机
                p = FighterProfileFactory.Create(FighterProfileFactory.Classify(BuildSoldierInput(agent)));
                _profiles[agentIndex] = p;
            }
            return p;
        }

        // 从可观察的兵种信息构造分类输入：骑乘→骑兵（骑射归并）、有远程武器→射手、否则步兵；
        // 等级取 CharacterObject.Level；护甲取 SpawnEquipment 身体甲材质（None→Cloth 对齐 ArmorGrade）；
        // 老兵旗标 = 英雄 或 等级≥4（久战折算）。
        private static SoldierInput BuildSoldierInput(Agent agent)
        {
            var input = new SoldierInput();
            if (agent.HasMount) input.Kind = SoldierKind.Cavalry;
            else if (HasRangedWeapon(agent)) input.Kind = SoldierKind.Ranged;
            else input.Kind = SoldierKind.Infantry;

            input.Level = agent.Character != null ? agent.Character.Level : 3; // Classify 内 clamp 1-6
            input.Armor = ClassifyArmorGrade(agent);
            input.IsVeteran = (agent.Character != null && agent.Character.IsHero) || input.Level >= 4;
            return input;
        }

        private static bool HasRangedWeapon(Agent agent)
        {
            for (EquipmentIndex i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
            {
                MissionWeapon w = agent.Equipment[i];
                if (w.IsEmpty || w.CurrentUsageItem == null) continue;
                if (w.CurrentUsageItem.IsRangedWeapon) return true;
            }
            return false;
        }

        private static ArmorGrade ClassifyArmorGrade(Agent agent)
        {
            try
            {
                EquipmentElement body = agent.SpawnEquipment[EquipmentIndex.Body];
                if (body.IsEmpty || body.Item == null || body.Item.ArmorComponent == null)
                    return ArmorGrade.Cloth; // 无甲视为布
                switch (body.Item.ArmorComponent.MaterialType)
                {
                    case ArmorComponent.ArmorMaterialTypes.Plate: return ArmorGrade.Plate;
                    case ArmorComponent.ArmorMaterialTypes.Chainmail: return ArmorGrade.Chainmail;
                    case ArmorComponent.ArmorMaterialTypes.Leather: return ArmorGrade.Leather;
                    default: return ArmorGrade.Cloth; // None / Cloth
                }
            }
            catch
            {
                return ArmorGrade.Cloth;
            }
        }

        private AttackStats GetAttackStats(int agentIndex)
        {
            AttackStats s;
            if (!_attackStats.TryGetValue(agentIndex, out s))
            {
                s = new AttackStats(8);
                _attackStats[agentIndex] = s;
            }
            return s;
        }

        private static AttackDir MapAttackDir(Agent.UsageDirection u)
        {
            switch (u)
            {
                case Agent.UsageDirection.AttackUp: return AttackDir.Up;
                case Agent.UsageDirection.AttackDown: return AttackDir.Down;
                case Agent.UsageDirection.AttackLeft: return AttackDir.Left;
                case Agent.UsageDirection.AttackRight: return AttackDir.Right;
            }
            return AttackDir.None;
        }

        private static Agent.UsageDirection ToUsageDefend(AttackDir d)
        {
            switch (d)
            {
                case AttackDir.Up: return Agent.UsageDirection.DefendUp;
                case AttackDir.Down: return Agent.UsageDirection.DefendDown;
                case AttackDir.Left: return Agent.UsageDirection.DefendLeft;
                case AttackDir.Right: return Agent.UsageDirection.DefendRight;
            }
            return Agent.UsageDirection.DefendAny;
        }
    }
}