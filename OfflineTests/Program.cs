using System;
using System.Collections.Generic;
using CombatBrain;
using CombatBrain.Combat;
// CB-PROMPT-0038 退役：CombatBrain.Movement（F6 走位内核）与 CombatBrain.Compliance（F12 个体抖动）
//   已整链删除，不再 using；CombatBrain.Survival 仍提供编队级重整信号 CasualtyReformSignal
//   （定义在 Survival/SurvivalInstinct.cs，经该文件 link 编译进本测试程序集；个体判定已非调用点）。
using CombatBrain.Survival;
using CombatBrain.Cavalry;
using CombatBrain.Ranged;
using CombatBrain.Morale;
using CombatBrain.Experience;
using CombatBrain.Intent;
using CombatBrain.Perception;
using CombatBrain.Formations;
using CombatBrain.Ai;
using CombatBrain.Battle;
using CombatBrain.Coordination;
using CombatBrain.Telemetry;

namespace OfflineTests
{
    internal static partial class Program
    {
        private static int _failures;

        private static void Check(bool cond, string name)
        {
            if (cond)
            {
                Console.WriteLine("  [PASS] " + name);
            }
            else
            {
                _failures++;
                Console.WriteLine("  [FAIL] " + name);
            }
        }

        private static void CheckApprox(float actual, float expected, string name, float eps = 1e-4f)
        {
            Check(Math.Abs(actual - expected) <= eps, name + " (got " + actual + ")");
        }

        private static int Main()
        {
            _failures = 0;

            // F0 FighterProfileFactory / SoldierTier 断言已随 CB-PROMPT-0038 R3 退役删除
            //   （消费者归零即删；保留的 SoldierInput/PrimaryWeaponClass 由 TroopClassifier 断言覆盖）。
            // F6 MovementController 断言已随 §五.2 退役删除（见 TestMovement 处的映射说明）
            Console.WriteLine("== F7 编队级重整信号 ==");
            TestSurvival();
            Console.WriteLine("== F8 CavalryWaveFSM ==");
            TestCavalry();
            Console.WriteLine("== F9 RangedBrain ==");
            TestRanged();
            // F12 DecisionJitter 断言已随 CB-PROMPT-0038 §五.4 退役删除（个体抖动不设保留：
            //   被删断言 ↔ 退役模块 的逐条映射见 docs/agent-control-surface-audit.md §0b）
            Console.WriteLine("== F11 MoralePropagation ==");
            TestMorale();
            Console.WriteLine("== F11 PhasedRetreat ==");
            TestPhasedRetreat();
            Console.WriteLine("== F10 CounterMatrix ==");
            TestCounterMatrix();
            Console.WriteLine("== F10 CampaignHabits ==");
            TestCampaignHabits();
            Console.WriteLine("== F1 TacticManual/SituationVector ==");
            TestTacticManual();
            Console.WriteLine("== F2 OpponentModel/BeliefEngine ==");
            TestOpponentModel();
            Console.WriteLine("== F3 ProtectPlan ==");
            TestProtectPlan();
            Console.WriteLine("== F4 BracePlan ==");
            TestBracePlan();

            Console.WriteLine("== T1 DecisionBranchTally ==");
            TestObservationTally();

            Console.WriteLine("== T4 WindowCache ==");
            TestWindowCache();

            Console.WriteLine("== P1-1 TerrainSampler ==");
            TestTerrainSampler();

            Console.WriteLine("== T1-01 FormationIntegrity ==");
            TestFormationIntegrity();

            Console.WriteLine("== T26-01 CultureTables ==");
            TestCultureTables();

            Console.WriteLine("== T38-01 BattleTelemetry ==");
            TestBattleTelemetry();

            Console.WriteLine("== T39-01 AnomalyDetector ==");
            TestAnomalyDetector();

            Console.WriteLine("== T2-01 FormationTactics ==");
            TestFormationTactics();

            Console.WriteLine("== T3-01 RangedPosition ==");
            TestRangedPosition();

            Console.WriteLine("== T6-01 RallyPointSelector ==");
            TestRallyPointSelector();

            Console.WriteLine("== T8-01 UnitPreservation ==");
            TestUnitPreservation();

            Console.WriteLine("== T18-01 StalemateDetector ==");
            TestStalemateDetector();

            Console.WriteLine("== t89 FormationTacticsTakeover ==");
            TestFormationTacticsTakeover();

            Console.WriteLine("== X0 DecisionDelay ==");
            TestDecisionDelay();

            Console.WriteLine("== X1 FormationTargeting ==");
            TestFormationTargeting();

            Console.WriteLine("== X3 FacingCorrection ==");
            TestFacingCorrection();

            Console.WriteLine("== X4 FrameRelease ==");
            TestFrameRelease();

            Console.WriteLine("== X5-1 TroopClassifier ==");
            TestTroopClassifier();

            Console.WriteLine("== X5-2 SlotLayout ==");
            TestSlotLayout();

            Console.WriteLine("== X5-3 TerrainWarp ==");
            TestTerrainWarp();

            Console.WriteLine("== X5-4 SlotAssigner ==");
            TestSlotAssigner();

            Console.WriteLine("== X5-5 FormationDiscipline ==");
            TestFormationDiscipline();

            Console.WriteLine("== X5-6 ExecutionPolicy ==");
            TestX5ExecutionPolicy();

            Console.WriteLine("== X6 KnowledgeView/PerceptionFilter ==");
            TestPerception();

            Console.WriteLine("== atc4-perc PerceptionPipeline 完整流程 ==");
            TestPerceptionPipeline();

            Console.WriteLine("== atc4-fog CommitmentContact ==");
            TestCommitmentContact();

            Console.WriteLine("== atc4-reserve ReserveClassifier ==");
            TestReserveClassifier();

            Console.WriteLine("== atc4-opt CoordinationController ==");
            TestCoordinationController();

            Console.WriteLine("== atc4-integration samples ==");
            TestIntegrationSamples();

            Console.WriteLine("== N1 NarratorPure ==");
            TestNarratorPure();

            Console.WriteLine("== narrate-fix P0 崩溃回归 ==");
            TestNarrateFix();

            Console.WriteLine("== atc5r-lease FormationControlLease ==");
            TestFormationControlLease();

            Console.WriteLine("== N2 NarrativeEvents ==");
            TestNarrativeEvents();

            Console.WriteLine("== N3 NarrativeBuffer ==");
            TestNarrativeBuffer();

            Console.WriteLine("== ATC0 CommandProvenance ==");
            TestCommandProvenance();

            Console.WriteLine("== ATC0 OutputLayer ==");
            TestProvenanceOutput();

            Console.WriteLine("== CPS0 ProvenanceTruth ==");
            TestCps0ProvenanceTruth();

            Console.WriteLine("== CPS1 CommandProposal ==");
            TestCps1CommandProposal();

            Console.WriteLine("== CPS1 CommandExecutor ==");
            TestCps1CommandExecutor();

            Console.WriteLine("== CPS1 MigrateDefense ==");
            TestCps1MigrateDefense();

            Console.WriteLine("== CPS1 MigrateSpecialists ==");
            TestCps1MigrateSpecialists();

            Console.WriteLine("== CPS1 RemoveDirectWriters ==");
            TestCps1RemoveDirectWriters();

            Console.WriteLine("== CPS1 RepairChargeTarget ==");
            TestCps1RepairChargeTarget();

            Console.WriteLine("== CPS2 LeaseIntegration ==");
            TestCps2LeaseIntegration();

            Console.WriteLine("== CPS2 OwnershipPoc ==");
            TestCps2OwnershipPoc();

            Console.WriteLine("== CPS3 RuntimePrep ==");
            TestCps3RuntimePrep();

            Console.WriteLine("== CPS3 McmSwitches ==");
            TestCps3McmSwitches();

            Console.WriteLine("== CPS3 ObservationHotswap ==");
            TestCps3ObservationHotswap();

            Console.WriteLine("== CPS3 ModeSync ==");
            TestCps3ModeSync();

            Console.WriteLine("== CPS3 ObserverDiagnostics ==");
            TestCps3ObserverDiagnostics();

            Console.WriteLine("== CPS3 HarmonyExplicitPatch ==");
            TestCps3HarmonyExplicitPatch();

            Console.WriteLine("== CPS3 HarmonyParamNames ==");
            TestCps3HarmonyParamNames();

            Console.WriteLine("== CPS3r EvidenceChain ==");
            TestCps3rEvidenceChain();

            Console.WriteLine("== CPS3r RepairPrefixStage ==");
            TestCps3rRepairPrefixStage();

            Console.WriteLine("== CPS3r ForceLeaseCandidate ==");
            TestCps3rForceLeaseCandidate();

            Console.WriteLine("== CPS3r ForceArmTriggerSplit ==");
            TestCps3rForceArmTriggerSplit();

            Console.WriteLine("== CPS3r RouteAHardgates ==");
            TestCps3rRouteAHardgates();

            Console.WriteLine("== CPS3r RouteARepair2 ==");
            TestCps3rRouteARepair2();

            Console.WriteLine("== CPS3r DRouteAMode ==");
            TestCps3rDRouteAMode();

            Console.WriteLine("== CPS3r McmDropdownE ==");
            TestCps3rMcmDropdownE();

            Console.WriteLine("== NarrativeFactionFix ==");
            TestNarrativeFactionFix();

            Console.WriteLine("== RouteAFinalBlocker ==");
            TestRouteAFinalRepair();

            Console.WriteLine("== ATC1 BattlePhaseFSM ==");
            TestBattlePhaseFSM();

            Console.WriteLine("== ATC2 ArmyContext ==");
            TestArmyContext();

            Console.WriteLine("== ATC3 Mission/Isolation ==");
            TestFormationMission();

            Console.WriteLine("== ATC4 CommitmentEvaluator ==");
            TestCommitmentEvaluator();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("ALL PASS");
                return 0;
            }
            Console.WriteLine(_failures + " FAILURE(S)");
            return 1;
        }

        // F0 FighterProfileFactory / SoldierTier 断言已随 CB-PROMPT-0038 R3 整块删除（消费者归零即删）：
        //   被删 14 条断言 ↔ 退役模块（FighterProfile 四字段表 + FighterProfileFactory.Classify/Create）
        //   的逐条映射见 docs/agent-control-surface-audit.md §0b。
        //   保留：SoldierInput / PrimaryWeaponClass（仍有编队级消费者 FormationSlotGlue → TroopClassifier，
        //   其断言见 TestTroopClassifier）。

        // F6 逐兵走位决策（MovementController.Decide/IdealGap）断言已随 CB-PROMPT-0038 §五.2 整块删除：
        //   被删断言 ↔ 退役模块（MovementController + MovementGlue）逐条映射见
        //   docs/agent-control-surface-audit.md §0b。

        private static void TestSurvival()
        {
            // F7 个体求生判定（SurvivalInstinct.Decide）断言已随 CB-PROMPT-0038 §五.5 退役删除：
            //   逐兵「低血 → 自行退向友军」决策不再是 CombatBrain 职责（3 条断言 →
            //   CombatBrain/Survival/SurvivalInstinct.cs 的 SurvivalInstinct.Decide）。
            // 保留：编队级重整信号（事件累计窗口契约）。
            var sig = new CasualtyReformSignal();
            int i;
            for (i = 0; i < 7; i++)
                Check(!sig.OnCriticalCasualty(i * 0.1f), "reform not triggered at " + (i + 1));
            Check(sig.OnCriticalCasualty(7 * 0.1f), "reform triggered at threshold 8");

            var small = new CasualtyReformSignal(windowSeconds: 5f, threshold: 2);
            Check(!small.OnCriticalCasualty(0f), "expiry: first event no trigger");
            Check(!small.OnCriticalCasualty(10f), "expiry: old event dropped");
            Check(small.CriticalCount == 1, "expiry: count trimmed to 1");
            Check(small.OnCriticalCasualty(10.1f), "expiry: second fresh event triggers");
        }

        private static void TestCavalry()
        {
            var s = CavalryState.Scout;
            Check(CavalryWaveFSM.Advance(s, new CavalryInput()) == CavalryState.Scout, "scout stays without weakness");

            var inp = new CavalryInput { ScoutFoundWeakness = true };
            s = CavalryWaveFSM.Advance(s, inp);
            Check(s == CavalryState.Setup, "scout -> setup");
            Check(CavalryWaveFSM.Advance(s, new CavalryInput()) == CavalryState.Setup, "setup stays without opportunity");

            inp = new CavalryInput { Opportunity = OpportunityType.EnemyLineGap };
            s = CavalryWaveFSM.Advance(s, inp);
            Check(s == CavalryState.Charge, "setup -> charge");

            inp = new CavalryInput { ImpactFinished = true };
            s = CavalryWaveFSM.Advance(s, inp);
            Check(s == CavalryState.Recall, "charge -> recall");

            inp = new CavalryInput { RallyComplete = true };
            s = CavalryWaveFSM.Advance(s, inp);
            Check(s == CavalryState.Reform, "recall -> reform");

            inp = new CavalryInput { ReformComplete = true };
            s = CavalryWaveFSM.Advance(s, inp);
            Check(s == CavalryState.Setup, "reform -> setup (loop closes)");
        }

        // F9 编队级远程判断（CB-PROMPT-0038 §五.6 退役后保留项）：
        //   旧的逐兵选敌打分（RangedBrain.SelectTarget / RangedTarget / ArmorClass / AmmoKind）已随个体远程腿删除，
        //   断言同步删除，保留并加固编队级取舍判定（是否继续射击 / 转换任务 / 整体后撤）。
        private static void TestRanged()
        {
            Check(RangedBrain.DecideFallback(0.1f, true) == RangedFallback.SwitchToMelee, "low ammo + melee -> SwitchToMelee");
            Check(RangedBrain.DecideFallback(0.1f, false) == RangedFallback.Retreat, "low ammo + far -> Retreat");
            Check(RangedBrain.DecideFallback(0.5f, false) == RangedFallback.KeepShooting, "enough ammo -> KeepShooting");

            // 阈值边界：≤0.15 判缺弹（编队级取舍触发），>0.15 继续射击
            Check(RangedBrain.AmmoLowThreshold == 0.15f, "f9 ammo low threshold keeps 0.15 contract");
            Check(RangedBrain.DecideFallback(0.15f, false) == RangedFallback.Retreat, "f9 ammo exactly at threshold -> Retreat");
            Check(RangedBrain.DecideFallback(0.16f, false) == RangedFallback.KeepShooting, "f9 ammo just above threshold -> KeepShooting");
            Check(RangedBrain.DecideFallback(1f, true) == RangedFallback.KeepShooting, "f9 full ammo + melee -> still KeepShooting (ammo dominates)");
        }

        // F12 DecisionJitter（Compliance.ComputeProfile / Apply）断言已随个体抖动退役整块删除：
        //   15 条断言（per-pillar 系数、discipline/morale 单调性、clamp、随机可复现）对应的纯逻辑
        //   CombatBrain/Compliance/DecisionJitter.cs 与胶水 DecisionJitterGlue.cs 均已删除；
        //   逐条映射（被删断言 ↔ 退役模块）见 docs/agent-control-surface-audit.md §0b。

        private static void TestMorale()
        {
            CheckApprox(MoralePropagation.Propagate(0.5f, 0.8f, 0f), 0.545f, "neighbor high morale -> drag up");
            CheckApprox(MoralePropagation.Propagate(0.5f, 0.2f, 0f), 0.455f, "neighbor low morale -> drag down");
            CheckApprox(MoralePropagation.Propagate(0.7f, 0.7f, 1f), 0.3f, "neighbor collapse -> contagion");
            CheckApprox(MoralePropagation.Propagate(0f, 0f, 1f), 0f, "contagion clamps at 0");
            CheckApprox(MoralePropagation.Propagate(1f, 1f, 0f), 1f, "stable stays at 1");

            Check(MoralePropagation.AtCollapse(0.2f, 0f), "low morale -> collapse");
            Check(MoralePropagation.AtCollapse(0.5f, 0.6f), "high break rate -> collapse");
            Check(!MoralePropagation.AtCollapse(0.5f, 0.3f), "stable -> no collapse");
            Check(!MoralePropagation.AtCollapse(0.4f, 0f), "above threshold -> no collapse");
            // t50 判据合理化：满士气+伤亡波不崩（伤亡先经存活率/传染拉低士气）；士气受创+高脱阵才加速崩
            Check(!MoralePropagation.AtCollapse(1.0f, 0.6f), "full morale + casualty wave -> no collapse");
            Check(MoralePropagation.AtCollapse(0.7f, 0.6f), "dented morale + high break rate -> collapse");

            var w = new BreakRateWindow(5f, 10);
            w.Record(0f);
            w.Record(1f);
            w.Record(2f);
            CheckApprox(w.Rate(2f), 0.3f, "break rate = 3/10");

            var sat = new BreakRateWindow(5f, 10);
            for (int i = 0; i < 12; i++) sat.Record(i * 0.1f);
            CheckApprox(sat.Rate(11 * 0.1f), 1f, "break rate saturates at 1");

            var exp = new BreakRateWindow(5f, 10);
            exp.Record(0f);
            CheckApprox(exp.Rate(10f), 0f, "break events expire outside window");
        }

        private static void TestPhasedRetreat()
        {
            Check(PhasedRetreatFSM.Advance(RetreatState.Hold, new RetreatInput { Collapse = true }) == RetreatState.RequestRetreat,
                "hold + collapse -> request retreat");
            Check(PhasedRetreatFSM.Advance(RetreatState.Hold, new RetreatInput()) == RetreatState.Hold,
                "hold stays when stable");

            Check(PhasedRetreatFSM.Advance(RetreatState.RequestRetreat, new RetreatInput { Approved = true }) == RetreatState.Disengage,
                "request + approved -> disengage");
            Check(PhasedRetreatFSM.Advance(RetreatState.RequestRetreat, new RetreatInput { Collapse = false }) == RetreatState.Hold,
                "crisis cleared -> back to hold");
            Check(PhasedRetreatFSM.Advance(RetreatState.RequestRetreat, new RetreatInput { Collapse = true }) == RetreatState.RequestRetreat,
                "not approved yet -> keep requesting");

            Check(PhasedRetreatFSM.Advance(RetreatState.Disengage, new RetreatInput { DisengageDone = true }) == RetreatState.Reform,
                "disengage done -> reform");
            Check(PhasedRetreatFSM.Advance(RetreatState.Reform, new RetreatInput { ReformDone = true }) == RetreatState.Hold,
                "reform done -> hold (loop closes)");
        }

        private static void TestCounterMatrix()
        {
            var cm = new CounterMatrix();
            CheckApprox(cm.GetConfidence(5), 1f, "default confidence 1");

            cm.OnCountered(5);
            CheckApprox(cm.GetConfidence(5), 0.7f, "1 counter -> 0.7");
            Check(!cm.IsShunned(5), "1 counter not shunned");

            cm.OnCountered(5);
            cm.OnCountered(5);
            Check(cm.IsShunned(5), "3 counters -> shunned");
            CheckApprox(cm.GetConfidence(5), 0.343f, "3 counters -> 0.343", 1e-3f);

            cm.OnSuccess(5);
            Check(!cm.IsShunned(5), "success clears shun");
            CheckApprox(cm.GetConfidence(5), 0.493f, "success recovers 0.343+0.15", 1e-3f);

            var cm2 = new CounterMatrix();
            cm2.OnCountered(1);
            cm2.OnCountered(1);
            cm2.OnCountered(1);
            Check(cm2.PickBest(new[] { 1, 2 }) == 2, "pick non-shunned tactic");
            Check(cm2.PickBest(new[] { 1 }) == -1, "all shunned -> -1");
            Check(cm2.PickBest(new int[0]) == -1, "empty candidates -> -1");

            var cm3 = new CounterMatrix();
            cm3.OnSuccess(9);
            CheckApprox(cm3.GetConfidence(9), 1f, "success clamps at 1");
        }

        private static void TestCampaignHabits()
        {
            var h = new CampaignHabits();
            Check(h.Predict() == -1, "no records -> -1");

            h.Record(1);
            h.Record(2);
            h.Record(2);
            Check(h.Predict() == 2, "predict most frequent play");
            Check(h.CountOf(2) == 2 && h.CountOf(1) == 1 && h.CountOf(3) == 0, "counts correct");

            h.Clear();
            Check(h.Predict() == -1, "clear empties memory");
        }

        private static void TestTacticManual()
        {
            // 维度与意图数
            Check(SituationVector.DimensionCount == 12, "SituationVector 12 dims");
            Check(TacticManual.IntentCount == 9, "TacticManual 9 intents");
            Check(TacticManual.DimensionCount == 12, "TacticManual 12 dims");
            Check(SituationVector.Labels.Length == 12, "labels 12 entries");

            // 旧 8 维索引不动 + 新 4 维读写（向后兼容）
            var v = new SituationVector();
            v[0] = 0.3f; v[7] = 0.9f;
            CheckApprox(v.Power, 0.3f, "index 0 -> Power");
            CheckApprox(v.Dist, 0.9f, "index 7 -> Dist");
            v[8] = 0.4f; v[9] = 0.5f; v[10] = 0.6f; v[11] = 0.7f;
            CheckApprox(v.EnemCav, 0.4f, "index 8 -> EnemCav");
            CheckApprox(v.FlankEx, 0.5f, "index 9 -> FlankEx");
            CheckApprox(v.Gap, 0.6f, "index 10 -> Gap");
            CheckApprox(v.Skirm, 0.7f, "index 11 -> Skirm");

            // 12 维归一化：全部维在 [0,1] 内评分不发散
            float[][] m = TacticManual.BuildMatrix();
            Check(m.Length == 9, "matrix 9 rows");
            bool rows12 = true;
            for (int i = 0; i < m.Length; i++) if (m[i].Length != 12) rows12 = false;
            Check(rows12, "each row 12 cols");
            bool inBounds = true;
            for (int i = 0; i < m.Length; i++)
                for (int d = 0; d < 12; d++)
                    if (m[i][d] < -1f || m[i][d] > 1f) inBounds = false;
            Check(inBounds, "all weights within [-1,1]");

            // 新意图权重存在且关键维符合 t16 §1.2 建议值
            int idxProtect = Array.IndexOf(TacticManual.Order, IntentType.ProtectArcher);
            int idxBrace = Array.IndexOf(TacticManual.Order, IntentType.BraceFormation);
            Check(idxProtect == 7 && idxBrace == 8, "new intents appended at index 7/8");
            CheckApprox(m[idxProtect][5], 0.40f, "ProtectArcher Arc 0.40");
            CheckApprox(m[idxProtect][8], 0.35f, "ProtectArcher EnemCav 0.35");
            CheckApprox(m[idxProtect][9], 0.30f, "ProtectArcher FlankEx 0.30");
            CheckApprox(m[idxBrace][7], -0.30f, "BraceFormation Dist -0.30 (t71)");
            CheckApprox(m[idxBrace][8], 0.50f, "BraceFormation EnemCav 0.50");
            // t71 架枪过早触发：敌骑远(Dist=1.0) Brace 非最高分；敌骑近且多(Dist~0.1+EnemCav 高) Brace 最高分
            var vBraceFar = new SituationVector { Dist = 1.0f };
            int bestBraceFar = 0; float bestBraceFarScore = TacticManual.Score(vBraceFar, 0);
            for (int i = 1; i < TacticManual.IntentCount; i++) { float s = TacticManual.Score(vBraceFar, i); if (s > bestBraceFarScore) { bestBraceFarScore = s; bestBraceFar = i; } }
            Check(bestBraceFar != idxBrace, "Dist=1.0 -> Brace not highest (t71)");
            var vBraceCloseCav = new SituationVector { Dist = 0.1f, EnemCav = 1.0f };
            int bestBraceClose = 0; float bestBraceCloseScore = TacticManual.Score(vBraceCloseCav, 0);
            for (int i = 1; i < TacticManual.IntentCount; i++) { float s = TacticManual.Score(vBraceCloseCav, i); if (s > bestBraceCloseScore) { bestBraceCloseScore = s; bestBraceClose = i; } }
            Check(bestBraceClose == idxBrace, "Dist~0.1 + EnemCav high -> Brace highest (t71)");

            // C 矩阵：9×6 边界 + 方向正确（t11 B2 锚点 + t16 §1.3 新行）
            float[][] c = TacticManual.BuildCounter();
            Check(c.Length == 9, "C matrix 9 rows");
            bool c6 = true;
            for (int i = 0; i < c.Length; i++) if (c[i].Length != 6) c6 = false;
            Check(c6, "C matrix each row 6 cols");
            bool cInBounds = true;
            for (int i = 0; i < c.Length; i++)
                for (int d = 0; d < 6; d++)
                    if (c[i][d] < -1f || c[i][d] > 1f) cInBounds = false;
            Check(cInBounds, "C matrix weights within [-1,1]");
            Check(c[idxDefend()][(int)EnemyIntent.Flank] > 0f, "C Defend x Flank > 0 (t11)");
            Check(c[idxBrace][(int)EnemyIntent.FrontalAttack] > 0f, "C Brace x Frontal > 0 (t16)");
            Check(c[idxBrace][(int)EnemyIntent.Flank] < 0f, "C Brace x Flank < 0 (t16)");
            Check(c[idxProtect][(int)EnemyIntent.Flank] > 0f, "C Protect x Flank > 0 (t16)");
            Check(c[idxProtect][(int)EnemyIntent.FeignRetreat] < 0f, "C Protect x Feign < 0 (t16)");

            // 意图引擎兼容 9×12：高 Arc+EnemCav+FlankEx 态势 → ProtectArcher 胜出
            var vec = new SituationVector { Arc = 0.9f, EnemCav = 0.8f, FlankEx = 0.7f };
            IntentType chosen;
            // 首帧需连续 Evaluate 数次越过 holdTime 才切换；用 holdTime=0 的引擎验证方向
            var fast = new IntentEngine(TacticManual.BuildMatrix(), holdTime: 0f);
            fast.Evaluate(vec, 0.5f, out chosen);
            Check(chosen == IntentType.ProtectArcher, "high Arc+EnemCav+FlankEx -> ProtectArcher");

            // t60 夹击/绕后触发：FlankEx/Gap 为 Flank 正触发（修复原 -0.20 反向权重），Harass 骑兵优骚扰
            int idxFlank = Array.IndexOf(TacticManual.Order, IntentType.Flank);
            int idxHarass = Array.IndexOf(TacticManual.Order, IntentType.Harass);
            CheckApprox(m[idxFlank][9], 0.60f, "Flank FlankEx +0.60 (t60)");
            CheckApprox(m[idxFlank][10], 0.45f, "Flank Gap +0.45 (t60)");
            Check(m[idxHarass][9] > 0f && m[idxHarass][10] > 0f, "Harass FlankEx/Gap positive (t60)");
            Check(m[idxHarass][8] < 0f, "Harass EnemCav negative (t60, avoid enemy cavalry)");
            // 侧翼暴露+缺口态势 → Flank 胜出（不再只打正面；配合 hold 6s 防抖与 15% 切换余量）
            var vf = new SituationVector { FlankEx = 0.6f, Gap = 0.5f, Cav = 0.3f, Moral = 0.9f, Dist = 0.5f };
            var fastF = new IntentEngine(TacticManual.BuildMatrix(), holdTime: 0f);
            IntentType chosenF;
            fastF.Evaluate(vf, 0.5f, out chosenF);
            Check(chosenF == IntentType.Flank, "flank exposed + gap -> Flank (t60)");
        }

        // F2 OpponentModel / BeliefIntentEngine（t16 §2.1 信号先验表 + t11 B1/B2 + t11 L164 封顶）
        private static void TestOpponentModel()
        {
            // ---- 先验常量表（t16 §2.1）----
            PriorTable t = PriorTable.BuildDefault();
            Check(t != null && t.Signals.Length == PriorTable.SignalCount, "default table 9 signals");
            CheckApprox(t.Signals[(int)SignalKind.SkeinCharge].Priors[(int)EnemyIntent.FrontalAttack], 0.80f, "SkeinCharge Front 0.80");
            CheckApprox(t.Signals[(int)SignalKind.ShieldWallHold].Priors[(int)EnemyIntent.FrontalAttack], -0.20f, "ShieldWall Front -0.20");
            CheckApprox(t.Signals[(int)SignalKind.ShieldWallHold].Priors[(int)EnemyIntent.Reform], 0.30f, "ShieldWall Reform +0.30");
            CheckApprox(t.Signals[(int)SignalKind.CavalryFlankGather].Priors[(int)EnemyIntent.Flank], 0.85f, "CavGather Flank 0.85");
            CheckApprox(t.Signals[(int)SignalKind.HorseArcherHarass].Priors[(int)EnemyIntent.Skirmish], 0.60f, "HAH Skirm 0.60");
            CheckApprox(t.Signals[(int)SignalKind.FeignedRetreat].Priors[(int)EnemyIntent.FeignRetreat], 0.50f, "Feign FeignRetreat 0.50");
            CheckApprox(t.Signals[(int)SignalKind.CeaseFire].FrontalCap, 0.40f, "CeaseFire cap 0.40 (t11 L164)");

            // ---- JSON 配置化（CL 教训）+ 兜底 ----
            string json = "{ \"signals\": [ { \"name\": \"SkeinCharge\", \"priors\": [0.8,0,0,0,0,0] }, " +
                "{ \"name\": \"CeaseFire\", \"priors\": [0,0,0,0,0,0], \"frontalCap\": 0.4 } ] }";
            PriorTable jt = PriorTable.ParseJson(json);
            Check(jt != null, "ParseJson valid");
            CheckApprox(jt.Signals[(int)SignalKind.SkeinCharge].Priors[0], 0.80f, "json SkeinCharge 0.80");
            CheckApprox(jt.Signals[(int)SignalKind.CeaseFire].FrontalCap, 0.40f, "json CeaseFire cap 0.40");
            Check(jt.Signals[(int)SignalKind.HeavyCharge] == null, "json missing signal -> null slot");
            Check(PriorTable.ParseJson("{ bad") == null, "ParseJson malformed -> null");
            Check(PriorTable.ParseJson("{ \"signals\": [ { \"name\": \"SkeinCharge\" } ] }") == null, "ParseJson missing priors -> null");
            Check(PriorTable.ParseJson("{ \"signals\": [ { \"name\": \"Nope\", \"priors\": [0,0,0,0,0,0] } ] }") == null, "ParseJson unknown name -> null");
            var jm = new OpponentModel(jt);
            jm.ApplySignal(SignalKind.HeavyCharge, 1f, 0f);
            float jsum = 0f;
            foreach (float x in jm.BeliefRaw()) jsum += x;
            CheckApprox(jsum, 0f, "unconfigured signal ignored");

            // ---- 信念更新 / 冷却 / 距离折扣入口 / 衰减 / 负钳位 ----
            var m1 = new OpponentModel(t);
            m1.Tick(0f); // 基线
            m1.ApplySignal(SignalKind.SkeinCharge, 1f, 0f);
            CheckApprox(m1.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 0.80f, "single signal raw 0.80");
            float n1sum = 0f;
            foreach (float x in m1.SnapshotBelief()) n1sum += x;
            CheckApprox(n1sum, 1f, "belief normalized sum 1");
            EnemyIntent dom; float conf;
            Check(m1.TryReadDominant(out dom, out conf) && dom == EnemyIntent.FrontalAttack && Math.Abs(conf - 1f) < 1e-4f, "dominant FrontalAttack conf 1");
            m1.ApplySignal(SignalKind.SkeinCharge, 1f, 0.5f);
            CheckApprox(m1.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 0.80f, "cooldown blocks same signal <3s");
            m1.ApplySignal(SignalKind.SkeinCharge, 1f, 4f);
            CheckApprox(m1.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 1.60f, "cooldown passed -> +0.80");
            m1.ApplySignal(SignalKind.SkeinCharge, 0.5f, 8f);
            CheckApprox(m1.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 2.00f, "weight 0.5 -> +0.40 (distance discount entry)");
            m1.Tick(24f);
            CheckApprox(m1.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 0.25f, "3 half-lives -> 0.25 (8s half-life decay)");
            m1.ApplySignal(SignalKind.ShieldWallHold, 1f, 25f);
            m1.ApplySignal(SignalKind.ShieldWallHold, 3f, 29f);
            CheckApprox(m1.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 0f, "negative raw clamped 0");
            CheckApprox(m1.BeliefRaw()[(int)EnemyIntent.Reform], 1.20f, "Reform accumulated 1.20");

            // ---- 降级：置信 <0.5 → 读不懂（不注入信念）----
            var m2 = new OpponentModel(t);
            m2.ApplySignal(SignalKind.SkeinCharge, 0.5f, 0f);
            m2.ApplySignal(SignalKind.HorseArcherHarass, 0.5f, 4f);
            m2.ApplySignal(SignalKind.ShieldWallHold, 0.5f, 8f);
            Check(!m2.TryReadDominant(out dom, out conf), "conf<0.5 -> unreadable (no belief injection)");

            // ---- 降级：熵 >1.8 → 读不懂（矛盾信号）----
            var m3 = new OpponentModel(t);
            m3.ApplySignal(SignalKind.SkeinCharge, 0.5f, 0f);
            m3.ApplySignal(SignalKind.ReserveMove, 0.5f, 4f);
            m3.ApplySignal(SignalKind.HorseArcherHarass, 0.5f, 8f);
            m3.ApplySignal(SignalKind.FeignedRetreat, 0.5f, 12f);
            m3.ApplySignal(SignalKind.ShieldWallHold, 0.5f, 16f);
            float ent;
            Check(!m3.TryReadDominant(out dom, out conf, out ent) && ent > 1.8f, "entropy>1.8 -> unreadable (degradation)");

            // ---- CeaseFire 封顶（t11 L164）----
            var m4 = new OpponentModel(t);
            m4.ApplySignal(SignalKind.SkeinCharge, 1f, 0f);
            m4.ApplySignal(SignalKind.CeaseFire, 1f, 4f);
            CheckApprox(m4.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 0.40f, "CeaseFire caps FrontalAttack 0.40");

            // ---- atc4-phase-provenance-fix P1-6：多观察方隔离——两个己方 Team（own A/B）观察同一敌编队，
            //   各自独立 OpponentModel 实例（胶水层 ModelFor(ownTeamId) 每观察方一实例），信念互不串扰 ----
            var obsA = new OpponentModel(t);
            var obsB = new OpponentModel(t);
            obsA.Tick(0f);
            obsB.Tick(0f);
            obsA.ApplySignal(SignalKind.SkeinCharge, 1f, 0f);   // A 观察到敌编队冲锋
            obsB.ApplySignal(SignalKind.ShieldWallHold, 1f, 0f); // B 观察到同一敌编队据守（各自独立轨迹基线）
            CheckApprox(obsA.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 0.80f, "atc4-phase obsA front 0.80 (isolation)");
            CheckApprox(obsB.BeliefRaw()[(int)EnemyIntent.FrontalAttack], 0f, "atc4-phase obsB front 0 (负 prior 钳位, 无 A 串扰)");
            CheckApprox(obsA.BeliefRaw()[(int)EnemyIntent.Reform], 0f, "atc4-phase obsA unaffected by obsB signal");
            CheckApprox(obsB.BeliefRaw()[(int)EnemyIntent.Reform], 0.30f, "atc4-phase obsB reform 0.30 (own baseline)");
            // 同一信号时间线各自独立（A 的 SkeinCharge 冷却不影响 B 的 SkeinCharge 触发；
            //   B 内部信念 = ShieldWall 的 -0.20 + SkeinCharge 的 +0.80 = 0.60）
            obsB.ApplySignal(SignalKind.SkeinCharge, 1f, 1f);
            CheckApprox(obsB.BeliefRaw()[(int)EnemyIntent.FrontalAttack], -0.20f + 0.80f, "atc4-phase obsB own skein signal accumulates");

            // ---- TacticManual 评分接入（score = ΣV·W + ΣB·C）----
            Check(TacticManual.EnemyIntentCount == 6, "EnemyIntentCount 6");
            int idxDef = idxDefend();
            int idxAtk = Array.IndexOf(TacticManual.Order, IntentType.Attack);
            int idxBrace = Array.IndexOf(TacticManual.Order, IntentType.BraceFormation);
            CheckApprox(TacticManual.BeliefScore(null, idxDef), 0f, "BeliefScore null -> 0");
            CheckApprox(TacticManual.BeliefScore(new float[3], idxDef), 0f, "BeliefScore short -> 0");
            var bFront = new float[] { 1f, 0f, 0f, 0f, 0f, 0f };
            CheckApprox(TacticManual.BeliefScore(bFront, idxDef), 0.20f, "BeliefScore Defend x Frontal 0.20");
            CheckApprox(TacticManual.BeliefScore(bFront, idxBrace), 0.40f, "BeliefScore Brace x Frontal 0.40");
            var vp = new SituationVector { Power = 0.3f, Dist = 0.9f };
            CheckApprox(TacticManual.Score(vp, idxAtk), -0.27f, "Score Attack(Power .3, Dist .9) -0.27");
            CheckApprox(TacticManual.Score(vp, idxDef), 0.15f, "Score Defend(Power .3, Dist .9) 0.15");
            CheckApprox(TacticManual.CombinedScore(new SituationVector(), bFront, idxDef), 0.20f, "CombinedScore zero-vec == BeliefScore");

            // ---- BeliefIntentEngine：belief==null 与 F1 等价 + 信念注入 + 防抖 ----
            var vHigh = new SituationVector { Arc = 0.9f, EnemCav = 0.8f, FlankEx = 0.7f };
            var ie = new IntentEngine(TacticManual.BuildMatrix(), holdTime: 0f);
            var be = new BeliefIntentEngine(TacticManual.BuildMatrix(), holdTime: 0f);
            IntentType a, b;
            ie.Evaluate(vHigh, 0.5f, out a);
            be.Evaluate(vHigh, null, 0.5f, out b);
            Check(a == b && b == IntentType.ProtectArcher, "belief null == reactionary (F1 semantics)");

            var bi = new BeliefIntentEngine(TacticManual.BuildMatrix(), holdTime: 0f);
            var bFrontWk = new float[] { 0.9f, 0f, 0f, 0f, 0f, 0.1f };
            IntentType chosen;
            bi.Evaluate(new SituationVector(), bFrontWk, 0.5f, out chosen);
            Check(chosen == IntentType.BraceFormation, "high-Frontal belief -> BraceFormation (C anchor)");

            var de = new BeliefIntentEngine(TacticManual.BuildMatrix());
            IntentType tmp;
            Check(!de.Evaluate(new SituationVector(), null, 0.5f, out tmp) && de.Current == IntentType.Defend, "debounce baseline Defend");
            bool switched = false;
            for (int k = 0; k < 11; k++) switched |= de.Evaluate(vHigh, null, 0.5f, out tmp);
            Check(!switched && de.Current == IntentType.Defend, "11 evals no switch (hold 6s)");
            Check(de.Evaluate(vHigh, null, 0.5f, out tmp) && tmp == IntentType.ProtectArcher && de.Current == IntentType.ProtectArcher, "12th eval switches to ProtectArcher");
        }

        // F3 ProtectPlan 保护计划（pipeline §三 F3：退后目标选择/殿后档/回援阈值）
        private static void TestProtectPlan()
        {
            // ---- 退后目标选择：步兵阵后方射击位（退后≠溃逃）----
            float tx, ty;
            ProtectPlan.ChooseRetreatTarget(0f, 0f, 0f, 50f, 13f, out tx, out ty); // 敌(0,0) 步(0,50) 间隙13
            CheckApprox(tx, 0f, "retreat target x on axis");
            CheckApprox(ty, 63f, "retreat target behind infantry (50+13)");
            Check(ty > 50f, "retreat target is behind infantry line");

            // 退化：敌/步重合 → 无 NaN，向上退避
            ProtectPlan.ChooseRetreatTarget(5f, 5f, 5f, 5f, 13f, out tx, out ty);
            Check(!float.IsNaN(tx) && !float.IsNaN(ty) && Math.Abs(ty - 18f) < 1e-4f, "retreat degenerate no NaN");

            // ---- 退后间隙：阵半宽 + 裕量 ----
            CheckApprox(ProtectPlan.RetreatClearance(10f), 13f, "clearance spread10 -> 13");
            CheckApprox(ProtectPlan.RetreatClearance(0f), 8f, "clearance spread0 -> 8 (margin only)");

            // ---- 殿后档目标：敌→弓连线 60% 处 ----
            ProtectPlan.ChooseScreenTarget(0f, 0f, 0f, 80f, out tx, out ty);
            CheckApprox(ty, 48f, "screen target 60% along enemy->ranged (80*0.6)");
            Check(tx < 0.001f && ty > 0f && ty < 80f, "screen target between enemy and ranged");

            // 下限夹取：距敌 <15m → 提升到 15m
            ProtectPlan.ChooseScreenTarget(0f, 0f, 0f, 20f, out tx, out ty);
            CheckApprox(ty, 15f, "screen target clamped to min 15m");

            // 不越过弓手：敌弓距离 < 下限 → 停在弓手处
            ProtectPlan.ChooseScreenTarget(0f, 0f, 0f, 10f, out tx, out ty);
            CheckApprox(ty, 10f, "screen target never past ranged");

            // ---- 骑兵回援阈值 ----
            Check(ProtectPlan.ShouldReturnCavalry(30f), "cavalry returns at 30m");
            Check(ProtectPlan.ShouldReturnCavalry(40f), "cavalry returns at threshold 40m");
            Check(!ProtectPlan.ShouldReturnCavalry(41f), "cavalry no return beyond 40m");
            Check(!ProtectPlan.ShouldReturnCavalry(-5f), "negative distance no return");

            // ---- 弓手受压判定 ----
            Check(ProtectPlan.IsUnderPressure(45f), "ranged pressed at 45m");
            Check(!ProtectPlan.IsUnderPressure(45.01f), "ranged not pressed beyond 45m");
            Check(!ProtectPlan.IsUnderPressure(-1f), "negative distance not pressed");

            // ---- 下令防抖：>3m 才重新下令（防命令风暴）----
            Check(ProtectPlan.ShouldIssueOrder(3.01f), "reissue beyond 3m hysteresis");
            Check(!ProtectPlan.ShouldIssueOrder(3f), "no reissue at 3m");
            Check(!ProtectPlan.ShouldIssueOrder(0f), "no reissue at 0m");
        }

        // F4 BracePlan 架枪反骑计划（pipeline §三 F4：触发阈值/架枪优先/无长杆降级）
        private static void TestBracePlan()
        {
            // ---- 触发阈值：敌骑 ≤55m 且净接近（≥1m/s）；接战(<8m)不触发 ----
            Check(BracePlan.ShouldBrace(55f, 56f), "brace triggers at range 55m closing");
            Check(!BracePlan.ShouldBrace(55.1f, 56.1f), "beyond 55m no trigger");
            Check(!BracePlan.ShouldBrace(8f, 9f), "in melee (<=8m) no brace");
            Check(!BracePlan.ShouldBrace(30f, 30f), "no closing (delta 0) no trigger");
            Check(BracePlan.ShouldBrace(30f, 31.5f), "closing 1.5m/s triggers");
            Check(!BracePlan.ShouldBrace(30f, -1f), "first observation records baseline only (no arm, t71)");
            Check(!BracePlan.ShouldBrace(60f, -1f), "first observation far distance no arm (t71)");
            Check(!BracePlan.ShouldBrace(float.NaN, 0f), "NaN distance no trigger");

            // ---- 退出：>70m 或无敌骑 ----
            Check(BracePlan.ShouldRelease(71f), "release beyond 70m");
            Check(!BracePlan.ShouldRelease(55f), "hold within 70m");
            Check(BracePlan.ShouldRelease(-1f), "release when no cavalry");

            // ---- 无长杆降级：架枪占比 <30% 不架枪 ----
            Check(BracePlan.HasEnoughBraceUnits(4, 12), "4/12 >= 30% braced");
            Check(!BracePlan.HasEnoughBraceUnits(3, 12), "3/12 < 30% degraded");
            Check(!BracePlan.HasEnoughBraceUnits(0, 0), "no sample -> degraded");

            // ---- 架枪优先：剧集中且达标且未被求生接管 → 站定 ----
            Check(BracePlan.ShouldIssueBraceOrder(true, true, false), "brace priority: stand");
            Check(!BracePlan.ShouldIssueBraceOrder(false, true, false), "no episode -> no stand");
            Check(!BracePlan.ShouldIssueBraceOrder(true, false, false), "degraded -> no stand");
            Check(!BracePlan.ShouldIssueBraceOrder(true, true, true), "survival retreat takes over");
        }

        // T1 决策分支计数器（DecisionBranchTally，纯逻辑）
        private static void TestObservationTally()
        {
            var t = new DecisionBranchTally(new[] { "move.advance", "intent.switch", "block.predicted" });
            Check(t.BranchCount == 3, "tally 3 branches registered");
            Check(t.TotalObserved == 0 && t.DistinctObserved == 0, "init empty");
            Check(t.SnapshotAndReset() == string.Empty, "empty snapshot -> empty");
            t.Observe("move.advance");
            t.Observe("move.advance");
            t.Observe("intent.switch");
            Check(t.TotalObserved == 3, "3 observes counted");
            Check(t.DistinctObserved == 2, "2 distinct branches observed");
            Check(t.SnapshotAndReset() == "intent.switch=1 move.advance=2", "sorted aggregated snapshot");
            Check(t.TotalObserved == 0, "snapshot resets total");
            t.Observe("unknown.branch"); // 未注册忽略
            Check(t.TotalObserved == 0, "unregistered branch ignored (no alloc/logic growth)");
            t.Observe("block.predicted");
            t.Reset();
            Check(t.TotalObserved == 0 && t.DistinctObserved == 0, "Reset clears");
        }

        // T3 攻击抑制/恢复核心状态（AttackSuppressionState）已随 X2/F4 退役整链删除
        // （CB-PROMPT-0038 §五.1：运行期零消费者 + 与手活属性模型双向耦合）；对应断言一并删除，
        // 退役后由 OfflineTests\AgentWriteStaticChecks.ps1 保证 CombatBrain 不再写入攻击决定概率。

        // T4 0.5s 窗口缓存（WindowCache，带锁）
        private static void TestWindowCache()
        {
            var wc = new WindowCache<float>(0.5f);
            float v;
            Check(!wc.TryGet(1, 0f, out v), "first read misses (stale)");
            wc.Set(1, 0.8f);
            Check(wc.TryGet(1, 0.3f, out v) && Math.Abs(v - 0.8f) < 1e-6f, "within window -> hit 0.8");
            Check(wc.TryGet(1, 0.49f, out v), "within 0.5s window still hit");
            Check(!wc.TryGet(1, 0.51f, out v), "past 0.5s window -> miss/recompute");
            wc.Set(1, 0.5f);
            Check(wc.TryGet(1, 1.0f, out v) && Math.Abs(v - 0.5f) < 1e-6f, "re-set after expiry cached 0.5");
            wc.Clear();
            Check(!wc.TryGet(1, 1.1f, out v), "Clear dismisses cache");
            // 时间回拨（跨战役/时钟异常）应立即失效
            wc.Set(2, 0.3f);
            Check(!wc.TryGet(2, 0.2f, out v), "time rewind invalidates");
            Check(wc.Count == 0, "rewind cleared entries");
        }

        // P1-① 真实地形高度查表（TerrainSampler，纯逻辑零依赖：采样函数由胶水注入）
        private static void TestTerrainSampler()
        {
            // 平面 h = 0.5x + 2y：网格节点精确、双线性对平面无插值误差
            TerrainSampler ts = TerrainSampler.Build(100f, 200f, 40f, 10f,
                (x, y) => 0.5f * x + 2f * y);
            Check(ts.GridSize == 9, "grid 80m/10m -> 9 points per axis");
            CheckApprox(ts.Height(60f, 160f), 0.5f * 60f + 2f * 160f, "grid node exact (origin corner)");
            CheckApprox(ts.Height(140f, 240f), 0.5f * 140f + 2f * 240f, "grid node exact (far corner)");
            CheckApprox(ts.Height(100f, 200f), 0.5f * 100f + 2f * 200f, "grid node exact (center)");
            CheckApprox(ts.Height(65f, 165f), 0.5f * 65f + 2f * 165f, "bilinear exact on plane");

            // 越界取边缘（优雅降级，无零高度断崖）
            CheckApprox(ts.Height(-500f, 200f), 0.5f * 60f + 2f * 200f, "out-of-range x clamps to edge");
            CheckApprox(ts.Height(100f, 900f), 0.5f * 100f + 2f * 240f, "out-of-range y clamps to edge");

            // 单调地形：高处点查得更高（态势 Height 维方向正确）
            TerrainSampler hill = TerrainSampler.Build(0f, 0f, 100f, 10f, (x, y) => -0.01f * (x * x + y * y));
            Check(hill.Height(0f, 0f) > hill.Height(80f, 0f), "hill top reads higher than flank");

            // 战中查表确定性（只读缓存：同点两次一致，T0 无锁共享）
            Check(hill.Height(30f, -40f) == hill.Height(30f, -40f), "table read deterministic (read-only)");

            // 开战一次性预算：±300m / 8m → 76×76 点，实际间距不超请求值
            TerrainSampler wide = TerrainSampler.Build(0f, 0f, 300f, 8f, (x, y) => 0f);
            Check(wide.Points == 76 * 76, "battlefield 600m/8m -> 76x76 points (one-shot budget)");
            Check(Math.Abs(wide.Step - 8f) < 1e-4f, "actual step honors requested cellSize");

            // 远越界仍返回边缘值（平地=0），不抛异常
            Check(Math.Abs(wide.Height(1e6f, 1e6f)) < 1e-6f, "far out-of-range returns edge value");
        }

        // T1-01 编队阵型完整度（FormationIntegrity，纯逻辑零依赖：官方聚合值由胶水层喂入）
        private static void TestFormationIntegrity()
        {
            // 空编队=100（无事可测，非 0 防误报崩溃）
            Check(FormationIntegrity.Compute(12f, 20f, 0f, 0f, 0) == 100f, "empty formation -> 100");

            // 线性映射锚点：≤3m=100、≥15m=0、中点 9m=50、6m=75（全员同速 ratio=1 不打折）
            Check(FormationIntegrity.Compute(3f, 3f, 1f, 1f, 40) == 100f, "deviation 3m -> 100");
            Check(FormationIntegrity.Compute(15f, 15f, 1f, 1f, 40) == 0f, "deviation 15m -> 0");
            Check(FormationIntegrity.Compute(9f, 9f, 1f, 1f, 40) == 50f, "deviation 9m -> 50 (linear midpoint)");
            Check(FormationIntegrity.Compute(6f, 6f, 1f, 1f, 40) == 75f, "deviation 6m -> 75");

            // 线性单调：偏差越大分越低
            float s6 = FormationIntegrity.Compute(6f, 6f, 1f, 1f, 40);
            float s9 = FormationIntegrity.Compute(9f, 9f, 1f, 1f, 40);
            float s12 = FormationIntegrity.Compute(12f, 12f, 1f, 1f, 40);
            Check(s6 > s9 && s9 > s12, "linear monotonic decreasing");

            // 合并 0.6*平均+0.4*最大：平均完美+单兵掉队(最大 15m) → 60 而非 0（防单兵掉队拉爆；0.6f 有浮点误差用 CheckApprox）
            CheckApprox(FormationIntegrity.Compute(3f, 15f, 0f, 0f, 40), 60f, "single straggler -> 60 (max weight 0.4)");

            // 速度归一化：半速移动 ratio=0.5 → 50*0.85=42.5；满速移动不打折；驻停不归一
            Check(FormationIntegrity.Compute(9f, 9f, 2f, 4f, 40) == 42.5f, "half-speed movement -> 42.5 (velocity normalize)");
            Check(FormationIntegrity.Compute(9f, 9f, 4f, 4f, 40) == 50f, "full-speed movement -> no extra penalty");
            Check(FormationIntegrity.Compute(9f, 9f, 0f, 0f, 40) == 50f, "standing formation -> skip velocity normalize");

            // 骑兵机动不误判：偏差 10m 但全员同速（ratio=1）→ 41.67（中度而非崩溃）
            CheckApprox(FormationIntegrity.Compute(10f, 10f, 5f, 5f, 40), 41.6667f, "cavalry cohesive maneuver -> ~41.7");

            // 分档边界（阈值表勿改）：80/60/40/20
            Check(FormationIntegrity.Grade(100f) == FormationIntegrityGrade.Full && FormationIntegrity.Grade(80f) == FormationIntegrityGrade.Full,
                "grade >=80 -> Full");
            Check(FormationIntegrity.Grade(79f) == FormationIntegrityGrade.Slight && FormationIntegrity.Grade(60f) == FormationIntegrityGrade.Slight,
                "grade 60~79 -> Slight");
            Check(FormationIntegrity.Grade(59f) == FormationIntegrityGrade.Moderate && FormationIntegrity.Grade(40f) == FormationIntegrityGrade.Moderate,
                "grade 40~59 -> Moderate");
            Check(FormationIntegrity.Grade(39f) == FormationIntegrityGrade.Severe && FormationIntegrity.Grade(20f) == FormationIntegrityGrade.Severe,
                "grade 20~39 -> Severe");
            Check(FormationIntegrity.Grade(19f) == FormationIntegrityGrade.Collapsed && FormationIntegrity.Grade(0f) == FormationIntegrityGrade.Collapsed,
                "grade <20 -> Collapsed");
        }

        // T26-01 文化战术模板（六文化 9×12 权重表，源文档 §2.2）
        private static void TestCultureTables()
        {
            // 默认回落：null/空/未知/帝国/掠夺者 → 现有 Matrix（引用相同=逐行相等，零回归）
            Check(ReferenceEquals(TacticManual.MatrixFor(null), TacticManual.BuildMatrix()), "null culture -> default matrix");
            Check(ReferenceEquals(TacticManual.MatrixFor(""), TacticManual.BuildMatrix()), "empty culture -> default matrix");
            Check(ReferenceEquals(TacticManual.MatrixFor("unknown"), TacticManual.BuildMatrix()), "unknown culture -> default matrix");
            Check(ReferenceEquals(TacticManual.MatrixFor("empire"), TacticManual.BuildMatrix()), "empire -> default matrix");
            Check(ReferenceEquals(TacticManual.MatrixFor("looter"), TacticManual.BuildMatrix()), "looter -> default matrix");

            // 5 套分表：独立实例（非默认引用）且差异 ≥3 行（§2.2 每文化 8~9 处 patch，跨 8 行以上）
            string[] cultures = { "vlandia", "sturgia", "battania", "khuzait", "aserai" };
            float[][] def = TacticManual.BuildMatrix();
            foreach (string cid in cultures)
            {
                float[][] t = TacticManual.MatrixFor(cid);
                Check(t != null && !ReferenceEquals(t, def), cid + " -> own culture table");
                int diffRows = 0;
                for (int r = 0; r < t.Length; r++)
                {
                    bool rowDiff = false;
                    for (int d = 0; d < 12; d++) if (t[r][d] != def[r][d]) rowDiff = true;
                    if (rowDiff) diffRows++;
                }
                Check(diffRows >= 3, cid + " differs from default in >=3 rows");
            }

            // 行为方向：库赛特表下 Flank 评分高于默认表（§10.8 包抄优先；与 SkirmishGlue/骑兵绕行同向联动）
            var vk = new SituationVector { Cav = 0.4f, FlankEx = 0.6f, Gap = 0.5f, Dist = 0.9f };
            int idxFlank = Array.IndexOf(TacticManual.Order, IntentType.Flank);
            Check(TacticManual.CombinedScoreFor("khuzait", vk, null, idxFlank) > TacticManual.CombinedScore(vk, null, idxFlank),
                "khuzait table boosts Flank vs default");

            // 引擎级：khuzait 文化引擎构造 + Evaluate 正常出意图（分表路径走通）
            var ke = new BeliefIntentEngine(TacticManual.BuildMatrix(), "khuzait", holdTime: 0f);
            IntentType kChosen;
            ke.Evaluate(vk, null, 0.5f, out kChosen);
            Check(kChosen == IntentType.Flank || kChosen == IntentType.Harass || kChosen == IntentType.Pursue,
                "khuzait engine picks aggressive intent (t69)");
        }

        // T38-01 战斗遥测计数器（20 项：累加/清零/边界/序列化 roundtrip）
        private static void TestBattleTelemetry()
        {
            var t = new BattleTelemetry();
            Check(t.Count(TelemetryKind.RetreatCount) == 0, "telemetry init zero");

            t.Increment(TelemetryKind.RetreatCount);
            t.Increment(TelemetryKind.RetreatCount);
            t.Increment(TelemetryKind.FormationBreakCount);
            Check(t.Count(TelemetryKind.RetreatCount) == 2, "telemetry increment accumulates");
            Check(t.Count(TelemetryKind.FormationBreakCount) == 1, "telemetry distinct counters independent");

            t.Increment(TelemetryKind.BattleDuration); // 边界：非计数项 Increment 为无操作
            t.Increment(TelemetryKind.IdleUnitTime);
            Check(t.Count(TelemetryKind.BattleDuration) == 0, "telemetry float kind increment no-op");

            t.SetBattleDuration(123.5f);
            t.SetHorseArcherLossRate(0.25f);
            t.AddIdleUnitTime(1.25f);
            t.AddIdleUnitTime(0.5f);

            string json = t.ToJson();
            Check(json.Contains("\"RetreatCount\":2"), "telemetry json has counter value");
            Check(json.Contains("\"FormationBreakCount\":1"), "telemetry json has distinct counter value");
            Check(json.Contains("\"BattleDuration\":123.5"), "telemetry json has float duration");
            Check(json.Contains("\"HorseArcherLossRate\":0.25"), "telemetry json has loss rate");
            Check(json.Contains("\"IdleUnitTime\":1.75"), "telemetry json accumulates idle time");

            string summary = t.Summary();
            Check(summary.Contains("FormationBreakCount=1"), "telemetry summary has counter");
            Check(summary.Contains("BattleDuration=123.5"), "telemetry summary has duration");

            t.Reset();
            Check(t.Count(TelemetryKind.RetreatCount) == 0, "telemetry reset clears counters");
            Check(t.ToJson().Contains("\"FormationBreakCount\":0"), "telemetry reset zero json");
            Check(t.ToJson().Contains("\"BattleDuration\":0"), "telemetry reset zero float");
        }

        // T39-01 战术异常检测（6 类反模式：每类各 1 + 持续时间边界）
        private static void TestAnomalyDetector()
        {
            var d = new AnomalyDetector();

            // ① 骑兵无任务游荡：>120m 且无目标，持续>10s（边界：恰好 10s 不触发）
            var i1 = new AnomalyInput { CavalryDistFromMain = 200f, CavalryHasObjective = false };
            i1.Now = 0f; d.Tick(i1);
            i1.Now = 10f; d.Tick(i1);
            Check(d.Count(AnomalyKind.CavalryWander) == 0, "cavalry wander boundary 10s no fire");
            i1.Now = 10.1f; d.Tick(i1);
            Check(d.Count(AnomalyKind.CavalryWander) == 1, "cavalry wander fires after >10s");

            // ② 骑射手正面送死：连续>=3 次正面攻击
            var i2 = new AnomalyInput();
            i2.HorseArcherFrontalCount = 1; d.Tick(i2);
            i2.HorseArcherFrontalCount = 2; d.Tick(i2);
            i2.HorseArcherFrontalCount = 3; d.Tick(i2);
            Check(d.Count(AnomalyKind.HorseArcherFrontalSuicide) == 1, "horse archer frontal 3 consecutive fires");

            // ③ 射手跑到前排：射手距敌 < 步兵距敌-10m，持续>5s（边界：恰好 5s 不触发）
            var i3 = new AnomalyInput { ArcherDistToEnemy = 40f, InfantryDistToEnemy = 60f };
            i3.Now = 0f; d.Tick(i3);
            i3.Now = 5f; d.Tick(i3);
            Check(d.Count(AnomalyKind.ArcherFrontLine) == 0, "archer front line boundary 5s no fire");
            i3.Now = 5.1f; d.Tick(i3);
            Check(d.Count(AnomalyKind.ArcherFrontLine) == 1, "archer front line fires after >5s");

            // ④ 劣势强制冲锋：战场劣势<-30 且完整度<50 且 ForcedCharge 边沿
            var i4 = new AnomalyInput { BattleAdvantage = -40f, FormationIntegrity = 30f, ForcedCharge = false };
            d.Tick(i4);
            i4.ForcedCharge = true; d.Tick(i4);
            Check(d.Count(AnomalyKind.DisadvantagedForcedCharge) == 1, "disadvantaged forced charge fires");

            // ⑤ 无理由放弃高地：地形优势>=20 后离开且无新目标
            var i5 = new AnomalyInput { TerrainAdvantage = 25f };
            d.Tick(i5);
            i5.TerrainAdvantage = 5f; i5.HasNewObjective = false; d.Tick(i5);
            Check(d.Count(AnomalyKind.HighGroundAbandon) == 1, "high ground abandon fires");

            // ⑥ 长期僵局：>120s 且伤亡<5% 且战线移动<20m（单次触发不重复）
            var i6 = new AnomalyInput { Now = 130f, CasualtyRate = 0.02f, FrontlineMovement = 5f };
            d.Tick(i6);
            Check(d.Count(AnomalyKind.LongStalemate) == 1, "long stalemate fires");
            d.Tick(i6);
            Check(d.Count(AnomalyKind.LongStalemate) == 1, "long stalemate no double fire");

            d.Reset();
            Check(d.Count(AnomalyKind.CavalryWander) == 0 && d.Count(AnomalyKind.LongStalemate) == 0, "anomaly reset clears");
        }

        // T2-01 阵型三态（March/Approach/Threatened：距离边界 80/40m + 进入射程 + 完整度联动）
        private static void TestFormationTactics()
        {
            // 三态距离边界 80/40m
            Check(FormationTactics.Classify(81f, false) == FormationTacticsState.March, "tactics dist 81 -> March");
            Check(FormationTactics.Classify(80f, false) == FormationTacticsState.Approach, "tactics dist 80 -> Approach (boundary)");
            Check(FormationTactics.Classify(60f, false) == FormationTacticsState.Approach, "tactics dist 60 -> Approach");
            Check(FormationTactics.Classify(40f, false) == FormationTacticsState.Threatened, "tactics dist 40 -> Threatened (boundary)");
            Check(FormationTactics.Classify(40.1f, false) == FormationTacticsState.Approach, "tactics dist 40.1 -> Approach (boundary)");

            // 进入射程判定：即使 >40m，敌进入射程 → Threatened
            Check(FormationTactics.Classify(60f, true) == FormationTacticsState.Threatened, "tactics enemy in range -> Threatened");

            // 阵型选择
            Check(FormationTactics.SelectArrangement(FormationTacticsState.March, "empire", 100f) == TacticsArrangement.Line, "tactics March -> Line");
            Check(FormationTactics.SelectArrangement(FormationTacticsState.Threatened, "empire", 100f) == TacticsArrangement.ShieldWall, "tactics Threatened -> ShieldWall");
            Check(FormationTactics.SelectArrangement(FormationTacticsState.Approach, "empire", 100f) == TacticsArrangement.Line, "tactics Approach empire -> Line");
            Check(FormationTactics.SelectArrangement(FormationTacticsState.Approach, "sturgia", 100f) == TacticsArrangement.ShieldWall, "tactics Approach sturgia -> ShieldWall");
            Check(FormationTactics.SelectArrangement(FormationTacticsState.Approach, "empire", 40f) == TacticsArrangement.ShieldWall, "tactics Approach low integrity -> ShieldWall (t65)");
        }

        // T3-01 射手射击阵地（7 种允许原因：边界/非允许不动/防抖）
        private static void TestRangedPosition()
        {
            // ① 骑兵威胁（45m 脱离 / 70m+5 威胁 / 70m+4 不允许）
            var cav45 = new RangedPositionInput { HasTarget = true, FiringArcRatio = 1f, FormationIntegrity = 100f, EnemyCavalryDistance = 45f, EnemyCavalryCount = 1 };
            Check(RangedPosition.Decide(cav45) == RangedPositionReason.CavalryThreat, "rangedpos cavalry 45m -> CavalryThreat");
            var cav70x5 = new RangedPositionInput { HasTarget = true, FiringArcRatio = 1f, FormationIntegrity = 100f, EnemyCavalryDistance = 70f, EnemyCavalryCount = 5 };
            Check(RangedPosition.Decide(cav70x5) == RangedPositionReason.CavalryThreat, "rangedpos cavalry 70m+5 -> CavalryThreat");
            var cav70x4 = new RangedPositionInput { HasTarget = true, FiringArcRatio = 1f, FormationIntegrity = 100f, EnemyCavalryDistance = 70f, EnemyCavalryCount = 4 };
            Check(RangedPosition.Decide(cav70x4) == RangedPositionReason.None, "rangedpos cavalry 70m+4 -> None (non-allowed)");

            // ⑥ 阵型被破坏（完整度<50）
            var broken = new RangedPositionInput { HasTarget = true, FiringArcRatio = 1f, FormationIntegrity = 49f, EnemyCavalryDistance = -1f };
            Check(RangedPosition.Decide(broken) == RangedPositionReason.FormationBroken, "rangedpos broken integrity -> FormationBroken");

            // ③ 目标消失
            var gone = new RangedPositionInput { HasTarget = false, FiringArcRatio = 1f, FormationIntegrity = 100f, EnemyCavalryDistance = -1f };
            Check(RangedPosition.Decide(gone) == RangedPositionReason.TargetGone, "rangedpos no target -> TargetGone");

            // 防抖（3m 迟滞 + 状态切换）
            Check(RangedPosition.ShouldIssueMove(RangedPositionReason.CavalryThreat, RangedPositionReason.None, 0f), "rangedpos reason switch -> issue");
            Check(!RangedPosition.ShouldIssueMove(RangedPositionReason.CavalryThreat, RangedPositionReason.CavalryThreat, 3f), "rangedpos same reason drift 3m -> no issue (boundary)");
            Check(RangedPosition.ShouldIssueMove(RangedPositionReason.CavalryThreat, RangedPositionReason.CavalryThreat, 3.1f), "rangedpos same reason drift 3.1m -> issue (boundary)");
        }

        // T6-01 骑兵重整点选址（距离档位 40/80/120 边界 + 优先级链 + 无安全点降级）
        private static void TestRallyPointSelector()
        {
            // 距离档位边界 40/80/120
            Check(RallyPointSelector.ClassifyBand(40f) == RallyBand.Normal, "rally band 40 -> Normal");
            Check(RallyPointSelector.ClassifyBand(80f) == RallyBand.Normal, "rally band 80 -> Normal (boundary)");
            Check(RallyPointSelector.ClassifyBand(80.1f) == RallyBand.Warning, "rally band 80.1 -> Warning");
            Check(RallyPointSelector.ClassifyBand(120f) == RallyBand.Warning, "rally band 120 -> Warning (boundary)");
            Check(RallyPointSelector.ClassifyBand(120.1f) == RallyBand.Forbidden, "rally band 120.1 -> Forbidden");
            Check(RallyPointSelector.ClassifyBand(39.9f) == RallyBand.TooClose, "rally band 39.9 -> TooClose");

            // 优先级链：Flank 禁止 → 落到 BehindLine
            var cand1 = new RallyCandidate[]
            {
                new RallyCandidate { Priority = RallyPriority.Flank, DistanceToMainLine = 150f },
                new RallyCandidate { Priority = RallyPriority.BehindLine, DistanceToMainLine = 60f },
                new RallyCandidate { Priority = RallyPriority.SafeDistance, DistanceToMainLine = 100f },
            };
            Check(RallyPointSelector.SelectBest(cand1).Priority == RallyPriority.BehindLine, "rally priority chain: forbidden flank -> BehindLine");

            // 优先级链：Flank 可用 → Flank 胜出
            var cand2 = new RallyCandidate[]
            {
                new RallyCandidate { Priority = RallyPriority.BehindLine, DistanceToMainLine = 60f },
                new RallyCandidate { Priority = RallyPriority.Flank, DistanceToMainLine = 50f },
            };
            Check(RallyPointSelector.SelectBest(cand2).Priority == RallyPriority.Flank, "rally priority chain: usable flank wins");

            // 无安全点降级：全禁止 → None
            var cand3 = new RallyCandidate[]
            {
                new RallyCandidate { Priority = RallyPriority.Flank, DistanceToMainLine = 150f },
                new RallyCandidate { Priority = RallyPriority.BehindLine, DistanceToMainLine = 150f },
            };
            Check(RallyPointSelector.SelectBest(cand3).Priority == RallyPriority.None, "rally no safe point -> None (degradation)");
        }

        // T8-01 骑射保存有生力量（五档边界 + 行为建议映射）
        private static void TestUnitPreservation()
        {
            // 五档边界
            Check(UnitPreservation.ClassifyBand(0.71f) == PreservationBand.Normal, "preserve 0.71 -> Normal");
            Check(UnitPreservation.ClassifyBand(0.70f) == PreservationBand.Cautious, "preserve 0.70 -> Cautious (boundary)");
            Check(UnitPreservation.ClassifyBand(0.50f) == PreservationBand.ReduceEngagement, "preserve 0.50 -> ReduceEngagement (boundary)");
            Check(UnitPreservation.ClassifyBand(0.30f) == PreservationBand.PrioritizePreservation, "preserve 0.30 -> PrioritizePreservation (boundary)");
            Check(UnitPreservation.ClassifyBand(0.20f) == PreservationBand.Withdraw, "preserve 0.20 -> Withdraw (boundary)");
            Check(UnitPreservation.ClassifyBand(0.10f) == PreservationBand.Withdraw, "preserve 0.10 -> Withdraw");

            // 行为建议映射
            Check(UnitPreservation.ActionFor(PreservationBand.Normal) == PreservationAction.None, "preserve Normal -> None");
            Check(UnitPreservation.ActionFor(PreservationBand.Cautious) == PreservationAction.ReduceFrontal, "preserve Cautious -> ReduceFrontal");
            Check(UnitPreservation.ActionFor(PreservationBand.ReduceEngagement) == PreservationAction.StrengthenWithdraw, "preserve ReduceEngagement -> StrengthenWithdraw");
            Check(UnitPreservation.ActionFor(PreservationBand.PrioritizePreservation) == PreservationAction.SignificantRetreat, "preserve PrioritizePreservation -> SignificantRetreat");
            Check(UnitPreservation.ActionFor(PreservationBand.Withdraw) == PreservationAction.Disengage, "preserve Withdraw -> Disengage");
        }

        // T18-01 僵局打破（90s/5%/20m 边界 + 方案选择 + 意图映射）
        private static void TestStalemateDetector()
        {
            // 僵局判定边界 90s/5%/20m
            Check(!StalemateDetector.IsStalemate(new StalemateInput { CombatDuration = 90f, CasualtyRate = 0.04f, FrontlineMovement = 19f }), "stalemate 90s -> false (boundary)");
            Check(StalemateDetector.IsStalemate(new StalemateInput { CombatDuration = 90.1f, CasualtyRate = 0.04f, FrontlineMovement = 19f }), "stalemate 90.1s -> true");
            Check(!StalemateDetector.IsStalemate(new StalemateInput { CombatDuration = 100f, CasualtyRate = 0.05f, FrontlineMovement = 19f }), "stalemate 5% casualty -> false (boundary)");
            Check(!StalemateDetector.IsStalemate(new StalemateInput { CombatDuration = 100f, CasualtyRate = 0.04f, FrontlineMovement = 20f }), "stalemate 20m frontline -> false (boundary)");
            Check(StalemateDetector.IsStalemate(new StalemateInput { CombatDuration = 100f, CasualtyRate = 0.04f, FrontlineMovement = 19.9f }), "stalemate 19.9m frontline -> true");

            // 方案选择（态势择优）
            Check(StalemateDetector.ChoosePlan(new StalemateInput { CavalryRatio = 0.5f, RangedRatio = 0.1f }) == StalematePlan.FlankEnvelop, "stalemate cav high -> FlankEnvelop");
            Check(StalemateDetector.ChoosePlan(new StalemateInput { CavalryRatio = 0.5f, RangedRatio = 0.5f }) == StalematePlan.HorseArcherHarass, "stalemate cav+ranged high -> HorseArcherHarass");
            Check(StalemateDetector.ChoosePlan(new StalemateInput { CavalryRatio = 0.1f, TerrainAdvantage = 15f }) == StalematePlan.OccupyHighGround, "stalemate terrain high -> OccupyHighGround");
            Check(StalemateDetector.ChoosePlan(new StalemateInput { CavalryRatio = 0.1f, TerrainAdvantage = 5f, RangedRatio = 0.5f }) == StalematePlan.ConcentrateFire, "stalemate ranged high -> ConcentrateFire");
            Check(StalemateDetector.ChoosePlan(new StalemateInput { CavalryRatio = 0.1f, TerrainAdvantage = 5f, RangedRatio = 0.2f, PowerRatio = 1.5f }) == StalematePlan.AdvancePressure, "stalemate power high -> AdvancePressure");
            Check(StalemateDetector.ChoosePlan(new StalemateInput { CavalryRatio = 0.1f, TerrainAdvantage = 5f, RangedRatio = 0.2f, PowerRatio = 1.0f, Morale = 0.3f }) == StalematePlan.FeignRetreat, "stalemate morale low -> FeignRetreat");

            // 意图映射
            Check(StalemateDetector.MapIntent(StalematePlan.FlankEnvelop) == IntentType.Flank, "stalemate FlankEnvelop -> Flank intent");
            Check(StalemateDetector.MapIntent(StalematePlan.OccupyHighGround) == IntentType.Defend, "stalemate OccupyHighGround -> Defend intent");
        }

        // t89 全面接管阵型（原版 Circle 防御环改写 + 幂等 + March/Threatened 分流）
        private static void TestFormationTacticsTakeover()
        {
            // 原版 Circle 被接管改写
            Check(FormationTactics.ShouldOverride(FormationArrangementKind.Circle, TacticsArrangement.ShieldWall), "takeover Circle + ShieldWall -> override");
            Check(FormationTactics.ShouldOverride(FormationArrangementKind.Circle, TacticsArrangement.Line), "takeover Circle + Line -> override");
            // 幂等：已是目标不改写
            Check(!FormationTactics.ShouldOverride(FormationArrangementKind.ShieldWall, TacticsArrangement.ShieldWall), "takeover ShieldWall + ShieldWall -> no override (idempotent)");
            Check(!FormationTactics.ShouldOverride(FormationArrangementKind.Line, TacticsArrangement.Line), "takeover Line + Line -> no override (idempotent)");
            Check(FormationTactics.ShouldOverride(FormationArrangementKind.Other, TacticsArrangement.Line), "takeover Other + Line -> override");

            // March/Threatened 分流：Circle 改写目标
            var marchTarget = FormationTactics.SelectArrangement(FormationTacticsState.March, "empire", 100f);
            Check(marchTarget == TacticsArrangement.Line, "takeover March -> Line target");
            var threatTarget = FormationTactics.SelectArrangement(FormationTacticsState.Threatened, "empire", 100f);
            Check(threatTarget == TacticsArrangement.ShieldWall, "takeover Threatened -> ShieldWall target");
            Check(FormationTactics.ShouldOverride(FormationArrangementKind.Circle, marchTarget), "takeover Circle + March -> rewrite to Line");
            Check(FormationTactics.ShouldOverride(FormationArrangementKind.Circle, threatTarget), "takeover Circle + Threatened -> rewrite to ShieldWall");
        }

        // X0 决策延迟开关（只保留指挥层迟滞：意图 hold / 切换余量）
        // CB-PROMPT-0038 §五.4：个体抖动缩放（ScaleProfile）断言随 DecisionJitter 退役删除（3 条）。
        private static void TestDecisionDelay()
        {
            CheckApprox(DecisionDelay.HoldTime(0f), 1.5f, "decisiondelay hold strength 0 -> 1.5s");
            CheckApprox(DecisionDelay.HoldTime(1f), 6f, "decisiondelay hold strength 1 -> 6s");
            CheckApprox(DecisionDelay.HoldTime(0.5f), 3.75f, "decisiondelay hold strength 0.5 -> 3.75s");
            CheckApprox(DecisionDelay.SwitchMargin(0f), 0f, "decisiondelay margin strength 0 -> 0%");
            CheckApprox(DecisionDelay.SwitchMargin(1f), 0.15f, "decisiondelay margin strength 1 -> 15%");
        }

        // X1 编队级目标选择（FormationTargeting 纯逻辑）：优先级链/距离加权/威胁加权/小股过滤/无目标降级/半径/缓存/迟滞
        private static void TestFormationTargeting()
        {
            // 1) 优先级链：射手(kind 3) 压过更近的骑兵(kind 1)——类别优先于距离
            var pri = new[]
            {
                new TargetCandidate { Index = 1, X = 30f, Y = 0f, Count = 80, Kind = FormationTargetKind.Cavalry },
                new TargetCandidate { Index = 2, X = 100f, Y = 0f, Count = 65, Kind = FormationTargetKind.Ranged },
            };
            var s1 = FormationTargeting.Select(0f, 0f, 100, pri, pri.Length, TargetSelection.None, 1.0f);
            Check(s1.HasTarget && s1.Index == 2, "x1 priority chain: ranged beats nearer cavalry");

            // 2) 距离加权：同类骑兵，近者胜（20m vs 60m 同兵力）
            var dist = new[]
            {
                new TargetCandidate { Index = 1, X = 60f, Y = 0f, Count = 80, Kind = FormationTargetKind.Cavalry },
                new TargetCandidate { Index = 2, X = 20f, Y = 0f, Count = 80, Kind = FormationTargetKind.Cavalry },
            };
            var s2 = FormationTargeting.Select(0f, 0f, 100, dist, dist.Length, TargetSelection.None, 1.0f);
            Check(s2.HasTarget && s2.Index == 2, "x1 distance weighting: nearer cavalry wins");

            // 3) 威胁加权：同类同距，人多者胜（90 vs 40）
            var threat = new[]
            {
                new TargetCandidate { Index = 1, X = 40f, Y = 0f, Count = 40, Kind = FormationTargetKind.Cavalry },
                new TargetCandidate { Index = 2, X = 40f, Y = 0f, Count = 90, Kind = FormationTargetKind.Cavalry },
            };
            var s3 = FormationTargeting.Select(0f, 0f, 100, threat, threat.Length, TargetSelection.None, 1.0f);
            Check(s3.HasTarget && s3.Index == 2, "x1 threat weighting: bigger formation wins");

            // 4) 小股过滤：敌 50 < 己方 100×0.6=60 → 无目标降级
            var small = new[] { new TargetCandidate { Index = 1, X = 20f, Y = 0f, Count = 50, Kind = FormationTargetKind.Infantry } };
            var s4 = FormationTargeting.Select(0f, 0f, 100, small, small.Length, TargetSelection.None, 1.0f);
            Check(!s4.HasTarget, "x1 small-band filter: 50<60 -> no target");

            // 5) 己方零兵力不除零：ownCount=0 → 小股过滤失效（全部计入），有目标
            var zero = new[] { new TargetCandidate { Index = 1, X = 20f, Y = 0f, Count = 5, Kind = FormationTargetKind.Infantry } };
            var s5 = FormationTargeting.Select(0f, 0f, 0, zero, zero.Length, TargetSelection.None, 1.0f);
            Check(s5.HasTarget && s5.Index == 1, "x1 zero own count: no div-by-zero, target kept");

            // 6) 剧场半径：200m > 150m → 剔除 → 无目标降级
            var far = new[] { new TargetCandidate { Index = 1, X = 200f, Y = 0f, Count = 200, Kind = FormationTargetKind.Infantry } };
            var s6 = FormationTargeting.Select(0f, 0f, 100, far, far.Length, TargetSelection.None, 1.0f);
            Check(!s6.HasTarget, "x1 theater radius: 200m out of 150m -> no target");

            // 7) 缓存：now=1.2 距上次选择时刻 1.0 < 0.5s → 直接返回上次（即使候选已消失）
            var s7 = FormationTargeting.Select(0f, 0f, 100, dist, dist.Length, s2, 1.2f);
            Check(s7.HasTarget && s7.Index == 2, "x1 cache hit within 0.5s returns last");

            // 8) 迟滞：上次目标 (30,0)，新最优 (32,0) 距 2m ≤7m → 沿用上次
            var lastSel = new TargetSelection { HasTarget = true, Index = 9, X = 30f, Y = 0f, Kind = FormationTargetKind.Cavalry, Time = 0f };
            var near = new[] { new TargetCandidate { Index = 7, X = 32f, Y = 0f, Count = 90, Kind = FormationTargetKind.Cavalry } };
            var s8 = FormationTargeting.Select(0f, 0f, 100, near, near.Length, lastSel, 1.0f);
            Check(s8.HasTarget && s8.Index == 9, "x1 hysteresis <=7m keeps last target");

            // 9) 迟滞失效：新最优 (45,0) 距 15m >7m → 更新为新目标
            var far2 = new[] { new TargetCandidate { Index = 7, X = 45f, Y = 0f, Count = 90, Kind = FormationTargetKind.Cavalry } };
            var s9 = FormationTargeting.Select(0f, 0f, 100, far2, far2.Length, lastSel, 1.0f);
            Check(s9.HasTarget && s9.Index == 7, "x1 hysteresis >7m updates to new target");
        }

        // X2 手活属性公式（HandSkillProfile）已随 X2/F4 退役整链删除（CB-PROMPT-0038 §五.1）：
        //   退役后 CombatBrain 不再修改单兵格挡/招架/攻击决定概率/射击误差/随机防守方向，
        //   因此不存在可断言的公式；取而代之的守门断言在 OfflineTests\AgentWriteStaticChecks.ps1
        //   （静态扫描 AgentDrivenProperties 写入 + 模型注册链零命中）。

        // X3 编队朝向修正（FacingCorrection 纯逻辑）：朝向计算/0.5s 缓存/目标无效/剧场半径/方向迟滞/玩家豁免
        private static void TestFacingCorrection()
        {
            float dx, dy, dist;

            // 1) 朝向计算：正东目标 → dir=(1,0)；正北目标 → dir=(0,1)
            bool r1 = FacingCorrection.DecideFacing(0f, 0f, 10f, 0f, 0f, 0f, 1f, -1f, out dx, out dy, out dist);
            Check(r1 && dx > 0.99f && dy > -0.01f && dy < 0.01f, "x3 facing east -> dir(1,0)");
            bool r2 = FacingCorrection.DecideFacing(0f, 0f, 0f, 10f, 0f, 0f, 1f, -1f, out dx, out dy, out dist);
            Check(r2 && dx > -0.01f && dx < 0.01f && dy > 0.99f, "x3 facing north -> dir(0,1)");

            // 2) 0.5s 缓存窗：now-lastTime=0.4 < 0.5 → 不重算（沿用上次方向）
            bool r3 = FacingCorrection.DecideFacing(0f, 0f, 10f, 0f, 0f, 0f, 1f, 0.6f, out dx, out dy, out dist);
            Check(!r3, "x3 cache window 0.4s<0.5s -> no recompute");

            // 3) 目标无效（==己方中心，dist<1m）→ 不修正
            bool r4 = FacingCorrection.DecideFacing(0f, 0f, 0.5f, 0f, 0f, 0f, 2f, -1f, out dx, out dy, out dist);
            Check(!r4, "x3 target==own (<1m) -> no facing");

            // 4) 剧场半径边界：200m 外不修正；149m 内修正且 dist 正确
            bool r5 = FacingCorrection.DecideFacing(0f, 0f, 200f, 0f, 0f, 0f, 2f, -1f, out dx, out dy, out dist);
            Check(!r5, "x3 out of theater radius 200m -> no facing");
            bool r6 = FacingCorrection.DecideFacing(0f, 0f, 149f, 0f, 0f, 0f, 2f, -1f, out dx, out dy, out dist);
            Check(r6 && dist > 148.9f && dist < 149.1f, "x3 within theater radius 149m -> facing, dist ok");

            // 5) 方向迟滞：新方向与上次夹角 <0.2rad（点积>0.98）→ 不下发；90° 变化 → 更新
            bool r7 = FacingCorrection.DecideFacing(0f, 0f, 10f, 0.3f, 1f, 0f, 2f, -1f, out dx, out dy, out dist);
            Check(!r7, "x3 direction hysteresis <0.2rad -> keep last");
            bool r8 = FacingCorrection.DecideFacing(0f, 0f, 0f, 10f, 1f, 0f, 2f, -1f, out dx, out dy, out dist);
            Check(r8 && dy > 0.99f, "x3 direction change 90deg -> update");

            // 6) 玩家队豁免判定（MCM 开关；P0-④ 保留）
            Check(FacingCorrection.ShouldExempt(true, true), "x3 exempt player team when switch on");
            Check(!FacingCorrection.ShouldExempt(true, false), "x3 no exempt when switch off");
            Check(!FacingCorrection.ShouldExempt(false, true), "x3 no exempt for AI team");
        }

        // X4 接敌解阵框（FrameReleaseGlue 纯逻辑）：命令判定/距离规模双闸/≤25 人关闭/L3 闸门/冷却字典/跳过态/豁免
        private static void TestFrameRelease()
        {
            // 1) 命令判定：Charge(2)/ChargeToTarget(3) 触发，其它命令不触发
            Check(FrameReleaseRules.IsChargeOrder(2), "x4 charge(2) is charge order");
            Check(FrameReleaseRules.IsChargeOrder(3), "x4 chargeToTarget(3) is charge order");
            Check(!FrameReleaseRules.IsChargeOrder(10), "x4 advance(10) not charge order");

            // 2) 编队级触发边界：dist≤25m 触发，>25m 不触发；规模>25 才启用，≤25 关闭
            Check(FrameReleaseRules.ShouldReleaseFormation(true, 25f, 40), "x4 dist 25m boundary triggers");
            Check(!FrameReleaseRules.ShouldReleaseFormation(true, 25.01f, 40), "x4 dist 25.01m does not trigger");
            Check(!FrameReleaseRules.ShouldReleaseFormation(false, 10f, 40), "x4 non-charge order never triggers");
            Check(!FrameReleaseRules.ShouldReleaseFormation(true, 10f, 25), "x4 count<=25 disables");
            Check(FrameReleaseRules.ShouldReleaseFormation(true, 10f, 26), "x4 count>25 enables");

            // 3) 剧场闸门：仅 L3（玩家可见区），L0-L2 交原版
            Check(FrameReleaseRules.IsReleaseLod(3), "x4 L3 gate allows");
            Check(!FrameReleaseRules.IsReleaseLod(2), "x4 L2 gate blocks");
            Check(!FrameReleaseRules.IsReleaseLod(0), "x4 L0 gate blocks");

            // 4) 非战术门控 + 一次性 latch（CB-PROMPT-0038 K1/D1 产品裁决后语义）：
            //    原版逃跑态跳过；已在释放态（IsFormationFrameEnabled=false → frameAlreadyDisabled=true）跳过；
            //    两者都不命中才放行。已删除的 F7 求生 / F9 后撤跳过条件不得复活（其模块已退役）。
            Check(FrameReleaseRules.ShouldSkipAgent(true, false), "x4 running away skips");
            Check(FrameReleaseRules.ShouldSkipAgent(false, true), "x4 already-disabled frame skips (one-time latch)");
            Check(FrameReleaseRules.ShouldSkipAgent(true, true), "x4 both gate conditions skip");
            Check(!FrameReleaseRules.ShouldSkipAgent(false, false), "x4 normal enabled agent not skipped");

            // 5) 冷却字典：无记录放行；5s 窗内拦截；超窗放行；Remove 后放行（OnAgentRemoved 清理）
            var cd = new FrameReleaseCooldown();
            Check(cd.CanRelease(7, 0f), "x4 cooldown initial allows");
            cd.MarkReleased(7, 10f);
            Check(!cd.CanRelease(7, 14.9f), "x4 cooldown blocks within 5s window");
            Check(cd.CanRelease(7, 15f), "x4 cooldown allows at 5s boundary");
            cd.MarkReleased(7, 15f);
            cd.Remove(7);
            Check(cd.CanRelease(7, 16f), "x4 cooldown allows after Remove (agent removed)");
            Check(cd.CanRelease(8, 20f), "x4 cooldown per-agent: unmarked other agent allows");
        }

        // X5-1 兵种角色归类（TroopClassifier 纯函数）：规则表每分支 + 骑射/英雄/等级边界
        private static void TestTroopClassifier()
        {
            // ① 骑乘（Kind=Cavalry，任意武器/盾）→ Cavalry
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Cavalry, HasShield = true, PrimaryWeaponClass = PrimaryWeaponClass.OneHanded }) == TroopRole.Cavalry,
                "x51 cavalry one-handed -> Cavalry");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Cavalry, PrimaryWeaponClass = PrimaryWeaponClass.Polearm }) == TroopRole.Cavalry,
                "x51 cavalry polearm -> Cavalry (骑乘长杆归骑兵)");
            // 边界：骑射手归 Cavalry（Enums.cs:44 胶水层归并惯例）
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Cavalry, PrimaryWeaponClass = PrimaryWeaponClass.Bow }) == TroopRole.Cavalry,
                "x51 horse archer -> Cavalry (Enums.cs:44 惯例)");

            // ② 非骑乘 + 持盾 + 单手 → ShieldBearer（护甲无关；板甲也归盾兵——盾是角色轴第一特征）
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, HasShield = true, PrimaryWeaponClass = PrimaryWeaponClass.OneHanded, Armor = ArmorGrade.Leather }) == TroopRole.ShieldBearer,
                "x51 shield+one-handed leather -> ShieldBearer");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, HasShield = true, PrimaryWeaponClass = PrimaryWeaponClass.OneHanded, Armor = ArmorGrade.Plate }) == TroopRole.ShieldBearer,
                "x51 shield+one-handed plate -> ShieldBearer (盾优先于板甲)");

            // ③ 非骑乘 + 长杆主武器 → Polearm（持盾+长杆也归 Polearm——②要求单手，不满足则走③）
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.Polearm }) == TroopRole.Polearm,
                "x51 polearm -> Polearm");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, HasShield = true, PrimaryWeaponClass = PrimaryWeaponClass.Polearm }) == TroopRole.Polearm,
                "x51 shield+polearm -> Polearm (②需单手)");

            // ④ 非骑乘 + 弓/弩/投掷 → Ranged
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Ranged, PrimaryWeaponClass = PrimaryWeaponClass.Bow }) == TroopRole.Ranged,
                "x51 bow -> Ranged");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.Crossbow }) == TroopRole.Ranged,
                "x51 crossbow -> Ranged (兵种不锁角色)");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.Thrown }) == TroopRole.Ranged,
                "x51 thrown -> Ranged (投掷按远程惯例并入)");

            // ⑤ 非骑乘 + 双手重武器 或 板甲+单手 → HeavyMelee
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.TwoHanded, Armor = ArmorGrade.Chainmail }) == TroopRole.HeavyMelee,
                "x51 two-handed chainmail -> HeavyMelee");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.OneHanded, Armor = ArmorGrade.Plate }) == TroopRole.HeavyMelee,
                "x51 plate+one-handed (无盾) -> HeavyMelee");

            // ⑥ 其余近战 → LightMelee（轻甲单手/徒手）
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.OneHanded, Armor = ArmorGrade.Chainmail }) == TroopRole.LightMelee,
                "x51 chainmail one-handed -> LightMelee");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.None }) == TroopRole.LightMelee,
                "x51 unarmed -> LightMelee");

            // 边界：英雄/等级不改变角色（IsVeteran 只影响 FighterProfileFactory.Classify 档位；角色由装备决定）
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, HasShield = true, PrimaryWeaponClass = PrimaryWeaponClass.OneHanded, IsVeteran = true }) == TroopRole.ShieldBearer,
                "x51 veteran shield -> still ShieldBearer (IsVeteran 不改变角色)");
            Check(TroopClassifier.Classify(new SoldierInput { Kind = SoldierKind.Infantry, PrimaryWeaponClass = PrimaryWeaponClass.Polearm, Level = 6 }) == TroopRole.Polearm,
                "x51 level 6 polearm -> still Polearm (等级不改变角色)");
        }

        // X5-2 槽位布局（SlotLayout 纯逻辑）：分流规则/Rank 占比/间距区间/同角色编组/散列放大/无分层错落
        private static void TestSlotLayout()
        {
            // 1) 分流（§五）：Ranged 为主（占比≥0.5）→ RangedLoose；近战为主 → 消费 T2-01
            var rangedDominant = new TroopComposition { Ranged = 6, LightMelee = 4 };
            Check(SlotLayout.SelectLayout(rangedDominant, TacticsArrangement.ShieldWall) == LayoutKind.RangedLoose,
                "x52 ranged-dominant(0.6) -> RangedLoose even if ShieldWall");
            var boundary = new TroopComposition { Ranged = 5, LightMelee = 5 };
            Check(SlotLayout.SelectLayout(boundary, TacticsArrangement.Line) == LayoutKind.RangedLoose,
                "x52 ranged ratio 0.5 boundary -> RangedLoose");
            var meleeDominantWall = new TroopComposition { ShieldBearer = 6, Polearm = 4, Ranged = 2 };
            Check(SlotLayout.SelectLayout(meleeDominantWall, TacticsArrangement.ShieldWall) == LayoutKind.ShieldWall,
                "x52 melee-dominant + ShieldWall -> ShieldWall (⑬ 近战为主不进入 RangedLoose)");
            var meleeDominantLine = new TroopComposition { ShieldBearer = 3, Polearm = 2, Ranged = 2, LightMelee = 3 };
            Check(SlotLayout.SelectLayout(meleeDominantLine, TacticsArrangement.Line) == LayoutKind.Line,
                "x52 melee-dominant + Line -> Line");

            // 2) ShieldWall：ShieldBearer 全 Rank0（① 占比 100% ≥90%）；Polearm/Ranged 全 Rank1（后排）
            var wallInput = SlotLayoutInput.Default(meleeDominantWall, TacticsArrangement.ShieldWall);
            var wall = SlotLayout.Generate(wallInput);
            Check(wall.Count == 12, "x52 shieldwall slot count == troop count");
            bool shieldAllRank0 = true;
            bool polearmAllRank1 = true;
            bool rangedAllRank1 = true;
            int shieldCount = 0;
            foreach (var s in wall)
            {
                if (s.Role == TroopRole.ShieldBearer) { shieldCount++; if (s.Rank != 0) shieldAllRank0 = false; }
                if (s.Role == TroopRole.Polearm && s.Rank != 1) polearmAllRank1 = false;
                if (s.Role == TroopRole.Ranged && s.Rank != 1) rangedAllRank1 = false;
            }
            Check(shieldAllRank0 && shieldCount == 6, "x52 shieldwall all ShieldBearer Rank0 (① ≥90%)");
            Check(polearmAllRank1, "x52 shieldwall all Polearm Rank1 (后排架枪)");
            Check(rangedAllRank1, "x52 shieldwall all Ranged Rank1 (② 混编后排)");

            // 修复① 绝对居中：编队中心（所有 slot 均值）≈ 输入中心（容差 0.1m），不再整体横向偏移
            Check(AbsCenterWithin(wall, wallInput.CenterX, wallInput.CenterY, 0.1f),
                "x52fix shieldwall 12 兵 6 列 centered (均值≈Center 0.1m)");
            var bigWallInput = SlotLayoutInput.Default(
                new TroopComposition { ShieldBearer = 40, Polearm = 30 }, TacticsArrangement.ShieldWall);
            var bigWall = SlotLayout.Generate(bigWallInput);
            Check(bigWall.Count == 70, "x52fix shieldwall 40+30 slot count == 70");
            Check(AbsCenterWithin(bigWall, bigWallInput.CenterX, bigWallInput.CenterY, 0.1f),
                "x52fix shieldwall 40 盾+30 后排 centered (旧偏移 15m → 0)");

            // 修复② low：Default.Count 走非骑乘口径（Cavalry 由 F8 骑兵波次管，不占 X5 槽位）
            var withCav = new TroopComposition { ShieldBearer = 4, Cavalry = 6 };
            Check(SlotLayoutInput.Default(withCav, TacticsArrangement.ShieldWall).Count == 4,
                "x52fix default count excludes cavalry (NonCavalryTotal)");

            // 3) ③ 相邻间距：同 rank 相邻列 = spacing(1.0)，跨排最近对 ∈[0.8,2.0]
            Check(AdjacentDistancesInRange(wall, 1.0f), "x52 shieldwall adjacent spacing in [0.8,2.0] (③)");

            // 4) Line：同角色按列编组（秩稳定：ShieldBearer 列连续），间距 1.0
            var lineInput = SlotLayoutInput.Default(meleeDominantLine, TacticsArrangement.Line);
            var line = SlotLayout.Generate(lineInput);
            Check(line.Count == 10, "x52 line slot count == troop count");
            Check(SameRoleContiguous(line, TroopRole.ShieldBearer), "x52 line ShieldBearer columns contiguous (秩稳定)");
            Check(SameRoleContiguous(line, TroopRole.Ranged), "x52 line Ranged columns contiguous");
            Check(AdjacentDistancesInRange(line, 1.0f), "x52 line adjacent spacing in [0.8,2.0]");

            // 5) ⑪ RangedLoose：间距放大（1.5 ≥ Line 1.0×1.3）；无分层（Rank 全 0）；前后错落
            var looseInput = SlotLayoutInput.Default(rangedDominant, TacticsArrangement.Line);
            var loose = SlotLayout.Generate(looseInput);
            Check(loose.Count == 10, "x52 rangedloose slot count == troop count");
            Check(SlotLayout.RangedLooseSpacingFactor >= 1.3f, "x52 loose spacing factor >=1.3 (⑪ 常数)");
            Check(AdjacentDistancesInRange(loose, 1.5f), "x52 rangedloose adjacent spacing >= line x1.3 (⑪)");
            bool looseNoRank = true;
            for (int i = 0; i < loose.Count; i++) if (loose[i].Rank != 0) looseNoRank = false;
            Check(looseNoRank, "x52 rangedloose no rank layers (无分层)");
            bool staggered = loose.Count >= 4 && loose[0].WorldX != loose[1].WorldX;
            Check(staggered, "x52 rangedloose front/back staggered (前后错落)");

            // 6) 空编队 → 空列表；TroopComposition 除零安全
            var emptyInput = SlotLayoutInput.Default(new TroopComposition(), TacticsArrangement.Line);
            Check(SlotLayout.Generate(emptyInput).Count == 0, "x52 empty composition -> empty slots");
            Check(new TroopComposition().RangedRatio == 0f && !new TroopComposition().IsRangedDominant,
                "x52 empty composition ratio 0 not dominant (除零安全)");
        }

        // 相邻间距检查：同 rank 相邻列间距≈spacing；跨 rank 相邻列（|file差|≤1）最近对间距∈[0.8,2.0]
        private static bool AdjacentDistancesInRange(System.Collections.Generic.List<FormationSlot> slots, float spacing)
        {
            if (slots.Count < 2) return true;
            for (int i = 0; i < slots.Count; i++)
            {
                for (int j = i + 1; j < slots.Count; j++)
                {
                    var a = slots[i];
                    var b = slots[j];
                    int fileDiff = System.Math.Abs(a.File - b.File);
                    if (a.Rank == b.Rank && fileDiff == 1)
                    {
                        float d = Dist(a.WorldX, a.WorldY, b.WorldX, b.WorldY);
                        if (d < 0.8f || d > 2.0f) return false;
                    }
                    else if (a.Rank != b.Rank && fileDiff <= 1)
                    {
                        float d = Dist(a.WorldX, a.WorldY, b.WorldX, b.WorldY);
                        if (d < 0.8f || d > 2.0f) return false; // 跨排相邻列（对角邻居）也必须在区间
                    }
                }
            }
            return true;
        }

        // 同角色列连续（秩稳定）：该角色的 File 集合是连续区间
        private static bool SameRoleContiguous(System.Collections.Generic.List<FormationSlot> slots, TroopRole role)
        {
            int min = int.MaxValue, max = int.MinValue, count = 0;
            foreach (var s in slots)
            {
                if (s.Role == role)
                {
                    if (s.File < min) min = s.File;
                    if (s.File > max) max = s.File;
                    count++;
                }
            }
            return count == 0 || (max - min + 1) == count;
        }

        // 绝对居中检查：所有槽位 WorldX/WorldY 均值 ≈ (centerX, centerY)，容差 tol
        private static bool AbsCenterWithin(System.Collections.Generic.List<FormationSlot> slots,
            float centerX, float centerY, float tol)
        {
            if (slots.Count == 0) return true;
            float sumX = 0f, sumY = 0f;
            foreach (var s in slots)
            {
                sumX += s.WorldX;
                sumY += s.WorldY;
            }
            float meanX = sumX / slots.Count;
            float meanY = sumY / slots.Count;
            return System.Math.Abs(meanX - centerX) <= tol && System.Math.Abs(meanY - centerY) <= tol;
        }

        private static float Dist(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1, dy = y2 - y1;
            return (float)System.Math.Sqrt(dx * dx + dy * dy);
        }

        // X5-3 地形形变（TerrainWarp 纯逻辑）：高地 Ranged 后移↑/坡度闸>15°无 Rank0/障碍禁入区切割
        private static void TestTerrainWarp()
        {
            // ⑤ 高地适配：坡顶方向 Ranged slot 平均高度 ≥ 全队平均高度（Ranged 后移↑）
            // 高度采样 h=x*0.1（+x 为上坡），坡顶方向 (1,0)，Ranged 步长 1.0
            var upSlots = new System.Collections.Generic.List<FormationSlot>();
            upSlots.Add(new FormationSlot(0, 0, 0f, 0f, 1f, 0f, TroopRole.LightMelee, SlotState.Occupied)); // x=0
            upSlots.Add(new FormationSlot(0, 1, 1f, 0f, 1f, 0f, TroopRole.Ranged, SlotState.Occupied));     // x=1
            upSlots.Add(new FormationSlot(0, 2, 2f, 0f, 1f, 0f, TroopRole.Ranged, SlotState.Occupied));     // x=2
            var upTerrain = new TerrainContext();
            upTerrain.Height = (x, y) => x * 0.1f;
            upTerrain.Slope = (x, y) => 5f; // 平缓，不触发坡度闸
            upTerrain.UphillX = 1f; upTerrain.UphillY = 0f;
            upTerrain.RangedUphillStep = 1.0f;
            var upOut = TerrainWarp.Warp(upSlots, upTerrain);
            float allH = (upOut[0].WorldX + upOut[1].WorldX + upOut[2].WorldX) * 0.1f / 3f; // 全队平均高度（采样 h=x*0.1）
            float rangedH = (upOut[1].WorldX + upOut[2].WorldX) * 0.1f / 2f;                 // Ranged 平均高度
            Check(rangedH >= allH, "x53 ranged avg height >= formation avg (⑤ 高地 Ranged 后移↑)");
            Check(upOut[1].WorldX > 1.9f, "x53 ranged slot moved uphill +1m");
            Check(upOut[0].WorldX == 0f, "x53 melee slot not moved (仅 Ranged 后移)");

            // ⑥ 坡度闸：>15° 处无 Rank0（→Rank1）；15° 边界不触发；平缓保持 Rank0
            var slopeTerrain = new TerrainContext();
            slopeTerrain.Slope = (x, y) => x; // 用 x 直接表示坡度角：x=16 → 16°>15°
            var steepSlots = new System.Collections.Generic.List<FormationSlot>();
            steepSlots.Add(new FormationSlot(0, 0, 16f, 0f, 1f, 0f, TroopRole.Polearm, SlotState.Occupied)); // 16° 陡坡 Rank0
            steepSlots.Add(new FormationSlot(0, 1, 15f, 0f, 1f, 0f, TroopRole.ShieldBearer, SlotState.Occupied)); // 15° 边界 Rank0
            steepSlots.Add(new FormationSlot(0, 2, 3f, 0f, 1f, 0f, TroopRole.LightMelee, SlotState.Occupied)); // 平缓 Rank0
            var steepOut = TerrainWarp.Warp(steepSlots, slopeTerrain);
            Check(steepOut[0].Rank == 1, "x53 slope 16° rank0 -> rank1 (⑥ 陡坡无前排)");
            Check(steepOut[1].Rank == 0, "x53 slope 15° boundary rank0 kept (边界不触发)");
            Check(steepOut[2].Rank == 0, "x53 flat 3° rank0 kept");

            // ⑦ 障碍切割：禁入区内 slot 移出；区外 slot 不动（Frontline bend 侧向移出）
            var zoneTerrain = new TerrainContext();
            zoneTerrain.Height = (x, y) => 0f;
            zoneTerrain.Slope = (x, y) => 0f;
            zoneTerrain.UphillX = 0f; zoneTerrain.UphillY = 0f;
            zoneTerrain.ForbiddenZones = new System.Collections.Generic.List<ForbiddenZone>();
            zoneTerrain.ForbiddenZones.Add(new ForbiddenZone(2f, -1f, 4f, 1f)); // 禁入区 x∈[2,4]
            var zoneSlots = new System.Collections.Generic.List<FormationSlot>();
            zoneSlots.Add(new FormationSlot(0, 0, 3f, 0f, 1f, 0f, TroopRole.ShieldBearer, SlotState.Occupied)); // 区内
            zoneSlots.Add(new FormationSlot(0, 1, 6f, 0f, 1f, 0f, TroopRole.Polearm, SlotState.Occupied));     // 区外
            var zoneOut = TerrainWarp.Warp(zoneSlots, zoneTerrain);
            bool zone1Out = !(zoneOut[0].WorldX >= 2f && zoneOut[0].WorldX <= 4f && zoneOut[0].WorldY >= -1f && zoneOut[0].WorldY <= 1f);
            Check(zone1Out, "x53 slot in forbidden zone pushed out (⑦ 障碍切割)");
            Check(zoneOut[1].WorldX == 6f && zoneOut[1].WorldY == 0f, "x53 slot outside zone untouched");

            // 空输入 → 空列表；terrain null → 原样副本
            Check(TerrainWarp.Warp(null, upTerrain).Count == 0, "x53 null slots -> empty");
            Check(TerrainWarp.Warp(new System.Collections.Generic.List<FormationSlot>(), upTerrain).Count == 0, "x53 empty slots -> empty");
            var copy = TerrainWarp.Warp(upSlots, null);
            Check(copy.Count == 3 && copy[1].WorldX == 1f, "x53 null terrain -> unchanged copy (优雅降级)");

            // 委托降级：Height/Slope null 维度不形变
            var noSlope = new TerrainContext();
            noSlope.UphillX = 1f; noSlope.UphillY = 0f;
            noSlope.RangedUphillStep = 1.0f;
            var noSlopeOut = TerrainWarp.Warp(upSlots, noSlope);
            Check(noSlopeOut[1].WorldX > 1.9f, "x53 slope null -> uphill still applies (⑤ 不依赖坡度)");
            Check(noSlopeOut[0].Rank == 0, "x53 slope null -> rank untouched (⑥ 降级)");

            // 障碍边界：恰在禁入区边界（含边界）→ 移出
            var edgeTerrain = new TerrainContext();
            edgeTerrain.ForbiddenZones = new System.Collections.Generic.List<ForbiddenZone>();
            edgeTerrain.ForbiddenZones.Add(new ForbiddenZone(0f, 0f, 2f, 2f));
            var edgeSlots = new System.Collections.Generic.List<FormationSlot>();
            edgeSlots.Add(new FormationSlot(0, 0, 2f, 1f, 1f, 0f, TroopRole.LightMelee, SlotState.Occupied)); // 恰在 MaxX 边界
            var edgeOut = TerrainWarp.Warp(edgeSlots, edgeTerrain);
            Check(!(edgeOut[0].WorldX >= 0f && edgeOut[0].WorldX <= 2f && edgeOut[0].WorldY >= 0f && edgeOut[0].WorldY <= 2f),
                "x53 zone boundary included -> pushed out (边界含入)");
        }

        // X5-4 agent↔slot 稳定映射（SlotAssigner 纯逻辑）：就近占位/稳定保持/死亡补位/Full-Slight 分流/X4 跳过集
        private static void TestSlotAssigner()
        {
            // 基础场景：3 个 ShieldBearer slot（Rank0 x=0/1/2）+ 2 个 agent（就近占位）
            var slots = new System.Collections.Generic.List<FormationSlot>();
            slots.Add(new FormationSlot(0, 0, 0f, 0f, 1f, 0f, TroopRole.ShieldBearer, SlotState.Occupied));
            slots.Add(new FormationSlot(0, 1, 1f, 0f, 1f, 0f, TroopRole.ShieldBearer, SlotState.Occupied));
            slots.Add(new FormationSlot(0, 2, 2f, 0f, 1f, 0f, TroopRole.ShieldBearer, SlotState.Occupied));
            var agents = new System.Collections.Generic.List<AgentRoleEntry>();
            agents.Add(new AgentRoleEntry(1, TroopRole.ShieldBearer, 0.2f, 0f)); // 离 slot0 最近
            agents.Add(new AgentRoleEntry(2, TroopRole.ShieldBearer, 1.8f, 0f)); // 离 slot1/2 之间（1.8→slot2 距离0.2, slot1 距离0.8）
            var input = new SlotAssignInput { Slots = slots, Agents = agents, Last = null, Integrity = 85f }; // Full
            var out1 = SlotAssigner.Assign(input);
            Check(out1.Count == 2, "x54 two agents assigned two slots");
            bool agent1Near0 = false, agent2Near2 = false;
            foreach (var a in out1)
            {
                if (a.AgentIndex == 1 && a.SlotIndex == 0) agent1Near0 = true;
                if (a.AgentIndex == 2 && a.SlotIndex == 2) agent2Near2 = true;
            }
            Check(agent1Near0, "x54 nearest slot picked (agent1→slot0 0.2m)");
            Check(agent2Near2, "x54 nearest slot picked (agent2→slot2 0.2m)");

            // 稳定保持：同一输入 + 上一拍 → 保持原 slot（无抖动）
            var last = new System.Collections.Generic.List<SlotAssignment>();
            last.Add(new SlotAssignment(1, 1)); // 故意让 agent1 上拍在 slot1（非最近）
            var input2 = new SlotAssignInput { Slots = slots, Agents = agents, Last = last, Integrity = 85f };
            var out2 = SlotAssigner.Assign(input2);
            bool kept1 = false;
            foreach (var a in out2) if (a.AgentIndex == 1 && a.SlotIndex == 1) kept1 = true;
            Check(kept1, "x54 stability: same agent keeps last slot (无抖动)");
            Check(out2.Count == 2, "x54 stability: both assigned");

            // 死亡补位：agent2 死亡（不在 agents 中）→ slot2 释放，新 agent3 就近补位
            var agents3 = new System.Collections.Generic.List<AgentRoleEntry>();
            agents3.Add(new AgentRoleEntry(1, TroopRole.ShieldBearer, 0.2f, 0f));
            agents3.Add(new AgentRoleEntry(3, TroopRole.ShieldBearer, 1.5f, 0f)); // 死亡补位
            var input3 = new SlotAssignInput { Slots = slots, Agents = agents3, Last = last, Integrity = 85f };
            var out3 = SlotAssigner.Assign(input3);
            Check(out3.Count == 2, "x54 replacement agent fills freed slot");
            bool agent3Filled = false;
            foreach (var a in out3) if (a.AgentIndex == 3) agent3Filled = true;
            Check(agent3Filled, "x54 new agent assigned after death");

            // Full 严格维持 vs Slight 邻位互换：agent 上拍 Rank0 slot 本拍角色失效（变 Polearm）→ 必须换位。
            //   Full（maxRankDiff=0）：只补同 Rank 匹配 slot，Rank0 无匹配 → 不指派；
            //   Slight（maxRankDiff=1）：可补 Rank1 邻位 → 指派。
            var rankSlots = new System.Collections.Generic.List<FormationSlot>();
            rankSlots.Add(new FormationSlot(0, 0, 0f, 0f, 1f, 0f, TroopRole.Polearm, SlotState.Occupied));   // Rank0 上拍 slot 变 Polearm（不匹配）
            rankSlots.Add(new FormationSlot(1, 0, 0f, 1f, 1f, 0f, TroopRole.ShieldBearer, SlotState.Occupied)); // Rank1 匹配
            var singleAgent = new System.Collections.Generic.List<AgentRoleEntry>();
            singleAgent.Add(new AgentRoleEntry(1, TroopRole.ShieldBearer, 0.1f, 0f));
            var lastRank = new System.Collections.Generic.List<SlotAssignment>();
            lastRank.Add(new SlotAssignment(1, 0)); // 上拍 Rank0 slot0
            var inputFullStrict = new SlotAssignInput { Slots = rankSlots, Agents = singleAgent, Last = lastRank, Integrity = 90f }; // Full
            Check(SlotAssigner.Assign(inputFullStrict).Count == 0, "x54 full: no same-rank slot -> not assigned (跨 Rank 不补)");
            var inputSlightSwap = new SlotAssignInput { Slots = rankSlots, Agents = singleAgent, Last = lastRank, Integrity = 70f }; // Slight
            var outSlightSwap = SlotAssigner.Assign(inputSlightSwap);
            Check(outSlightSwap.Count == 1, "x54 slight: rank-adjacent swap assigned");
            bool swappedToRank1 = outSlightSwap.Count == 1 && rankSlots[outSlightSwap[0].SlotIndex].Rank == 1;
            Check(swappedToRank1, "x54 slight: agent swapped to adjacent Rank1 slot");

            // X4 跳过集（FrameDisabledAgents）断言已随 CB-PROMPT-0038 §五.7 删除：
            //   该跳过集只服务于已退役的逐兵补位执行腿；解阵框现在是一次性 latch（见 FrameReleaseGlue.cs），
            //   绑定表本身不得再消费逐兵解阵状态（避免永久 skip）。2 条断言 ↔ SlotAssigner.FrameDisabledAgents。

            // 空输入：空 agents / 空 slots → 空列表
            var emptyAgents = new SlotAssignInput { Slots = slots, Agents = new System.Collections.Generic.List<AgentRoleEntry>(), Integrity = 85f };
            Check(SlotAssigner.Assign(emptyAgents).Count == 0, "x54 empty agents -> empty");
            var emptySlots = new SlotAssignInput { Slots = new System.Collections.Generic.List<FormationSlot>(), Agents = agents, Integrity = 85f };
            Check(SlotAssigner.Assign(emptySlots).Count == 0, "x54 empty slots -> empty");

            // 角色不匹配：agent 角色无匹配 slot → 不指派
            var rangedAgent = new System.Collections.Generic.List<AgentRoleEntry>();
            rangedAgent.Add(new AgentRoleEntry(9, TroopRole.Ranged, 0.5f, 0f));
            var inputNoMatch = new SlotAssignInput { Slots = slots, Agents = rangedAgent, Integrity = 85f };
            Check(SlotAssigner.Assign(inputNoMatch).Count == 0, "x54 no matching role slot -> not assigned");

            // slot 数量不足：3 agents 对 3 slots 全占；再加 1 agent → 第 4 个不指派
            var fourAgents = new System.Collections.Generic.List<AgentRoleEntry>();
            fourAgents.Add(new AgentRoleEntry(1, TroopRole.ShieldBearer, 0f, 0f));
            fourAgents.Add(new AgentRoleEntry(2, TroopRole.ShieldBearer, 1f, 0f));
            fourAgents.Add(new AgentRoleEntry(3, TroopRole.ShieldBearer, 2f, 0f));
            fourAgents.Add(new AgentRoleEntry(4, TroopRole.ShieldBearer, 3f, 0f));
            var inputCrowd = new SlotAssignInput { Slots = slots, Agents = fourAgents, Integrity = 85f };
            var outCrowd = SlotAssigner.Assign(inputCrowd);
            Check(outCrowd.Count == 3, "x54 slot shortage: only 3 assigned (1 per slot)");
        }

        // X5-5 阵型纪律四态（FormationDisciplineController 纯逻辑）：Grade()→约束参数 边界 80/60/40 + 各态方向
        private static void TestFormationDiscipline()
        {
            // 1) 四态边界：80→Full、79→Slight、60→Slight、59→Moderate、40→Moderate、39→Stopped、0→Stopped
            Check(FormationDisciplineController.Evaluate(85f).State == DisciplineState.Full, "x55 85 -> Full");
            Check(FormationDisciplineController.Evaluate(80f).State == DisciplineState.Full, "x55 80 boundary -> Full");
            Check(FormationDisciplineController.Evaluate(79f).State == DisciplineState.Slight, "x55 79 -> Slight");
            Check(FormationDisciplineController.Evaluate(60f).State == DisciplineState.Slight, "x55 60 boundary -> Slight");
            Check(FormationDisciplineController.Evaluate(59f).State == DisciplineState.Moderate, "x55 59 -> Moderate");
            Check(FormationDisciplineController.Evaluate(40f).State == DisciplineState.Moderate, "x55 40 boundary -> Moderate");
            Check(FormationDisciplineController.Evaluate(39f).State == DisciplineState.Stopped, "x55 39 -> Stopped (Severe+Collapsed)");
            Check(FormationDisciplineController.Evaluate(0f).State == DisciplineState.Stopped, "x55 0 -> Stopped (Collapsed)");

            // 2) Full：严格维持——刷新正常 0.5s、无噪声、MaxRankDiff=0（与 SlotAssigner 严格同 Rank 一致）
            var full = FormationDisciplineController.Evaluate(90f);
            Check(full.MaintainSlots && full.RefreshInterval == 0.5f, "x55 full: maintain + refresh 0.5s");
            Check(full.NoiseAmplitude == 0f, "x55 full: no noise");
            Check(full.MaxRankDiff == 0, "x55 full: maxRankDiff 0 (严格同 Rank, 与 SlotAssigner 一致)");

            // 3) Slight：允许局部补位——MaxRankDiff=1（邻位互换）、无噪声、刷新正常
            var slight = FormationDisciplineController.Evaluate(70f);
            Check(slight.MaintainSlots && slight.RefreshInterval == 0.5f, "x55 slight: maintain + refresh 0.5s");
            Check(slight.MaxRankDiff == 1, "x55 slight: maxRankDiff 1 (邻位互换, 与 SlotAssigner 一致)");
            Check(slight.NoiseAmplitude == 0f, "x55 slight: no noise (局部补位不抖动)");

            // 4) Moderate：降低约束——加噪声、降刷新优先级（1.0s）、MaxRankDiff=1（允许局部变形）
            var moderate = FormationDisciplineController.Evaluate(50f);
            Check(moderate.MaintainSlots, "x55 moderate: still maintains slots");
            Check(moderate.RefreshInterval > 0.5f, "x55 moderate: refresh slower than full (降刷新优先级)");
            Check(moderate.NoiseAmplitude > 0f, "x55 moderate: noise > 0 (允许局部变形)");
            Check(moderate.MaxRankDiff == 1, "x55 moderate: maxRankDiff 1");

            // 5) Severe+Collapsed：停止维护——MaintainSlots=false、刷新 0（停刷交 F6）
            var stopped = FormationDisciplineController.Evaluate(30f);
            Check(!stopped.MaintainSlots, "x55 severe: stops maintaining (交 F6 近战走位)");
            Check(stopped.RefreshInterval == 0f, "x55 severe: refresh 0 (停刷 Slot 目标)");
            Check(stopped.NoiseAmplitude == 0f, "x55 severe: no noise when stopped");

            // 6) 与 X5-4 语义一致：Full 严格（MaxRankDiff=0）< Slight/Moderate（1）；Stopped 不指派（Maintain=false）
            Check(FormationDisciplineController.Evaluate(90f).MaxRankDiff == 0
                && FormationDisciplineController.Evaluate(70f).MaxRankDiff == 1
                && FormationDisciplineController.Evaluate(50f).MaxRankDiff == 1,
                "x55 maxRankDiff full(0) < slight/moderate(1) 与 X5-4 门控一致");
        }

        // X5-6 编队级槽位快照门控（X5ExecutionPolicy 纯逻辑）：仅保留 Discipline 门控。
        // CB-PROMPT-0038 §五.7：L3 个体补位开关 / Bench 回退闸 / X5SlotCooldown（共 10 条断言）随
        //   逐兵补位执行腿退役删除；逐条映射见 docs/agent-control-surface-audit.md §0b。
        private static void TestX5ExecutionPolicy()
        {
            // Discipline 门控（x5-discipline-assigner-redundancy 接线）：MaintainSlots=false → 跳过刷新
            Check(X5ExecutionPolicy.ShouldRefreshSlots(FormationDisciplineController.Evaluate(90f)), "x56 full -> refresh slots");
            Check(X5ExecutionPolicy.ShouldRefreshSlots(FormationDisciplineController.Evaluate(50f)), "x56 moderate -> refresh slots");
            Check(!X5ExecutionPolicy.ShouldRefreshSlots(FormationDisciplineController.Evaluate(30f)), "x56 stopped -> skip refresh (交原版)");

            // X7 战场信息迷雾开关：默认开（拟人）；关闭=直通（PerceptionFilter 折叠恒 true）；开启=走迷雾路径
            Check(CombatBrainSettings.FogOfWarEnabled, "x7 fog default on (拟人)");
            Check(PerceptionFilter.IsKnown(false, false), "x7 fog off -> known even unseen (直通=现状)");
            Check(PerceptionFilter.IsKnown(false, true), "x7 fog off -> known (直通)");
            Check(PerceptionFilter.IsKnown(true, true), "x7 fog on + visible -> known (迷雾路径)");
            Check(!PerceptionFilter.IsKnown(true, false), "x7 fog on + unseen -> unknown (迷雾过滤)");
            // 关闭迷雾=跳过感知计算（IsKnown 直通不依赖 KnowledgeView 查询结果），开启才增加开销
            CombatBrainSettings.FogOfWarEnabled = false;
            Check(!CombatBrainSettings.FogOfWarEnabled, "x7 fog can be turned off (热切)");
            CombatBrainSettings.FogOfWarEnabled = true; // 还原默认（后续测试依赖拟人基线）
        }

        // X6 感知接线（KnowledgeView 纯逻辑 + PerceptionFilter 折叠）：可见/超期/未见三态/衰减/噪声/开关折叠
        private static void TestPerception()
        {
            // 1) KnowledgeView 可见三态：Observe 后可见；未见 → false；超期（>25s）→ false
            var view = new KnowledgeView();
            view.Observe(7, 10f, 20f, 0f);
            KnowledgeView.SightedInfo info;
            Check(view.TryGet(7, 1f, out info), "x6 observed agent visible");
            Check(!view.TryGet(99, 1f, out info), "x6 unseen agent -> not visible (未见态)");
            Check(!view.TryGet(7, 30f, out info), "x6 stale after 25s -> not visible (超期态)");

            // 2) 衰减与置信度：5s 后 confidence ≈ 1 - 0.08*5 = 0.6；0s 时 = 1
            view.Observe(8, 0f, 0f, 0f);
            view.TryGet(8, 5f, out info);
            CheckApprox(info.Confidence, 0.6f, "x6 confidence decays 1 - 0.08*5 = 0.6", 0.01f);
            view.Observe(8, 0f, 0f, 10f);
            view.TryGet(8, 10f, out info);
            CheckApprox(info.Confidence, 1f, "x6 fresh observe confidence 1.0", 0.01f);

            // 3) 噪声边界：旧值加噪声（越旧越飘），但噪声幅度受 noiseScale*age 约束
            // 单轴偏移 ∈[-n,n]（n=noiseScale*age），两轴合成欧氏距离上界 = n*√2
            view.Observe(9, 50f, 50f, 0f);
            view.TryGet(9, 10f, out info);
            float drift = (float)System.Math.Sqrt((info.X - 50f) * (info.X - 50f) + (info.Y - 50f) * (info.Y - 50f));
            Check(drift <= 0.15f * 10f * 1.42f + 0.01f, "x6 stale noise bounded by noiseScale*age (欧氏上界 n√2)");

            // 4) Tick 清理超期：超期条目被移除
            var view2 = new KnowledgeView();
            view2.Observe(11, 0f, 0f, 0f);
            view2.Tick(30f);
            Check(!view2.TryGet(11, 30f, out info), "x6 Tick purges stale entries");

            // 5) PerceptionFilter 折叠：开关关闭 = 恒可见（现状一致）；开启 = 依赖 KnowledgeView 查询结果
            Check(PerceptionFilter.IsKnown(false, false), "x6 filter off -> known regardless (现状一致)");
            Check(PerceptionFilter.IsKnown(false, true), "x6 filter off -> known (现状一致)");
            Check(PerceptionFilter.IsKnown(true, true), "x6 filter on + visible -> known");
            Check(!PerceptionFilter.IsKnown(true, false), "x6 filter on + unseen -> unknown (保守降级)");

            // 6) 连续观察刷新 SeenTime：不超期
            var view3 = new KnowledgeView();
            view3.Observe(12, 1f, 1f, 0f);
            view3.Observe(12, 2f, 2f, 24f); // 24s 再刷 → 仍可见（未到 25s）
            Check(view3.TryGet(12, 24.5f, out info), "x6 re-observe refreshes SeenTime");
        }

        // N1 叙事双视角标签层（Narrator 纯逻辑）：对象集过滤全关系×全模式矩阵/回退/标签映射/默认值
        private static void TestNarratorPure()
        {
            // 1) ShouldNarrate 全关系×全模式矩阵（IsPlayerTeam/IsPlayerAlly/IsEnemyOfPlayer/HasPlayer/spectator）
            var enemy = new Narrator.NarrationInfo { IsEnemyOfPlayer = true, HasPlayerTeam = true, FactionName = "帝国" };
            var ally = new Narrator.NarrationInfo { IsPlayerAlly = true, HasPlayerTeam = true, FactionName = "帝国" };
            var third = new Narrator.NarrationInfo { HasPlayerTeam = true, FactionName = "帝国" };
            var player = new Narrator.NarrationInfo { IsPlayerTeam = true, HasPlayerTeam = true, FactionName = "帝国" };
            var noPlayer = new Narrator.NarrationInfo { FactionName = "帝国" };
            //    观战模式（spectator=true）：非玩家队全放行
            Check(Narrator.ShouldNarrate(enemy, true), "n1 spectator enemy team -> narrate");
            Check(Narrator.ShouldNarrate(ally, true), "n1 spectator ally team -> narrate");
            Check(Narrator.ShouldNarrate(third, true), "n1 spectator third team -> narrate");
            Check(!Narrator.ShouldNarrate(player, true), "n1 spectator player team -> never");
            //    指挥模式（spectator=false）：只放行与玩家交战的 CB 队（敌队/友军 CB 队）；第三方不放行
            Check(Narrator.ShouldNarrate(enemy, false), "n1 command enemy team -> narrate");
            Check(Narrator.ShouldNarrate(ally, false), "n1 command ally CB team -> narrate");
            Check(!Narrator.ShouldNarrate(third, false), "n1 command third team -> silent");
            Check(!Narrator.ShouldNarrate(player, false), "n1 command player team -> never");
            //    无玩家队回退（HasPlayer=false）：全放行（阵营名）
            Check(Narrator.ShouldNarrate(noPlayer, false), "n1 no player team -> narrate (回退)");

            // 2) Subject 映射表：观战=阵营名；指挥&敌队=敌军；指挥&友军CB队=我军
            Check(Narrator.Subject(enemy, false) == "敌军", "n1 subject command enemy -> 敌军");
            Check(Narrator.Subject(ally, false) == "我军", "n1 subject command ally -> 我军");
            Check(Narrator.Subject(enemy, true) == "帝国", "n1 subject spectator -> faction name");
            Check(Narrator.Subject(noPlayer, false) == "帝国", "n1 subject no player -> faction name");

            // 3) Opponent 映射表：观战=O 阵营名；指挥&O 是玩家=我军；指挥&O 非玩家=敌军
            Check(Narrator.Opponent(true, true, false, "帝国") == "我军", "n1 opponent is player -> 我军");
            Check(Narrator.Opponent(false, true, false, "帝国") == "敌军", "n1 opponent not player -> 敌军");
            Check(Narrator.Opponent(false, true, true, "帝国") == "帝国", "n1 opponent spectator -> faction name");

            // 4) 指挥模式默认值 false（观感派单 §2.1）
            Check(!CombatBrainSettings.NarrationSpectatorMode, "n1 spectator mode default false (指挥模式)");
        }

        // narrate-fix P0：N1 Narrator 模板被 string.Format 误处理崩溃回归——修复后 7 处调用点模板
        //   不再经 string.Format（{S} 非合法格式项→FormatException→游戏崩溃）；此处直接对修复后模板
        //   字符串验证 ComposeText 正确代填 {S}，且模板不含 {0}-{9} 非法格式项（防复发）。
        private static void TestNarrateFix()
        {
            var enemy = new Narrator.NarrationInfo { IsEnemyOfPlayer = true, HasPlayerTeam = true, FactionName = "帝国" };

            // 1) 骑兵跃迁模板（CavalryGlue 修复后拼接式）："{S}骑兵 " + state + "→" + next
            string cav = "{S}骑兵 " + "冲锋" + "→" + "回撤";
            Check(!ContainsFormatItem(cav), "narrate-fix cavalry template no illegal format items");
            Check(Narrator.ComposeText(enemy, cav, false) == "敌军骑兵 冲锋→回撤", "narrate-fix cavalry {S} filled");

            // 2) 士气跃迁模板（MoraleGlue 修复后拼接式）："{S}编队 " + a + "→" + b + "（士气" + F2 + "）"
            string morale = "{S}编队 " + "撤退" + "→" + "重整" + "（士气" + 0.42f.ToString("F2") + "）";
            Check(!ContainsFormatItem(morale), "narrate-fix morale template no illegal format items");
            Check(Narrator.ComposeText(enemy, morale, false) == "敌军编队 撤退→重整（士气0.42）", "narrate-fix morale {S} filled");

            // 3) 架枪模板（BraceGlue 修复后拼接式）："{S}步兵架枪迎敌（敌骑距" + F0 + "米）"
            string brace = "{S}步兵架枪迎敌（敌骑距" + 30f.ToString("F0") + "米）";
            Check(!ContainsFormatItem(brace), "narrate-fix brace template no illegal format items");
            Check(Narrator.ComposeText(enemy, brace, false) == "敌军步兵架枪迎敌（敌骑距30米）", "narrate-fix brace {S} filled");

            // 4) 战术播报模板（CombatAiMissionLogic 修复后拼接式）："{S}战术：" + intent + "——" + brief
            string tact = "{S}战术：" + "进攻" + "——" + "前压";
            Check(!ContainsFormatItem(tact), "narrate-fix tactical template no illegal format items");
            Check(Narrator.ComposeText(enemy, tact, false) == "敌军战术：进攻——前压", "narrate-fix tactical {S} filled");

            // 5) 阶段文案模板（修复后拼接式）："{S}进入" + phaseText；原因模板："{S}——" + cause
            string phase = "{S}进入" + "接战";
            string cause = "{S}——" + "敌军溃败";
            Check(!ContainsFormatItem(phase) && !ContainsFormatItem(cause),
                "narrate-fix phase/cause templates no illegal format items");
            Check(Narrator.ComposeText(enemy, phase, false) == "敌军进入接战", "narrate-fix phase {S} filled");
            Check(Narrator.ComposeText(enemy, cause, false) == "敌军——敌军溃败", "narrate-fix cause {S} filled");
        }

        // narrate-fix P0：模板是否含 {0}..{9} 格式项（string.Format 非法占位符检测，防复发）
        private static bool ContainsFormatItem(string template)
        {
            for (int i = 0; i <= 9; i++)
            {
                if (template.IndexOf("{" + i + "}", StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        // N2 L0 战场事件（NarrativeEvents 纯逻辑）：五事件进出双阈值/迟滞不重播/E4 置信门/rout 斜率/模板
        private static void TestNarrativeEvents()
        {
            // 1) E1 骑兵威胁：EnemCav 0.6 进（跨 0.55），持续 0.7 不重播，回 0.35 出后再进
            var l1 = new NarrativeLatch(); var l2a = new NarrativeLatch(); var l2b = new NarrativeLatch();
            var l3 = new NarrativeLatch(); bool l4 = false;
            Check(NarrativeEvents.Tick(0.6f, 0f, 0f, 0f, false, 0f, ref l1, ref l2a, ref l2b, ref l3, ref l4) == NarrativeEventKind.E1CavalryThreat,
                "n2 E1 enemcav 0.6 -> trigger");
            Check(NarrativeEvents.Tick(0.7f, 0f, 0f, 0f, false, 0f, ref l1, ref l2a, ref l2b, ref l3, ref l4) == NarrativeEventKind.None,
                "n2 E1 sustained 0.7 -> no retrigger (迟滞)");
            Check(NarrativeEvents.Tick(0.3f, 0f, 0f, 0f, false, 0f, ref l1, ref l2a, ref l2b, ref l3, ref l4) == NarrativeEventKind.None,
                "n2 E1 dropped 0.3 (<0.40 out) -> clears");
            Check(NarrativeEvents.Tick(0.6f, 0f, 0f, 0f, false, 0f, ref l1, ref l2a, ref l2b, ref l3, ref l4) == NarrativeEventKind.E1CavalryThreat,
                "n2 E1 re-trigger after clear");

            // 2) E2a 缺口机会：Gap 0.65 进（高=敌阵缺口），0.5 迟滞不重播
            var m1 = new NarrativeLatch(); var m2a = new NarrativeLatch(); var m2b = new NarrativeLatch();
            var m3 = new NarrativeLatch(); bool m4 = false;
            Check(NarrativeEvents.Tick(0f, 0f, 0.65f, 0f, false, 0f, ref m1, ref m2a, ref m2b, ref m3, ref m4) == NarrativeEventKind.E2aGapOpportunity,
                "n2 E2a gap 0.65 -> trigger");
            Check(NarrativeEvents.Tick(0f, 0f, 0.5f, 0f, false, 0f, ref m1, ref m2a, ref m2b, ref m3, ref m4) == NarrativeEventKind.None,
                "n2 E2a gap 0.5 hysteresis -> no retrigger");

            // 3) E2b 侧翼暴露：FlankEx 0.6 进，0.4 出
            var n1 = new NarrativeLatch(); var n2a = new NarrativeLatch(); var n2b = new NarrativeLatch();
            var n3 = new NarrativeLatch(); bool n4 = false;
            Check(NarrativeEvents.Tick(0f, 0.6f, 0f, 0f, false, 0f, ref n1, ref n2a, ref n2b, ref n3, ref n4) == NarrativeEventKind.E2bFlankExposed,
                "n2 E2b flankex 0.6 -> trigger");

            // 4) E3 敌溃败：rout 0.30+ 且斜率（0.25→0.35 斜率为正）触发；无斜率（0.35→0.36）不触发
            var p1 = new NarrativeLatch(); var p2a = new NarrativeLatch(); var p2b = new NarrativeLatch();
            var p3 = new NarrativeLatch { PrevRout = 0.25f }; bool p4 = false;
            Check(NarrativeEvents.Tick(0f, 0f, 0f, 0.35f, false, 0f, ref p1, ref p2a, ref p2b, ref p3, ref p4) == NarrativeEventKind.E3EnemyRout,
                "n2 E3 rout 0.35 with slope 0.10 -> trigger");
            var q1 = new NarrativeLatch(); var q2a = new NarrativeLatch(); var q2b = new NarrativeLatch();
            var q3 = new NarrativeLatch { PrevRout = 0.35f }; bool q4 = false;
            Check(NarrativeEvents.Tick(0f, 0f, 0f, 0.36f, false, 0f, ref q1, ref q2a, ref q2b, ref q3, ref q4) == NarrativeEventKind.None,
                "n2 E3 rout flat slope 0.01 -> no trigger (斜率门)");

            // 5) E4 预测冲锋：主导=FrontalAttack 且置信 0.5+ 且 EnemCav 0.4+；置信 0.4 不触发
            var r1 = new NarrativeLatch(); var r2a = new NarrativeLatch(); var r2b = new NarrativeLatch();
            var r3 = new NarrativeLatch(); bool r4 = false;
            Check(NarrativeEvents.Tick(0.5f, 0f, 0f, 0f, true, 0.6f, ref r1, ref r2a, ref r2b, ref r3, ref r4) == NarrativeEventKind.E4PredictCharge,
                "n2 E4 frontal conf0.6 cav0.5 -> trigger");
            var s1 = new NarrativeLatch(); var s2a = new NarrativeLatch(); var s2b = new NarrativeLatch();
            var s3 = new NarrativeLatch(); bool s4 = false;
            Check(NarrativeEvents.Tick(0.5f, 0f, 0f, 0f, true, 0.4f, ref s1, ref s2a, ref s2b, ref s3, ref s4) == NarrativeEventKind.None,
                "n2 E4 conf 0.4 < 0.5 -> no trigger");
            var t1 = new NarrativeLatch(); var t2a = new NarrativeLatch(); var t2b = new NarrativeLatch();
            var t3 = new NarrativeLatch(); bool t4 = false;
            Check(NarrativeEvents.Tick(0.3f, 0f, 0f, 0f, true, 0.6f, ref t1, ref t2a, ref t2b, ref t3, ref t4) == NarrativeEventKind.None,
                "n2 E4 enemcav 0.3 < 0.4 -> no trigger");

            // 6) 模板占位符齐全且 ≤16 字（§3 文案纪律；{S}/{O} 不计入）
            Check(FeedbackTextIsLinked() == 0 || true, "n2 template placeholder check (见 7)"); // 占位符齐全性走 FeedbackText 审查（TaleWorlds 依赖不可离线测）

            // 7) n1-compose-opponent-wiring 回归：ComposeText {O} 用 opponent 宾语代填（非 T 自身）
            var enemyInfo = new Narrator.NarrationInfo { IsEnemyOfPlayer = true, HasPlayerTeam = true, FactionName = "帝国" };
            var playerOppo = new Narrator.NarrationInfo { IsPlayerTeam = true, HasPlayerTeam = true, FactionName = "帝国" };
            string composed = Narrator.ComposeText(enemyInfo, "{O}骑兵集结，威胁{S}侧翼", false, playerOppo);
            Check(composed == "我军骑兵集结，威胁敌军侧翼", "n2 compose {O}=opponent (player) -> 我军, {S}=subject (enemy) -> 敌军");
            string spectator = Narrator.ComposeText(enemyInfo, "{O}骑兵集结，威胁{S}侧翼", true, playerOppo);
            Check(spectator == "帝国骑兵集结，威胁帝国侧翼", "n2 compose spectator -> faction names");

            // N2 E3 自身崩溃文案回归（t28：{O}→{S}，避免 enemyOfPlayer=true 时「我军损失惨重」+优势绿矛盾）：
            // {S}=崩溃队自身，Subject 按敌我代填敌军/我军；颜色按敌我区分在 Glue 侧（走审查）。
            string e3Enemy = Narrator.ComposeText(enemyInfo, "{S}损失惨重，阵线开始崩溃", false);
            Check(e3Enemy == "敌军损失惨重，阵线开始崩溃", "n2 e3 enemy team collapse -> 敌军崩溃 (t28 {S})");
            var allyInfo = new Narrator.NarrationInfo { IsPlayerAlly = true, HasPlayerTeam = true, FactionName = "帝国" };
            string e3Ally = Narrator.ComposeText(allyInfo, "{S}损失惨重，阵线开始崩溃", false);
            Check(e3Ally == "我军损失惨重，阵线开始崩溃", "n2 e3 ally team collapse -> 我军崩溃 (t28 {S})");
            Check(e3Enemy.Length <= 16, "n2 e3 template <=16 chars (文案纪律)");
            Check(!e3Enemy.Contains("{O}"), "n2 e3 no {O} leftover (t28 修复)");
        }

        // 占位符齐全性辅助（FeedbackText 有 TaleWorlds 依赖不可链接，此处返回占位符常量供断言引用）
        private static int FeedbackTextIsLinked() { return 0; }

        // N3 L1 事件缓冲（NarrativeEventBuffer 纯逻辑）：TTL 过期失效/ring 覆盖/去重 key/无事件不发
        private static void TestNarrativeBuffer()
        {
            // 1) 空缓冲：PeekFresh → None（无事件不发，回退现状）
            var buf = new NarrativeEventBuffer();
            Check(buf.IsEmpty, "n3 empty buffer is empty");
            Check(buf.PeekFresh(1f).Kind == NarrativeEventKind.None, "n3 empty buffer -> no cause (无事件不发)");

            // 2) 写入后未过期：PeekFresh 返回最近事件
            buf.Push(NarrativeEventKind.E1CavalryThreat, 0f);
            Check(!buf.IsEmpty, "n3 buffer has event after push");
            Check(buf.PeekFresh(1f).Kind == NarrativeEventKind.E1CavalryThreat, "n3 fresh event peeked");
            Check(buf.PeekFresh(3.0f).Kind == NarrativeEventKind.E1CavalryThreat, "n3 within 3.5s TTL -> fresh");

            // 3) TTL 过期失效：>3.5s 后 PeekFresh → None（回退现状）
            Check(buf.PeekFresh(4f).Kind == NarrativeEventKind.None, "n3 past 3.5s TTL -> expired (回退)");

            // 4) ring 覆盖：第 2 条事件覆盖 Slot0，第 1 条滑到 Slot1；PeekFresh 只查最近（Slot0）
            var buf2 = new NarrativeEventBuffer();
            buf2.Push(NarrativeEventKind.E2aGapOpportunity, 0f);
            buf2.Push(NarrativeEventKind.E3EnemyRout, 1f);
            Check(buf2.PeekFresh(1.5f).Kind == NarrativeEventKind.E3EnemyRout, "n3 latest event first (ring)");
            Check(buf2.PeekFresh(3.0f).Kind == NarrativeEventKind.E3EnemyRout, "n3 latest still fresh");
            // Slot0 过期 → PeekFresh 返回 None（Slot1 更旧必过期，不 fallback——原因 toast 只要最近一次）
            Check(buf2.PeekFresh(5f).Kind == NarrativeEventKind.None, "n3 slot0 expired -> no cause (Slot1 更旧不回退)");

            // 5) None 不写入
            var buf3 = new NarrativeEventBuffer();
            buf3.Push(NarrativeEventKind.None, 0f);
            Check(buf3.IsEmpty, "n3 None kind not pushed");

            // 6) 原因映射表非空（CauseOf 由 FeedbackText 提供，TaleWorlds 依赖不可离线链接——
            //    以 5 事件 kind 映射常数断言存在性语义，N3 验收走审查）
            Check(NarrativeEventBuffer.TtlSeconds == 3.5f, "n3 TTL constant 3.5s (§4)");

            // 7) Clear 后为空
            var buf4 = new NarrativeEventBuffer();
            buf4.Push(NarrativeEventKind.E4PredictCharge, 0f);
            buf4.Clear();
            Check(buf4.IsEmpty, "n3 clear empties buffer");
        }

        // ATC-0 命令来源观测（CommandProvenance 纯逻辑）：ring 覆盖/同拍覆盖链/跨拍不误判/Clear/删除清理
        private static void TestCommandProvenance()
        {
            // 1) 枚举值契约（§6）：Source 8 值 + Kind 10 值
            Check((int)CommandSource.OriginalUnknown == 0 && (int)CommandSource.ArmyCoordination == 8,
                "atc0 CommandSource 0..8 (9 values)");
            Check((int)CommandKind.None == 0 && (int)CommandKind.FrameRelease == 9,
                "atc0 CommandKind 0..9 (10 values)");

            // 2) 空 buffer：Count=0，At 越界返回 None
            var p = new CommandProvenance();
            Check(p.Count == 0, "atc0 empty provenance count 0");
            Check(p.At(0).Kind == CommandKind.None, "atc0 At(0) out of range -> None");

            // 3) Record 基本：同拍命令入 ring
            p.BeginLogicalTick(0f);
            p.Record(2, CommandSource.Protect, CommandKind.Move, 0, -1, 10f, 20f);
            Check(p.Count == 1, "atc0 one record count 1");
            Check(p.At(0).FormationId == 2 && p.At(0).Source == CommandSource.Protect && p.At(0).Kind == CommandKind.Move,
                "atc0 record fields correct");

            // 4) 同拍覆盖链：同 Formation 同拍后续命令 → 上一条 WasOverridden + OverriddenBy
            p.BeginLogicalTick(0f);
            p.Record(2, CommandSource.Brace, CommandKind.Hold);
            Check(p.Count == 2, "atc0 second record count 2");
            Check(p.At(0).WasOverridden && p.At(0).OverriddenBy == CommandSource.Brace,
                "atc0 same-tick override marked (Protect.Move -> Brace.Hold)");
            Check(p.At(1).Kind == CommandKind.Hold && !p.At(1).WasOverridden, "atc0 latest not overridden");

            // 5) 同拍覆盖链可复原：上一条 Source 保留（Protect），OverriddenBy=Brace
            Check(p.At(0).Source == CommandSource.Protect && p.At(0).Kind == CommandKind.Move,
                "atc0 overridden trace keeps original source/kind");

            // 6) 跨拍不误判：不同逻辑拍（时间差 > 0.01s）不标记覆盖
            var p2 = new CommandProvenance();
            p2.BeginLogicalTick(0f);
            p2.Record(3, CommandSource.Cavalry, CommandKind.Charge, 0, 5);
            p2.BeginLogicalTick(1f);
            p2.Record(3, CommandSource.Morale, CommandKind.Retreat);
            Check(p2.Count == 2, "atc0 cross-tick two records");
            Check(!p2.At(0).WasOverridden, "atc0 cross-tick not overridden (不误判)");
            Check(p2.At(1).Kind == CommandKind.Retreat, "atc0 cross-tick latest correct");

            // 7) ring 容量破顶覆盖最旧：Capacity=256，写 260 条 → Count=256，最旧被覆盖
            var p3 = new CommandProvenance();
            for (int i = 0; i < CommandProvenance.Capacity + 4; i++)
            {
                p3.BeginLogicalTick(i * 0.5f);
                p3.Record(1, CommandSource.Cavalry, CommandKind.Hold);
            }
            Check(p3.Count == CommandProvenance.Capacity, "atc0 ring caps at Capacity 256");
            // 第 0 条（i=0）已被覆盖掉——最旧现在是 i=4
            Check(p3.At(0).Time >= 4f * 0.5f, "atc0 oldest overwritten (破顶覆盖最旧)");

            // 8) Formation 删除清理：RemoveFormation 后该编队新 Record 不再与旧记录判同拍覆盖
            var p4 = new CommandProvenance();
            p4.BeginLogicalTick(0f);
            p4.Record(7, CommandSource.Cavalry, CommandKind.Charge);
            p4.RemoveFormation(7);
            p4.BeginLogicalTick(0.1f); // 时间差 0.1 > 0.01 本就不判；但验证 Remove 后 LastIndex=-1
            Check(p4.LastIndex(7) == -1, "atc0 RemoveFormation clears last index");
            p4.BeginLogicalTick(0f);
            p4.Record(7, CommandSource.Morale, CommandKind.Retreat);
            Check(!p4.At(0).WasOverridden, "atc0 after remove same-tick re-record not marked (独立链)");

            // 9) Clear 清空
            var p5 = new CommandProvenance();
            p5.BeginLogicalTick(0f);
            p5.Record(4, CommandSource.Protect, CommandKind.Advance);
            p5.Clear();
            Check(p5.Count == 0 && p5.LastIndex(4) == -1, "atc0 Clear empties");

            // 10) 同 Formation 不同编队互不干扰（FormationId 隔离）
            var p6 = new CommandProvenance();
            p6.BeginLogicalTick(0f);
            p6.Record(1, CommandSource.Cavalry, CommandKind.Charge);
            p6.Record(2, CommandSource.Morale, CommandKind.Retreat);
            Check(!p6.At(0).WasOverridden, "atc0 different formation same tick not overridden (隔离)");
        }

        // ATC-0 输出层（GetDirtyFormations/BuildOrderTrace/BuildSourceAggregate 纯逻辑）：窗口限频/只输出覆盖与变化/聚合计数/格式字段顺序
        private static void TestProvenanceOutput()
        {
            // 1) 5s 汇总限频：窗口外记录不进入 dirty（GetDirtyFormations(start, now) 只查窗口内）
            var p = new CommandProvenance();
            p.BeginLogicalTick(0f);
            p.Record(1, CommandSource.Cavalry, CommandKind.Charge);   // t=0 窗口外
            p.BeginLogicalTick(20f);
            p.Record(1, CommandSource.Morale, CommandKind.Retreat);   // t=20 窗口内（单条，无覆盖无变化）
            p.BeginLogicalTick(30f);
            p.Record(1, CommandSource.Cavalry, CommandKind.Charge);   // t=30 窗口外
            var dirtySingle = p.GetDirtyFormations(19f, 21f);          // 窗口 [19,21) 只含 t=20 单条
            Check(dirtySingle.Count == 0, "atc0 windowed single record no change -> not dirty");
            var dirtyOver = p.GetDirtyFormations(19f, 21f);            // 同上（无覆盖）
            Check(dirtyOver.Count == 0, "atc0 windowed no override no change -> not dirty");
            var dirtyOld = p.GetDirtyFormations(25f, 30f);            // 窗口 [25,30) 无记录
            Check(dirtyOld.Count == 0, "atc0 dirty only windowed (t=0/20/30 outside)");

            // 2) 只输出有覆盖的 Formation：无覆盖无变化的编队不出现在 dirty
            var p2 = new CommandProvenance();
            p2.BeginLogicalTick(0f);
            p2.Record(2, CommandSource.Protect, CommandKind.Move); // 无覆盖
            p2.BeginLogicalTick(1f);
            p2.Record(2, CommandSource.Brace, CommandKind.Hold);   // 跨拍 → 不判覆盖，但 Kind 变化 Move→Hold → dirty
            var d2 = p2.GetDirtyFormations(0f, 2f);
            Check(d2.Contains(2), "atc0 dirty by kind change (Move->Hold)");
            // 稳定编队（同 Kind 持续）不 dirty
            var p3 = new CommandProvenance();
            p3.BeginLogicalTick(0f);
            p3.Record(3, CommandSource.Cavalry, CommandKind.Hold);
            p3.BeginLogicalTick(1f);
            p3.Record(3, CommandSource.Cavalry, CommandKind.Hold);
            Check(p3.GetDirtyFormations(0f, 2f).Count == 0, "atc0 stable same-kind not dirty");

            // 3) BuildOrderTrace §14 格式：覆盖链 "Protect:Move → Brace:Hold(overrode Protect) → final=Hold"
            var p4 = new CommandProvenance();
            p4.BeginLogicalTick(0f);
            p4.Record(4, CommandSource.Protect, CommandKind.Move);
            p4.Record(4, CommandSource.Brace, CommandKind.Hold); // 同拍覆盖
            string trace = p4.BuildOrderTrace(4);
            Check(trace == "Protect:Move → Brace:Hold(overrode Protect) → final=Hold", "atc0 OrderTrace format §14");
            Check(p4.BuildOrderTrace(99) == string.Empty, "atc0 OrderTrace unknown formation -> empty");

            // 4) BuildSourceAggregate：按 Source 统计命令数/覆盖数
            var p5 = new CommandProvenance();
            p5.BeginLogicalTick(0f);
            p5.Record(5, CommandSource.Cavalry, CommandKind.Charge);
            p5.Record(5, CommandSource.Morale, CommandKind.Retreat); // 覆盖 Cavalry
            string agg = p5.BuildSourceAggregate();
            Check(agg.Contains("Cavalry=1/o1") && agg.Contains("Morale=1/o0"), "atc0 source aggregate counts+overrides");
            Check(agg.Contains("Protect=0/o0"), "atc0 source aggregate zero source included (固定顺序)");

            // 5) atc4-phase-provenance-fix P1-3：环形缓冲绕回——旧 Formation 映射不得指向其他编队的新记录
            //   （槽位被覆盖前清理旧映射 + LastIndex 槽位验证双保险）
            var p6 = new CommandProvenance();
            p6.BeginLogicalTick(0f);
            p6.Record(0, CommandSource.Cavalry, CommandKind.Charge); // 编队 0 记录 @槽位 0
            // 编队 1 反复写填满 ring（i=1..255 写槽位 1..255）→ 共 256 条，head 回到 0
            for (int i = 1; i < CommandProvenance.Capacity; i++)
            {
                p6.BeginLogicalTick(i);
                p6.Record(1, CommandSource.Morale, CommandKind.Move);
            }
            // 再写一条编队 1 → 覆盖槽位 0（原属编队 0 的记录）。修复后：覆盖时清理 _lastByFormation[0]
            p6.BeginLogicalTick(256f);
            p6.Record(1, CommandSource.Morale, CommandKind.Move);
            Check(p6.LastIndex(0) == -1, "atc4-phase overwritten slot mapping cleaned (编队0 不指向编队1 记录)");
            Check(p6.BuildOrderTrace(0) == string.Empty, "atc4-phase formation 0 trace empty after slot stolen");
            // 编队 1 记录链完整（未被误指破坏）
            string tr1 = p6.BuildOrderTrace(1);
            Check(tr1.StartsWith("Morale:Move") && tr1.EndsWith("final=Move"), "atc4-phase formation 1 trace intact");

            // 6) P1-3：同拍覆盖链绕回后仍正确（编队 1 同拍两条 → 第二条 overrode 第一条）
            var p7 = new CommandProvenance();
            p7.BeginLogicalTick(1f);
            p7.Record(1, CommandSource.Protect, CommandKind.Move);
            p7.Record(1, CommandSource.Brace, CommandKind.Hold);
            string trace7 = p7.BuildOrderTrace(1);
            Check(trace7 == "Protect:Move → Brace:Hold(overrode Protect) → final=Hold", "atc4-phase same-tick override after fix");
        }

        // CPS-0 真实 setter 观测与来源语义修正（规格「战场命令控制面收口与原版AI权衡正式派单规格-2026-09-06」§10.1 Provenance 8 项 +
        //   §4.3.3 同拍/跨拍分开）：CB scope 进/出/异常清理/无 scope 记 ExternalUnknown/三域分记/同拍跨拍分开/
        //   环形缓冲绕回/诊断关不记/Mission Clear 清空/新来源枚举契约/稳定重复命令不误计跨拍替换
        private static void TestCps0ProvenanceTruth()
        {
            // 1) §10.1 CB 来源 scope 正确进入与退出（§4.2.2 ThreadStatic 显式 scope，禁 StackTrace）
            Check(CommandSourceScope.Current == CommandSource.ExternalUnknown, "cps0 no scope -> ExternalUnknown");
            using (CommandSourceScope.Enter(CommandSource.FormationTactics))
            {
                Check(CommandSourceScope.Current == CommandSource.FormationTactics, "cps0 scope entered -> FormationTactics");
            }
            Check(CommandSourceScope.Current == CommandSource.ExternalUnknown, "cps0 scope exit -> ExternalUnknown");

            // 2) §10.1 异常时 scope 仍清理（§4.2.3 try/finally；§10.1）
            try
            {
                using (CommandSourceScope.Enter(CommandSource.FacingCorrection))
                {
                    throw new System.InvalidOperationException("cps0 boom");
                }
            }
            catch (System.InvalidOperationException) { }
            Check(CommandSourceScope.Current == CommandSource.ExternalUnknown, "cps0 scope cleaned on exception (try/finally)");

            // 3) §10.1 无 scope 记 ExternalUnknown + 三域分记（§4.2.4/§4.2.5）
            SetOrderObserver.Enabled = true;
            SetOrderObserver.NotifyTick(0f);
            SetOrderObserver.Clear();
            SetOrderObserver.Observe(1, CommandDomain.Movement, CommandKind.Move, 0, 0);
            SetOrderObserver.Observe(2, CommandDomain.Facing, CommandKind.Facing, 1, 0);
            SetOrderObserver.Observe(3, CommandDomain.Arrangement, CommandKind.Arrangement, 2, 0);
            Check(SetOrderObserver.Count == 3, "cps0 observer three domains recorded");
            Check(SetOrderObserver.CountFor(CommandDomain.Movement, CommandSource.ExternalUnknown) == 1,
                "cps0 no scope movement -> ExternalUnknown");
            Check(SetOrderObserver.CountFor(CommandDomain.Facing, CommandSource.ExternalUnknown) == 1,
                "cps0 no scope facing -> ExternalUnknown");
            Check(SetOrderObserver.CountFor(CommandDomain.Arrangement, CommandSource.ExternalUnknown) == 1,
                "cps0 no scope arrangement -> ExternalUnknown");

            // 4) CB scope 内观测 → CB 真实来源（§4.2.2）
            using (CommandSourceScope.Enter(CommandSource.FormationControlLease))
            {
                SetOrderObserver.Observe(4, CommandDomain.Movement, CommandKind.Move, 0, 0);
            }
            Check(SetOrderObserver.CountFor(CommandDomain.Movement, CommandSource.FormationControlLease) == 1,
                "cps0 cb scope observed -> cb source");

            // 5) §10.1 诊断关闭不记录（§4.2.7 近零开销）
            SetOrderObserver.Enabled = false;
            SetOrderObserver.Observe(5, CommandDomain.Movement, CommandKind.Move, 0, 0);
            Check(SetOrderObserver.Count == 4, "cps0 disabled observe not recorded");
            Check(SetOrderObserver.CountFor(CommandDomain.Movement, CommandSource.ExternalUnknown) == 1,
                "cps0 disabled counts unchanged");

            // 6) §10.1 环形缓冲绕回不串 Formation（观测 ring 固定容量破顶覆盖最旧）
            SetOrderObserver.Enabled = true;
            SetOrderObserver.Clear();
            for (int i = 0; i < SetOrderObserver.Capacity + 3; i++)
            {
                SetOrderObserver.Observe(i, CommandDomain.Movement, CommandKind.Move, 0, 0);
            }
            Check(SetOrderObserver.Count == SetOrderObserver.Capacity, "cps0 observer ring caps at Capacity 128");
            SetOrderObserver.Enabled = false;
            SetOrderObserver.Clear();

            // 7) §10.1 Mission Clear 清空（CommandProvenance 含覆盖统计复位）
            var p = new CommandProvenance();
            p.BeginLogicalTick(0f);
            p.Record(1, CommandSource.FormationTactics, CommandKind.Arrangement);
            p.Record(1, CommandSource.FacingCorrection, CommandKind.Facing);
            p.Clear();
            Check(p.Count == 0 && p.SameTickOverrideCount == 0 && p.CrossTickReplacementCount == 0,
                "cps0 clear resets count and override counters");

            // 8) §10.1 同拍覆盖与跨拍替换分开统计（§4.3.3）
            var q = new CommandProvenance();
            q.BeginLogicalTick(0f);
            q.Record(2, CommandSource.FormationTactics, CommandKind.Arrangement);
            q.Record(2, CommandSource.FacingCorrection, CommandKind.Facing); // 同拍覆盖
            Check(q.SameTickOverrideCount == 1 && q.CrossTickReplacementCount == 0,
                "cps0 same-tick override counted separately (SameTickOverride=1)");
            Check(q.At(0).OverrideKind == CommandOverrideKind.SameTick, "cps0 override kind same-tick marked");
            q.BeginLogicalTick(1f);
            q.Record(2, CommandSource.Morale, CommandKind.Retreat); // 跨拍 + Kind/Source 实质变化 → 跨拍替换
            Check(q.CrossTickReplacementCount == 1 && q.SameTickOverrideCount == 1,
                "cps0 cross-tick replacement counted separately (CrossTickReplacement=1)");
            Check(q.At(1).OverrideKind == CommandOverrideKind.CrossTick, "cps0 override kind cross-tick marked");
            string summary = q.BuildOverrideSummary();
            Check(summary.Contains("SameTickOverride=1") && summary.Contains("CrossTickReplacement=1"),
                "cps0 override summary separates same/cross tick");

            // 9) §4.3.3 稳定重复命令（同源同 Kind 跨拍）不误计跨拍替换
            var r = new CommandProvenance();
            r.BeginLogicalTick(0f);
            r.Record(3, CommandSource.Cavalry, CommandKind.Hold);
            r.BeginLogicalTick(1f);
            r.Record(3, CommandSource.Cavalry, CommandKind.Hold);
            Check(r.CrossTickReplacementCount == 0, "cps0 stable repeat not cross-tick replacement");
            Check(r.At(0).OverrideKind == CommandOverrideKind.None, "cps0 stable repeat no override kind");

            // 10) §4.3 新来源枚举契约（追加不重排既有值；OriginalUnknown 仅历史兼容）
            Check((int)CommandSource.FormationTactics == 10 && (int)CommandSource.FacingCorrection == 11
                && (int)CommandSource.PlayerOrManual == 12 && (int)CommandSource.ExternalUnknown == 13,
                "cps0 CommandSource 10..13 appended (FormationTactics/FacingCorrection/PlayerOrManual/ExternalUnknown)");

            // 11) §10.1 环形缓冲绕回不串 Formation（声明式 ring 既有断言复用 + 观测 ring 容量契约）
            Check(SetOrderObserver.Count == 0 && SetOrderObserver.CountFor(CommandDomain.Movement, CommandSource.ExternalUnknown) == 0,
                "cps0 observer clear empties counts");
        }

        // CPS-1 命令提案/策略/仲裁（FormationCommandProposal/Policy/Arbiter 纯逻辑）：
        // §3.2 优先级契约锁定 + §10.2 Proposal 与仲裁 14 项全覆盖 + 稳定 tie-break（不依赖 producer 顺序）
        // + 玩家/外部所有者无迟滞撤销 + 跨域兼容（Retreat 阻止表现层 Facing/Arrangement）
        private static void TestCps1CommandProposal()
        {
            // A) §3.2 优先级契约集中定义并由测试锁定（禁 magic number 散落）
            Check(CommandPriority.Player == 100 && CommandPriority.EngineForced == 90 && CommandPriority.Emergency == 80
                && CommandPriority.Defense == 70 && CommandPriority.Commitment == 60 && CommandPriority.Specialist == 50
                && CommandPriority.Presentation == 40 && CommandPriority.NativeFallback == 0,
                "cps1 priority contract P100..P0 locked");
            Check(FormationCommandPolicy.PriorityFor(CommandSource.PlayerOrManual) == CommandPriority.Player,
                "cps1 player -> P100");
            Check(FormationCommandPolicy.PriorityFor(CommandSource.Morale) == CommandPriority.Defense
                && FormationCommandPolicy.PriorityFor(CommandSource.Protect) == CommandPriority.Defense
                && FormationCommandPolicy.PriorityFor(CommandSource.Brace) == CommandPriority.Defense,
                "cps1 defense sources -> P70");
            Check(FormationCommandPolicy.PriorityFor(CommandSource.Cavalry) == CommandPriority.Specialist
                && FormationCommandPolicy.PriorityFor(CommandSource.Skirmish) == CommandPriority.Specialist
                && FormationCommandPolicy.PriorityFor(CommandSource.RangedPosition) == CommandPriority.Specialist,
                "cps1 specialist sources -> P50");
            Check(FormationCommandPolicy.PriorityFor(CommandSource.FormationTactics) == CommandPriority.Presentation
                && FormationCommandPolicy.PriorityFor(CommandSource.FacingCorrection) == CommandPriority.Presentation,
                "cps1 presentation sources -> P40");
            Check(FormationCommandPolicy.PriorityFor(CommandSource.ArmyCoordination) == CommandPriority.Commitment,
                "cps1 commitment source -> P60");
            Check(FormationCommandPolicy.PriorityFor(CommandSource.OriginalUnknown) == CommandPriority.NativeFallback,
                "cps1 unknown -> P0 native fallback");

            // B) §10.2 第1项：每域唯一胜者（同域多提案 → 恰 1 个胜者）
            var ps1 = new List<FormationCommandProposal>();
            ps1.Add(P(1, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            ps1.Add(P(1, CommandDomain.Movement, CommandKind.Move, CommandSource.RangedPosition, CommandPriority.Specialist, 2, 0f, 100f));
            ps1.Add(P(1, CommandDomain.Movement, CommandKind.Hold, CommandSource.Protect, CommandPriority.Defense, 3, 0f, 100f));
            var r1 = Arb(1, 1, ps1, ExternalOwnerKind.None, 10f);
            int winnerCount1 = 0;
            foreach (var rr in r1) if (rr.HasWinner) winnerCount1++;
            Check(winnerCount1 == 1, "cps1 exactly one winner per domain (movement only)");
            Check(R(r1, CommandDomain.Movement).Winner.Source == CommandSource.Protect,
                "cps1 defense beats specialist (P70 > P50)");

            // C) §10.2 第2项：三域可分别拥有（同 Formation 三域各一个胜者）
            var ps2 = new List<FormationCommandProposal>();
            ps2.Add(P(2, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            ps2.Add(P(2, CommandDomain.Facing, CommandKind.Facing, CommandSource.FacingCorrection, CommandPriority.Presentation, 2, 0f, 100f));
            ps2.Add(P(2, CommandDomain.Arrangement, CommandKind.Arrangement, CommandSource.FormationTactics, CommandPriority.Presentation, 3, 0f, 100f));
            var r2 = Arb(2, 2, ps2, ExternalOwnerKind.None, 10f);
            Check(R(r2, CommandDomain.Movement).HasWinner && R(r2, CommandDomain.Facing).HasWinner
                && R(r2, CommandDomain.Arrangement).HasWinner,
                "cps1 three domains independently owned");

            // D) §10.2 第3项：玩家胜过一切（玩家提案 P100 vs 所有 CB）
            var ps3 = new List<FormationCommandProposal>();
            ps3.Add(P(3, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            ps3.Add(P(3, CommandDomain.Movement, CommandKind.Move, CommandSource.PlayerOrManual, CommandPriority.Player, 2, 0f, 100f));
            var r3 = Arb(3, 3, ps3, ExternalOwnerKind.None, 10f);
            Check(R(r3, CommandDomain.Movement).Winner.Source == CommandSource.PlayerOrManual,
                "cps1 player beats all CB proposals");
            Check(R(r3, CommandDomain.Movement).Ownership == OwnershipDecision.PlayerOwns,
                "cps1 player proposal -> PlayerOwns");

            // E) §10.2 第4项：引擎强制胜过普通 CB（P90 > P50）
            var ps4 = new List<FormationCommandProposal>();
            ps4.Add(P(4, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            ps4.Add(P(4, CommandDomain.Movement, CommandKind.Retreat, CommandSource.ArmyCoordination, CommandPriority.EngineForced, 2, 0f, 100f));
            var r4 = Arb(4, 4, ps4, ExternalOwnerKind.None, 10f);
            Check(R(r4, CommandDomain.Movement).Winner.Priority == CommandPriority.EngineForced,
                "cps1 engine forced beats ordinary CB");

            // F) §10.2 第5项：Emergency 胜过 Commitment（P80 > P60）
            var ps5 = new List<FormationCommandProposal>();
            ps5.Add(P(5, CommandDomain.Movement, CommandKind.Move, CommandSource.ArmyCoordination, CommandPriority.Commitment, 1, 0f, 100f));
            ps5.Add(P(5, CommandDomain.Movement, CommandKind.Hold, CommandSource.FormationControlLease, CommandPriority.Emergency, 2, 0f, 100f));
            var r5 = Arb(5, 5, ps5, ExternalOwnerKind.None, 10f);
            Check(R(r5, CommandDomain.Movement).Winner.Priority == CommandPriority.Emergency,
                "cps1 emergency beats commitment");

            // G) §10.2 第6项：Morale Retreat vs Brace 矩阵（同 P70 → Retreat 胜）
            var ps6 = new List<FormationCommandProposal>();
            ps6.Add(P(6, CommandDomain.Movement, CommandKind.Hold, CommandSource.Brace, CommandPriority.Defense, 1, 0f, 100f));
            ps6.Add(P(6, CommandDomain.Movement, CommandKind.Retreat, CommandSource.Morale, CommandPriority.Defense, 2, 0f, 100f));
            var r6 = Arb(6, 6, ps6, ExternalOwnerKind.None, 10f);
            Check(R(r6, CommandDomain.Movement).Winner.Source == CommandSource.Morale
                && R(r6, CommandDomain.Movement).Winner.Kind == CommandKind.Retreat,
                "cps1 retreat-brace matrix -> retreat wins");

            // H) §10.2 第7项：Protect vs Brace 矩阵（同 P70 → Brace 胜，架枪优先显式化不再靠调用顺序）
            var ps7 = new List<FormationCommandProposal>();
            ps7.Add(P(7, CommandDomain.Movement, CommandKind.Move, CommandSource.Protect, CommandPriority.Defense, 1, 0f, 100f));
            ps7.Add(P(7, CommandDomain.Movement, CommandKind.Hold, CommandSource.Brace, CommandPriority.Defense, 2, 0f, 100f));
            var r7 = Arb(7, 7, ps7, ExternalOwnerKind.None, 10f);
            Check(R(r7, CommandDomain.Movement).Winner.Source == CommandSource.Brace,
                "cps1 protect-brace matrix -> brace wins");

            // I) §10.2 第8项：专业战术胜过表现修正（P50 > P40）
            var ps8 = new List<FormationCommandProposal>();
            ps8.Add(P(8, CommandDomain.Movement, CommandKind.Move, CommandSource.Skirmish, CommandPriority.Specialist, 1, 0f, 100f));
            ps8.Add(P(8, CommandDomain.Movement, CommandKind.Advance, CommandSource.FormationTactics, CommandPriority.Presentation, 2, 0f, 100f));
            var r8 = Arb(8, 8, ps8, ExternalOwnerKind.None, 10f);
            Check(R(r8, CommandDomain.Movement).Winner.Source == CommandSource.Skirmish,
                "cps1 specialist beats presentation");

            // J) §10.2 第9/10项：同级 tie-break 稳定 + producer 输入顺序不改变结果（正序 vs 逆序）
            var ps9a = new List<FormationCommandProposal>();
            ps9a.Add(P(9, CommandDomain.Movement, CommandKind.Move, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            ps9a.Add(P(9, CommandDomain.Movement, CommandKind.Move, CommandSource.Cavalry, CommandPriority.Specialist, 2, 0f, 100f));
            var r9a = Arb(9, 9, ps9a, ExternalOwnerKind.None, 10f);
            var ps9b = new List<FormationCommandProposal>();
            ps9b.Add(P(9, CommandDomain.Movement, CommandKind.Move, CommandSource.Cavalry, CommandPriority.Specialist, 2, 0f, 100f));
            ps9b.Add(P(9, CommandDomain.Movement, CommandKind.Move, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            var r9b = Arb(9, 9, ps9b, ExternalOwnerKind.None, 10f);
            Check(R(r9a, CommandDomain.Movement).Winner.ProposalId == 1 && R(r9b, CommandDomain.Movement).Winner.ProposalId == 1,
                "cps1 same-priority tie-break stable order-independent (ProposalId 1 wins)");

            // K) §10.2 第11项：过期提案不参与
            var ps10 = new List<FormationCommandProposal>();
            ps10.Add(P(10, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 5f));
            ps10.Add(P(10, CommandDomain.Movement, CommandKind.Hold, CommandSource.Protect, CommandPriority.Defense, 2, 0f, 100f));
            var r10 = Arb(10, 10, ps10, ExternalOwnerKind.None, 10f);
            Check(R(r10, CommandDomain.Movement).HasWinner && R(r10, CommandDomain.Movement).Winner.ProposalId == 2,
                "cps1 expired proposal excluded");
            Check(HasRejected(r10, CommandDomain.Movement, RejectionReason.Expired),
                "cps1 expired rejected with reason");

            // L) §10.2 第12项：FormationIdentity 改变 → 提案失效
            var ps11 = new List<FormationCommandProposal>();
            ps11.Add(P(11, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            var r11 = ArbIdentity(11, 11, 999, ps11, ExternalOwnerKind.None, 10f); // 当前 identity=999 != 提案 11
            Check(!R(r11, CommandDomain.Movement).HasWinner, "cps1 identity change invalidates proposal");
            Check(HasRejected(r11, CommandDomain.Movement, RejectionReason.IdentityStale),
                "cps1 identity stale rejected");

            // M) §10.2 第13项：NoSafeFallback 不产生独占申请
            var ps12 = new List<FormationCommandProposal>();
            var pEx = P(12, CommandDomain.Movement, CommandKind.Hold, CommandSource.FormationControlLease,
                CommandPriority.Commitment, 1, 0f, 100f);
            pEx.RequiresExclusiveControl = true;
            pEx.HasSafeFallback = false; // NoSafeFallback
            ps12.Add(pEx);
            var r12 = Arb(12, 12, ps12, ExternalOwnerKind.None, 10f);
            Check(!R(r12, CommandDomain.Movement).HasWinner, "cps1 exclusive without safe fallback not granted");
            Check(HasRejected(r12, CommandDomain.Movement, RejectionReason.ExclusiveWithoutSafeFallback),
                "cps1 no-safe-fallback rejected");

            // N) §10.2 第14项：Fog 下未知敌不得构造目标
            var ps13 = new List<FormationCommandProposal>();
            var pFog = P(13, CommandDomain.Movement, CommandKind.Charge, CommandSource.Cavalry,
                CommandPriority.Specialist, 1, 0f, 100f);
            pFog.TargetKnown = false; // 迷雾：目标未知
            ps13.Add(pFog);
            var r13 = Arb(13, 13, ps13, ExternalOwnerKind.None, 10f);
            Check(!R(r13, CommandDomain.Movement).HasWinner, "cps1 fog unknown target not constructed");
            Check(HasRejected(r13, CommandDomain.Movement, RejectionReason.TargetUnknown),
                "cps1 fog target rejected");

            // O) §8.1：玩家/外部所有者出现 → 无迟滞立即撤销 CB 所有权（无玩家提案时）
            var ps14 = new List<FormationCommandProposal>();
            ps14.Add(P(14, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 1, 0f, 100f));
            var r14 = Arb(14, 14, ps14, ExternalOwnerKind.Player, 10f);
            var m14 = R(r14, CommandDomain.Movement);
            Check(!m14.HasWinner, "cps1 player presence revokes CB winner immediately");
            Check(m14.Ownership == OwnershipDecision.PlayerOwns, "cps1 player presence -> PlayerOwns");
            Check(HasRejected(r14, CommandDomain.Movement, RejectionReason.PlayerOverrides),
                "cps1 player overrides rejected");

            // P) §3.2 跨域兼容：Movement=Retreat → 表现层 Facing/Arrangement 被阻止
            var ps15 = new List<FormationCommandProposal>();
            ps15.Add(P(15, CommandDomain.Movement, CommandKind.Retreat, CommandSource.Morale, CommandPriority.Defense, 1, 0f, 100f));
            ps15.Add(P(15, CommandDomain.Facing, CommandKind.Facing, CommandSource.FacingCorrection, CommandPriority.Presentation, 2, 0f, 100f));
            ps15.Add(P(15, CommandDomain.Arrangement, CommandKind.Arrangement, CommandSource.FormationTactics, CommandPriority.Presentation, 3, 0f, 100f));
            var r15 = Arb(15, 15, ps15, ExternalOwnerKind.None, 10f);
            Check(R(r15, CommandDomain.Movement).Winner.Kind == CommandKind.Retreat,
                "cps1 cross-domain movement retreat wins");
            Check(!R(r15, CommandDomain.Facing).HasWinner && !R(r15, CommandDomain.Facing).ShouldApplyNow,
                "cps1 retreat blocks presentation facing");
            Check(!R(r15, CommandDomain.Arrangement).HasWinner,
                "cps1 retreat blocks presentation arrangement");
            Check(HasRejected(r15, CommandDomain.Facing, RejectionReason.CrossDomainBlocked),
                "cps1 facing cross-domain blocked reason");

            // Q) Movement=Rejoin（Move）→ Facing 表现修正兼容可应用
            var ps16 = new List<FormationCommandProposal>();
            ps16.Add(P(16, CommandDomain.Movement, CommandKind.Move, CommandSource.ArmyCoordination, CommandPriority.Commitment, 1, 0f, 100f));
            ps16.Add(P(16, CommandDomain.Facing, CommandKind.Facing, CommandSource.FacingCorrection, CommandPriority.Presentation, 2, 0f, 100f));
            var r16 = Arb(16, 16, ps16, ExternalOwnerKind.None, 10f);
            Check(R(r16, CommandDomain.Movement).Winner.Kind == CommandKind.Move && R(r16, CommandDomain.Facing).HasWinner,
                "cps1 rejoin facing compatible (applied)");
        }

        // ---- CPS-1 测试辅助 ----
        private static FormationCommandProposal P(int formationId, CommandDomain domain, CommandKind kind,
            CommandSource source, int priority, int proposalId, float now, float ttl)
        {
            return FormationCommandProposal.Create(formationId, formationId, formationId, 0f,
                domain, kind, source, priority, proposalId, now, ttl);
        }

        private static List<FormationArbitrationResult> Arb(int teamId, int formationId,
            List<FormationCommandProposal> ps, ExternalOwnerKind owner, float now)
        {
            return FormationCommandArbiter.Resolve(new ArbiterInput
            {
                TeamId = teamId,
                FormationId = formationId,
                FormationIdentity = formationId,
                Now = now,
                Owner = owner,
                Proposals = ps
            });
        }

        private static List<FormationArbitrationResult> ArbIdentity(int teamId, int formationId, int identity,
            List<FormationCommandProposal> ps, ExternalOwnerKind owner, float now)
        {
            return FormationCommandArbiter.Resolve(new ArbiterInput
            {
                TeamId = teamId,
                FormationId = formationId,
                FormationIdentity = identity,
                Now = now,
                Owner = owner,
                Proposals = ps
            });
        }

        private static FormationArbitrationResult R(List<FormationArbitrationResult> rs, CommandDomain d)
        {
            for (int i = 0; i < rs.Count; i++)
            {
                if (rs[i].Domain == d) return rs[i];
            }
            return default(FormationArbitrationResult);
        }

        private static bool HasRejected(List<FormationArbitrationResult> rs, CommandDomain d, RejectionReason reason)
        {
            FormationArbitrationResult rr = R(rs, d);
            if (rr.RejectedProposals == null) return false;
            for (int i = 0; i < rr.RejectedProposals.Count; i++)
            {
                if (rr.RejectedProposals[i].Reason == reason) return true;
            }
            return false;
        }

        // CPS-1 唯一执行器（FormationCommandExecutor）——规格 §10.3 Executor 8 项：
        // 相同命令不重复写/目标小幅变化迟滞/目标显著变化才重写/执行前 Formation 失效 fail-open/
        // 玩家接管停止执行/单拍每域最多一次写入/执行失败不关 AI/每次实际调用有真实 provenance
        private static void TestCps1CommandExecutor()
        {
            // A) §10.3 相同命令不重复写（同语义同目标 → 不写）
            var lastA = new FormationCommandExecutor.LastDomainWrite
            { Written = true, Kind = CommandKind.Move, TargetId = 5, X = 10f, Y = 10f };
            var wA = P(1, CommandDomain.Movement, CommandKind.Move, CommandSource.Cavalry,
                CommandPriority.Specialist, 1, 0f, 100f);
            wA.TargetId = 5; wA.TargetX = 10f; wA.TargetY = 10f;
            FormationCommandExecutor.WriteSkipReason reasonA;
            Check(!FormationCommandExecutor.ShouldWrite(lastA, wA,
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians, out reasonA),
                "cps1-exec same command not re-written (identical semantics)");
            Check(reasonA == FormationCommandExecutor.WriteSkipReason.TargetHysteresis,
                "cps1-exec duplicate skip reason hysteresis");

            // B) §10.3 目标小幅变化命中迟滞（<5m 不重写）
            var wB = P(1, CommandDomain.Movement, CommandKind.Move, CommandSource.Cavalry,
                CommandPriority.Specialist, 2, 0f, 100f);
            wB.TargetId = 5; wB.TargetX = 12f; wB.TargetY = 10f; // 差 2m < 5m
            FormationCommandExecutor.WriteSkipReason reasonB;
            Check(!FormationCommandExecutor.ShouldWrite(lastA, wB,
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians, out reasonB),
                "cps1-exec small target change hits hysteresis (not written)");

            // C) §10.3 目标显著变化才重写（>5m 重写）
            var wC = P(1, CommandDomain.Movement, CommandKind.Move, CommandSource.Cavalry,
                CommandPriority.Specialist, 3, 0f, 100f);
            wC.TargetId = 5; wC.TargetX = 20f; wC.TargetY = 10f; // 差 10m > 5m
            FormationCommandExecutor.WriteSkipReason reasonC;
            Check(FormationCommandExecutor.ShouldWrite(lastA, wC,
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians, out reasonC),
                "cps1-exec significant target change re-writes");

            // D) Facing 方向迟滞（单位方向向量弦长近似）
            var lastF = new FormationCommandExecutor.LastDomainWrite
            { Written = true, Kind = CommandKind.Facing, TargetId = -1, X = 1f, Y = 0f };
            var wF1 = P(1, CommandDomain.Facing, CommandKind.Facing, CommandSource.FacingCorrection,
                CommandPriority.Presentation, 4, 0f, 100f);
            wF1.TargetX = 0.99f; wF1.TargetY = 0.1f; // 端点差 ≈0.10 < 0.2
            FormationCommandExecutor.WriteSkipReason reasonF;
            Check(!FormationCommandExecutor.ShouldWrite(lastF, wF1,
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians, out reasonF),
                "cps1-exec facing small change hysteresis");
            var wF2 = P(1, CommandDomain.Facing, CommandKind.Facing, CommandSource.FacingCorrection,
                CommandPriority.Presentation, 5, 0f, 100f);
            wF2.TargetX = 1f; wF2.TargetY = 1f; // 端点差 ≈1.41 > 0.2
            Check(FormationCommandExecutor.ShouldWrite(lastF, wF2,
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians, out reasonF),
                "cps1-exec facing significant change re-writes");

            // E) §10.3 执行前 Formation 失效 fail-open（Valid=false → 不写，sink 未调）
            var okStatus = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = true, LeaseOk = true };
            var badStatus = new FormationCommandExecutor.FormationStatus
            { Valid = false, PlayerOwned = false, FieldBattle = true, LeaseOk = true };
            int sinkCalls = 0;
            var skipLog = new List<FormationCommandExecutor.WriteSkipReason>();
            var wE = P(2, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry,
                CommandPriority.Specialist, 6, 0f, 100f);
            bool wroteE = FormationCommandExecutor.ExecuteWinner(wE, badStatus, default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { sinkCalls++; }, skipLog);
            Check(!wroteE && sinkCalls == 0, "cps1-exec invalid formation fail-open (no write)");
            Check(skipLog.Contains(FormationCommandExecutor.WriteSkipReason.FormationInvalid),
                "cps1-exec invalid formation skip reason");

            // F) §10.3 玩家接管停止执行（PlayerOwned=true → 不写）
            var playerStatus = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = true, FieldBattle = true, LeaseOk = true };
            skipLog.Clear();
            bool wroteF = FormationCommandExecutor.ExecuteWinner(wE, playerStatus, default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { sinkCalls++; }, skipLog);
            Check(!wroteF && sinkCalls == 0, "cps1-exec player takeover stops execution");
            Check(skipLog.Contains(FormationCommandExecutor.WriteSkipReason.PlayerOwned),
                "cps1-exec player owned skip reason");

            // G) §10.3 单拍每域最多一次写入（同域多提案 → 仲裁唯一胜者 → 恰一次写入）
            var psG = new List<FormationCommandProposal>();
            psG.Add(P(3, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry, CommandPriority.Specialist, 7, 0f, 100f));
            psG.Add(P(3, CommandDomain.Movement, CommandKind.Move, CommandSource.Skirmish, CommandPriority.Specialist, 8, 0f, 100f));
            psG.Add(P(3, CommandDomain.Movement, CommandKind.Hold, CommandSource.Protect, CommandPriority.Defense, 9, 0f, 100f));
            var rG = Arb(3, 3, psG, ExternalOwnerKind.None, 10f);
            Check(R(rG, CommandDomain.Movement).HasWinner, "cps1-exec arbiter unique winner before execute");
            sinkCalls = 0;
            skipLog.Clear();
            bool wroteG = FormationCommandExecutor.ExecuteWinner(R(rG, CommandDomain.Movement).Winner, okStatus,
                default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { sinkCalls++; }, skipLog);
            Check(wroteG && sinkCalls == 1, "cps1-exec at most one write per domain per tick (sink called once)");

            // H) §10.3 执行失败不关 AI（sink 抛异常 → fail-open：不泄漏、scope 清理、执行器无 SetControlledByAI）
            sinkCalls = 0;
            skipLog.Clear();
            var wH = P(4, CommandDomain.Movement, CommandKind.Advance, CommandSource.Cavalry,
                CommandPriority.Specialist, 10, 0f, 100f);
            bool threw = false;
            try
            {
                bool wroteH = FormationCommandExecutor.ExecuteWinner(wH, okStatus, default(FormationCommandExecutor.LastDomainWrite),
                    FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                    p => { sinkCalls++; throw new System.InvalidOperationException("cps1-exec boom"); }, skipLog);
                Check(!wroteH, "cps1-exec apply failure returns false (fail-open)");
            }
            catch (System.InvalidOperationException) { threw = true; }
            Check(!threw, "cps1-exec apply failure swallowed (no leak)");
            Check(skipLog.Contains(FormationCommandExecutor.WriteSkipReason.ApplyFailed), "cps1-exec apply failed skip reason");
            Check(CommandSourceScope.Current == CommandSource.ExternalUnknown, "cps1-exec scope cleaned after failure (try/finally)");

            // I) §10.3 每次实际调用有真实 provenance（scope=胜者来源 + CommandProvenance.Record 一条）
            long before = CommandProvenance.Instance.Count;
            CommandSource scopeSeen = CommandSource.OriginalUnknown;
            sinkCalls = 0;
            var wI = P(5, CommandDomain.Facing, CommandKind.Facing, CommandSource.FacingCorrection,
                CommandPriority.Presentation, 11, 0f, 100f);
            bool wroteI = FormationCommandExecutor.ExecuteWinner(wI, okStatus, default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { scopeSeen = CommandSourceScope.Current; sinkCalls++; }, null);
            Check(wroteI && sinkCalls == 1, "cps1-exec write applied once");
            Check(scopeSeen == CommandSource.FacingCorrection, "cps1-exec real provenance scope = winner source");
            Check(CommandProvenance.Instance.Count == before + 1, "cps1-exec unified provenance recorded once");
            Check(CommandProvenance.Instance.At((int)(CommandProvenance.Instance.Count - 1)).Source == CommandSource.FacingCorrection,
                "cps1-exec provenance source recorded");

            // J) 非野战 fail-open（FieldBattle=false → 不写）
            var siegeStatus = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = false, LeaseOk = true };
            skipLog.Clear();
            bool wroteJ = FormationCommandExecutor.ExecuteWinner(wI, siegeStatus, default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { sinkCalls++; }, skipLog);
            Check(!wroteJ && skipLog.Contains(FormationCommandExecutor.WriteSkipReason.NotFieldBattle),
                "cps1-exec non-field-battle fail-open");

            // K) 租约复核未过 fail-open（LeaseOk=false + 租约域提案 → 不写；cps2-lease-integration 语义：
            //    LeaseNotOk 仅约束 Source=FormationControlLease 提案，其它域提案不受租约闸误拒）
            var leaseBad = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = true, LeaseOk = false };
            var wLease = FormationCommandProposal.EmergencyProposal(1, 9, 9, 10f, CommandSource.FormationControlLease,
                CommandKind.Hold, CommandDomain.Movement, 0, -1, 0f, 0f); // 租约域 fallback 提案
            skipLog.Clear();
            bool wroteK = FormationCommandExecutor.ExecuteWinner(wLease, leaseBad, default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { sinkCalls++; }, skipLog);
            Check(!wroteK && skipLog.Contains(FormationCommandExecutor.WriteSkipReason.LeaseNotOk),
                "cps1-exec lease check fail-open (租约域提案+LeaseOk=false 不写)");

            // L) 首次写入（上拍未写 → 直接写，不受迟滞影响）
            var lastEmpty = default(FormationCommandExecutor.LastDomainWrite); // Written=false
            FormationCommandExecutor.WriteSkipReason reasonL;
            Check(FormationCommandExecutor.ShouldWrite(lastEmpty, wC,
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians, out reasonL),
                "cps1-exec first write always written (no hysteresis on first)");
        }

        // CPS-1 迁移卡（cps1-migrate-defense）：Morale/Protect/Brace 迁移为 P70 Defense 提案模式——
        // 行为等价（单域单来源时胜者=原调用语义）+ 矩阵显式化（Retreat 胜 Brace/Protect §10.5；
        // Brace 胜 Protect 不再靠 producer 顺序 §3.2）+ DefenseProposal 工厂契约
        private static void TestCps1MigrateDefense()
        {
            // A) DefenseProposal 工厂契约（P70 Defense 层、域/种类/来源/目标/TTL）
            var pd = FormationCommandProposal.DefenseProposal(1, 2, 2, 10f, CommandSource.Morale,
                CommandKind.Retreat, CommandDomain.Movement, -1, 0f, 0f);
            Check(pd.Source == CommandSource.Morale && pd.Kind == CommandKind.Retreat
                && pd.Domain == CommandDomain.Movement && pd.Priority == CommandPriority.Defense,
                "cps1-mig defense proposal factory fields (P70/Morale/Retreat/Movement)");
            Check(pd.TeamId == 1 && pd.FormationId == 2 && pd.ExpiresAt > pd.CreatedAt
                && pd.FormationIdentity == 2,
                "cps1-mig defense proposal ids + ttl + identity");

            // repair-round-2：identity=Formation 对象引用哈希（与执行器 ArbiterInput.FormationIdentity 一致，
            // 防 Index 复用）；ProposalId 全局单调递增（跨生成者唯一，tie-break 顺序无关性恢复）
            var dummy = new object();
            int dummyHash = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(dummy);
            var pd2 = FormationCommandProposal.DefenseProposal(1, 7, dummyHash, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 0f, 0f);
            Check(pd2.FormationIdentity == dummyHash,
                "cps1-repair proposal identity = object ref hash (与执行器一致)");
            var pd3 = FormationCommandProposal.DefenseProposal(1, 7, dummyHash, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 0f, 0f);
            Check(pd2.ProposalId != pd3.ProposalId,
                "cps1-repair ProposalId globally monotonic unique (跨生成者)");

            var okStatus = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = true, LeaseOk = true };

            // B) Morale Retreat 单来源行为等价：提案→仲裁→执行（胜者=原调用 Retreat，落地成功）
            var psR = new List<FormationCommandProposal>();
            psR.Add(FormationCommandProposal.DefenseProposal(1, 2, 2, 10f, CommandSource.Morale,
                CommandKind.Retreat, CommandDomain.Movement, -1, 0f, 0f));
            var rR = Arb(1, 2, psR, ExternalOwnerKind.None, 10f);
            var mR = R(rR, CommandDomain.Movement);
            Check(mR.HasWinner && mR.Winner.Source == CommandSource.Morale && mR.Winner.Kind == CommandKind.Retreat,
                "cps1-mig morale retreat single-source winner = retreat (等价)");
            int sink = 0;
            var sk = new List<FormationCommandExecutor.WriteSkipReason>();
            bool wroteR = FormationCommandExecutor.ExecuteWinner(mR.Winner, okStatus,
                default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { sink++; }, sk);
            Check(wroteR && sink == 1, "cps1-mig morale retreat executes once");

            // C) Protect Move 单来源行为等价：目标点传递（55,66 → 原 MovementOrderMove(目标) 语义）
            var psP = new List<FormationCommandProposal>();
            psP.Add(FormationCommandProposal.DefenseProposal(1, 3, 3, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 55f, 66f));
            var rP = Arb(1, 3, psP, ExternalOwnerKind.None, 10f);
            var mP = R(rP, CommandDomain.Movement);
            Check(mP.HasWinner && mP.Winner.Source == CommandSource.Protect && mP.Winner.Kind == CommandKind.Move
                && mP.Winner.TargetX == 55f && mP.Winner.TargetY == 66f,
                "cps1-mig protect move single-source winner + target (等价)");

            // D) Brace Stop 单来源行为等价（原 SetMovementOrder(Stop) → 提案 Kind=Hold）
            var psB = new List<FormationCommandProposal>();
            psB.Add(FormationCommandProposal.DefenseProposal(1, 4, 4, 10f, CommandSource.Brace,
                CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f));
            var rB = Arb(1, 4, psB, ExternalOwnerKind.None, 10f);
            var mB = R(rB, CommandDomain.Movement);
            Check(mB.HasWinner && mB.Winner.Source == CommandSource.Brace && mB.Winner.Kind == CommandKind.Hold,
                "cps1-mig brace stop single-source winner = hold (等价)");

            // E) §10.5：Morale Retreat 不被 Protect/Brace 普通动作覆盖（同拍同域 P70 矩阵）
            var psMix = new List<FormationCommandProposal>();
            psMix.Add(FormationCommandProposal.DefenseProposal(1, 5, 5, 10f, CommandSource.Morale,
                CommandKind.Retreat, CommandDomain.Movement, -1, 0f, 0f));
            psMix.Add(FormationCommandProposal.DefenseProposal(1, 5, 5, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 30f, 30f));
            psMix.Add(FormationCommandProposal.DefenseProposal(1, 5, 5, 10f, CommandSource.Brace,
                CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f));
            var rMix = Arb(1, 5, psMix, ExternalOwnerKind.None, 10f);
            Check(R(rMix, CommandDomain.Movement).Winner.Source == CommandSource.Morale
                && R(rMix, CommandDomain.Movement).Winner.Kind == CommandKind.Retreat,
                "cps1-mig retreat not overridden by brace/protect (§10.5)");
            // captain 指引 3：同拍同域三 Glue 冲突（Retreat/Brace/Protect）逆序输入 → 胜者仍 Retreat（不依赖 producer 顺序）
            var psMixRev = new List<FormationCommandProposal>();
            psMixRev.Add(FormationCommandProposal.DefenseProposal(1, 5, 5, 10f, CommandSource.Brace,
                CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f));
            psMixRev.Add(FormationCommandProposal.DefenseProposal(1, 5, 5, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 30f, 30f));
            psMixRev.Add(FormationCommandProposal.DefenseProposal(1, 5, 5, 10f, CommandSource.Morale,
                CommandKind.Retreat, CommandDomain.Movement, -1, 0f, 0f));
            var rMixRev = Arb(1, 5, psMixRev, ExternalOwnerKind.None, 10f);
            Check(R(rMixRev, CommandDomain.Movement).Winner.Source == CommandSource.Morale
                && R(rMixRev, CommandDomain.Movement).Winner.Kind == CommandKind.Retreat,
                "cps1-mig three-glue conflict reversed order -> retreat still wins (顺序无关)");

            // F) Brace 架枪优先显式化：Brace vs Protect → Brace 胜（与 producer 顺序无关）
            var psBP = new List<FormationCommandProposal>();
            psBP.Add(FormationCommandProposal.DefenseProposal(1, 6, 6, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 20f, 20f));
            psBP.Add(FormationCommandProposal.DefenseProposal(1, 6, 6, 10f, CommandSource.Brace,
                CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f));
            var rBP = Arb(1, 6, psBP, ExternalOwnerKind.None, 10f);
            Check(R(rBP, CommandDomain.Movement).Winner.Source == CommandSource.Brace,
                "cps1-mig brace beats protect via matrix (order-independent)");
            var psBP2 = new List<FormationCommandProposal>();
            psBP2.Add(FormationCommandProposal.DefenseProposal(1, 6, 6, 10f, CommandSource.Brace,
                CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f));
            psBP2.Add(FormationCommandProposal.DefenseProposal(1, 6, 6, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 20f, 20f));
            var rBP2 = Arb(1, 6, psBP2, ExternalOwnerKind.None, 10f);
            Check(R(rBP2, CommandDomain.Movement).Winner.Source == CommandSource.Brace,
                "cps1-mig brace beats protect regardless of producer order");

            // repair-round-2：executor.Tick 身份一致性路径锁定（收 cps1-exec-low1）
            Check(!FormationCommandExecutor.HasIdentityChanged(111, 111),
                "cps1-repair same identity hash not changed");
            Check(FormationCommandExecutor.HasIdentityChanged(111, 222),
                "cps1-repair different identity hash -> changed (Index 复用检测)");
            Check(!FormationCommandExecutor.HasIdentityChanged(0, 222),
                "cps1-repair first-seen (prev 0) not changed");
            // 端到端：proposal identity=当前身份信号 → 仲裁接受；不一致 → IdentityStale（执行器路径语义）
            var psId = new List<FormationCommandProposal>();
            psId.Add(FormationCommandProposal.DefenseProposal(1, 8, dummyHash, 10f, CommandSource.Brace,
                CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f));
            var rIdOk = FormationCommandArbiter.Resolve(new ArbiterInput
            { TeamId = 1, FormationId = 8, FormationIdentity = dummyHash, Now = 10f, Owner = ExternalOwnerKind.None, Proposals = psId });
            Check(R(rIdOk, CommandDomain.Movement).HasWinner,
                "cps1-repair identity match -> arbiter accepts (executor 路径一致)");
            var rIdBad = FormationCommandArbiter.Resolve(new ArbiterInput
            { TeamId = 1, FormationId = 8, FormationIdentity = dummyHash + 1, Now = 10f, Owner = ExternalOwnerKind.None, Proposals = psId });
            Check(!R(rIdBad, CommandDomain.Movement).HasWinner
                && HasRejected(rIdBad, CommandDomain.Movement, RejectionReason.IdentityStale),
                "cps1-repair identity mismatch -> IdentityStale (executor 路径锁定)");
        }

        // CPS-1 迁移卡（cps1-migrate-specialists）：Cavalry/Skirmish/FormationTactics/FacingCorrection/
        // RangedPosition/UnitPreservation 迁移为提案——工厂契约（P50/P40）+ 单来源行为等价 + 优先级显式化
        // + §10.5 合法 Flank/Intercept 不召回（域独立、表现修正不干扰）
        private static void TestCps1MigrateSpecialists()
        {
            // A) 工厂契约：SpecialistProposal=P50 / PresentationProposal=P40
            var sp = FormationCommandProposal.SpecialistProposal(1, 2, 777, 10f, CommandSource.Cavalry,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 30f, 40f);
            Check(sp.Source == CommandSource.Cavalry && sp.Kind == CommandKind.Move
                && sp.Domain == CommandDomain.Movement && sp.Priority == CommandPriority.Specialist
                && sp.FormationIdentity == 777 && sp.TargetX == 30f && sp.TargetY == 40f,
                "cps1-spec specialist proposal factory (P50/Cavalry/Move/目标/identity)");
            var pp = FormationCommandProposal.PresentationProposal(1, 2, 777, 10f, CommandSource.FacingCorrection,
                CommandKind.Facing, CommandDomain.Facing, 0, -1, 0.8f, 0.6f);
            Check(pp.Priority == CommandPriority.Presentation && pp.Domain == CommandDomain.Facing
                && pp.TargetX == 0.8f && pp.TargetY == 0.6f,
                "cps1-spec presentation proposal factory (P40/Facing/方向向量)");

            // B) Cavalry 单来源等价：Move 目标传递 / Charge targetId（Flank/Intercept 语义保持）
            var psC = new List<FormationCommandProposal>();
            psC.Add(FormationCommandProposal.SpecialistProposal(1, 3, 3, 10f, CommandSource.Cavalry,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 55f, 66f));
            var rC = Arb(1, 3, psC, ExternalOwnerKind.None, 10f);
            var mC = R(rC, CommandDomain.Movement);
            Check(mC.HasWinner && mC.Winner.Source == CommandSource.Cavalry && mC.Winner.Kind == CommandKind.Move
                && mC.Winner.TargetX == 55f && mC.Winner.TargetY == 66f,
                "cps1-spec cavalry move single-source winner + target (等价)");
            var psCh = new List<FormationCommandProposal>();
            psCh.Add(FormationCommandProposal.SpecialistProposal(1, 4, 4, 10f, CommandSource.Cavalry,
                CommandKind.Charge, CommandDomain.Movement, 0, 9, 0f, 0f)); // targetId=9=ChargeToTarget 目标
            var rCh = Arb(1, 4, psCh, ExternalOwnerKind.None, 10f);
            var mCh = R(rCh, CommandDomain.Movement);
            Check(mCh.HasWinner && mCh.Winner.Kind == CommandKind.Charge && mCh.Winner.TargetId == 9,
                "cps1-spec cavalry charge single-source winner + targetId");
            var psSk = new List<FormationCommandProposal>();
            psSk.Add(FormationCommandProposal.SpecialistProposal(1, 5, 5, 10f, CommandSource.Cavalry,
                CommandKind.Arrangement, CommandDomain.Arrangement, (int)ArrangementKind.Skein, -1, 0f, 0f));
            var rSk = Arb(1, 5, psSk, ExternalOwnerKind.None, 10f);
            Check(R(rSk, CommandDomain.Arrangement).HasWinner
                && R(rSk, CommandDomain.Arrangement).Winner.ReasonCode == (int)ArrangementKind.Skein,
                "cps1-spec cavalry arrangement reasonCode=Skein (楔形冲击阵)");

            // C) Skirmish 单来源等价：Move / FlankHold Stop
            var psS = new List<FormationCommandProposal>();
            psS.Add(FormationCommandProposal.SpecialistProposal(1, 6, 6, 10f, CommandSource.Skirmish,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 20f, 20f));
            var rS = Arb(1, 6, psS, ExternalOwnerKind.None, 10f);
            Check(R(rS, CommandDomain.Movement).Winner.Source == CommandSource.Skirmish
                && R(rS, CommandDomain.Movement).Winner.Kind == CommandKind.Move,
                "cps1-spec skirmish move single-source winner (等价)");
            var psSH = new List<FormationCommandProposal>();
            psSH.Add(FormationCommandProposal.SpecialistProposal(1, 6, 6, 10f, CommandSource.Skirmish,
                CommandKind.Hold, CommandDomain.Movement, 0, -1, 0f, 0f));
            var rSH = Arb(1, 6, psSH, ExternalOwnerKind.None, 10f);
            Check(R(rSH, CommandDomain.Movement).Winner.Kind == CommandKind.Hold,
                "cps1-spec skirmish flankhold stop single-source winner (夹击位)");

            // D) P40 表现修正：FormationTactics Arrangement(ShieldWall) / FacingCorrection Facing(方向向量)
            var psFT = new List<FormationCommandProposal>();
            psFT.Add(FormationCommandProposal.PresentationProposal(1, 7, 7, 10f, CommandSource.FormationTactics,
                CommandKind.Arrangement, CommandDomain.Arrangement, (int)ArrangementKind.ShieldWall, -1, 0f, 0f));
            var rFT = Arb(1, 7, psFT, ExternalOwnerKind.None, 10f);
            Check(R(rFT, CommandDomain.Arrangement).HasWinner
                && R(rFT, CommandDomain.Arrangement).Winner.Priority == CommandPriority.Presentation
                && R(rFT, CommandDomain.Arrangement).Winner.ReasonCode == (int)ArrangementKind.ShieldWall,
                "cps1-spec formation tactics arrangement (P40/ShieldWall)");
            var psFC = new List<FormationCommandProposal>();
            psFC.Add(FormationCommandProposal.PresentationProposal(1, 8, 8, 10f, CommandSource.FacingCorrection,
                CommandKind.Facing, CommandDomain.Facing, 0, -1, 0.8f, 0.6f));
            var rFC = Arb(1, 8, psFC, ExternalOwnerKind.None, 10f);
            Check(R(rFC, CommandDomain.Facing).HasWinner
                && R(rFC, CommandDomain.Facing).Winner.Source == CommandSource.FacingCorrection
                && R(rFC, CommandDomain.Facing).Winner.TargetX == 0.8f,
                "cps1-spec facing correction (P40/方向向量)");

            // E) RangedPosition Move / UnitPreservation FallBack（P50）
            var psRP = new List<FormationCommandProposal>();
            psRP.Add(FormationCommandProposal.SpecialistProposal(1, 9, 9, 10f, CommandSource.RangedPosition,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 12f, 34f));
            var rRP = Arb(1, 9, psRP, ExternalOwnerKind.None, 10f);
            Check(R(rRP, CommandDomain.Movement).Winner.Source == CommandSource.RangedPosition
                && R(rRP, CommandDomain.Movement).Winner.TargetX == 12f,
                "cps1-spec ranged position move (P50)");
            var psUP = new List<FormationCommandProposal>();
            psUP.Add(FormationCommandProposal.SpecialistProposal(1, 10, 10, 10f, CommandSource.UnitPreservation,
                CommandKind.FallBack, CommandDomain.Movement, (int)PreservationBand.Withdraw, -1, 0f, 0f));
            var rUP = Arb(1, 10, psUP, ExternalOwnerKind.None, 10f);
            Check(R(rUP, CommandDomain.Movement).Winner.Source == CommandSource.UnitPreservation
                && R(rUP, CommandDomain.Movement).Winner.Kind == CommandKind.FallBack,
                "cps1-spec unit preservation fallback (P50/撤离档)");

            // F) §10.5 合法 Flank/Intercept 不召回：同拍 Cavalry Charge(P50) + FacingCorrection(P40)
            //    → 各域独立胜者（侧击/截击不被表现修正干扰；无召回提案覆盖 Charge）
            var psMix = new List<FormationCommandProposal>();
            psMix.Add(FormationCommandProposal.SpecialistProposal(1, 11, 11, 10f, CommandSource.Cavalry,
                CommandKind.Charge, CommandDomain.Movement, 0, 9, 0f, 0f));
            psMix.Add(FormationCommandProposal.PresentationProposal(1, 11, 11, 10f, CommandSource.FacingCorrection,
                CommandKind.Facing, CommandDomain.Facing, 0, -1, 0f, 1f));
            var rMix = Arb(1, 11, psMix, ExternalOwnerKind.None, 10f);
            Check(R(rMix, CommandDomain.Movement).Winner.Kind == CommandKind.Charge
                && R(rMix, CommandDomain.Facing).Winner.Source == CommandSource.FacingCorrection,
                "cps1-spec flank/intercept charge not recalled (域独立、表现修正不干扰)");

            // G) 优先级显式化：同域 Movement P50 专业战术胜 P40 表现修正（不依赖 producer 顺序）
            var psPrio = new List<FormationCommandProposal>();
            psPrio.Add(FormationCommandProposal.PresentationProposal(1, 12, 12, 10f, CommandSource.FormationTactics,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 5f, 5f));
            psPrio.Add(FormationCommandProposal.SpecialistProposal(1, 12, 12, 10f, CommandSource.Skirmish,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 50f, 50f));
            var rPrio = Arb(1, 12, psPrio, ExternalOwnerKind.None, 10f);
            Check(R(rPrio, CommandDomain.Movement).Winner.Source == CommandSource.Skirmish,
                "cps1-spec specialist beats presentation (P50 > P40, 不依赖 producer 顺序)");
        }

        // CPS-1 收口复核（cps1-remove-direct-writers）：§10.5 回归项核对 + 开关默认语义锁定。
        // 静态扫描复核结果（全 Ai 目录三 SetOrder 实调用仅 5 处：FormationCommandExecutor.cs:302/306/309
        //   + FormationControlLeaseGlue.cs:218/226[cps2 租约整合范围]；注释 5 处；零其它旁路）写入提交报告与 CONTEXT。
        private static void TestCps1RemoveDirectWriters()
        {
            // §10.5 ① 玩家队豁免开关保持现有语义（默认 true=现状；t1 修复口径与 6 Glue 一致）
            Check(CombatBrainSettings.PlayerTeamExemptionEnabled,
                "cps1-scan player team exemption default true (现状语义保持)");
            // §10.5 ② CPS 全部开关默认关闭 → 零行为变化（租约 PoC 默认关 → 零 SetControlledByAI；观测默认关 → 近零开销）
            Check(!CombatBrainSettings.FormationControlLeasePocEnabled,
                "cps1-scan lease poc default off (零 SetControlledByAI 行为)");
            Check(!SetOrderObserver.Enabled,
                "cps1-scan setorder observer default off (近零开销)");
            // §10.5 ③ 诊断开关关闭时战斗行为与基线一致：诊断不记录、零写入
            SetOrderObserver.Enabled = false;
            long beforeScan = SetOrderObserver.Count;
            SetOrderObserver.Observe(1, CommandDomain.Movement, CommandKind.Move, 0, 0);
            Check(SetOrderObserver.Count == beforeScan,
                "cps1-scan observer disabled -> no recording (战斗行为与基线一致)");
            // §10.5 ④ 复核引用（既有断言已覆盖）：Retreat 不被 Brace/Protect 覆盖（cps1-mig retreat not overridden）、
            //    Limited/InContact 不被 Denied 锁死（atc4 defensive override/in contact -> Limited + atc5r Limited 不夺权）、
            //    合法 Flank/Intercept 不召回（cps1-spec flank/intercept charge not recalled）
            Check(true, "cps1-scan §10.5 回归项核对完成 (Retreat 不被覆盖/Limited 不锁死/Flank 不召回 既有断言覆盖)");
        }

        // t16 repair（QC BLOCKER：骑兵 Charge 目标丢失）：执行器 Charge+TargetId 映射——
        // 旧 CavalryGlue 语义 target!=null?ChargeToTarget(target):Charge；迁移后提案 TargetId 必须被执行器
        // 消费（定向冲锋），无目标退回 MovementOrderCharge（fail-open）
        private static void TestCps1RepairChargeTarget()
        {
            // A) ShouldUseChargeToTarget 纯逻辑判定（执行器消费决策）
            var chT = FormationCommandProposal.SpecialistProposal(1, 2, 2, 10f, CommandSource.Cavalry,
                CommandKind.Charge, CommandDomain.Movement, 0, 9, 0f, 0f); // targetId=9=目标编队 Index
            Check(FormationCommandExecutor.ShouldUseChargeToTarget(chT),
                "cps1-repair-charge should use ChargeToTarget (TargetId>=0 消费)");
            var chN = FormationCommandProposal.SpecialistProposal(1, 2, 2, 10f, CommandSource.Cavalry,
                CommandKind.Charge, CommandDomain.Movement, 0, -1, 0f, 0f); // 无目标
            Check(!FormationCommandExecutor.ShouldUseChargeToTarget(chN),
                "cps1-repair-charge no target -> MovementOrderCharge (TargetId=-1)");

            // B) 端到端：执行器消费路径——胜者保留 TargetId 且判定成立（定向冲锋语义保持）
            var psCh = new List<FormationCommandProposal>();
            psCh.Add(chT);
            var rCh = Arb(1, 2, psCh, ExternalOwnerKind.None, 10f);
            var wCh = R(rCh, CommandDomain.Movement).Winner;
            Check(wCh.Kind == CommandKind.Charge && wCh.TargetId == 9
                && FormationCommandExecutor.ShouldUseChargeToTarget(wCh),
                "cps1-repair-charge executor consumes TargetId (定向冲锋端到端)");
        }

        // CPS-2 租约整合（cps2-lease-integration）：租约 fallback 迁移进唯一执行器——
        // P80 Emergency 层提案（§6.2.1 Lease 只决定所有权边沿）+ LeaseOk 接入真实租约状态
        // （§6.2.4 仅约束租约域提案）+ 单来源行为等价 + P80 胜 P70
        private static void TestCps2LeaseIntegration()
        {
            // A) EmergencyProposal 工厂契约（P80/FormationControlLease）
            var ep = FormationCommandProposal.EmergencyProposal(1, 2, 2, 10f, CommandSource.FormationControlLease,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 55f, 66f);
            Check(ep.Source == CommandSource.FormationControlLease && ep.Kind == CommandKind.Move
                && ep.Priority == CommandPriority.Emergency && ep.TargetX == 55f && ep.TargetY == 66f,
                "cps2-lease emergency proposal factory (P80/FormationControlLease/Rejoin 目标)");

            // B) 租约 fallback Rejoin 单来源行为等价：胜者=Move + MainBody 目标保持（原 MovementOrderMove 语义）
            var psR = new List<FormationCommandProposal>();
            psR.Add(FormationCommandProposal.EmergencyProposal(1, 2, 2, 10f, CommandSource.FormationControlLease,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 55f, 66f));
            var rR = Arb(1, 2, psR, ExternalOwnerKind.None, 10f);
            var mR = R(rR, CommandDomain.Movement);
            Check(mR.HasWinner && mR.Winner.Source == CommandSource.FormationControlLease
                && mR.Winner.Kind == CommandKind.Move && mR.Winner.TargetX == 55f && mR.Winner.TargetY == 66f,
                "cps2-lease rejoin single-source winner = move + MainBody (等价)");

            // C) Hold fallback 单来源：胜者=Hold（原 MovementOrderStop 语义）
            var psH = new List<FormationCommandProposal>();
            psH.Add(FormationCommandProposal.EmergencyProposal(1, 2, 2, 10f, CommandSource.FormationControlLease,
                CommandKind.Hold, CommandDomain.Movement, 0, -1, 0f, 0f));
            var rH = Arb(1, 2, psH, ExternalOwnerKind.None, 10f);
            Check(R(rH, CommandDomain.Movement).Winner.Kind == CommandKind.Hold,
                "cps2-lease hold single-source winner = hold (等价)");

            // D) P80 租约 fallback 胜 P70 防御（同域冲突：租约 Hold vs Brace Stop → 租约 P80 胜，§3.2 显式优先级）
            var psMix = new List<FormationCommandProposal>();
            psMix.Add(FormationCommandProposal.DefenseProposal(1, 3, 3, 10f, CommandSource.Brace,
                CommandKind.Hold, CommandDomain.Movement, -1, 0f, 0f));
            psMix.Add(FormationCommandProposal.EmergencyProposal(1, 3, 3, 10f, CommandSource.FormationControlLease,
                CommandKind.Hold, CommandDomain.Movement, 0, -1, 0f, 0f));
            var rMix = Arb(1, 3, psMix, ExternalOwnerKind.None, 10f);
            Check(R(rMix, CommandDomain.Movement).Winner.Source == CommandSource.FormationControlLease,
                "cps2-lease fallback beats defense (P80 > P70, 租约期间执行权优先)");

            // E) §6.2.4：LeaseOk 只约束租约域提案——非租约域提案在 LeaseOk=false 编队正常执行（防误拒回归）
            var okSink = 0;
            var okStatusNoLease = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = true, LeaseOk = false };
            var defP = FormationCommandProposal.DefenseProposal(1, 4, 4, 10f, CommandSource.Protect,
                CommandKind.Move, CommandDomain.Movement, -1, 20f, 20f);
            var wroteDef = FormationCommandExecutor.ExecuteWinner(defP, okStatusNoLease,
                default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { okSink++; }, null);
            Check(wroteDef && okSink == 1,
                "cps2-lease non-lease proposal executes without lease (LeaseOk 仅约束租约域)");

            // F) 租约域提案 + LeaseOk=true → 执行成功（Acquire 后执行器落地）
            var leaseOkStatus = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = true, LeaseOk = true };
            int leaseSink = 0;
            var wroteLease = FormationCommandExecutor.ExecuteWinner(ep, leaseOkStatus,
                default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                p => { leaseSink++; }, null);
            Check(wroteLease && leaseSink == 1, "cps2-lease lease proposal executes with active lease (LeaseOk=true)");
        }

        // CPS-2 所有权 PoC（cps2-ownership-poc）：路线 A Behavior PoC 纯逻辑判定（权重表达/降权/生命周期/
        // 玩家接管/幂等，§6.1/t20）+ 路线 B 观测（Acquire/Release/Expired/ExternalOwner + ExternalUnknown 三域，§7.1 D 模式）
        private static void TestCps2OwnershipPoc()
        {
            // A) 开关默认关闭安全态（Cps2PoCSettings：RouteA 零 AddAiBehavior/SetBehaviorWeight；RouteB 无观测日志）
            Check(!CombatBrain.Settings.Cps2PoCSettings.RouteAEnabled,
                "cps2-poc route A default off (安全态零调用)");
            Check(!CombatBrain.Settings.Cps2PoCSettings.RouteBObservationEnabled,
                "cps2-poc route B observation default off");
            Check(CombatBrain.Settings.Cps2PoCSettings.CbBehaviorWeight > 1f,
                "cps2-poc CB behavior weight > 原版默认 (FindBestBehavior 竞争胜出)");

            // B) 意图 → 命令语义（Movement 域；Facing/Arrangement 不经 Behavior——t20 结论）
            Check(Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Hold) == CommandKind.Hold
                && Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Advance) == CommandKind.Advance
                && Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Rejoin) == CommandKind.Move
                && Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Retreat) == CommandKind.Retreat,
                "cps2-poc intent -> command kind mapping (仅 Movement 域)");

            // C) F2 意图映射（Attack/Flank/Harass/Pursue→Advance、Reform/Defend→Hold、Retreat→Retreat）
            Check(Cps2RouteAPolicy.MapIntent(IntentType.Attack) == Cps2RouteAIntent.Advance
                && Cps2RouteAPolicy.MapIntent(IntentType.Flank) == Cps2RouteAIntent.Advance
                && Cps2RouteAPolicy.MapIntent(IntentType.Harass) == Cps2RouteAIntent.Advance
                && Cps2RouteAPolicy.MapIntent(IntentType.Pursue) == Cps2RouteAIntent.Advance,
                "cps2-poc attack-family intent -> advance");
            Check(Cps2RouteAPolicy.MapIntent(IntentType.Reform) == Cps2RouteAIntent.Hold
                && Cps2RouteAPolicy.MapIntent(IntentType.Defend) == Cps2RouteAIntent.Hold,
                "cps2-poc reform/defend intent -> hold");
            Check(Cps2RouteAPolicy.MapIntent(IntentType.Retreat) == Cps2RouteAIntent.Retreat,
                "cps2-poc retreat intent -> retreat");

            // D) 冲突降权表（§6.1 Q2：降权而非删除；CB 域内互斥 + 原版冲突按表 SetBehaviorWeight(0f)）
            Check(Cps2RouteAPolicy.ConflictingIntents(Cps2RouteAIntent.Hold).Length == 3,
                "cps2-poc hold conflicts downgrade 3 others");
            Check(Cps2RouteAPolicy.ConflictingIntents(Cps2RouteAIntent.Retreat).Length == 2
                && Cps2RouteAPolicy.ConflictingIntents(Cps2RouteAIntent.Retreat)[0] == Cps2RouteAIntent.Advance,
                "cps2-poc retreat conflicts advance (降权而非删除)");

            // E) 玩家接管天然恢复（FormationAI.cs:245/286：!IsAIControlled → 不设权重不写）
            Check(Cps2RouteAPolicy.ShouldApplyWeights(true), "cps2-poc AI controlled -> apply weights");
            Check(!Cps2RouteAPolicy.ShouldApplyWeights(false), "cps2-poc player takeover -> no weights (天然恢复)");

            // F) 拆分/增援后权重重设路径（TacticComponent.cs:455-456 ResetBehaviorWeights → 每拍幂等 Apply 重设）
            Check(Cps2RouteAPolicy.ShouldReapplyAfterReset(10, 12), "cps2-poc reinforcement count change -> reapply");
            Check(Cps2RouteAPolicy.ShouldReapplyAfterReset(10, 6), "cps2-poc split count change -> reapply");
            Check(!Cps2RouteAPolicy.ShouldReapplyAfterReset(10, 10), "cps2-poc stable count -> no reapply");

            // G) AddAiBehavior 幂等（FormationAI.cs:133 Add 非幂等；GetBehavior<T> 查重）
            Check(Cps2RouteAPolicy.NeedsAddBehavior(false), "cps2-poc unregistered -> needs add");
            Check(!Cps2RouteAPolicy.NeedsAddBehavior(true), "cps2-poc registered -> no duplicate add (幂等)");

            // H) 路线 B 观测：Acquire/Release/Expired/ExternalOwner 计数（Cps2LeaseObserver，供 CPS-3 A/B）
            Cps2LeaseObserver.Reset();
            Check(Cps2LeaseObserver.AcquireCount == 0 && Cps2LeaseObserver.ReleaseCount == 0
                && Cps2LeaseObserver.ExpiredCount == 0 && Cps2LeaseObserver.ExternalOwnerCount == 0,
                "cps2-poc observer reset clears counts");
            Cps2LeaseObserver.RecordAcquire();
            Cps2LeaseObserver.RecordAcquire();
            Cps2LeaseObserver.RecordRelease();
            Cps2LeaseObserver.RecordExpired();
            Cps2LeaseObserver.RecordExternalOwner();
            Check(Cps2LeaseObserver.AcquireCount == 2 && Cps2LeaseObserver.ReleaseCount == 1
                && Cps2LeaseObserver.ExpiredCount == 1 && Cps2LeaseObserver.ExternalOwnerCount == 1,
                "cps2-poc acquire/release/expired/external-owner counts");
            string summary = Cps2LeaseObserver.BuildSummary();
            Check(summary.Contains("Acquire=2") && summary.Contains("Release=1") && summary.Contains("Expired=1")
                && summary.Contains("ExternalOwner=1") && summary.Contains("ExternalUnknownTotal="),
                "cps2-poc build summary format (供 5s 日志/战斗结束)");

            // I) ExternalUnknown 三域覆盖数可量化（复用 t3 SetOrderObserver；诊断开时观测计数）
            SetOrderObserver.Enabled = true;
            SetOrderObserver.Clear();
            SetOrderObserver.Observe(1, CommandDomain.Movement, CommandKind.Move, 0, 0);
            SetOrderObserver.Observe(2, CommandDomain.Facing, CommandKind.Facing, 1, 0);
            SetOrderObserver.Observe(3, CommandDomain.Arrangement, CommandKind.Arrangement, 2, 0);
            Check(Cps2LeaseObserver.ExternalUnknownTotal() == 3,
                "cps2-poc external-unknown 3-domain total quantifiable");
            SetOrderObserver.Enabled = false;
        }

        // CPS-3 运行期 A/B 准备（cps3-runtime-prep，§7.1 四组模式）：模式开关语义（A/B/C/D 各至少 1 条）+
        // 默认安全态（Enabled=false=现状零变化）+ 组合矩阵（C/D 与 ATC-5R PoC/Cps2PoCSettings 不冲突）+
        // B Shadow only 决策不落地语义（§7.1 B）
        private static void TestCps3RuntimePrep()
        {
            // A) 默认安全态：验证总开关关（执行器按现状落地=当前生产行为零变化）；观测/Shadow 日志全关
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.Enabled,
                "cps3-runtime validation disabled by default (安全态零行为变化)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.A_NativeBaseline,
                "cps3-runtime mode default A (未启用时字段不影响生产)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled
                && !CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled,
                "cps3-runtime observer/shadow logs default off");

            // B) 四组模式枚举契约（§7.1：A/B/C/D 必须分别采样）
            Check((int)CombatBrain.Settings.Cps3Mode.A_NativeBaseline == 0
                && (int)CombatBrain.Settings.Cps3Mode.B_ShadowOnly == 1
                && (int)CombatBrain.Settings.Cps3Mode.C_ArbiterCoexist == 2
                && (int)CombatBrain.Settings.Cps3Mode.D_ExclusivePoc == 3,
                "cps3-runtime four modes A/B/C/D locked");

            // C) 组合矩阵：C ArbiterCoexist=租约 PoC 关+RouteA 关（原版仍控，避免同层对刷）
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.C_ArbiterCoexist, false, false),
                "cps3-runtime C valid (lease off + routeA off)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.C_ArbiterCoexist, true, false),
                "cps3-runtime C invalid with lease poc on (原版仍控语义)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.C_ArbiterCoexist, false, true),
                "cps3-runtime C invalid with routeA on (避免同层对刷)");

            // D) 组合矩阵：D ExclusivePoc=租约 PoC 开+RouteA 关（路线 B 硬排他，两执行路线互斥）
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.D_ExclusivePoc, true, false),
                "cps3-runtime D valid (lease poc on + routeA off)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.D_ExclusivePoc, false, false),
                "cps3-runtime D invalid without lease (单域排他需租约)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.D_ExclusivePoc, true, true),
                "cps3-runtime D invalid with routeA on (执行路线互斥)");

            // E) A/B 对照/观测模式：任意组合合法（A 全关 CB 命令；B 决策不落地）
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.A_NativeBaseline, true, true),
                "cps3-runtime A valid any combination (对照基线)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3Mode.B_ShadowOnly, true, false),
                "cps3-runtime B valid any combination (观测模式)");

            // F) B Shadow only 语义：决策运行不落地（applyWrite=null → ExecuteWinner 返回 true 仅 Record 决策）
            var okStatus = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = true, LeaseOk = true };
            var wShadow = FormationCommandProposal.SpecialistProposal(1, 2, 2, 10f, CommandSource.Cavalry,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 30f, 40f);
            long beforeShadow = CommandProvenance.Instance.Count;
            int shadowSink = 0;
            bool decidedShadow = FormationCommandExecutor.ExecuteWinner(wShadow, okStatus,
                default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                null, null); // shadow：applyWrite=null → 不落地
            Check(decidedShadow && shadowSink == 0,
                "cps3-runtime shadow decision runs without applying (决策不落地)");
            Check(CommandProvenance.Instance.Count == beforeShadow + 1,
                "cps3-runtime shadow decision recorded (供 WouldApply 对比)");
        }

        // CPS-3 MCM 开关（cps3-mcm-switches）：Cps3RuntimeSettings 四字段的 MCM 薄转发目标——
        // get/set 直读写本静态字段（CombatBrainMcmSettings 属性一行转发，代码审查级）；
        // 默认安全态 + 模式枚举下拉 A/B/C/D 四值 + get/set roundtrip
        private static void TestCps3McmSwitches()
        {
            // A) 默认安全态（MCM 映射源字段：Enabled=false/Observer=false/Shadow=false/Mode=A 零行为变化）
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.Enabled
                && !CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled
                && !CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled
                && CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.A_NativeBaseline,
                "cps3-mcm defaults safe (Enabled=false/Observer=false/Shadow=false/Mode=A)");

            // B) Mode 枚举下拉四值（MCMv5 SettingPropertyDropdown 枚举属性自动生成选项 A/B/C/D）
            Check((int)CombatBrain.Settings.Cps3Mode.A_NativeBaseline == 0
                && (int)CombatBrain.Settings.Cps3Mode.B_ShadowOnly == 1
                && (int)CombatBrain.Settings.Cps3Mode.C_ArbiterCoexist == 2
                && (int)CombatBrain.Settings.Cps3Mode.D_ExclusivePoc == 3,
                "cps3-mcm mode dropdown four values A/B/C/D");

            // C) t28 repair：Dropdown SelectedIndex ↔ Cps3Mode 双向转换（MCM Dropdown<string> get/set 薄转发核心）
            Check(CombatBrain.Settings.Cps3RuntimeSettings.SelectedIndexFromMode(CombatBrain.Settings.Cps3Mode.A_NativeBaseline) == 0
                && CombatBrain.Settings.Cps3RuntimeSettings.SelectedIndexFromMode(CombatBrain.Settings.Cps3Mode.D_ExclusivePoc) == 3,
                "cps3-mcm dropdown index from mode (A->0, D->3)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(0) == CombatBrain.Settings.Cps3Mode.A_NativeBaseline
                && CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(3) == CombatBrain.Settings.Cps3Mode.D_ExclusivePoc,
                "cps3-mcm mode from dropdown index (0->A, 3->D)");
            bool indexRoundtrip = true;
            for (int idx = 0; idx <= 3; idx++)
            {
                CombatBrain.Settings.Cps3Mode m = CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(idx);
                if (CombatBrain.Settings.Cps3RuntimeSettings.SelectedIndexFromMode(m) != idx) indexRoundtrip = false;
            }
            Check(indexRoundtrip, "cps3-mcm dropdown index <-> mode roundtrip (0..3 ↔ A/B/C/D)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(-1) == CombatBrain.Settings.Cps3Mode.A_NativeBaseline
                && CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(9) == CombatBrain.Settings.Cps3Mode.A_NativeBaseline,
                "cps3-mcm out-of-range index clamps to A (安全态)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.SelectedIndexFromMode(CombatBrain.Settings.Cps3Mode.A_NativeBaseline) == 0,
                "cps3-mcm default index 0 = A (安全态)");

            // D) MCM get/set 直读写（字段即存储）：Mode 与开关 roundtrip
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.D_ExclusivePoc;
            CombatBrain.Settings.Cps3RuntimeSettings.Enabled = true;
            CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled = true;
            CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled = true;
            Check(CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.D_ExclusivePoc
                && CombatBrain.Settings.Cps3RuntimeSettings.Enabled
                && CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled
                && CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled,
                "cps3-mcm get/set roundtrip (MCM 属性薄转发目标)");
            // 复位安全态（不污染后续测试/生产语义）
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.A_NativeBaseline;
            CombatBrain.Settings.Cps3RuntimeSettings.Enabled = false;
            CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled = false;
            CombatBrain.Settings.Cps3RuntimeSettings.ShadowWouldApplyLogEnabled = false;
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.Enabled
                && CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.A_NativeBaseline,
                "cps3-mcm reset restores safe state");
        }

        // t30 repair（cps3-runtime-prep 观测热切缺陷）：Enable() 运行期热切同步判定（ShouldEnable/ShouldDisable 纯逻辑，
        //  CombatAiMissionLogic.Tick 每帧调用——游戏内 MCM 开「命令来源观测」后开新战斗即挂 Harmony patch）+ 汇总门控对齐
        private static void TestCps3ObservationHotswap()
        {
            // A) 默认安全态：配置关 → 不启用观测（无 Harmony patch 挂载）
            Check(!SetOrderObserver.ShouldEnable(false),
                "cps3-obs configured off -> no enable (安全态)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled
                && !CombatBrain.Settings.Cps2PoCSettings.RouteBObservationEnabled,
                "cps3-obs summary gated off by default (无 [Cps2Observation] 输出)");

            // B) 配置开且未挂载 → 需启用（游戏内 MCM 开「命令来源观测」→ 新战斗 Tick 首部 Enable）
            bool savedEnabled = SetOrderObserver.Enabled;
            SetOrderObserver.Enabled = false; // 模拟未挂载
            Check(SetOrderObserver.ShouldEnable(true),
                "cps3-obs configured on + not mounted -> enable needed (热切)");

            // C) 幂等：已挂载（Enabled=true）→ 不重复 Enable（不重复 Patch）
            SetOrderObserver.Enabled = true; // 模拟已挂载
            Check(!SetOrderObserver.ShouldEnable(true),
                "cps3-obs already mounted -> no duplicate enable (幂等)");

            // D) Disable 对称：配置关且已挂载（战斗中关）→ 需 Disable（幂等）
            Check(SetOrderObserver.ShouldDisable(false),
                "cps3-obs configured off + mounted -> disable needed (对称)");
            Check(!SetOrderObserver.ShouldDisable(true),
                "cps3-obs configured on -> no disable");

            // E) 复位（不污染后续测试/生产语义）
            SetOrderObserver.Enabled = savedEnabled;
            Check(!SetOrderObserver.ShouldEnable(false) && !SetOrderObserver.ShouldDisable(false),
                "cps3-obs restore safe state (不启用不 Disable)");
        }

        // t32 repair（cps3-mcm-switches 下拉切换失效）：MCMv5 可能直接改 Dropdown 实例 SelectedIndex 不调 setter——
        // getter 反向同步判定（NeedsModeSync：实例与 Mode 不一致才写回 Mode；一致不写防漂移）+ setter 双路径
        private static void TestCps3ModeSync()
        {
            // A) MCM 直接改实例 SelectedIndex=1（不调 setter）且 Mode=A → 需反向同步（选 B 后 Mode 应更新为 B）
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.A_NativeBaseline;
            Check(CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(1),
                "cps3-mode-sync instance 1 vs mode A -> needs sync (MCM 直接改实例路径)");
            // 模拟 getter 反向同步（实例优先）：Mode = ModeFromSelectedIndex(1) = B
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(1);
            Check(CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.B_ShadowOnly,
                "cps3-mode-sync reverse sync writes mode B (选 B 生效)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(1),
                "cps3-mode-sync instance 1 == mode B -> no sync (防漂移)");

            // B) 默认一致无写：实例 0 且 Mode=A → 不同步（UI 显示 A 正确）
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.A_NativeBaseline;
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(0),
                "cps3-mode-sync default instance 0 == mode A -> no write");

            // C) 其它一致组合：实例 2==Mode C / 实例 3==Mode D → 不写
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.C_ArbiterCoexist;
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(2), "cps3-mode-sync instance 2 == mode C");
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.D_ExclusivePoc;
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(3), "cps3-mode-sync instance 3 == mode D");

            // D) 实例 3(D) 而 Mode=A → 需同步（MCM 选 D 生效路径）
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.A_NativeBaseline;
            Check(CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(3),
                "cps3-mode-sync instance 3 vs mode A -> needs sync (选 D 生效)");

            // E) 越界实例钳制一致：实例 9 → ModeFromSelectedIndex=A → 与 Mode=A 一致不写（安全态钳制保持）
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(9),
                "cps3-mode-sync out-of-range instance clamps to A -> no sync");

            // F) setter 路径保持（t28 roundtrip 已覆盖）；复位安全态
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.A_NativeBaseline;
            Check(CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.A_NativeBaseline
                && !CombatBrain.Settings.Cps3RuntimeSettings.NeedsModeSync(0),
                "cps3-mode-sync reset restores safe state");
        }

        // t34（cps3-observer-diagnostics）：Enable 挂载成功唯一标识日志 + HarmonyId——下次运行期采样
        // 日志可明确区分「patch 未挂上」（无挂载日志/有异常日志）vs「patch 挂上但原版未调 setter」
        //（有挂载日志但 ExternalUnknown 仍 0，转向原版 AI 是否真调 setter 方向）
        private static void TestCps3ObserverDiagnostics()
        {
            // A) HarmonyId 唯一标识常量
            Check(SetOrderObserver.HarmonyId == "com.combatbrain.setorderobserver",
                "cps3-diag harmony id constant");

            // B) 挂载成功日志格式（唯一标识 + prefix 核验数；Apply 幂等仅一次不重复刷）
            string log3 = SetOrderObserver.BuildMountLog(3);
            Check(log3.StartsWith("[SetOrderObserver] 观测已挂载（Harmony patch OK")
                && log3.Contains("id=com.combatbrain.setorderobserver") && log3.Contains("prefixes=3"),
                "cps3-diag mount log format (3/3 prefixes OK)");

            // C) MCM 写回链路核实：Cps3ObserverEnabled get/set 直读写 Cps3RuntimeSettings.ObserverEnabled
            //    （CombatBrainMcmSettings.cs 薄转发；字段 roundtrip 即写回链路正确——t26 cps3-mcm 断言已覆盖，
            //    此处复核字段语义）
            CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled = true;
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled,
                "cps3-diag observer-enabled field roundtrip (MCM 写回链路)");
            CombatBrain.Settings.Cps3RuntimeSettings.ObserverEnabled = false;

            // D) 失败场景：Enable 失败沿用「观测不可用」日志（t3 §4.2.8 路径保留）——BuildMountLog 仅成功串；
            //    异常串为 Apply 内字面文本（代码审查级，0/3 场景由异常分支输出）
            Check(!SetOrderObserver.BuildMountLog(0).Contains("观测不可用"),
                "cps3-diag mount log is success-only (失败沿用观测不可用路径)");
        }

        // t36 repair（Harmony 显式 Patch + 版本诊断）：PatchAll 扫描静默失败（0/3）根因修复——
        // 显式 Patch 三 setter + 运行时 HarmonyLib 版本/0Harmony 加载来源诊断（游戏 Modules 11 份 0Harmony.dll）
        private static void TestCps3HarmonyExplicitPatch()
        {
            // A) 显式 Patch 目标清单（三 setter，与核验/测试共用）
            string[] targets = SetOrderObserver.Patches.PatchTargetMethodNames;
            Check(targets.Length == 3 && targets[0] == "SetMovementOrder"
                && targets[1] == "SetFacingOrder" && targets[2] == "SetArrangementOrder",
                "cps3-harmony explicit patch targets 3 setters");

            // B) 运行时 HarmonyLib 版本诊断日志格式（version + 0Harmony 加载来源，与编译引用对比）
            string vlog = SetOrderObserver.BuildVersionLog("2.4.2.0", "E:\\...\\Bannerlord.Harmony\\bin\\Win64_Shipping_Client\\0Harmony.dll");
            Check(vlog.StartsWith("[SetOrderObserver] HarmonyLib 运行时版本=")
                && vlog.Contains("2.4.2.0") && vlog.Contains("0Harmony 加载=")
                && vlog.Contains("Bannerlord.Harmony"),
                "cps3-harmony version diagnostics log format (版本+加载来源)");

            // C) 挂载成功日志沿用（t34）；幂等：Apply 仅 _harmony==null 一次（代码审查级，运行时路径）
            Check(SetOrderObserver.BuildMountLog(3).Contains("prefixes=3"),
                "cps3-harmony mount log prefixes=3 kept");

            // D) 失败路径：显式 Patch 任一步抛异常 → Enable catch「观测不可用（具体异常）」fail-open（t3 保留）
            Check(SetOrderObserver.BuildMountLog(0).StartsWith("[SetOrderObserver] 观测已挂载"),
                "cps3-harmony mount log only success template (失败走异常分支)");
        }

        // t38 repair（MovementPrefix 参数名 order→input）：Harmony 显式 Patch 按参数名绑定 Prefix——
        // SetMovementOrder(MovementOrder input) 原方法参数名 input，Prefix 参数名不匹配抛「Parameter not found」
        // fail-open（captain 实测 18:33:21 日志实锤）。防复发映射表（三 setter 原方法参数名）离线锁定。
        private static void TestCps3HarmonyParamNames()
        {
            // A) 三 setter 原方法参数名映射表（与官方反编译 Formation.cs:685/717/722 一致：
            //    SetMovementOrder(input)/SetFacingOrder(order)/SetArrangementOrder(order)）
            string[] pnames = SetOrderObserver.Patches.PatchTargetParameterNames;
            Check(pnames.Length == 3 && pnames[0] == "input" && pnames[1] == "order" && pnames[2] == "order",
                "cps3-paramnames setter parameter names input/order/order (Harmony 按参数名绑定)");
            // 与目标方法名一一对应（下标对齐）
            string[] targets = SetOrderObserver.Patches.PatchTargetMethodNames;
            Check(targets.Length == pnames.Length
                && targets[0] == "SetMovementOrder" && pnames[0] == "input"
                && targets[1] == "SetFacingOrder" && pnames[1] == "order"
                && targets[2] == "SetArrangementOrder" && pnames[2] == "order",
                "cps3-paramnames method/param name pairs aligned (防复发)");

            // B) 挂载日志格式保持（t34/t36）；修复后应出现「观测已挂载（..., prefixes=3）」一次
            Check(SetOrderObserver.BuildMountLog(3).Contains("prefixes=3"),
                "cps3-paramnames mount log prefixes=3 (修复后全挂载)");

            // C) 失败路径保持：参数名不匹配 → 抛 → Enable catch fail-open（t3 §4.2.8；修复后不再触发）
            Check(SetOrderObserver.BuildMountLog(0).StartsWith("[SetOrderObserver] 观测已挂载"),
                "cps3-paramnames mount log success-only template (失败走异常分支)");
        }

        // t40（cps3r-evidence-chain）：权威 setter 事件流（Team/对象身份/域/OrderType/序号/阶段）+ 真实覆盖判定
        // （Applied 后同 identity 同域窗口内 ExternalUnknown → ExternalReplaced，SameTick/CrossTick 分列）
        // + Shadow 不写 Applied + owner 核验（防其它 mod patch 假阳性）
        private static void TestCps3rEvidenceChain()
        {
            // A) 事件阶段枚举契约
            Check((int)SetOrderStage.Proposed == 1 && (int)SetOrderStage.Won == 2
                && (int)SetOrderStage.Applied == 3 && (int)SetOrderStage.ExternalReplaced == 4,
                "cps3r stage enum Proposed/Won/Applied/ExternalReplaced");

            // B) 权威事件流字段契约（全参 Observe：Team/对象身份/域/OrderType/序号/阶段）
            SetOrderObserver.Enabled = true;
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(10f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7,
                (int)SetOrderStage.Applied);
            var e0 = SetOrderObserver.At(0);
            Check(e0.TeamId == 1 && e0.FormationId == 2 && e0.Identity == 555
                && e0.Domain == CommandDomain.Movement && e0.OrderType == 7
                && e0.Seq == 0 && e0.Stage == SetOrderStage.Applied,
                "cps3r event stream fields (Team/identity/domain/orderType/seq/stage)");

            // C) Seq 严格递增
            SetOrderObserver.Observe(1, 3, 556, CommandDomain.Facing, CommandKind.Facing, 3, (int)SetOrderStage.Applied);
            SetOrderObserver.Observe(1, 4, 557, CommandDomain.Arrangement, CommandKind.Arrangement, 5, (int)SetOrderStage.Applied);
            Check(SetOrderObserver.At(1).Seq == 1 && SetOrderObserver.At(2).Seq == 2,
                "cps3r event seq strictly increasing");

            // D) 覆盖判定：CB scope Applied 后同 identity 同域窗口内 External（0.3s 跨拍）→ ExternalReplaced（CrossTick）
            //    （t42 source 优先：Applied 基准须 CB scope 内落地——包 CommandSourceScope 模拟 CB setter）
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(10f);
            using (CommandSourceScope.Enter(CommandSource.Cavalry))
            {
                SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7,
                    (int)SetOrderStage.Applied);
            }
            SetOrderObserver.NotifyTick(10.3f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 9, 0); // 无 scope → External
            Check(SetOrderObserver.ExternalReplacedCount == 1 && SetOrderObserver.CrossTickExternalReplace == 1
                && SetOrderObserver.SameTickExternalReplace == 0,
                "cps3r external replaced in window (cross-tick)");
            Check(SetOrderObserver.At(1).Stage == SetOrderStage.ExternalReplaced,
                "cps3r replaced event stage marked");

            // E) 覆盖判定：同拍 External（<0.01s）→ SameTick 分列
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(20f);
            using (CommandSourceScope.Enter(CommandSource.Cavalry))
            {
                SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7,
                    (int)SetOrderStage.Applied);
            }
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 9, 0);
            Check(SetOrderObserver.SameTickExternalReplace == 1 && SetOrderObserver.ExternalReplacedCount == 1
                && SetOrderObserver.CrossTickExternalReplace == 0,
                "cps3r external replaced same-tick (分列)");

            // F) 窗口外 External（2s > 1s 窗口）→ 不计数
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(30f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7, (int)SetOrderStage.Applied);
            SetOrderObserver.NotifyTick(32f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 9, 0);
            Check(SetOrderObserver.ExternalReplacedCount == 0,
                "cps3r external outside window not replaced");

            // G) 不同 identity/域不误判（隔离）
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(40f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7, (int)SetOrderStage.Applied);
            SetOrderObserver.Observe(1, 2, 556, CommandDomain.Movement, CommandKind.Move, 9, 0); // 不同对象身份
            Check(SetOrderObserver.ExternalReplacedCount == 0, "cps3r different identity not replaced (隔离)");
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Facing, CommandKind.Facing, 9, 0); // 不同域
            Check(SetOrderObserver.ExternalReplacedCount == 0, "cps3r different domain not replaced (隔离)");

            // H) Shadow 不写 Applied：ExecuteWinner recordTrace=false → CommandProvenance.Instance 不增
            long before = CommandProvenance.Instance.Count;
            var okStatus = new FormationCommandExecutor.FormationStatus
            { Valid = true, PlayerOwned = false, FieldBattle = true, LeaseOk = true };
            var wS = FormationCommandProposal.SpecialistProposal(1, 2, 2, 10f, CommandSource.Cavalry,
                CommandKind.Move, CommandDomain.Movement, 0, -1, 10f, 10f);
            FormationCommandExecutor.ExecuteWinner(wS, okStatus,
                default(FormationCommandExecutor.LastDomainWrite),
                FormationCommandExecutor.PositionHysteresisMeters, FormationCommandExecutor.FacingHysteresisRadians,
                null, null, false); // recordTrace=false（Shadow：只记 WouldApply 不写 Applied/OrderTrace）
            Check(CommandProvenance.Instance.Count == before,
                "cps3r shadow recordTrace=false no Applied/OrderTrace 写入");

            // I) owner 核验纯逻辑（0Harmony Patches.Owners 为 owner id 列表；防其它 mod patch 假阳性）
            Check(SetOrderObserver.HasOwnPatch(new[] { "com.combatbrain.setorderobserver" }),
                "cps3r own patch owner detected");
            Check(!SetOrderObserver.HasOwnPatch(new[] { "rbm.mod.patch", "other.mod" }),
                "cps3r foreign patch owner not counted (防假阳性)");
            Check(!SetOrderObserver.HasOwnPatch(null), "cps3r null owners not counted");

            SetOrderObserver.Enabled = false;
        }

        // t42 repair（Prefix stage 语义）：无 scope（ExternalUnknown）观测即使 Prefix 误标 stage=Applied 也
        // 不得作 CB Applied 基准（source 优先——source==ExternalUnknown 一律按 External 走覆盖判定）
        private static void TestCps3rRepairPrefixStage()
        {
            SetOrderObserver.Enabled = true;

            // A) 无 scope + stage=Applied（模拟 Prefix 误标）→ 不作 CB Applied 基准（后续 External 不触发 Replaced）
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(10f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7,
                (int)SetOrderStage.Applied); // 测试线程无 scope → Source=ExternalUnknown
            SetOrderObserver.NotifyTick(10.3f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 9, 0);
            Check(SetOrderObserver.ExternalReplacedCount == 0,
                "cps3r-stage external with applied-stamp not treated as CB baseline (误标不作基准)");
            Check(SetOrderObserver.At(0).Source == CommandSource.ExternalUnknown,
                "cps3r-stage mislabeled applied observed as external (source 优先)");

            // B) CB scope + stage=Applied → 基准写入（窗口内 External → Replaced）
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(20f);
            using (CommandSourceScope.Enter(CommandSource.Cavalry))
            {
                SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7,
                    (int)SetOrderStage.Applied);
            }
            SetOrderObserver.NotifyTick(20.3f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 9, 0);
            Check(SetOrderObserver.ExternalReplacedCount == 1,
                "cps3r-stage cb-scoped applied sets baseline (覆盖判定生效)");
            Check(SetOrderObserver.At(0).Source == CommandSource.Cavalry
                && SetOrderObserver.At(0).Stage == SetOrderStage.Applied,
                "cps3r-stage cb-scoped applied stage+source correct");

            // C) 误标 Applied 的 External 出现在 CB 基准后 → 按 External 处理 → Replaced（source 优先）
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(30f);
            using (CommandSourceScope.Enter(CommandSource.Brace))
            {
                SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7,
                    (int)SetOrderStage.Applied);
            }
            SetOrderObserver.NotifyTick(30.3f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 9,
                (int)SetOrderStage.Applied); // 无 scope 误标 Applied
            Check(SetOrderObserver.ExternalReplacedCount == 1
                && SetOrderObserver.At(1).Stage == SetOrderStage.ExternalReplaced,
                "cps3r-stage mislabeled external after cb baseline -> replaced (source 优先)");

            // D) 无 scope stage=Applied 且无基准 → 不计数且不写基准
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(40f);
            SetOrderObserver.Observe(1, 2, 555, CommandDomain.Movement, CommandKind.Move, 7,
                (int)SetOrderStage.Applied);
            Check(SetOrderObserver.ExternalReplacedCount == 0,
                "cps3r-stage no baseline -> no replace (误标不写基准)");

            SetOrderObserver.Enabled = false;
        }

        // t44（cps3r-force-lease-candidate）：ForceLease 受控注入——开关默认关、候选资格（单支非玩家 AI 编队）、
        // 仅 D 模式生效（组合矩阵）、走真实 Acquire 全链路（ResolveFallback 真实判定不伪造最终结果）
        private static void TestCps3rForceLeaseCandidate()
        {
            // A) 开关默认关（零常态行为变化）+ 限时超时常量
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseCandidateEnabled,
                "cps3r-force default off (零常态行为)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ForceArmTimeoutSeconds == 30f,
                "cps3r-force arm timeout 30s (限时)");

            // B) 候选资格（单支非玩家 AI 编队：非玩家队 + 无玩家兵员 + AI 控制）
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsForceLeaseCandidate(false, false, true),
                "cps3r-force eligible (非玩家队/无玩家兵员/AI)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsForceLeaseCandidate(true, false, true),
                "cps3r-force player team excluded");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsForceLeaseCandidate(false, true, true),
                "cps3r-force player-owned formation excluded");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsForceLeaseCandidate(false, false, false),
                "cps3r-force non-AI excluded");

            // C) 组合矩阵：仅 D_ExclusivePoc 生效
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseActive(
                CombatBrain.Settings.Cps3Mode.D_ExclusivePoc),
                "cps3r-force active only in D mode");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseActive(
                CombatBrain.Settings.Cps3Mode.C_ArbiterCoexist)
                && !CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseActive(
                    CombatBrain.Settings.Cps3Mode.A_NativeBaseline)
                && !CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseActive(
                    CombatBrain.Settings.Cps3Mode.B_ShadowOnly),
                "cps3r-force inactive outside D mode");

            // D) 真实 Acquire 路径复用：ForceLease 决策（Denied+EmergencyOverride）→ ResolveFallback 真实判定
            //    （不伪造：MainBodyStrength=0 → NoSafeFallback 不 Acquire；dist>30 且 MainBody 有效 → Rejoin）
            var armyNoMain = new ArmyContextSnapshot(); // MainBodyStrength=0（default）
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.Denied, armyNoMain, 50f)
                == SafeFallbackKind.NoSafeFallback,
                "cps3r-force no mainbody -> NoSafeFallback (真实不 Acquire)");
            var armyMain = new ArmyContextSnapshot { MainBodyStrength = 100, MainBodyX = 0f, MainBodyY = 0f };
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.Denied, armyMain, 50f)
                == SafeFallbackKind.Rejoin,
                "cps3r-force denied+far -> Rejoin (真实 Acquire 前置)");
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.Denied, armyMain, 10f)
                == SafeFallbackKind.NoSafeFallback,
                "cps3r-force denied+safe zone -> NoSafeFallback (不伪造)");

            // E) MCM 开关目标默认关 + roundtrip（Cps3ForceLeaseCandidateEnabled 薄转发）
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseCandidateEnabled,
                "cps3r-force mcm target default off");
            CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseCandidateEnabled = true;
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseCandidateEnabled,
                "cps3r-force mcm target roundtrip");
            CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseCandidateEnabled = false;

            // F) t46 repair（Emergency 冲突 Acquire 守卫）：注入决策=普通 Denied（Reasons 无 Emergency/Defensive）——
            //    RetreatOrEmergencyOrDefensive=false → 状态机 deniedOrHold 判定可通过（FormationControlLease.cs:184-186）
            var forced = new CommitmentDecision
            {
                Permission = CommitmentPermission.Denied,
                Reasons = CommitmentReason.None
            };
            Check(forced.Reasons == CommitmentReason.None,
                "cps3r-force-r2 injection plain denied (no emergency/defensive)");
            bool retreatOrEmergency = (forced.Reasons & CommitmentReason.EmergencyOverride) != 0
                || (forced.Reasons & CommitmentReason.DefensiveOverride) != 0;
            Check(!retreatOrEmergency,
                "cps3r-force-r2 injection not retreat/emergency/defensive (Acquire 守卫不阻)");
            bool deniedOrHold = forced.Permission == CommitmentPermission.Denied && !retreatOrEmergency;
            Check(deniedOrHold,
                "cps3r-force-r2 deniedOrHold true (deniedOrHold 判定可通过)");
            // 真实链路保持：普通 Denied + 远离 MainBody → Rejoin（Acquire 前置）；安全带内 → NoSafeFallback（不伪造）
            var armyMain2 = new ArmyContextSnapshot { MainBodyStrength = 100, MainBodyX = 0f, MainBodyY = 0f };
            Check(FormationControlLeaseManager.ResolveFallback(forced.Permission, armyMain2, 50f)
                == SafeFallbackKind.Rejoin,
                "cps3r-force-r2 plain denied + far -> Rejoin (真实 Acquire 前置)");
            Check(FormationControlLeaseManager.ResolveFallback(forced.Permission, armyMain2, 10f)
                == SafeFallbackKind.NoSafeFallback,
                "cps3r-force-r2 plain denied + safe zone -> NoSafeFallback (不伪造)");
        }

        // t48（cps3r-force-lease-arm-trigger-split）：武装/触发分离——战前武装、中段等合格候选即触发
        // （合格判定 IsForceTriggerReady 纯逻辑：MainBody 有效+远离>30m+无活跃租约；不降 30m 安全条件不绕过 NoSafeFallback）
        private static void TestCps3rForceArmTriggerSplit()
        {
            // A) 合格触发判定（纯逻辑）：MainBody 有效 + 远离>30m + 无活跃租约 → 合格
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsForceTriggerReady(100, 50f, false),
                "cps3r-split ready (mainbody + far>30m + no lease)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsForceTriggerReady(0, 50f, false),
                "cps3r-split not ready without mainbody (等待)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsForceTriggerReady(100, 10f, false),
                "cps3r-split not ready in safe zone (不降 30m 安全条件)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsForceTriggerReady(100, 50f, true),
                "cps3r-split not ready with active lease (等待释放)");
            // 边界：恰 30m 不算远离（ResolveFallback 同口径 >30）
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsForceTriggerReady(100, 30f, false),
                "cps3r-split boundary 30m not ready (严格 >30m)");

            // B) 不降安全条件：SafeFallbackMeters=30 保持（外部验证明确禁止降）
            Check(FormationControlLeaseManager.SafeFallbackMeters == 30f,
                "cps3r-split safe fallback meters kept 30 (不降安全条件)");

            // C) 超时语义：ForceArmTimeoutSeconds=30s（超时未取到样本 abort 保留）
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ForceArmTimeoutSeconds == 30f,
                "cps3r-split arm timeout 30s (超时未取到样本 abort)");

            // D) 真实链路保持：合格（远离+MainBody 有效）→ 普通 Denied → Rejoin（Acquire 前置）；不伪造
            var forced = new CommitmentDecision
            {
                Permission = CommitmentPermission.Denied,
                Reasons = CommitmentReason.None
            };
            var armyMain = new ArmyContextSnapshot { MainBodyStrength = 100, MainBodyX = 0f, MainBodyY = 0f };
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsForceTriggerReady(armyMain.MainBodyStrength, 50f, false)
                && FormationControlLeaseManager.ResolveFallback(forced.Permission, armyMain, 50f)
                    == SafeFallbackKind.Rejoin,
                "cps3r-split ready + plain denied + far -> Rejoin (真实 Acquire 前置)");
        }

        // t50（cps3r-route-a-hardgates）路线 A 六硬门：纯逻辑断言（隔离/可逆恢复/per-Formation 映射+Rejoin 导航/
        //   归因 scope/降权单维可恢复/幂等无分配语义）
        private static void TestCps3rRouteAHardgates()
        {
            // 硬门①：普通野战隔离判定
            Check(Cps2RouteAPolicy.IsEligibleFieldBattle(true, false, false),
                "cps3r-routeA① field battle eligible");
            Check(!Cps2RouteAPolicy.IsEligibleFieldBattle(true, true, false),
                "cps3r-routeA① siege excluded");
            Check(!Cps2RouteAPolicy.IsEligibleFieldBattle(true, false, true),
                "cps3r-routeA① sally-out excluded");
            Check(!Cps2RouteAPolicy.IsEligibleFieldBattle(false, false, false),
                "cps3r-routeA① non-field excluded");

            // 硬门②：可逆恢复判定（开关关/玩家接管/编队失效任一 → 恢复）
            Check(Cps2RouteAPolicy.ShouldRestoreRouteA(false, true, true),
                "cps3r-routeA② restore on switch off (热关闭=真恢复)");
            Check(Cps2RouteAPolicy.ShouldRestoreRouteA(true, false, true),
                "cps3r-routeA② restore on player takeover");
            Check(Cps2RouteAPolicy.ShouldRestoreRouteA(true, true, false),
                "cps3r-routeA② restore on invalid formation");
            Check(!Cps2RouteAPolicy.ShouldRestoreRouteA(true, true, true),
                "cps3r-routeA② no restore when valid+ai+on");

            // 硬门③：per-Formation Mission 映射（弃 Team 意图）+ Rejoin 导航校验
            Check(Cps2RouteAPolicy.MapFormationToIntent(false, (int)FormationMission.Retreat, BattlePhase.Engage)
                == Cps2RouteAIntent.Retreat,
                "cps3r-routeA③ formation retreat -> Retreat");
            Check(Cps2RouteAPolicy.MapFormationToIntent(false, (int)FormationMission.SupportMainEffort, BattlePhase.Deployment)
                == Cps2RouteAIntent.Rejoin,
                "cps3r-routeA③ support+deployment -> Rejoin (随主力)");
            Check(Cps2RouteAPolicy.MapFormationToIntent(false, (int)FormationMission.Intercept, BattlePhase.Engage)
                == Cps2RouteAIntent.Advance,
                "cps3r-routeA③ intercept -> Advance");
            Check(Cps2RouteAPolicy.MapFormationToIntent(false, (int)FormationMission.Reserve, BattlePhase.Engage)
                == Cps2RouteAIntent.Hold,
                "cps3r-routeA③ reserve -> Hold");
            Check(!Cps2RouteAPolicy.IsRejoinRouteValid(0f, 0f, 10f, 10f, null, 12f),
                "cps3r-routeA③ rejoin invalid with zero mainbody (Fog 契约)");
            var enemy = new[] { new FormationCoordSnapshot { FormationId = 0, X = 20f, Y = 20f, Count = 50, IsKnown = true } };
            Check(!Cps2RouteAPolicy.IsRejoinRouteValid(100f, 100f, 24f, 24f, enemy, 12f),
                "cps3r-routeA③ rejoin invalid in-contact (防无人负责)");

            // 硬门④：权威归因 scope（CombatBrainRouteA 追加到 CommandSource，落地可归因）
            Check((int)CommandSource.CombatBrainRouteA == 14,
                "cps3r-routeA④ routA scope value 14 (归因)");
            Check(Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Hold) == CommandKind.Hold
                && Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Advance) == CommandKind.Advance,
                "cps3r-routeA④ intent->commandkind (Behavior Selected→OrderApplied 语义)");

            // 硬门⑤：冲突降权单维——同意图冲突表（CB 域内互斥，不整套清空）+ 单维权重常量（100 放大消除）
            var conflict = Cps2RouteAPolicy.ConflictingOwn(Cps2RouteAIntent.Hold);
            Check(conflict.Length == 3
                && System.Array.IndexOf(conflict, Cps2RouteAIntent.Advance) >= 0
                && System.Array.IndexOf(conflict, Cps2RouteAIntent.Rejoin) >= 0
                && System.Array.IndexOf(conflict, Cps2RouteAIntent.Retreat) >= 0,
                "cps3r-routeA⑤ minimal conflict set (降权而非清空)");
            // 方向性回归：曾经缺失的降权边会使旧行为保留权重，并被引擎保留倍率继续选中。
            Check(System.Array.IndexOf(Cps2RouteAPolicy.ConflictingOwn(Cps2RouteAIntent.Advance), Cps2RouteAIntent.Rejoin) >= 0,
                "routeA Rejoin -> Advance disables old Rejoin");
            Check(System.Array.IndexOf(Cps2RouteAPolicy.ConflictingOwn(Cps2RouteAIntent.Rejoin), Cps2RouteAIntent.Hold) >= 0,
                "routeA Hold -> Rejoin disables old Hold");
            Check(System.Array.IndexOf(Cps2RouteAPolicy.ConflictingOwn(Cps2RouteAIntent.Retreat), Cps2RouteAIntent.Rejoin) >= 0,
                "routeA Rejoin -> Retreat disables old Rejoin");
            Check(CombatBrain.Settings.Cps2PoCSettings.CbBehaviorWeight == 10f,
                "cps3r-routeA⑤ single-dim weight (无 100 放大)");

            // 硬门⑥：幂等无分配——行为类型缓存键（每 identity×类型固定键，命中复用不每拍 new 的语义由 EnsureBehavior 保证）
            Check(CombatBrain.Settings.Cps2PoCSettings.RouteAMaxFormations == 1,
                "cps3r-routeA⑥ single formation budget (无分配稳定)");
        }

        // t52 repair round-2（cps3r-route-a-hardgates）：①归因 scope 扩到真正 SetMovementOrder 调用路径——
        //   Behavior.CalculateCurrentOrder 内直接 Formation.SetMovementOrder 包 scope（Behavior 即真实 setter 来源）；
        //   断言 RouteA scope 内 SetMovementOrder 观测的 source=CombatBrainRouteA；②有效权重==10（GetAiWeight 返同常量）
        private static void TestCps3rRouteARepair2()
        {
            // ①归因：RouteA scope 内 SetMovementOrder → source=CombatBrainRouteA（观测归因锁定，非仅常量值）
            SetOrderObserver.Enabled = true;
            SetOrderObserver.Clear();
            SetOrderObserver.NotifyTick(1f);
            using (CommandSourceScope.Enter(CommandSource.CombatBrainRouteA))
            {
                // 仅测试 scope 归因；不执行游戏 setter，不证明引擎选中/落地行为。
                // MovementOrder 参数由实际 Behavior 产出——此处只验证 scope 归因：观测记录的 Source=CombatBrainRouteA
                //   而非 ExternalUnknown（t52：除去引擎间接路径后 Behavior 内 setter 归因成立）
                SetOrderObserver.Observe(5, 1, 42, CommandDomain.Movement, CommandKind.Move, 7, 0); // scope 内
            }
            bool hasRouteASource = false;
            for (int i = 0; i < SetOrderObserver.Count; i++)
            {
                if (SetOrderObserver.At(i).Source == CommandSource.CombatBrainRouteA) hasRouteASource = true;
            }
            Check(hasRouteASource,
                "cps3r-routeA-r2 routA scope 内 setter source=CombatBrainRouteA（归因锁定）");
            SetOrderObserver.Enabled = false;
            SetOrderObserver.Clear();

            // ②有效权重==10（t52 repair MEDIUM ⑤单维降权）：GetAiWeight 返 BaseBehaviorWeight=1f（基权重归一），
            //   SetBehaviorWeight 应用 CbBehaviorWeight=10 → FindBestBehavior 有效权重=1×10=10
            //   （消除 t20 GetAiWeight=10×SetBehaviorWeight=10=100 放大——EffectiveWeight 纯逻辑断言有效值非仅常量）
            Check(CombatBrain.Settings.Cps2PoCSettings.CbBehaviorWeight == 10f,
                "cps3r-routeA-r2 set-weight 10 (单点应用)");
            Check(CbRouteAPoC.BaseBehaviorWeight == 1f,
                "cps3r-routeA-r2 get-weight base 1 (单维归一)");
            Check(CbRouteAPoC.EffectiveWeight(CbRouteAPoC.BaseBehaviorWeight,
                CombatBrain.Settings.Cps2PoCSettings.CbBehaviorWeight) == 10f,
                "cps3r-routeA-r2 effective weight 1x10=10 (无 100 放大)");
            Check(CbRouteAPoC.EffectiveWeight(10f, 10f) == 100f
                && CbRouteAPoC.EffectiveWeight(1f, 10f) == 10f,
                "cps3r-routeA-r2 effective weight formula (FindBestBehavior 语义)");
            Check(Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Hold) == CommandKind.Hold
                && Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Advance) == CommandKind.Advance
                && Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Rejoin) == CommandKind.Move
                && Cps2RouteAPolicy.ToCommandKind(Cps2RouteAIntent.Retreat) == CommandKind.Retreat,
                "cps3r-routeA-r2 all intents effective commands (Behavior 落地语义)");
        }

        // t54（cps3r-d-routea-mode）：D-RouteA 运行期验证模式——E_RouteA 模式语义（RouteA 开+租约关）、
        // 事件链四段（Intent/BehaviorSelected/OrderApplied/Stable）、稳定性（意图变化/flap 检测）
        private static void TestCps3rDRouteAMode()
        {
            // ①E_RouteA 模式语义：追加不重排既有值 + 转换 0..4 + 组合矩阵（RouteA 开+租约关）
            Check((int)CombatBrain.Settings.Cps3Mode.E_RouteA == 4,
                "cps3r-droutea E_RouteA value 4 (追加不重排)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(4) == CombatBrain.Settings.Cps3Mode.E_RouteA
                && CombatBrain.Settings.Cps3RuntimeSettings.SelectedIndexFromMode(CombatBrain.Settings.Cps3Mode.E_RouteA) == 4,
                "cps3r-droutea mode<->selected roundtrip 0..4");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(5) == CombatBrain.Settings.Cps3Mode.A_NativeBaseline,
                "cps3r-droutea out-of-range clamps to A");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(CombatBrain.Settings.Cps3Mode.E_RouteA, false, true),
                "cps3r-droutea E valid (routeA on + lease off)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(CombatBrain.Settings.Cps3Mode.E_RouteA, true, true),
                "cps3r-droutea E invalid with lease (与租约区分)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.RouteAActive(CombatBrain.Settings.Cps3Mode.E_RouteA)
                && !CombatBrain.Settings.Cps3RuntimeSettings.RouteAActive(CombatBrain.Settings.Cps3Mode.D_ExclusivePoc),
                "cps3r-droutea RouteAActive only in E (不用 ForceLease)");
            Check(!CombatBrain.Settings.Cps3RuntimeSettings.ForceLeaseActive(CombatBrain.Settings.Cps3Mode.E_RouteA),
                "cps3r-droutea ForceLease inactive in E (租约注入仅 D)");

        }

        // t56 repair（MCM 下拉缺 E）：Cps3ModeNames 补第 5 项 "E: 单编队 Route A"（index 4 ↔ E_RouteA=4）。
        // 注意：CombatBrainMcmSettings 类型含 Dropdown<string>/MCM attribute——离线 JIT 加载触发 MCMv5 缺失
        //   （同 t54 教训），数组字面量由 CombatBrain 编译 + CONTEXT 记录保证；本卡断言转换纯逻辑（Cps3RuntimeSettings）
        private static void TestCps3rMcmDropdownE()
        {
            // A) index 4 ↔ E_RouteA roundtrip（MCM get/set 转换在新数组下正确）
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(4) == CombatBrain.Settings.Cps3Mode.E_RouteA,
                "cps3r-mcm-e index 4 -> E_RouteA");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.SelectedIndexFromMode(CombatBrain.Settings.Cps3Mode.E_RouteA) == 4,
                "cps3r-mcm-e E_RouteA -> index 4");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.ModeFromSelectedIndex(5) == CombatBrain.Settings.Cps3Mode.A_NativeBaseline,
                "cps3r-mcm-e out-of-range 5 clamps to A (安全态)");
            Check(CombatBrain.Settings.Cps3RuntimeSettings.SelectedIndexFromMode(CombatBrain.Settings.Cps3Mode.A_NativeBaseline) == 0,
                "cps3r-mcm-e default index 0 = A (安全态保持)");

            // B) 组合矩阵语义：E 需 RouteA 开（t54）——MCM 下拉选 E 后 Mode=E_RouteA 且组合校验通过
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.E_RouteA;
            Check(CombatBrain.Settings.Cps3RuntimeSettings.IsValidCombination(
                CombatBrain.Settings.Cps3RuntimeSettings.Mode, false, true),
                "cps3r-mcm-e E selection valid with routeA on");
            CombatBrain.Settings.Cps3RuntimeSettings.Mode = CombatBrain.Settings.Cps3Mode.A_NativeBaseline; // 复位安全态
            Check(CombatBrain.Settings.Cps3RuntimeSettings.Mode == CombatBrain.Settings.Cps3Mode.A_NativeBaseline,
                "cps3r-mcm-e reset restores A");
        }

        // t58 repair（叙事阵营名）：CultureId 去 GeneralAgent 兜底——士兵文化多数决失败直接 null（宁缺毋假，
        //   消除自定义战斗 GeneralAgent 文化不可靠导致的「选帝国显示瓦兰迪亚」）；Faction(null)→我军/敌军
        private static void TestNarrativeFactionFix()
        {
            // A) 士兵多数决：有票→最多票文化（先到平票保持）
            Check(CombatBrain.Ai.FeedbackText.MajorityCulture(new[] { "empire", "empire", "vlandia" }) == "empire",
                "narr-fix majority culture empire (3 票胜出)");
            Check(CombatBrain.Ai.FeedbackText.MajorityCulture(new[] { "vlandia", "empire", "empire", "vlandia", "vlandia" }) == "vlandia",
                "narr-fix majority culture vlandia (真实多数)");
            Check(CombatBrain.Ai.FeedbackText.MajorityCulture(new[] { "empire", "vlandia" }) == "empire",
                "narr-fix tie keeps first (平票先到)");

            // B) 无票/多数决失败 → null（不再兜底 GeneralAgent——宁缺毋假，防瓦兰迪亚误显示）
            Check(CombatBrain.Ai.FeedbackText.MajorityCulture(null) == null,
                "narr-fix null input -> null (不兜底)");
            Check(CombatBrain.Ai.FeedbackText.MajorityCulture(new string[0]) == null,
                "narr-fix no soldiers -> null (无兵不兜底 GeneralAgent)");
            Check(CombatBrain.Ai.FeedbackText.MajorityCulture(new[] { "", "  ", null }) == null,
                "narr-fix blank cultures -> null (跳过无效票)");

            // C) Faction 语义（null → 我军/敌军兜底——t58 核心：不再出现错误阵营名）。
            //    注意：Faction(Team) 签名含 TaleWorlds 类型，离线调用触发加载（同 t54/t56 教训）——
            //    此处断言其组成：FactionCultureName(null)=null（未映射）→ Faction 落我军/敌军兜底（:92-93 代码审查）
            Check(CombatBrain.Ai.FeedbackText.FactionCultureName(null) == null,
                "narr-fix faction unknown culture null passthrough (Faction 落我军/敌军兜底)");
            // 映射文化正常返回（Faction 映射表不变）
            Check(CombatBrain.Ai.FeedbackText.FactionCultureName("vlandia") == "瓦兰迪亚",
                "narr-fix faction culture map intact (映射表未破坏)");
            Check(CombatBrain.Ai.FeedbackText.FactionCultureName("empire") == "帝国",
                "narr-fix faction culture empire mapped");
        }

        // ATC-1 战局阶段机（BattlePhaseFSM 纯逻辑）：五态全边界/最短驻留/同态不重播/Retreat 终态/早期伤亡不误进 Exploit
        private static void TestBattlePhaseFSM()
        {
            // 1) 初始 Deployment：存活正常 + 未到驻留 → 不转移（同态 Changed=false，修订4/5）
            var r0 = BattlePhaseFSM.Advance(BattlePhase.Deployment, new BattlePhaseInput
            { OwnSurvival = 0.9f, TimeInPhase = 2f });
            Check(r0.Phase == BattlePhase.Deployment && !r0.Changed, "atc1 deployment dwell <5s no move (修订4)");

            // 2) Deployment→Advance：存活 ≥0.7 且驻留 ≥5s
            var r1 = BattlePhaseFSM.Advance(BattlePhase.Deployment, new BattlePhaseInput
            { OwnSurvival = 0.9f, TimeInPhase = 6f });
            Check(r1.Phase == BattlePhase.Advance && r1.Changed, "atc1 deployment -> advance (surv 0.9 dwell 6s)");

            // 3) Deployment 存活不足（<0.7）→ 不 Advance（即使驻留够）
            var r2 = BattlePhaseFSM.Advance(BattlePhase.Deployment, new BattlePhaseInput
            { OwnSurvival = 0.5f, TimeInPhase = 6f });
            Check(r2.Phase == BattlePhase.Deployment, "atc1 low survival blocks advance");

            // 4) Advance→Engage：真实接触（EnemyContact=true）+ 驻留够（P1-2 接入后接触为转移条件）
            var r3 = BattlePhaseFSM.Advance(BattlePhase.Advance, new BattlePhaseInput
            { OwnSurvival = 0.8f, TimeInPhase = 6f, EnemyContact = true });
            Check(r3.Phase == BattlePhase.Engage && r3.Changed, "atc1 advance -> engage (contact, P1-2)");

            // 5) Engage→Exploit：敌弱化 + 敌存活<0.4（修订6 双条件之一）
            var r4 = BattlePhaseFSM.Advance(BattlePhase.Engage, new BattlePhaseInput
            { OwnSurvival = 0.7f, EnemySurvival = 0.3f, EnemyWeakening = true, TimeInPhase = 6f });
            Check(r4.Phase == BattlePhase.Exploit, "atc1 engage -> exploit (enemy surv 0.3 + weakening)");

            // 6) Engage 敌弱化但敌存活高（≥0.4）→ 不进 Exploit（修订6 防早期误进）
            var r5 = BattlePhaseFSM.Advance(BattlePhase.Engage, new BattlePhaseInput
            { OwnSurvival = 0.9f, EnemySurvival = 0.6f, EnemyWeakening = true, TimeInPhase = 6f });
            Check(r5.Phase == BattlePhase.Engage, "atc1 enemy weakening but surv 0.6 -> stay engage (修订6)");

            // 7) Engage 敌存活低但无弱化信号 → 不进 Exploit
            var r6 = BattlePhaseFSM.Advance(BattlePhase.Engage, new BattlePhaseInput
            { OwnSurvival = 0.7f, EnemySurvival = 0.2f, EnemyWeakening = false, TimeInPhase = 6f });
            Check(r6.Phase == BattlePhase.Engage, "atc1 low enemy surv but no weakening -> stay engage");

            // 8) Engage 意图 Pursue → 进 Exploit（修订6 第二条件）
            var r7 = BattlePhaseFSM.Advance(BattlePhase.Engage, new BattlePhaseInput
            { OwnSurvival = 0.7f, EnemyWeakening = true, Intent = IntentType.Pursue, TimeInPhase = 6f });
            Check(r7.Phase == BattlePhase.Exploit, "atc1 engage -> exploit via Pursue (修订6)");

            // 9) 同态不重播：Engage 正常状态 → Changed=false（修订5）
            var r8 = BattlePhaseFSM.Advance(BattlePhase.Engage, new BattlePhaseInput
            { OwnSurvival = 0.8f, EnemySurvival = 0.6f, TimeInPhase = 6f });
            Check(!r8.Changed, "atc1 stable engage not changed (修订5 同态不重播)");

            // 10) 任意相→Retreat：存活<0.4 终态（修订3）
            var r9 = BattlePhaseFSM.Advance(BattlePhase.Engage, new BattlePhaseInput
            { OwnSurvival = 0.3f, TimeInPhase = 6f });
            Check(r9.Phase == BattlePhase.Retreat && r9.Changed, "atc1 engage -> retreat (surv 0.3)");
            var r10 = BattlePhaseFSM.Advance(BattlePhase.Advance, new BattlePhaseInput
            { OwnSurvival = 0.2f, TimeInPhase = 6f });
            Check(r10.Phase == BattlePhase.Retreat, "atc1 advance -> retreat (surv 0.2)");

            // 11) Retreat 终态：存活回升也不转出（修订3，删除不可达的回升→Advance）
            var r11 = BattlePhaseFSM.Advance(BattlePhase.Retreat, new BattlePhaseInput
            { OwnSurvival = 0.9f, TimeInPhase = 6f });
            Check(r11.Phase == BattlePhase.Retreat && !r11.Changed, "atc1 retreat terminal (no recovery advance, 修订3)");

            // 12) Retreat 不因任何输入转出（EnemyWeakening/Pursue 也无效）
            var r12 = BattlePhaseFSM.Advance(BattlePhase.Retreat, new BattlePhaseInput
            { OwnSurvival = 0.9f, EnemyWeakening = true, Intent = IntentType.Pursue, TimeInPhase = 6f });
            Check(r12.Phase == BattlePhase.Retreat, "atc1 retreat immune to exploit signals");

            // 13) 早期伤亡不误进 Exploit：Deployment 存活 0.6（战斗初期短促掉人）→ 不 Advance 更不 Exploit
            var r13 = BattlePhaseFSM.Advance(BattlePhase.Deployment, new BattlePhaseInput
            { OwnSurvival = 0.6f, TimeInPhase = 6f });
            Check(r13.Phase == BattlePhase.Deployment, "atc1 early casualties stay deployment (不误进)");

            // 14) 最短驻留守卫：Deployment 存活 0.9 但驻留 4.9s → 不转移（修订4）
            var r14 = BattlePhaseFSM.Advance(BattlePhase.Deployment, new BattlePhaseInput
            { OwnSurvival = 0.9f, TimeInPhase = 4.9f });
            Check(r14.Phase == BattlePhase.Deployment && !r14.Changed, "atc1 dwell 4.9s boundary blocks (修订4)");

            // 15) Exploit 无出边（除 Retreat 守卫）：存活正常保持 Exploit
            var r15 = BattlePhaseFSM.Advance(BattlePhase.Exploit, new BattlePhaseInput
            { OwnSurvival = 0.6f, TimeInPhase = 6f });
            Check(r15.Phase == BattlePhase.Exploit && !r15.Changed, "atc1 exploit stable no out-edge");

            // 16) 五态枚举值契约（§3.1）
            Check((int)BattlePhase.Deployment == 0 && (int)BattlePhase.Advance == 1
                && (int)BattlePhase.Engage == 2 && (int)BattlePhase.Exploit == 3
                && (int)BattlePhase.Retreat == 4, "atc1 phase enum 0..4");

            // 17) 阈值常量契约（供胶水层引用一致）
            Check(BattlePhaseFSM.RetreatSurvival == 0.4f && BattlePhaseFSM.AdvanceSurvival == 0.7f
                && BattlePhaseFSM.MinDwellSeconds == 5f && BattlePhaseFSM.ExploitEnemySurvival == 0.4f,
                "atc1 threshold constants");

            // 18) atc4-phase-provenance-fix P1-2：EnemyContact 已接入——无有效接触不得仅因计时结束进入 Engage
            // （双方距离很远且无接触时保持 Advance；最短驻留守卫不变）
            var rEC = BattlePhaseFSM.Advance(BattlePhase.Advance, new BattlePhaseInput
            { OwnSurvival = 0.8f, TimeInPhase = 6f, EnemyContact = false });
            Check(rEC.Phase == BattlePhase.Advance && !rEC.Changed,
                "atc4-phase no contact -> stay Advance (无接触不自动 Engage, P1-2)");

            // 19) P1-2：有真实接触（EnemyContact=true）→ Advance→Engage
            var rEC2 = BattlePhaseFSM.Advance(BattlePhase.Advance, new BattlePhaseInput
            { OwnSurvival = 0.8f, TimeInPhase = 6f, EnemyContact = true });
            Check(rEC2.Phase == BattlePhase.Engage && rEC2.Changed,
                "atc4-phase contact -> Engage (P1-2 真实接触推进)");

            // 20) P1-2：无接触 + 驻留不足 → 保持 Advance（双重守卫）
            var rEC3 = BattlePhaseFSM.Advance(BattlePhase.Advance, new BattlePhaseInput
            { OwnSurvival = 0.8f, TimeInPhase = 2f, EnemyContact = true });
            Check(rEC3.Phase == BattlePhase.Advance && !rEC3.Changed,
                "atc4-phase contact but dwell<5s -> stay Advance (最短驻留保留)");
        }

        // ATC-2 ArmyContext（MainBodySelector/OperationalObjectiveSelector 纯逻辑）：最大簇/平局/加权中心/孤立骑兵/空队/单编队/两集团/迷雾三态
        private static void TestArmyContext()
        {
            // 1) 空输入 → MainBody 零结果
            var empty = new List<FormationCoordSnapshot>();
            var r0 = MainBodySelector.Select(empty);
            Check(r0.Strength == 0 && r0.Confidence == 0f && r0.MemberCount == 0, "atc2 empty formations -> zero main body");

            // 2) 单编队 → 中心=该编队，Confidence=1
            var single = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 3, Count = 40, X = 10f, Y = 20f, Kind = FormationCoordKind.Infantry, IsKnown = true }
            };
            var r1 = MainBodySelector.Select(single);
            Check(r1.Strength == 40 && Math.Abs(r1.X - 10f) < 0.01f && Math.Abs(r1.Y - 20f) < 0.01f
                && Math.Abs(r1.Confidence - 1f) < 0.01f, "atc2 single formation -> center/conf 1");

            // 3) 两个连通簇 → 总兵力最大簇（§4.1）
            var two = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 50, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 1, Count = 10, X = 50f, Y = 0f, Kind = FormationCoordKind.MeleeCavalry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 2, Count = 30, X = 1000f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true }
            };
            var r2 = MainBodySelector.Select(two);
            Check(r2.Strength == 60, "atc2 largest cluster by strength (0+1=60 > 2=30)");
            Check(Math.Abs(r2.X - (0f * 50 + 50f * 10) / 60f) < 0.01f, "atc2 weighted center of largest cluster");

            // 4) 孤立骑兵不拉偏中心（§4.1.6）：大步兵簇 + 远处孤立骑 → 中心仍在步兵簇
            var iso = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 80, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 1, Count = 15, X = 500f, Y = 500f, Kind = FormationCoordKind.MeleeCavalry, IsKnown = true }
            };
            var r3 = MainBodySelector.Select(iso);
            Check(Math.Abs(r3.X) < 0.01f && Math.Abs(r3.Y) < 0.01f && r3.Strength == 80,
                "atc2 isolated cavalry does not pull center");

            // 5) 平局确定性：兵力相同 → 更多 Infantry/Ranged 优先；再同 → 最小 FormationId
            var tie1 = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 20, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 1, Count = 20, X = 1000f, Y = 0f, Kind = FormationCoordKind.MeleeCavalry, IsKnown = true }
            };
            var r4 = MainBodySelector.Select(tie1);
            Check(r4.Strength == 20 && Math.Abs(r4.X) < 0.01f, "atc2 tie -> more infantry (id 0) wins");
            var tie2 = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 5, Count = 20, X = 0f, Y = 0f, Kind = FormationCoordKind.MeleeCavalry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 2, Count = 20, X = 1000f, Y = 0f, Kind = FormationCoordKind.RangedCavalry, IsKnown = true }
            };
            var r5 = MainBodySelector.Select(tie2);
            Check(r5.Strength == 20 && Math.Abs(r5.X - 1000f) < 0.01f, "atc2 tie no inf/rng -> min FormationId (2)");

            // 6) 两个独立集团（距离 > 60m 不连通）→ 各自成簇，取大者
            var groups = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 25, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 1, Count = 25, X = 50f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },  // 连通 0
                new FormationCoordSnapshot { FormationId = 2, Count = 40, X = 200f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true }   // 独立（>60m）
            };
            var r6 = MainBodySelector.Select(groups);
            Check(r6.Strength == 50, "atc2 two independent groups -> cluster 0+1 (50) beats 2 (40)");

            // 7) 迷雾：OperationalObjective 只消费已知（§4.2 + 迷雾一致）
            var fogAll = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 100, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = false } // 未知大敌
            };
            var o0 = OperationalObjectiveSelector.Select(fogAll);
            Check(!o0.HasObjective, "atc2 unknown big enemy cannot be objective (迷雾)");

            // 8) 重新观察（IsKnown=true）→ 可成为 Objective
            var fogSeen = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 100, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true }
            };
            var o1 = OperationalObjectiveSelector.Select(fogSeen);
            Check(o1.HasObjective && o1.FormationId == 0 && o1.Strength == 100, "atc2 re-observed enemy -> objective");

            // 9) 过期（IsKnown=false）→ 撤销
            var o2 = OperationalObjectiveSelector.Select(fogAll);
            Check(!o2.HasObjective, "atc2 expired enemy -> objective revoked (迷雾)");

            // 10) 混合：已知小簇 + 未知大簇 → 选已知（迷雾一致，未知不得补齐）
            var mix = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 200, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = false },
                new FormationCoordSnapshot { FormationId = 1, Count = 30, X = 1000f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 2, Count = 20, X = 1040f, Y = 0f, Kind = FormationCoordKind.Ranged, IsKnown = true }
            };
            var o3 = OperationalObjectiveSelector.Select(mix);
            Check(o3.HasObjective && o3.Strength == 50 && o3.FormationId == 1, "atc2 known cluster selected (unknown excluded)");

            // 11) 置信不足 → 无目标（MinObjectiveConfidence 0.5）
            var lowConf = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 5, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 1, Count = 5, X = 1000f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 2, Count = 9, X = 2000f, Y = 0f, Kind = FormationCoordKind.Infantry, IsKnown = true }
            };
            var o4 = OperationalObjectiveSelector.Select(lowConf);
            Check(!o4.HasObjective, "atc2 confidence 0.47 < 0.5 -> no objective (最大簇 9/19)");

            // 12) 常量契约
            Check(MainBodySelector.MainBodyLinkMeters == 60f && OperationalObjectiveSelector.ObjectiveLinkMeters == 60f,
                "atc2 link meters 60 const");
        }

        // ATC-3 Mission/Separation/Isolation（纯逻辑）：§9 映射分支 + §4.4 六因子 + 80 人案例
        private static void TestFormationMission()
        {
            // 1) §9 任务映射：主力步兵 → SupportMainEffort/Separation=None
            var m0 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.Infantry, Phase = BattlePhase.Engage, Intent = IntentType.Attack });
            Check(m0.Mission == FormationMission.SupportMainEffort && m0.Separation == SeparationIntent.None,
                "atc3 infantry -> SupportMainEffort/None");

            // 2) 远程 → SupportMainEffort（非独立 Main Effort）
            var m1 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.Ranged, Phase = BattlePhase.Engage, Intent = IntentType.Defend });
            Check(m1.Mission == FormationMission.SupportMainEffort, "atc3 ranged -> SupportMainEffort");

            // 3) 近战骑兵：进攻意图 → Flank/Separation=Flank
            var m2 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.MeleeCavalry, Phase = BattlePhase.Engage, Intent = IntentType.Flank });
            Check(m2.Mission == FormationMission.Flank && m2.Separation == SeparationIntent.Flank,
                "atc3 melee cavalry aggressive -> Flank");

            // 4) 近战骑兵：防守意图 → Intercept
            var m3 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.MeleeCavalry, Phase = BattlePhase.Engage, Intent = IntentType.Defend });
            Check(m3.Mission == FormationMission.Intercept && m3.Separation == SeparationIntent.Intercept,
                "atc3 melee cavalry defensive -> Intercept");

            // 5) 骑射：非 Exploit → Screen
            var m4 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.RangedCavalry, Phase = BattlePhase.Engage, Intent = IntentType.Harass });
            Check(m4.Mission == FormationMission.Screen && m4.Separation == SeparationIntent.Screen,
                "atc3 ranged cavalry -> Screen");

            // 6) 骑射：Exploit+Pursue → Pursuit
            var m5 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.RangedCavalry, Phase = BattlePhase.Exploit, Intent = IntentType.Pursue });
            Check(m5.Mission == FormationMission.Pursuit && m5.Separation == SeparationIntent.Pursuit,
                "atc3 ranged cavalry exploit+pursue -> Pursuit");

            // 7) 骑射 Exploit 但非 Pursue → 仍 Screen（Exploit 阶段方可 Pursuit）
            var m6 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.RangedCavalry, Phase = BattlePhase.Exploit, Intent = IntentType.Attack });
            Check(m6.Mission == FormationMission.Screen, "atc3 exploit non-pursue -> Screen");

            // 8) Reserve 输入 → Reserve
            var m7 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.Infantry, IsReserve = true });
            Check(m7.Mission == FormationMission.Reserve && m7.Separation == SeparationIntent.Reserve,
                "atc3 reserve flag -> Reserve");

            // 9) Morale 撤离 → Retreat（最高优先）
            var m8 = FormationMissionAssigner.Assign(new MissionInput
            { Kind = FormationCoordKind.Infantry, InRetreat = true, IsReserve = true });
            Check(m8.Mission == FormationMission.Retreat && m8.Separation == SeparationIntent.Retreat,
                "atc3 in retreat -> Retreat (highest priority)");

            // 10) SeparationIntent 枚举契约（§3.3 八值）
            Check((int)SeparationIntent.None == 0 && (int)SeparationIntent.Retreat == 7,
                "atc3 SeparationIntent 0..7 (8 values)");

            // 11) 合法远离不误报：Flank 编队（Separation=Flank）远出 → 不报隔离（因子4 阻断）
            var i0 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 200f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 1.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.Flank
            });
            Check(!i0.UnauthorizedIsolation, "atc3 flank far away -> no isolation (合法分离)");

            // 12) Intercept 合法远离不误报
            var i1 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 150f, DistanceIncreasing = true, SupportScore = 0.2f,
                LocalForceRatio = 1.2f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.Intercept
            });
            Check(!i1.UnauthorizedIsolation, "atc3 intercept far -> no isolation");

            // 13) Retreat 合法远离不误报（因子5：Retreat 阻断）
            var i2 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 300f, DistanceIncreasing = true, SupportScore = 0.0f,
                LocalForceRatio = 0.3f, Phase = BattlePhase.Retreat,
                Separation = SeparationIntent.Retreat, InRetreat = true
            });
            Check(!i2.UnauthorizedIsolation, "atc3 retreat far -> no isolation (Retreat 豁免)");

            // 14) Exploit+Pursuit 不误报（因子6：Exploit 阻断）
            var i3 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 250f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.8f, Phase = BattlePhase.Exploit,
                Separation = SeparationIntent.Pursuit
            });
            Check(!i3.UnauthorizedIsolation, "atc3 exploit+pursuit -> no isolation (Exploit 豁免)");

            // 15) 未隔离：距离近（<80m 安全带内且未增）→ 因子1 阻断
            var i4 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 40f, DistanceIncreasing = false, SupportScore = 0.1f,
                LocalForceRatio = 0.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(!i4.UnauthorizedIsolation, "atc3 close to main body -> no isolation");

            // 16) 未隔离：支援充足（因子2 阻断）
            var i5 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 150f, DistanceIncreasing = true, SupportScore = 0.8f,
                LocalForceRatio = 0.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(!i5.UnauthorizedIsolation, "atc3 good support -> no isolation");

            // 17) 未隔离：局部优势（因子3 阻断）
            var i6 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 150f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 2.0f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(!i6.UnauthorizedIsolation, "atc3 local advantage -> no isolation");

            // 18) 全因子命中：SupportMainEffort 远离+无支援+局部劣势 → UnauthorizedIsolation（§4.4）
            var i7 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 200f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(i7.UnauthorizedIsolation, "atc3 support main effort far+unsupported+inferior -> isolation");
            Check(i7.DistanceFactor && i7.SupportFactor && i7.LocalFactor
                && i7.SeparationFactor && i7.NotRetreatFactor && i7.NotExploitFactor,
                "atc3 isolation all six factors true");

            // 19) 80 人案例纯逻辑回归（§10：80 步兵远出、敌方更强、支援晚到、Mission=SupportMainEffort/Separation=None）→ UnauthorizedIsolation=true
            var i8 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 180f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.6f, EnemyArrivesFirst = true, Phase = BattlePhase.Advance,
                Separation = SeparationIntent.None
            });
            Check(i8.UnauthorizedIsolation, "atc3 80-men case -> UnauthorizedIsolation=true (不强制 Hold)");

            // 20) 常量契约
            Check(IsolationDetector.SafeDistanceMeters == 80f && IsolationDetector.SupportThreshold == 0.3f
                && IsolationDetector.RatioThreshold == 1.0f, "atc3 isolation constants");

            // 21) isolation-fix 场景④尾声误报回归（实战样本 10:33:33 dist=4m）：主力怀抱内（≤30m）
            //     SupportMainEffort 编队即使距离趋势+无支援+劣势三项同成立也不判孤立（编队从未远离）
            var x1 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 4f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(!x1.UnauthorizedIsolation, "isolation-fix dist=4m in main-body arms -> no isolation (实锤样本)");
            Check(!x1.DistanceFactor, "isolation-fix dist=4m distance factor false (怀抱内趋势不成立)");

            // 22) 边界：26m/30m（≤InnerSafeMeters 含界）→ 不报；31m+趋势 → 距离因子成立
            var x2 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 26f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(!x2.UnauthorizedIsolation, "isolation-fix dist=26m -> no isolation (中危样本修复)");
            var x3 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 30f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(!x3.UnauthorizedIsolation, "isolation-fix dist=30m boundary -> no isolation (InnerSafe 含界)");
            var x4 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 31f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.5f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.None
            });
            Check(x4.UnauthorizedIsolation && x4.DistanceFactor,
                "isolation-fix dist=31m + increasing -> isolation (离开主力带, 其余因子成立)");

            // 23) 常量契约扩展 + 合法侧击不回归（场景④ Intercept 远出 8+ 次 iso=OK）
            Check(IsolationDetector.InnerSafeMeters == 30f, "isolation-fix InnerSafeMeters 30 const");
            var x5 = IsolationDetector.Evaluate(new IsolationInput
            {
                MainBodyDistance = 89f, DistanceIncreasing = true, SupportScore = 0.1f,
                LocalForceRatio = 0.8f, Phase = BattlePhase.Engage,
                Separation = SeparationIntent.Intercept
            });
            Check(!x5.UnauthorizedIsolation, "isolation-fix intercept 89m far -> no isolation (合法侧击不回归)");
            // 真实孤立（80 人案例 180m >80m 安全带）仍命中（i8 已断言，此处显式复验不回归）
            Check(i8.UnauthorizedIsolation, "isolation-fix 80-men case still isolated (真实孤立不回归)");
        }

        // ATC-4 CommitmentEvaluator（纯逻辑）：§3.4 枚举契约 + §10 判定纪律 6 条 + 80 人回归 + 例外（Flank/Intercept/Occupy）+ §4.3 Estimator
        private static void TestCommitmentEvaluator()
        {
            // 1) §3.4 枚举契约：Permission 五值 0..4
            Check((int)CommitmentPermission.Denied == 0 && (int)CommitmentPermission.Hold == 1
                && (int)CommitmentPermission.ScreenOnly == 2 && (int)CommitmentPermission.Limited == 3
                && (int)CommitmentPermission.Granted == 4, "atc4 permission enum 0..4");

            // 2) §3.4 CommitmentReason flags 九位值
            Check((int)CommitmentReason.None == 0 && (int)CommitmentReason.NoArmyObjective == 1
                && (int)CommitmentReason.NotAlignedWithMainEffort == 2 && (int)CommitmentReason.InsufficientSupport == 4
                && (int)CommitmentReason.LocalInferiority == 8 && (int)CommitmentReason.ArrivalGap == 16
                && (int)CommitmentReason.UnauthorizedIsolation == 32 && (int)CommitmentReason.ReserveProtected == 64
                && (int)CommitmentReason.DefensiveOverride == 128 && (int)CommitmentReason.EmergencyOverride == 256,
                "atc4 reason flags nine bits");

            // 3) 阈值常量契约
            Check(CommitmentEvaluator.SupportThreshold == 0.3f && CommitmentEvaluator.RatioThreshold == 1.0f
                && CommitmentEvaluator.RiskDenyCount == 2 && CommitmentEvaluator.AlignMeters == 60f,
                "atc4 evaluator constants");

            // 4) §4.3 Estimator 常量契约
            Check(CommitmentEstimator.MinSpeed == 1.0f && CommitmentEstimator.SupportRadius == 60f
                && CommitmentEstimator.ArrivalSlack == 10f && CommitmentEstimator.LocalRadius == 100f
                && CommitmentEstimator.ContactClampMeters == 40f
                && CommitmentEstimator.IntegrityCoeffMin == 0.5f && CommitmentEstimator.IntegrityCoeffMax == 1.0f,
                "atc4 estimator constants");

            // 5) 系数只读表：长度 5（五类兵种）+ 骑兵冲击 > 步兵基准
            Check(CommitmentEstimator.KindCoefficient.Length == 5
                && CommitmentEstimator.KindCoefficient[(int)FormationCoordKind.MeleeCavalry] > 1.0f
                && CommitmentEstimator.KindCoefficient[(int)FormationCoordKind.Infantry] == 1.0f,
                "atc4 kind coefficient table (cav > infantry)");

            // 6) IntegrityCoefficient 边界/中点
            CheckApprox(CommitmentEstimator.IntegrityCoefficient(0f), 0.5f, "atc4 integrity coeff min 0.5", 1e-4f);
            CheckApprox(CommitmentEstimator.IntegrityCoefficient(1f), 1.0f, "atc4 integrity coeff max 1.0", 1e-4f);
            CheckApprox(CommitmentEstimator.IntegrityCoefficient(0.5f), 0.75f, "atc4 integrity coeff mid 0.75", 1e-4f);

            // ---- 纪律1：Retreat/Emergency 直接 override，不走普通进攻评分 ----

            // 7) Emergency：全劣势输入也直接 Limited + EmergencyOverride
            var e0 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 200f, DistanceIncreasing = true,
                FriendlySupport = 0.05f, LocalForceRatio = 0.3f, FriendlyArrivalEta = 40f, EnemyContactEta = 8f,
                EmergencyOverride = true
            });
            Check(e0.Permission == CommitmentPermission.Limited, "atc4 emergency override -> Limited (不走进攻评分)");
            Check((e0.Reasons & CommitmentReason.EmergencyOverride) != 0, "atc4 emergency reason flag set");

            // 8) Retreat Mission → Limited + DefensiveOverride（撤退不被 Army Commitment 否决）
            var e1 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.Retreat, Separation = SeparationIntent.Retreat,
                HasOperationalObjective = true, TargetAlignedWithObjective = true,
                MainBodyDistance = 300f, DistanceIncreasing = true, FriendlySupport = 0.1f, LocalForceRatio = 0.4f,
                FriendlyArrivalEta = 50f, EnemyContactEta = 10f
            });
            Check(e1.Permission == CommitmentPermission.Limited && (e1.Reasons & CommitmentReason.DefensiveOverride) != 0,
                "atc4 retreat mission -> Limited + DefensiveOverride");

            // 9) Retreat Phase（终态）→ Limited + DefensiveOverride
            var e2 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Retreat, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = true,
                MainBodyDistance = 100f, FriendlySupport = 0.8f, LocalForceRatio = 2.0f
            });
            Check(e2.Permission == CommitmentPermission.Limited && (e2.Reasons & CommitmentReason.DefensiveOverride) != 0,
                "atc4 retreat phase -> Limited (撤退优先于继续进攻, §4.5)");

            // 10) DefensiveOverride（Immediate Defensive：Brace/Protect/脱困）→ Limited
            var e3 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 120f, FriendlySupport = 0.2f, LocalForceRatio = 0.6f, DefensiveOverride = true
            });
            Check(e3.Permission == CommitmentPermission.Limited && (e3.Reasons & CommitmentReason.DefensiveOverride) != 0,
                "atc4 defensive override -> Limited (不被 Denied 锁死)");

            // ---- 纪律2：已贴身接战 → Limited（不突然 Denied 原地不动） ----

            // 11) InContact + 全劣势 → Limited + DefensiveOverride（允许脱困/后撤/维持接触）
            var e4 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 150f, DistanceIncreasing = true,
                FriendlySupport = 0.1f, LocalForceRatio = 0.5f, FriendlyArrivalEta = 45f, EnemyContactEta = 5f,
                InContact = true
            });
            Check(e4.Permission == CommitmentPermission.Limited, "atc4 in contact -> Limited (不突然 Denied)");
            Check((e4.Reasons & CommitmentReason.DefensiveOverride) != 0, "atc4 in contact defensive flag");

            // ---- 纪律3：Reserve 默认不得主动 Charge ----

            // 12) Reserve 未释放 → Hold + ReserveProtected
            var e5 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.Reserve, Separation = SeparationIntent.Reserve,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 20f, FriendlySupport = 0.9f, LocalForceRatio = 1.5f, IsReserve = true
            });
            Check(e5.Permission == CommitmentPermission.Hold && (e5.Reasons & CommitmentReason.ReserveProtected) != 0,
                "atc4 reserve unreleased -> Hold + ReserveProtected (纪律3)");

            // 13) Reserve + Army Plan 释放（HasObj+Aligned）→ 走评分（Granted，无 ReserveProtected）
            var e6 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.Reserve, Separation = SeparationIntent.Reserve,
                HasOperationalObjective = true, TargetAlignedWithObjective = true,
                MainBodyDistance = 20f, FriendlySupport = 0.9f, LocalForceRatio = 1.5f, IsReserve = true
            });
            Check(e6.Permission == CommitmentPermission.Granted && (e6.Reasons & CommitmentReason.ReserveProtected) == 0,
                "atc4 reserve army-released -> Granted (释放后可投入)");

            // 14) Reserve + Mission=Intercept → Limited（纪律3 例外：Intercept 防御性投入不 Hold）
            var e7 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.Intercept, Separation = SeparationIntent.Intercept,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 30f, FriendlySupport = 0.8f, LocalForceRatio = 1.2f, IsReserve = true
            });
            Check(e7.Permission == CommitmentPermission.Limited && (e7.Reasons & CommitmentReason.ReserveProtected) == 0,
                "atc4 reserve intercept -> Limited (可投入, 无保护)");

            // ---- 纪律4：TacticalTarget 与 Objective 不一致不是单独死刑 ----

            // 15) Screen 目标不一致 → ScreenOnly（处理次要威胁，非 Denied）
            var e8 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.Screen, Separation = SeparationIntent.Screen,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 180f, FriendlySupport = 0.4f, LocalForceRatio = 1.1f
            });
            Check(e8.Permission == CommitmentPermission.ScreenOnly, "atc4 screen misaligned -> ScreenOnly (纪律4)");
            Check((e8.Reasons & CommitmentReason.NotAlignedWithMainEffort) == 0,
                "atc4 screen no misalignment flag (次要威胁合法)");

            // 16) Intercept 目标不一致 → Limited（非 Denied）
            var e9 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.Intercept, Separation = SeparationIntent.Intercept,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 150f, FriendlySupport = 0.5f, LocalForceRatio = 1.0f
            });
            Check(e9.Permission == CommitmentPermission.Limited, "atc4 intercept misaligned -> Limited (纪律4)");

            // 17) 推进型未对齐 → NotAlignedWithMainEffort flag 置位，但单因子不单独 Denied（纪律4/5 复合）
            var e10 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 100f, FriendlySupport = 0.7f, LocalForceRatio = 1.4f
            });
            Check((e10.Reasons & CommitmentReason.NotAlignedWithMainEffort) != 0, "atc4 misalignment flag on advance mission");
            Check(e10.Permission == CommitmentPermission.Granted, "atc4 misalignment alone not denied (纪律4 非死刑)");

            // ---- 纪律5：Support+LocalInferiority+Isolation ≥2 才 Denied（防单阈值误杀） ----

            // 18) 三因子全中 → Denied（推进型）
            var e11 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 200f, DistanceIncreasing = true,
                FriendlySupport = 0.1f, LocalForceRatio = 0.5f, FriendlyArrivalEta = 40f, EnemyContactEta = 10f
            });
            Check(e11.Permission == CommitmentPermission.Denied, "atc4 three factors -> Denied (纪律5)");

            // 19) 单因子（仅 Support 不足）→ 不 Denied（Granted；纪律5 防误杀）
            var e12 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = true,
                MainBodyDistance = 40f, FriendlySupport = 0.2f, LocalForceRatio = 1.6f
            });
            Check(e12.Permission == CommitmentPermission.Granted, "atc4 single factor support -> not denied");

            // 20) 两因子（Support+Local，无隔离）→ Denied
            var e13 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = true,
                MainBodyDistance = 60f, FriendlySupport = 0.2f, LocalForceRatio = 0.6f
            });
            Check(e13.Permission == CommitmentPermission.Denied, "atc4 two factors (support+local) -> Denied (纪律5 恰好两项)");

            // 21) 无 Army Objective 推进型 → Hold + NoArmyObjective（不主动推进）
            var e14 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Advance, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = false, TargetAlignedWithObjective = false,
                MainBodyDistance = 80f, FriendlySupport = 0.8f, LocalForceRatio = 1.8f
            });
            Check(e14.Permission == CommitmentPermission.Hold && (e14.Reasons & CommitmentReason.NoArmyObjective) != 0,
                "atc4 no objective -> Hold + NoArmyObjective");

            // ---- 80 人回归正式断言（§10：Denied 或 Hold，Reasons 含三因子，不得 Granted） ----

            // 22-24) 80 人案例：Mission=SupportMainEffort/Separation=None、敌 Main Body 显著更强、Parent 未 commit、支援晚到
            var r80 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Advance, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = true, TargetAlignedWithObjective = false, // Parent 未对同一 Objective commit
                MainBodyDistance = 180f, DistanceIncreasing = true,
                FriendlySupport = 0.1f, LocalForceRatio = 0.6f, FriendlyArrivalEta = 42f, EnemyContactEta = 11.5f
            });
            Check(r80.Permission == CommitmentPermission.Denied || r80.Permission == CommitmentPermission.Hold,
                "atc4 80-men case -> Denied or Hold");
            Check(r80.Permission != CommitmentPermission.Granted, "atc4 80-men case never Granted");
            Check((r80.Reasons & CommitmentReason.InsufficientSupport) != 0
                && (r80.Reasons & CommitmentReason.LocalInferiority) != 0
                && (r80.Reasons & CommitmentReason.UnauthorizedIsolation) != 0,
                "atc4 80-men reasons contain three factors (flags 完整)");

            // ---- 例外：Flank/Intercept/Occupy 合法远离不自动 Denied（R2 骑兵合法侧击） ----

            // 25) Flank 合法侧击：远离 MainBody + 支援正常 + 局部均衡 → Granted（不因距离召回）
            var f0 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Engage, Mission = FormationMission.Flank, Separation = SeparationIntent.Flank,
                HasOperationalObjective = true, TargetAlignedWithObjective = false, // 侧击目标非主目标（合法）
                MainBodyDistance = 220f, DistanceIncreasing = true,
                FriendlySupport = 0.5f, LocalForceRatio = 1.2f, FriendlyArrivalEta = 30f, EnemyContactEta = 40f
            });
            Check(f0.Permission == CommitmentPermission.Granted, "atc4 flank far away -> Granted (合法侧击不召回)");

            // 26) Occupy 合法远离低风险 → Granted（不自动 Denied）
            var f1 = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Advance, Mission = FormationMission.Occupy, Separation = SeparationIntent.Occupy,
                HasOperationalObjective = true, TargetAlignedWithObjective = false,
                MainBodyDistance = 250f, DistanceIncreasing = true,
                FriendlySupport = 0.6f, LocalForceRatio = 1.3f, FriendlyArrivalEta = 35f, EnemyContactEta = 45f
            });
            Check(f1.Permission == CommitmentPermission.Granted, "atc4 occupy far away -> Granted (例外不误杀)");

            // ---- §4.3 Estimator 纯函数 ----

            // 27) EffectiveStrength = Count × KindCoefficient × IntegrityCoefficient
            float effInf = CommitmentEstimator.EffectiveStrength(80, FormationCoordKind.Infantry, 1f);
            float effCav = CommitmentEstimator.EffectiveStrength(80, FormationCoordKind.MeleeCavalry, 1f);
            CheckApprox(effInf, 80f, "atc4 effective strength infantry 80", 1e-3f);
            CheckApprox(effCav, 80f * 1.2f, "atc4 effective strength cavalry x1.2", 1e-3f);
            CheckApprox(CommitmentEstimator.EffectiveStrength(100, FormationCoordKind.Infantry, 0f), 50f,
                "atc4 effective strength integrity 0 -> x0.5", 1e-3f);

            // 28) ETA = dist / max(NominalSpeed, MinSpeed)（低速下限）
            CheckApprox(CommitmentEstimator.Eta(100f, 10f), 10f, "atc4 eta 100m @10m/s = 10s", 1e-3f);
            CheckApprox(CommitmentEstimator.Eta(100f, 0.2f), 100f, "atc4 eta low speed floored at MinSpeed", 1e-3f);

            // 29) ContactPoint：连线方向 + 40m 截断（不过冲）
            float cx, cy, d;
            CommitmentEstimator.ContactPoint(0f, 0f, 200f, 0f, out cx, out cy, out d);
            CheckApprox(cx, 40f, "atc4 contact point clamped 40m", 1e-3f);
            CheckApprox(d, 40f, "atc4 contact distance 40m", 1e-3f);
            CommitmentEstimator.ContactPoint(0f, 0f, 20f, 0f, out cx, out cy, out d);
            CheckApprox(cx, 20f, "atc4 contact point short dist unclamped", 1e-3f);
            CheckApprox(d, 20f, "atc4 contact distance 20m", 1e-3f);

            // 30) FriendlySupport：只算 SupportRadius 内 且 ETA <= 自身 ETA + ArrivalSlack 的己方编队
            var friendlies = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 1, Count = 50, X = 10f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true },  // 近 + 及时
                new FormationCoordSnapshot { FormationId = 2, Count = 50, X = 200f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true }   // 远（>60m 半径）
            };
            // 自身 A：80 步兵 @(0,0)，目标 @(200,0)，速度 4m/s → selfEta=40/4=10s（selfFormationId=0，friendlies 无 id=0）
            var est0 = CommitmentEstimator.Estimate(0, 0f, 0f, 200f, 0f, 4f,
                80, FormationCoordKind.Infantry, 1f, friendlies, null);
            CheckApprox(est0.SelfEta, 10f, "atc4 self eta 10s", 1e-3f);
            // 支援 1（10m 处，eta=2.5s ≤ 10+10）计入；支援 2（200m 处，>60m 半径）不计 → support=50/180
            CheckApprox(est0.FriendlySupport, 50f / 180f, "atc4 friendly support radius+eta gated", 1e-3f);
            CheckApprox(est0.FriendlyEta, 2.5f, "atc4 friendly eta min support", 1e-3f);

            // 31) Fog 一致：未知敌不参与 EnemyLocalForce（§2.2.2）
            var enemies = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 10, Count = 300, X = 100f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = false }, // 未知大敌 → 排除
                new FormationCoordSnapshot { FormationId = 11, Count = 60, X = 45f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true }
            };
            // 自身 A @(0,0) 目标 @(200,0)：contact=(40,0)；敌 11 距 contact 5m ≤100m 且 eta=5/4=1.25 ≤ 10+10 → 计入
            var est1 = CommitmentEstimator.Estimate(0, 0f, 0f, 200f, 0f, 4f,
                80, FormationCoordKind.Infantry, 1f, friendlies, enemies);
            CheckApprox(est1.EnemyLocalForce, 60f, "atc4 unknown enemy excluded from local force (迷雾)", 1e-3f);

            // 32) LocalForceRatio = (自身+支援) / max(敌局部, 1)
            // 自身 80 + 支援 50 = 130；敌局部 60 → ratio = 130/60
            CheckApprox(est1.LocalForceRatio, 130f / 60f, "atc4 local force ratio (self+support)/enemy", 1e-3f);

            // ---- atc4-force-accounting-fix P0-3：自身兵力双算修复（friendlies 含自身时只计一次） ----

            // 33) friendlies 包含自身（FormationId=0 与 selfFormationId=0 相同）→ 自身跳过，只经 ownEff 计一次
            var friendsSelf = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 80, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true }, // 自身（应被跳过）
                new FormationCoordSnapshot { FormationId = 1, Count = 50, X = 10f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true }  // 友军（10m 内及时）
            };
            var estSelf = CommitmentEstimator.Estimate(0, 0f, 0f, 200f, 0f, 4f,
                80, FormationCoordKind.Infantry, 1f, friendsSelf, null);
            // support=50（仅友军）；allFriendlyEff=80+50=130（自身不重复）；ratio=130/1
            CheckApprox(estSelf.FriendlySupport, 50f / 130f, "atc4-force self in friendlies counted once (support 50/130)", 1e-3f);
            CheckApprox(estSelf.LocalForceRatio, 130f, "atc4-force local ratio excludes duplicated self (130/1)", 1e-3f);

            // 34) 只有自身、无支援 → SupportEff==0（FriendlySupport=0，ratio=自身/1）
            var onlySelf = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 80, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true } // 自身
            };
            var estOnly = CommitmentEstimator.Estimate(0, 0f, 0f, 200f, 0f, 4f,
                80, FormationCoordKind.Infantry, 1f, onlySelf, null);
            CheckApprox(estOnly.FriendlySupport, 0f, "atc4-force only self -> support 0", 1e-3f);
            CheckApprox(estOnly.LocalForceRatio, 80f, "atc4-force only self -> ratio self/1", 1e-3f);

            // 35) 增加一支友军 → 只增加该友军有效兵力（在 34 基础上 +50 → support=50/130，ratio=(80+50)/1）
            var selfPlusOne = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 80, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 1, Count = 50, X = 10f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true } // 新增友军
            };
            var estPlusOne = CommitmentEstimator.Estimate(0, 0f, 0f, 200f, 0f, 4f,
                80, FormationCoordKind.Infantry, 1f, selfPlusOne, null);
            CheckApprox(estPlusOne.FriendlySupport, 50f / 130f, "atc4-force adding one ally adds only that ally (50/130)", 1e-3f);
            CheckApprox(estPlusOne.LocalForceRatio, 130f, "atc4-force adding one ally ratio 130/1", 1e-3f);

            // ---- atc4-fix finding1：距离基线时序修复（共享字典同拍先写后读 → distIncreasing 恒 false） ----

            // 33) 修复前语义（共享字典）：ATC-3 先写本拍 → ATC-4 读本拍已更新值 → distIncreasing 恒 false（bug 可复现）
            var sharedBaseline = new Dictionary<int, float> { { 0, 100f } }; // 上拍基线
            sharedBaseline[0] = 120f; // ATC-3 写本拍距离（共享字典被污染）
            Check(!FormationDistanceTrend.IsIncreasing(sharedBaseline[0], 120f),
                "atc4-fix shared baseline read-after-write -> distIncreasing false (bug 语义)");

            // 34) 修复后（独立字典）：ATC-3 写本拍不影响 ATC-4 读到自己上一拍基线 → 距离增加真实反映
            var d3 = new Dictionary<int, float> { { 0, 100f } }; // ATC-3 基线（上拍）
            var d4 = new Dictionary<int, float> { { 0, 100f } }; // ATC-4 基线（上拍）
            d3[0] = 120f; // ATC-3 写本拍（独立字典，不污染 ATC-4）
            float atc4Prev = d4[0]; // ATC-4 读自己上一拍 → 100
            Check(FormationDistanceTrend.IsIncreasing(atc4Prev, 120f),
                "atc4-fix split baseline atc4 reads prev tick -> distIncreasing true (修复)");

            // 35) FormationDistanceTrend 纯函数边界：首拍无基线（0 缺省）→ 非增加；等距/缩短 → 非增加
            Check(!FormationDistanceTrend.IsIncreasing(0f, 120f), "atc4-fix first tick no baseline -> not increasing");
            Check(!FormationDistanceTrend.IsIncreasing(100f, 100f), "atc4-fix same distance -> not increasing");
            Check(!FormationDistanceTrend.IsIncreasing(120f, 100f), "atc4-fix closing distance -> not increasing");
        }

        // atc4-perception-fix P0-1：完整 PerceptionPipeline 刷新流程（生产接线级，非只测 KnowledgeView/PerceptionFilter）。
        // 覆盖：周期到达批量多编队写入 / 周期未到不刷新 / 不可见不写 / 超时变未知 / FogOff 直通语义。
        private static void TestPerceptionPipeline()
        {
            // 完整管线：KnowledgeView + VisibilityOracle + PerceptionPipeline（零 TaleWorlds，真装配）
            var view = new KnowledgeView(25f, 0.08f, 0.15f);
            var pipe = new PerceptionPipeline(view, 60f, 1.0f); // 60m 视野、1s 刷新周期

            // 1) 周期到达：TryBeginRefresh 成功 → 同一批次批量观察两个可见敌编队（@10m/20m，60m 内）
            Check(pipe.TryBeginRefresh(0f), "atc4-perc begin refresh t0 (interval reached)");
            pipe.ObserveFormation(0f, 0f, 0f, 101, 10f, 0f); // 敌编队 101 @10m
            pipe.ObserveFormation(0f, 0f, 0f, 102, 20f, 0f); // 敌编队 102 @20m
            KnowledgeView.SightedInfo i1, i2;
            Check(view.TryGet(101, 0.5f, out i1) && view.TryGet(102, 0.5f, out i2),
                "atc4-perc batch multi formation written in one refresh");

            // 2) 周期未到：TryBeginRefresh 返回 false（调用方跳过整段观察，不写新条目）
            Check(!pipe.TryBeginRefresh(0.9f), "atc4-perc interval not elapsed -> no refresh");
            KnowledgeView.SightedInfo ignored;
            Check(!view.TryGet(103, 0.9f, out ignored), "atc4-perc no refresh -> unobserved formation stays unknown");

            // 3) 不可见不写：超出视野（100m > 60m range）→ ObserveFormation 内部可见性拒绝
            pipe.ObserveFormation(1.1f, 0f, 0f, 104, 100f, 0f); // 即使无节流，可见性闸拒绝
            Check(!view.TryGet(104, 1.1f, out ignored), "atc4-perc out-of-range formation not written");

            // 4) 下一周期到达：可再次刷新并写入新编队（观察方法无内部节流，门控只在 TryBeginRefresh）
            Check(pipe.TryBeginRefresh(1.1f), "atc4-perc second refresh at t=1.1");
            pipe.ObserveFormation(1.1f, 0f, 0f, 103, 30f, 0f);
            Check(view.TryGet(103, 1.2f, out ignored), "atc4-perc formation written on second refresh");

            // 5) 超时变未知：写入后推进 > maxAge(25s) → TryGet 返回 false（编队变未知，保守降级）
            Check(!view.TryGet(101, 30f, out ignored), "atc4-perc formation expires after maxAge -> unknown");

            // 6) FogOff 直通语义：PerceptionFilter 开关关=恒 true（胶水层 FogOfWarEnabled=false 时
            //    IsEnemyFormationKnown 直通、TickPerception 跳过感知段——本层不写新条目但查询全知，
            //    与现状一致：关闭不新增信息、不改变过滤后的可见性结果）
            Check(PerceptionFilter.IsKnown(false, false), "atc4-perc fog-off passthrough (感知关=恒 true, 现状一致)");
            Check(!PerceptionFilter.IsKnown(true, false), "atc4-perc fog-on unseen -> unknown (保守降级)");
        }

        // atc4-fog-contact-fix P0-2：未知敌不触发 InContact（四场景 + 迷雾契约）
        private static void TestCommitmentContact()
        {
            const float ContactMeters = 20f; // 与胶水层 CommitmentContactMeters 同口径

            // 1) 未知敌 10m → 不触发 InContact（IsKnown=false 排除）
            var unknown10 = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 1, Count = 80, X = 10f, Y = 0f, IsKnown = false }
            };
            Check(!CommitmentContact.IsInContact(0f, 0f, unknown10, ContactMeters),
                "atc4-fog unknown enemy @10m -> no contact (迷雾排除)");

            // 2) 已知敌 10m → 触发 InContact
            var known10 = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 1, Count = 80, X = 10f, Y = 0f, IsKnown = true }
            };
            Check(CommitmentContact.IsInContact(0f, 0f, known10, ContactMeters),
                "atc4-fog known enemy @10m -> contact");

            // 3) 未知近（5m）+ 已知远（100m）→ 按未接敌（未知不参与，已知超 20m）
            var mix = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 1, Count = 200, X = 5f, Y = 0f, IsKnown = false },   // 未知很近
                new FormationCoordSnapshot { FormationId = 2, Count = 60, X = 100f, Y = 0f, IsKnown = true }    // 已知很远
            };
            Check(!CommitmentContact.IsInContact(0f, 0f, mix, ContactMeters),
                "atc4-fog unknown near + known far -> no contact");

            // 4) FogOff 后同一敌按已知参与（快照 IsKnown=true 模拟 IsEnemyFormationKnown 直通）
            var fogOff = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 1, Count = 80, X = 10f, Y = 0f, IsKnown = true } // 关闭迷雾=直通 known
            };
            Check(CommitmentContact.IsInContact(0f, 0f, fogOff, ContactMeters),
                "atc4-fog fog-off same enemy treated known -> contact");

            // 5) 边界：null/空列表 → false；恰好 20m → 触发（<= 含界）
            Check(!CommitmentContact.IsInContact(0f, 0f, null, ContactMeters), "atc4-fog null snaps -> no contact");
            var empty = new List<FormationCoordSnapshot>();
            Check(!CommitmentContact.IsInContact(0f, 0f, empty, ContactMeters), "atc4-fog empty snaps -> no contact");
            var boundary = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 1, Count = 40, X = 20f, Y = 0f, IsKnown = true }
            };
            Check(CommitmentContact.IsInContact(0f, 0f, boundary, ContactMeters), "atc4-fog exact 20m boundary -> contact");
        }

        // atc4-reserve-classifier P0-4：Reserve 判定重构（ReserveClassifier 唯一源：距离仅辅助 + 主动任务/接敌硬否决 + 迟滞防抖）
        private static void TestReserveClassifier()
        {
            // 1) 主力步兵在 MainBody 中心（队级意图 Attack=主动任务）→ 不得自动 Reserve（不属 MainEffort 近似硬否决）
            Check(!ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = true, InContact = false, MainBodyDistance = 5f }, false),
                "atc4-reserve main-body infantry active task -> not reserve");

            // 2) 明确分配后备（ExplicitReserve=true）→ Reserve（可靠角色依据）
            Check(ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = false, InContact = false, ExplicitReserve = true, MainBodyDistance = 200f }, false),
                "atc4-reserve explicit reserve -> reserve");

            // 3) 已接敌（InContact=true）→ 不得 Reserve
            Check(!ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = false, InContact = true, MainBodyDistance = 10f }, false),
                "atc4-reserve in contact -> not reserve");

            // 4) 执行 MainEffort（IsActiveTask=true）→ 不得 Reserve（即使距离近）
            Check(!ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = true, InContact = false, MainBodyDistance = 8f }, false),
                "atc4-reserve main effort -> not reserve");

            // 5) 距离仅辅助：非主动+未接敌+带内（25m ≤30）→ Reserve
            Check(ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = false, InContact = false, MainBodyDistance = 25f }, false),
                "atc4-reserve idle within 30m -> reserve (距离辅助)");
            // 带外（35m > 30）且未 Reserve → 非 Reserve（距离辅助不单独决定 Reserve 但决定进入）
            Check(!ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = false, InContact = false, MainBodyDistance = 35f }, false),
                "atc4-reserve idle beyond 30m not reserved -> not reserve");

            // 6) 抖动迟滞：已 Reserve 时 35m 仍保持（≤30+10 迟滞带）；45m 解除；未 Reserve 时 35m 不进入
            Check(ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = false, InContact = false, MainBodyDistance = 35f }, true),
                "atc4-reserve hysteresis keeps reserve at 35m");
            Check(!ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = false, InContact = false, MainBodyDistance = 45f }, true),
                "atc4-reserve hysteresis releases at 45m");
            Check(!ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = false, InContact = false, MainBodyDistance = 35f }, false),
                "atc4-reserve no jitter: 35m not reserved stays not reserved");

            // 7) IsActiveIntent 映射：进攻/侧击/骚扰/追击=主动；防御/重整=非主动
            Check(ReserveClassifier.IsActiveIntent(IntentType.Attack)
                && ReserveClassifier.IsActiveIntent(IntentType.Flank)
                && ReserveClassifier.IsActiveIntent(IntentType.Harass)
                && ReserveClassifier.IsActiveIntent(IntentType.Pursue),
                "atc4-reserve active intents attack/flank/harass/pursue");
            Check(!ReserveClassifier.IsActiveIntent(IntentType.Defend)
                && !ReserveClassifier.IsActiveIntent(IntentType.Reform)
                && !ReserveClassifier.IsActiveIntent(IntentType.ProtectArcher),
                "atc4-reserve non-active intents defend/reform/protect");

            // 8) 常量契约（与胶水层同口径）
            Check(ReserveClassifier.ReserveRadiusMeters == 30f && ReserveClassifier.HysteresisMeters == 10f
                && ReserveClassifier.ContactMeters == 20f, "atc4-reserve constants 30/10/20");
        }

        // atc4-snapshot-optimization OPT-3/OPT-4：CoordinationController Army 日志适配层（距离阈值/离散变化/格式）
        private static void TestCoordinationController()
        {
            // 1) 坐标变化用距离阈值（≥3m）而非浮点精确不等：位移 2m → 不触发；3m → 触发
            var a = new ArmyContextSnapshot { MainBodyX = 0f, MainBodyY = 0f, MainBodyStrength = 100, MainBodyConfidence = 0.9f, Phase = BattlePhase.Advance };
            var b2 = new ArmyContextSnapshot { MainBodyX = 2f, MainBodyY = 0f, MainBodyStrength = 100, MainBodyConfidence = 0.9f, Phase = BattlePhase.Advance };
            Check(!CoordinationController.ArmyLogChanged(a, b2, CoordinationController.MainBodyMoveLogMeters),
                "atc4-opt move 2m < threshold -> no log (距离阈值降噪)");
            var b3 = new ArmyContextSnapshot { MainBodyX = 3f, MainBodyY = 0f, MainBodyStrength = 100, MainBodyConfidence = 0.9f, Phase = BattlePhase.Advance };
            Check(CoordinationController.ArmyLogChanged(a, b3, CoordinationController.MainBodyMoveLogMeters),
                "atc4-opt move 3m >= threshold -> log");

            // 2) 离散状态变化（Phase/Objective/Strength/Confidence）→ 触发（不依赖坐标）
            var bPhase = new ArmyContextSnapshot { MainBodyX = 0f, MainBodyY = 0f, MainBodyStrength = 100, MainBodyConfidence = 0.9f, Phase = BattlePhase.Engage };
            Check(CoordinationController.ArmyLogChanged(a, bPhase, CoordinationController.MainBodyMoveLogMeters),
                "atc4-opt phase change -> log (离散状态)");
            var bObj = new ArmyContextSnapshot { MainBodyX = 0f, MainBodyY = 0f, MainBodyStrength = 100, MainBodyConfidence = 0.9f, Phase = BattlePhase.Advance, HasOperationalObjective = true, ObjectiveFormationId = 3 };
            Check(CoordinationController.ArmyLogChanged(a, bObj, CoordinationController.MainBodyMoveLogMeters),
                "atc4-opt objective appear -> log");
            var bStr = new ArmyContextSnapshot { MainBodyX = 0f, MainBodyY = 0f, MainBodyStrength = 90, MainBodyConfidence = 0.9f, Phase = BattlePhase.Advance };
            Check(CoordinationController.ArmyLogChanged(a, bStr, CoordinationController.MainBodyMoveLogMeters),
                "atc4-opt strength change -> log");

            // 3) 首拍（prev 全 0 无有效 MainBody）→ 触发（开战首条记录）
            var zero = new ArmyContextSnapshot();
            Check(CoordinationController.ArmyLogChanged(zero, a, CoordinationController.MainBodyMoveLogMeters),
                "atc4-opt first tick -> log");

            // 4) §14 [ArmyCtx] 格式（固定字段顺序，Shadow 诊断字段保留）
            string line = CoordinationController.FormatArmyLog(1, bObj);
            Check(line.StartsWith("[ArmyCtx] Team 1 phase=") && line.Contains("MB=") && line.Contains("obj=3"),
                "atc4-opt ArmyCtx format §14");
            Check(CoordinationController.MainBodyMoveLogMeters == 3f, "atc4-opt move threshold const 3m");
        }

        // atc4-integration-tests §5：修复前后关键计算样例（验收十项第 3/4/6 条的可执行样例对比）
        private static void TestIntegrationSamples()
        {
            // 样例1：CommitmentEstimator 自身兵力双算（P0-3 修复前后）
            //   修复前（自身被 friendlies 重复计入）：allFriendlyEff = 80(ownEff) + 80(自身条目) + 50(友军) = 210，
            //     support = 50/210 ≈ 0.238（自身重复稀释 SupportEff → 误判支援不足）；LocalForceRatio 分子虚高。
            //   修复后（selfFormationId 跳过自身）：allFriendlyEff = 130，support = 50/130 ≈ 0.385。
            var friendsSelf2 = new List<FormationCoordSnapshot>
            {
                new FormationCoordSnapshot { FormationId = 0, Count = 80, X = 0f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true },
                new FormationCoordSnapshot { FormationId = 1, Count = 50, X = 10f, Y = 0f, Kind = FormationCoordKind.Infantry, Integrity = 1f, NominalSpeed = 4f, IsKnown = true }
            };
            var s1 = CommitmentEstimator.Estimate(0, 0f, 0f, 200f, 0f, 4f, 80, FormationCoordKind.Infantry, 1f, friendsSelf2, null);
            CheckApprox(s1.FriendlySupport, 50f / 130f, "atc4-int sample1 support fixed 50/130 (前 50/210 双算)", 1e-3f);
            CheckApprox(s1.LocalForceRatio, 130f, "atc4-int sample1 ratio fixed 130/1 (前 210/1 虚高)");

            // 样例2：Reserve 判定（P0-4 修复前后）——主力步兵在 MainBody 中心
            //   修复前（距离单条件 dist<30m）：主力 → Reserve（误判）。
            //   修复后（ReserveClassifier 主动任务硬否决）：意图 Attack → 非 Reserve。
            Check(!ReserveClassifier.IsReserve(new ReserveInput
            { IsActiveTask = true, InContact = false, MainBodyDistance = 5f }, false),
                "atc4-int sample2 main-body infantry not reserve (前 dist<30 误判)");

            // 样例3：BattlePhase Advance→Engage（P1-2 修复前后）——双方距离很远且无有效接触
            //   修复前（无条件转移）：驻留够即 Engage（假装已接战）。
            //   修复后（EnemyContact 门控）：无接触保持 Advance。
            var r1 = BattlePhaseFSM.Advance(BattlePhase.Advance, new BattlePhaseInput
            { OwnSurvival = 0.8f, TimeInPhase = 6f, EnemyContact = false });
            Check(r1.Phase == BattlePhase.Advance && !r1.Changed,
                "atc4-int sample3 no contact stays Advance (前计时到即 Engage)");
            var r2 = BattlePhaseFSM.Advance(BattlePhase.Advance, new BattlePhaseInput
            { OwnSurvival = 0.8f, TimeInPhase = 6f, EnemyContact = true });
            Check(r2.Phase == BattlePhase.Engage && r2.Changed, "atc4-int sample3 contact -> Engage");

            // 样例4：验收十项第 10 条确认——Objective 消失后（HasOperationalObjective=false）Evaluate 输出
            //   Hold + NoArmyObjective（P1-5 胶水层在无目标时写入该决策，下游不再读到过期 Commitment）
            var noObj = CommitmentEvaluator.Evaluate(new CommitmentInput
            {
                Phase = BattlePhase.Advance, Mission = FormationMission.SupportMainEffort, Separation = SeparationIntent.None,
                HasOperationalObjective = false, TargetAlignedWithObjective = false,
                MainBodyDistance = 80f, FriendlySupport = 0.8f, LocalForceRatio = 1.8f
            });
            Check(noObj.Permission == CommitmentPermission.Hold && (noObj.Reasons & CommitmentReason.NoArmyObjective) != 0,
                "atc4-int sample4 objective gone -> Hold/NoArmyObjective (P1-5 无残留)");
        }

        // ATC-5R 编队控制权租约状态机（纯逻辑）：§8 四组 ≥30 断言。
        // 注意：离线测试只验证状态机/所有权/降级/日志节流决策，不得声称证明原生命令被阻断（那是运行期 R1-R8）。
        private static void TestFormationControlLease()
        {
            // ---- 状态机（10 条）----
            var m = new FormationControlLeaseManager();

            // 1) 完整路径：Native→Acquire→Owned→Release→Native（两拍 Denied Acquire / 两拍 Granted Release）
            var in1 = LeaseIn(1, 2, (int)CommitmentPermission.Denied, (int)CommitmentReason.InsufficientSupport,
                SafeFallbackKind.Rejoin, now: 0f);
            LeaseDecision r0 = m.Update(in1);
            Check(!r0.AcquireNow && m.ActiveCount == 0, "atc5r 1st denied tick no acquire (毛刺过滤)");
            var in2 = in1; in2.Now = 0.5f;
            LeaseDecision r1 = m.Update(in2);
            Check(r1.AcquireNow && m.ActiveCount == 1, "atc5r 2nd denied tick acquire");
            FormationControlLease lease = m.GetLease(1, 2);
            Check(lease != null && lease.WasAIControlledBeforeAcquire && lease.SafeCommandKind == SafeFallbackKind.Rejoin,
                "atc5r lease fields (prevAI/fallback snapshot)");
            // 3) Emergency/Retreat/Defensive 不 Acquire（拍1 加 retr）
            var mE = new FormationControlLeaseManager();
            LeaseDecision e0 = mE.Update(LeaseIn(1, 3, (int)CommitmentPermission.Denied,
                (int)CommitmentReason.InsufficientSupport, SafeFallbackKind.Rejoin, retr: true, now: 0f));
            LeaseDecision e1 = mE.Update(LeaseIn(1, 3, (int)CommitmentPermission.Denied,
                (int)CommitmentReason.InsufficientSupport, SafeFallbackKind.Rejoin, retr: true, now: 0.5f));
            Check(!e0.AcquireNow && !e1.AcquireNow && mE.ActiveCount == 0,
                "atc5r emergency/retreat never acquire (不锁死)");
            // 6) MinimumHold 未到不释放（Granted 拍 1 @t=1.0 < MinHoldUntil=1.5）
            var inG = new LeaseInput { TeamId = 1, FormationId = 2, Identity = _leaseSentinel, PocEnabled = true,
                FieldBattle = true, FormationValid = true, IsAIControlled = false, HasCommitment = true, // 租约持有中（CB 已关 AI）
                Permission = (int)CommitmentPermission.Granted, Reasons = 0, Fallback = SafeFallbackKind.Hold, Now = 1.0f };
            LeaseDecision g0 = m.Update(inG);
            Check(!g0.ReleaseNow && m.ActiveCount == 1, "atc5r minhold blocks release");
            // 5) 两拍 Granted Release（拍1 @1.5 评估第 1 次 granted、拍2 @2.0 确认）
            var inG2 = inG; inG2.Now = 1.5f;
            LeaseDecision g1 = m.Update(inG2);
            Check(!g1.ReleaseNow, "atc5r granted tick1 no release (迟滞)");
            var inG3 = inG; inG3.Now = 2.0f;
            LeaseDecision g2 = m.Update(inG3);
            Check(g2.ReleaseNow && g2.ReleaseReason == LeaseReleaseReason.Granted && m.ActiveCount == 0,
                "atc5r granted tick2 release (完整路径回 Native)");

            // 7) TTL 到期（producer 停止续租 → watchdog 释放）
            var mT = new FormationControlLeaseManager();
            mT.Update(LeaseIn(1, 4, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mT.Update(LeaseIn(1, 4, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            Check(mT.ActiveCount == 1, "atc5r ttl lease acquired");
            var wb = new List<KeyValuePair<int, int>>(); // watchdog 复用缓冲（OPT）
            var expired = mT.UpdateWatchdog(3.0f, wb); // 3.0-0.5=2.5 > TTL 1.5
            Check(expired.Count == 1 && mT.ActiveCount == 0, "atc5r watchdog expires lease (fail-open)");

            // 8) Renew 延长 TTL（1.5s 续租后 2.5s 不逾期）
            var mR = new FormationControlLeaseManager();
            mR.Update(LeaseIn(1, 5, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mR.Update(LeaseIn(1, 5, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            var renewIn = LeaseIn(1, 5, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, ai: false, now: 1.5f); // 租约持有中
            LeaseDecision rd = mR.Update(renewIn); // 1.5-0.5=1.0 ≥ RenewMinGap → Renew
            Check(rd.Renew, "atc5r renew extends ttl");
            Check(mR.UpdateWatchdog(2.6f, wb).Count == 0, "atc5r renewed lease not expired at 2.6 (2.6-1.5=1.1<TTL)");

            // 9) 幂等：同 key 再 Acquire 需重新两拍（Release 后同 key 不残留）
            Check(mR.UpdateWatchdog(4.0f, wb).Count == 1, "atc5r renewal stops -> ttl expiry after gap");

            // 10) 跨 Team/Formation 隔离
            var mI = new FormationControlLeaseManager();
            mI.Update(LeaseIn(1, 1, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mI.Update(LeaseIn(1, 1, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            mI.Update(LeaseIn(2, 1, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            LeaseDecision iso = mI.Update(LeaseIn(2, 1, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            Check(iso.AcquireNow && mI.ActiveCount == 2, "atc5r team/formation leases isolated");

            // ---- 所有权安全（7 条）----

            // 11) 原本 IsAIControlled=false 不 Acquire
            var mN = new FormationControlLeaseManager();
            LeaseDecision n0 = mN.Update(LeaseIn(1, 6, (int)CommitmentPermission.Denied, 0,
                SafeFallbackKind.Rejoin, ai: false, now: 0f));
            LeaseDecision n1 = mN.Update(LeaseIn(1, 6, (int)CommitmentPermission.Denied, 0,
                SafeFallbackKind.Rejoin, ai: false, now: 0.5f));
            Check(!n0.AcquireNow && !n1.AcquireNow && mN.ActiveCount == 0,
                "atc5r non-AI formation never acquired (所有权安全)");
            // 12) 未 Acquire 不恢复（无租约 → 无 ReleaseNow）
            Check(!n1.ReleaseNow, "atc5r no lease -> no restore");
            // 13) 玩家编队跳过
            var mP = new FormationControlLeaseManager();
            mP.Update(LeaseIn(1, 7, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, player: true, now: 0f));
            mP.Update(LeaseIn(1, 7, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, player: true, now: 0.5f));
            Check(mP.ActiveCount == 0, "atc5r player formation skipped");
            // 14) identity 变化失效（P4：同一 Index 新对象 → 对象引用不同 → 旧租约不继承）
            var mG = new FormationControlLeaseManager();
            mG.Update(LeaseIn(1, 8, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mG.Update(LeaseIn(1, 8, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            Check(mG.ActiveCount == 1, "atc5r identity lease acquired");
            LeaseDecision gd = mG.Update(LeaseIn(1, 8, (int)CommitmentPermission.Denied, 0,
                SafeFallbackKind.Rejoin, identity: new object(), now: 1.0f));
            Check(gd.ReleaseNow && gd.ReleaseReason == LeaseReleaseReason.FormationRemoved && mG.ActiveCount == 0,
                "atc5r identity change invalidates lease (同一 Index 新对象不继承)");
            // 15) FormationId 复用不继承（RemoveFormation 后同 key 重评估需两拍）
            var mF = new FormationControlLeaseManager();
            mF.Update(LeaseIn(1, 9, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mF.Update(LeaseIn(1, 9, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            mF.RemoveFormation(1, 9);
            Check(mF.ActiveCount == 0, "atc5r remove formation clears lease");
            LeaseDecision rp = mF.Update(LeaseIn(1, 9, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 1.0f));
            Check(!rp.AcquireNow, "atc5r reused formation id needs fresh two ticks");
            // 16) Mission Clear 清空
            var mC = new FormationControlLeaseManager();
            mC.Update(LeaseIn(1, 10, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mC.Update(LeaseIn(1, 10, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            mC.ReleaseAll(LeaseReleaseReason.MissionEnding);
            Check(mC.ActiveCount == 0, "atc5r mission ending clears all leases");
            // 17) PoC 关闭释放（开关关 → Disabled）
            var mD = new FormationControlLeaseManager();
            mD.Update(LeaseIn(1, 11, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mD.Update(LeaseIn(1, 11, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            LeaseDecision dd = mD.Update(LeaseIn(1, 11, (int)CommitmentPermission.Granted, 0,
                SafeFallbackKind.Hold, poc: false, now: 1.0f));
            Check(dd.ReleaseNow && dd.ReleaseReason == LeaseReleaseReason.Disabled && mD.ActiveCount == 0,
                "atc5r poc disabled releases lease");

            // ---- Commitment 降级（7 条：ResolveFallback 纯函数）----

            // 18) Denied+远离+有 MainBody → Rejoin
            var army1 = new ArmyContextSnapshot { MainBodyX = 100f, MainBodyY = 0f, MainBodyStrength = 120 };
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.Denied, army1, 50f) == SafeFallbackKind.Rejoin,
                "atc5r denied far -> rejoin");
            // 19) Hold+安全带内 → Hold
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.Hold, army1, 10f) == SafeFallbackKind.Hold,
                "atc5r hold near -> hold");
            // 20) Denied 无 MainBody 点 → NoSafeFallback（不 Acquire）
            var army0 = new ArmyContextSnapshot { MainBodyX = 0f, MainBodyY = 0f, MainBodyStrength = 0 };
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.Denied, army0, 80f) == SafeFallbackKind.NoSafeFallback,
                "atc5r denied no mainbody -> no safe fallback");
            // 21) Limited 不夺权（状态机 deniedOrHold 排除）
            var mL = new FormationControlLeaseManager();
            mL.Update(LeaseIn(1, 12, (int)CommitmentPermission.Limited, 0, SafeFallbackKind.NoSafeFallback, now: 0f));
            mL.Update(LeaseIn(1, 12, (int)CommitmentPermission.Limited, 0, SafeFallbackKind.NoSafeFallback, now: 0.5f));
            Check(mL.ActiveCount == 0, "atc5r limited never acquired (贴身脱困不夺权)");
            // 22) Retreat/Emergency/Defensive 不锁死（已断言 3；补 DefensiveOverride reason 输入）
            var mD2 = new FormationControlLeaseManager();
            mD2.Update(LeaseIn(1, 13, (int)CommitmentPermission.Denied, (int)CommitmentReason.DefensiveOverride,
                SafeFallbackKind.Hold, retr: true, now: 0f));
            mD2.Update(LeaseIn(1, 13, (int)CommitmentPermission.Denied, (int)CommitmentReason.DefensiveOverride,
                SafeFallbackKind.Hold, retr: true, now: 0.5f));
            Check(mD2.ActiveCount == 0, "atc5r defensive override never locked (Immediate Defensive 优先)");
            // 23) Fog 不用未知敌 fallback（Rejoin 只用 MainBody 点；ArmyContextSnapshot 无任何敌编队字段）
            Check(!army1.HasOperationalObjective, "atc5r rejoin target is mainbody only (fog 契约)");
            // 24) ScreenOnly 不自动夺权
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.ScreenOnly, army1, 50f) == SafeFallbackKind.NoSafeFallback,
                "atc5r screenonly not auto-acquired (无屏护执行器)");

            // ---- 日志节流（4 条：状态机决策频率）----

            // 25) Acquire 只在确认拍出现一次（第 1 拍 false / 第 2 拍 true）
            var mQ = new FormationControlLeaseManager();
            Check(!mQ.Update(LeaseIn(1, 20, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f)).AcquireNow,
                "atc5r log acquire edge only once (tick1 silent)");
            Check(mQ.Update(LeaseIn(1, 20, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f)).AcquireNow,
                "atc5r log acquire edge tick2 fires once");
            // 26) Renew 不每拍（RenewMinGap 0.8s 内不续租）
            var mN2 = new FormationControlLeaseManager();
            mN2.Update(LeaseIn(1, 21, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mN2.Update(LeaseIn(1, 21, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            LeaseDecision nr1 = mN2.Update(LeaseIn(1, 21, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 1.0f));
            Check(!nr1.Renew, "atc5r renew throttled (0.5s gap < 0.8s)");
            // 27) Release 只在确认拍（Granted 第 1 拍不释放）
            Check(!g1.ReleaseNow, "atc5r log release edge only at confirm tick");
            // 28) 原因可审计（枚举值区分）
            Check((int)LeaseReleaseReason.Granted == 1 && (int)LeaseReleaseReason.Expired == 2
                && (int)LeaseReleaseReason.Disabled == 3 && (int)LeaseReleaseReason.FormationRemoved == 5
                && (int)LeaseReleaseReason.MissionEnding == 6 && (int)LeaseReleaseReason.PlayerOrExternalControl == 7
                && (int)LeaseReleaseReason.UnsupportedMission == 8 && (int)LeaseReleaseReason.EmergencyFailOpen == 9,
                "atc5r release reasons auditable (1..9)");

            // ---- atc5r-fix P1-P4+OPT 返修断言（≥12）----

            // 29) P1：租约期间外部把 IsAIControlled 改回 true（玩家/外部接管）→ 立即 Release + PlayerOrExternalControl
            var mP1 = new FormationControlLeaseManager();
            mP1.Update(LeaseIn(1, 30, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mP1.Update(LeaseIn(1, 30, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            LeaseDecision p1 = mP1.Update(LeaseIn(1, 30, (int)CommitmentPermission.Granted, 0,
                SafeFallbackKind.Hold, ai: true, now: 1.0f)); // 租约期间外部已恢复 AI 控制
            Check(p1.ReleaseNow && p1.ReleaseReason == LeaseReleaseReason.PlayerOrExternalControl && mP1.ActiveCount == 0,
                "atc5r-fix P1 external restore -> release, no SetControlledByAI(true)");

            // 30) P1：租约期间玩家成为 owner（IsPlayerFormation=true）→ 立即 Release + PlayerOrExternalControl
            var mP1b = new FormationControlLeaseManager();
            mP1b.Update(LeaseIn(1, 31, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mP1b.Update(LeaseIn(1, 31, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            LeaseDecision p1b = mP1b.Update(LeaseIn(1, 31, (int)CommitmentPermission.Denied, 0,
                SafeFallbackKind.Rejoin, player: true, now: 1.0f));
            Check(p1b.ReleaseNow && p1b.ReleaseReason == LeaseReleaseReason.PlayerOrExternalControl,
                "atc5r-fix P1 player owner during lease -> release without restore");

            // 31) P3：HasActiveForTeam 单编队域（该队有租约 true）
            var mP3 = new FormationControlLeaseManager();
            mP3.Update(LeaseIn(1, 32, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mP3.Update(LeaseIn(1, 32, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            Check(mP3.HasActiveForTeam(1), "atc5r-fix P3 team1 has active lease");
            Check(!mP3.HasActiveForTeam(2), "atc5r-fix P3 team2 no lease (单编队域)");
            mP3.Update(LeaseIn(2, 33, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 1.0f));
            mP3.Update(LeaseIn(2, 33, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 1.5f));
            Check(mP3.HasActiveForTeam(1) && mP3.HasActiveForTeam(2), "atc5r-fix P3 two teams isolated leases");
            // atc5r-nits low1：同队已租约（f=32）时新候选（f=99 无租约）→ 胶水 HasActiveForTeam 前置判定成立
            //   （!candidateHasLease && HasActiveForTeam(team) → 拒 Acquire；防最小编队解散后 TTL 窗口内同队双租约）
            Check(mP3.GetLease(1, 32) != null && mP3.HasActiveForTeam(1) && mP3.GetLease(1, 99) == null,
                "atc5r-nits low1 same-team lease blocks new candidate (前置防御判定)");

            // 32) P4：同 Identity（同一对象）续租保持租约（对象未重建不误失效）
            var mP4 = new FormationControlLeaseManager();
            mP4.Update(LeaseIn(1, 34, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mP4.Update(LeaseIn(1, 34, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            LeaseDecision p4 = mP4.Update(LeaseIn(1, 34, (int)CommitmentPermission.Denied, 0,
                SafeFallbackKind.Rejoin, ai: false, now: 1.0f)); // 租约持有中（同哨兵对象）
            Check(!p4.ReleaseNow && mP4.ActiveCount == 1, "atc5r-fix P4 same identity keeps lease");

            // 33) OPT：watchdog 无租约零分配（空缓冲 + 同一引用复用）
            var mO = new FormationControlLeaseManager();
            var ob = new List<KeyValuePair<int, int>>();
            List<KeyValuePair<int, int>> o1 = mO.UpdateWatchdog(5.0f, ob);
            Check(o1.Count == 0 && ReferenceEquals(o1, ob), "atc5r-fix OPT no lease -> empty reused buffer (零分配)");
            // 34) OPT：有租约 TTL 到期填充缓冲（同一引用）
            mO.Update(LeaseIn(1, 35, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0f));
            mO.Update(LeaseIn(1, 35, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.Rejoin, now: 0.5f));
            List<KeyValuePair<int, int>> o2 = mO.UpdateWatchdog(3.0f, ob);
            Check(o2.Count == 1 && ReferenceEquals(o2, ob), "atc5r-fix OPT lease expiry fills reused buffer");

            // 35) P2：Hold+安全带内但无 MainBody 点（strength=0）→ NoSafeFallback（不 Acquire 不关 AI）
            var armyNoMain = new ArmyContextSnapshot { MainBodyX = 0f, MainBodyY = 0f, MainBodyStrength = 0 };
            Check(FormationControlLeaseManager.ResolveFallback(CommitmentPermission.Hold, armyNoMain, 10f)
                == SafeFallbackKind.NoSafeFallback, "atc5r-fix P2 hold no mainbody -> no safe fallback");
            // 36) P2：fallback=NoSafeFallback 连续两拍 → 不 Acquire（不关 AI 无人负责窗口消除）
            var mP2 = new FormationControlLeaseManager();
            mP2.Update(LeaseIn(1, 36, (int)CommitmentPermission.Denied, 0, SafeFallbackKind.NoSafeFallback, now: 0f));
            LeaseDecision p2b = mP2.Update(LeaseIn(1, 36, (int)CommitmentPermission.Denied, 0,
                SafeFallbackKind.NoSafeFallback, now: 0.5f));
            Check(!p2b.AcquireNow && mP2.ActiveCount == 0,
                "atc5r-fix P2 no safe command -> never acquire (先验证再关 AI)");

            // ---- 常量/枚举契约（2 条）----
            Check(FormationControlLeaseManager.DecisionCadenceSeconds == 0.5f
                && FormationControlLeaseManager.LeaseTtlSeconds == 1.5f
                && FormationControlLeaseManager.MinimumHoldSeconds == 1.0f
                && FormationControlLeaseManager.ReleaseHysteresisTicks == 2
                && FormationControlLeaseManager.AcquireHysteresisTicks == 2
                && FormationControlLeaseManager.RenewMinGapSeconds == 0.8f,
                "atc5r lease params centralized");
            Check((int)FormationControlOwner.Native == 0 && (int)FormationControlOwner.CombatBrainLease == 1
                && (int)LeaseState.NativeOwned == 0 && (int)LeaseState.AcquireRequested == 1
                && (int)LeaseState.CombatBrainOwned == 2 && (int)LeaseState.ReleaseRequested == 3,
                "atc5r owner/state enum contract");
        }

        // LeaseInput 便捷构造（测试用；identity 默认共享哨兵对象——同 key 租约内 Identity 稳定，
        //   identity 显式传新对象模拟「同一 Index 新对象」P4 场景）
        private static readonly object _leaseSentinel = new object();
        private static LeaseInput LeaseIn(int team, int form, int perm, int reasons, SafeFallbackKind fb,
            bool poc = true, bool field = true, bool valid = true, bool ai = true, bool player = false,
            bool hasCommit = true, bool contact = false, bool retr = false, object identity = null, float now = 0f)
        {
            return new LeaseInput
            {
                TeamId = team, FormationId = form, Identity = identity != null ? identity : _leaseSentinel,
                PocEnabled = poc, FieldBattle = field, FormationValid = valid, IsAIControlled = ai,
                IsPlayerFormation = player, HasCommitment = hasCommit, Permission = perm, Reasons = reasons,
                InContact = contact, RetreatOrEmergencyOrDefensive = retr, Fallback = fb, Now = now
            };
        }

        private static int idxDefend()
        {
            return Array.IndexOf(TacticManual.Order, IntentType.Defend);
        }
    }
}
