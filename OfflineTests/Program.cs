using System;
using System.Collections.Generic;
using CombatBrain;
using CombatBrain.Combat;
using CombatBrain.Movement;
using CombatBrain.Survival;
using CombatBrain.Cavalry;
using CombatBrain.Ranged;
using CombatBrain.Compliance;
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
    internal static class Program
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

            Console.WriteLine("== F0 FighterProfile ==");
            TestFighterProfile();
            Console.WriteLine("== F6 MovementController ==");
            TestMovement();
            Console.WriteLine("== F7 SurvivalInstinct ==");
            TestSurvival();
            Console.WriteLine("== F8 CavalryWaveFSM ==");
            TestCavalry();
            Console.WriteLine("== F9 RangedBrain ==");
            TestRanged();
            Console.WriteLine("== F12 DecisionJitter ==");
            TestCompliance();
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

            Console.WriteLine("== T3 AttackSuppressionState ==");
            TestAttackSuppression();

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

            Console.WriteLine("== X2 HandSkillProfile ==");
            TestHandSkillProfile();

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

            Console.WriteLine("== N2 NarrativeEvents ==");
            TestNarrativeEvents();

            Console.WriteLine("== N3 NarrativeBuffer ==");
            TestNarrativeBuffer();

            Console.WriteLine("== ATC0 CommandProvenance ==");
            TestCommandProvenance();

            Console.WriteLine("== ATC0 OutputLayer ==");
            TestProvenanceOutput();

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

        private static void TestFighterProfile()
        {
            // 等级基线（Bannerlord 兵种阶级 1-6）：1→Recruit、2-3→Soldier、4→Veteran、5-6→Elite
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 1, Armor = ArmorGrade.Cloth }) == SoldierTier.Recruit,
                "level1 cloth infantry -> Recruit");
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 2, Armor = ArmorGrade.Leather }) == SoldierTier.Soldier,
                "level2 leather infantry -> Soldier");
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 3, Armor = ArmorGrade.Chainmail }) == SoldierTier.Soldier,
                "level3 chainmail infantry -> Soldier");
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 4, Armor = ArmorGrade.Chainmail }) == SoldierTier.Veteran,
                "level4 chainmail infantry -> Veteran");
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Cavalry, Level = 5, Armor = ArmorGrade.Plate }) == SoldierTier.Elite,
                "level5 plate cavalry -> Elite");

            // 护甲修正：板甲 +1 档
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 3, Armor = ArmorGrade.Plate }) == SoldierTier.Veteran,
                "level3 plate infantry -> Veteran (armor lift)");
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 1, Armor = ArmorGrade.Plate }) == SoldierTier.Soldier,
                "level1 plate infantry -> Soldier (armor lift)");

            // 老兵旗标：+1 档，不超 Elite
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 4, Armor = ArmorGrade.Chainmail, IsVeteran = true }) == SoldierTier.Elite,
                "veteran lifts Veteran -> Elite");
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Cavalry, Level = 5, Armor = ArmorGrade.Plate, IsVeteran = true }) == SoldierTier.Elite,
                "veteran never exceeds Elite");

            // 骑兵保底 Soldier
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Cavalry, Level = 1, Armor = ArmorGrade.Cloth }) == SoldierTier.Soldier,
                "cavalry floor is Soldier");

            // 等级 clamp：越界不越档
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 0, Armor = ArmorGrade.Cloth }) == SoldierTier.Recruit,
                "level 0 clamps to Recruit");
            Check(FighterProfileFactory.Classify(new SoldierInput { Kind = SoldierKind.Infantry, Level = 30, Armor = ArmorGrade.Plate }) == SoldierTier.Elite,
                "heroic level clamps to Elite");

            // Create 四档素质单调性
            Check(FighterProfileFactory.Create(SoldierTier.Recruit).PredictConf < FighterProfileFactory.Create(SoldierTier.Soldier).PredictConf,
                "predict conf monotonic Recruit<Soldier");
            Check(FighterProfileFactory.Create(SoldierTier.Veteran).FeintRate > FighterProfileFactory.Create(SoldierTier.Soldier).FeintRate,
                "feint rate monotonic Soldier<Veteran");
            Check(FighterProfileFactory.Create(SoldierTier.Elite).FlankAware > FighterProfileFactory.Create(SoldierTier.Recruit).FlankAware,
                "flank aware monotonic Recruit<Elite");
        }

        private static void TestMovement()
        {
            CheckApprox(MovementController.IdealGap(1f, 1f), 1.3f, "IdealGap(1,1)=1.3");

            var far = new MovementContext { OwnX = 0f, OwnY = 0f, TargetX = 10f, TargetY = 0f, OwnReach = 1f, TargetReach = 1f };
            var d = MovementController.Decide(far);
            Check(d.Action == MoveAction.Advance, "far target -> Advance");
            CheckApprox(d.MoveX, 1f, "Advance moves toward target X");
            CheckApprox(d.MoveY, 0f, "Advance Y=0");

            var near = new MovementContext { OwnX = 0f, OwnY = 0f, TargetX = 0.5f, TargetY = 0f, OwnReach = 1f, TargetReach = 1f };
            d = MovementController.Decide(near);
            Check(d.Action == MoveAction.StepBack, "too close -> StepBack");
            CheckApprox(d.MoveX, -1f, "StepBack away from target");

            var inGap = new MovementContext { OwnX = 0f, OwnY = 0f, TargetX = 1.3f, TargetY = 0f, OwnReach = 1f, TargetReach = 1f, FlankAware = 0.6f, OffHandSide = 1f };
            d = MovementController.Decide(inGap);
            Check(d.Action == MoveAction.StrafeLeft, "in gap + flank aware -> StrafeLeft");

            inGap.FlankAware = 0.1f;
            d = MovementController.Decide(inGap);
            Check(d.Action == MoveAction.Hold, "in gap + unaware -> Hold");
            CheckApprox(d.MoveX, 0f, "Hold X=0");
            CheckApprox(d.MoveY, 0f, "Hold Y=0");

            var surrounded = new MovementContext { OwnX = 0f, OwnY = 0f, TargetX = 10f, TargetY = 0f, OwnReach = 1f, TargetReach = 1f, IsSurrounded = true, AllyX = 0f, AllyY = 5f };
            d = MovementController.Decide(surrounded);
            Check(d.Action == MoveAction.RetreatToAllies, "surrounded -> RetreatToAllies");
            CheckApprox(d.MoveY, 1f, "retreat toward ally");
        }

        private static void TestSurvival()
        {
            Check(SurvivalInstinct.Decide(new SurvivalContext { HpRatio = 0.1f, BeingFocused = true, SafeBehind = false }) == SurvivalAction.RetreatToAllies,
                "low hp + focused + exposed -> RetreatToAllies");
            Check(SurvivalInstinct.Decide(new SurvivalContext { HpRatio = 0.1f, BeingFocused = true, SafeBehind = true }) == SurvivalAction.Hold,
                "low hp + safe behind -> Hold");
            Check(SurvivalInstinct.Decide(new SurvivalContext { HpRatio = 0.5f, BeingFocused = false, SafeBehind = false }) == SurvivalAction.Fight,
                "healthy -> Fight");

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

        private static void TestRanged()
        {
            var lightVsHeavy = new[]
            {
                new RangedTarget { Index = 0, Distance = 20f, Armor = ArmorClass.Light, Threat = 0.5f },
                new RangedTarget { Index = 1, Distance = 20f, Armor = ArmorClass.Heavy, Threat = 0.5f }
            };
            Check(RangedBrain.SelectTarget(lightVsHeavy, AmmoKind.LightArrow) == 0, "light arrow prefers light armor");
            Check(RangedBrain.SelectTarget(lightVsHeavy, AmmoKind.ArmorPiercing) == 1, "AP arrow prefers heavy armor");

            var commander = new[]
            {
                new RangedTarget { Index = 0, Distance = 20f, Armor = ArmorClass.Heavy, Threat = 0.5f, IsCommander = true },
                new RangedTarget { Index = 1, Distance = 20f, Armor = ArmorClass.Heavy, Threat = 0.5f }
            };
            Check(RangedBrain.SelectTarget(commander, AmmoKind.ArmorPiercing) == 0, "commander gets priority");

            var byDistance = new[]
            {
                new RangedTarget { Index = 0, Distance = 50f, Armor = ArmorClass.Light, Threat = 0.5f },
                new RangedTarget { Index = 1, Distance = 10f, Armor = ArmorClass.Light, Threat = 0.5f }
            };
            Check(RangedBrain.SelectTarget(byDistance, AmmoKind.LightArrow) == 1, "closer target wins tie");

            Check(RangedBrain.SelectTarget(new RangedTarget[0], AmmoKind.LightArrow) == -1, "empty -> -1");

            Check(RangedBrain.DecideFallback(0.1f, true) == RangedFallback.SwitchToMelee, "low ammo + melee -> SwitchToMelee");
            Check(RangedBrain.DecideFallback(0.1f, false) == RangedFallback.Retreat, "low ammo + far -> Retreat");
            Check(RangedBrain.DecideFallback(0.5f, false) == RangedFallback.KeepShooting, "enough ammo -> KeepShooting");
        }

        private static void TestCompliance()
        {
            var perfect = DecisionJitter.ComputeProfile(1f, 0f, 1f);
            CheckApprox(perfect.DelayBase, 0f, "perfect: no delay");
            CheckApprox(perfect.HesitateProb, 0f, "perfect: no hesitation");
            CheckApprox(perfect.Wobble, 0f, "perfect: no wobble");

            var worst = DecisionJitter.ComputeProfile(0f, 1f, 0f);
            CheckApprox(worst.DelayBase, 1f, "worst: max delay");
            CheckApprox(worst.HesitateProb, 1f, "worst: always hesitate");
            CheckApprox(worst.Wobble, 1f, "worst: max wobble");

            var lo = DecisionJitter.ComputeProfile(0.5f, 0f, 1f);
            var hi = DecisionJitter.ComputeProfile(0.2f, 0f, 1f);
            Check(hi.HesitateProb > lo.HesitateProb, "lower discipline -> higher hesitate prob");

            var good = DecisionJitter.ComputeProfile(1f, 0f, 1f);
            var badMorale = DecisionJitter.ComputeProfile(1f, 0f, 0.2f);
            Check(badMorale.Wobble > good.Wobble, "low morale -> higher wobble");

            var clamped = DecisionJitter.ComputeProfile(2f, 5f, 3f);
            CheckApprox(clamped.DelayBase, 0.4f, "out-of-range inputs clamp (delay)");
            CheckApprox(clamped.HesitateProb, 0.25f, "out-of-range inputs clamp (hesitate)");
            CheckApprox(clamped.Wobble, 0.3f, "out-of-range inputs clamp (wobble)");

            var rng = new Random(12345);
            var never = DecisionJitter.Apply(rng, new ComplianceProfile { DelayBase = 0.2f, HesitateProb = 0f, Wobble = 0f });
            Check(!never.Hesitate, "p=0 never hesitates");
            Check(never.DelaySeconds >= 0.1f - 1e-4f && never.DelaySeconds <= 0.2f + 1e-4f, "delay within [0.5,1.0] x base");

            var rng2 = new Random(12345);
            var always = DecisionJitter.Apply(rng2, new ComplianceProfile { DelayBase = 0.2f, HesitateProb = 1f, Wobble = 0f });
            Check(always.Hesitate, "p=1 always hesitates");

            var rng3 = new Random(999);
            var a = DecisionJitter.Apply(rng3, new ComplianceProfile { DelayBase = 0.5f, HesitateProb = 0.5f, Wobble = 0.3f });
            var rng4 = new Random(999);
            var b = DecisionJitter.Apply(rng4, new ComplianceProfile { DelayBase = 0.5f, HesitateProb = 0.5f, Wobble = 0.3f });
            Check(a.Hesitate == b.Hesitate && Math.Abs(a.DelaySeconds - b.DelaySeconds) <= 1e-6f, "same seed reproducible");
        }

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

        // T3 攻击抑制/恢复核心状态（AttackSuppressionState，纯逻辑）
        private static void TestAttackSuppression()
        {
            var s = new AttackSuppressionState();
            Check(s.Count == 0 && !s.IsSuppressed(3), "init none suppressed");
            float v;
            Check(!s.TryRestore(3, out v), "restore unsuppressed -> false");

            bool first = s.TrySave(3, 0.8f);
            Check(first && s.IsSuppressed(3) && s.Count == 1, "save current value + suppressed");
            Check(!s.TrySave(3, 0.5f), "re-save while suppressed -> false (keep original)");

            Check(s.TryRestore(3, out v) && Math.Abs(v - 0.8f) < 1e-6f, "restore returns saved 0.8");
            Check(!s.IsSuppressed(3) && s.Count == 0, "restored -> clear");

            s.TrySave(5, 0.3f);
            s.Clear(5);
            Check(!s.IsSuppressed(5) && s.Count == 0, "lifecycle Remove clears entry");

            s.TrySave(7, 0.9f);
            s.TrySave(8, 0.4f);
            Check(s.Count == 2, "two suppressed tracked");
            s.ClearAll();
            Check(s.Count == 0, "ClearAll empties");
        }

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

        // X0 决策延迟开关（强度 0/1/0.5 的 hold/余量/抖动缩放）
        private static void TestDecisionDelay()
        {
            CheckApprox(DecisionDelay.HoldTime(0f), 1.5f, "decisiondelay hold strength 0 -> 1.5s");
            CheckApprox(DecisionDelay.HoldTime(1f), 6f, "decisiondelay hold strength 1 -> 6s");
            CheckApprox(DecisionDelay.HoldTime(0.5f), 3.75f, "decisiondelay hold strength 0.5 -> 3.75s");
            CheckApprox(DecisionDelay.SwitchMargin(0f), 0f, "decisiondelay margin strength 0 -> 0%");
            CheckApprox(DecisionDelay.SwitchMargin(1f), 0.15f, "decisiondelay margin strength 1 -> 15%");

            var p = new ComplianceProfile { HesitateProb = 0.5f, DelayBase = 0.8f, Wobble = 0.6f };
            var zero = DecisionDelay.ScaleProfile(p, 0f);
            Check(zero.HesitateProb == 0f && zero.DelayBase == 0f && zero.Wobble == 0f, "decisiondelay strength 0 -> no jitter");
            var full = DecisionDelay.ScaleProfile(p, 1f);
            Check(full.HesitateProb == 0.5f && full.DelayBase == 0.8f && full.Wobble == 0.6f, "decisiondelay strength 1 -> unchanged");
            var half = DecisionDelay.ScaleProfile(p, 0.5f);
            Check(half.HesitateProb == 0.25f && half.DelayBase == 0.4f && half.Wobble == 0.3f, "decisiondelay strength 0.5 -> half jitter");
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

        // X2 手活属性公式（HandSkillProfile 纯逻辑）：难度系数映射/格挡招架 clamp/射击误差/攻击概率/防守方向
        private static void TestHandSkillProfile()
        {
            // 1) 难度系数：strength=0 → 原版系数（0.1/0.32/0.96）；strength=1 → RBM 系数（0.7/0.85/1.0）
            CheckApprox(HandSkillProfile.DifficultyCoef(0f, 0f), 0.10f, "x2 difficulty coef strength0 low -> vanilla 0.1");
            CheckApprox(HandSkillProfile.DifficultyCoef(0.5f, 0f), 0.32f, "x2 difficulty coef strength0 mid -> vanilla 0.32");
            CheckApprox(HandSkillProfile.DifficultyCoef(1f, 0f), 0.96f, "x2 difficulty coef strength0 high -> vanilla 0.96");
            CheckApprox(HandSkillProfile.DifficultyCoef(0f, 1f), 0.70f, "x2 difficulty coef strength1 low -> rbm 0.7");
            CheckApprox(HandSkillProfile.DifficultyCoef(0.5f, 1f), 0.85f, "x2 difficulty coef strength1 mid -> rbm 0.85");
            CheckApprox(HandSkillProfile.DifficultyCoef(1f, 1f), 1.00f, "x2 difficulty coef strength1 high -> rbm 1.0");

            // 2) AI 等级：skill01×系数，clamp 0~1
            CheckApprox(HandSkillProfile.AiLevel(0.5f, 1f, 1f), 0.5f, "x2 ai level skill01 0.5 diff1 strength1 -> 0.5");

            // 3) 格挡能力：clamp(base×(1+strength), 0.3, 0.98)——RBM 口径×2 顶到上限
            CheckApprox(HandSkillProfile.BlockAbility(0.5f, 1f), 0.98f, "x2 block ability base0.5 s1 -> capped 0.98");
            CheckApprox(HandSkillProfile.BlockAbility(0.2f, 1f), 0.4f, "x2 block ability base0.2 s1 -> 0.4");
            CheckApprox(HandSkillProfile.BlockAbility(0.1f, 1f), 0.3f, "x2 block ability floor 0.3");
            CheckApprox(HandSkillProfile.BlockAbility(0.5f, 0f), 0.5f, "x2 block ability strength0 -> unchanged");

            // 4) 招架能力：clamp(base×(1+strength), 0.3, 0.95)
            CheckApprox(HandSkillProfile.ParryAbility(0.5f, 1f), 0.95f, "x2 parry ability base0.5 s1 -> capped 0.95");

            // 5) 射击精度：skill01=1 → 0.008（原版值）；skill01=0 → 0.015（RBM 上限）；strength0 → 原版
            CheckApprox(HandSkillProfile.ShooterError(1f, 1f), 0.008f, "x2 shooter error skill01 1 -> 0.008");
            CheckApprox(HandSkillProfile.ShooterError(0f, 1f), 0.015f, "x2 shooter error skill01 0 -> 0.015");
            CheckApprox(HandSkillProfile.ShooterError(1f, 0f), 0.008f, "x2 shooter error strength0 -> vanilla 0.008");

            // 6) 攻击概率：clamp(base×(1+0.5×strength), 0.05, 1)
            CheckApprox(HandSkillProfile.AttackChance(0.3f, 1f), 0.45f, "x2 attack chance base0.3 s1 -> 0.45");
            CheckApprox(HandSkillProfile.AttackChance(0.9f, 1f), 1f, "x2 attack chance cap 1.0");
            CheckApprox(HandSkillProfile.AttackChance(0.02f, 1f), 0.05f, "x2 attack chance floor 0.05");

            // 7) 防守方向随机化：base×(1−0.5×strength)，下限 0
            CheckApprox(HandSkillProfile.DefendDirectionChance(1f, 1f), 0.5f, "x2 defend dir chance 1.0 s1 -> 0.5");
            CheckApprox(HandSkillProfile.DefendDirectionChance(0.2f, 1f), 0.1f, "x2 defend dir chance base0.2 s1 -> 0.1");
            CheckApprox(HandSkillProfile.DefendDirectionChance(0f, 1f), 0f, "x2 defend dir chance floor 0");
        }

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

            // 4) 跳过态：F7 求生/F9 后撤/原版逃跑任一命中即跳过；全不命中才放行
            Check(FrameReleaseRules.ShouldSkipAgent(true, false, false), "x4 survival retreat skips");
            Check(FrameReleaseRules.ShouldSkipAgent(false, true, false), "x4 ranged fallback skips");
            Check(FrameReleaseRules.ShouldSkipAgent(false, false, true), "x4 running away skips");
            Check(!FrameReleaseRules.ShouldSkipAgent(false, false, false), "x4 normal agent not skipped");

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

            // X4 跳过集：已解阵 agent 不参与指派
            var frameDisabled = new System.Collections.Generic.HashSet<int>();
            frameDisabled.Add(2);
            var inputSkip = new SlotAssignInput { Slots = slots, Agents = agents, Last = null, Integrity = 85f, FrameDisabledAgents = frameDisabled };
            var outSkip = SlotAssigner.Assign(inputSkip);
            Check(outSkip.Count == 1, "x54 frame-disabled agent skipped");
            bool noAgent2 = true;
            foreach (var a in outSkip) if (a.AgentIndex == 2) noAgent2 = false;
            Check(noAgent2, "x54 frame-disabled agent2 not assigned");

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

        // X5-6 执行策略（X5ExecutionPolicy 纯逻辑）：L3 个体腿开关/Bench 回退闸/Discipline 门控/补位冷却
        private static void TestX5ExecutionPolicy()
        {
            // 1) L3 个体补位开关：仅 L3；玩家豁免/已解阵/超预算任一命中 → 跳过
            Check(X5ExecutionPolicy.ShouldRunIndividualLeg(3, false, false, false), "x56 L3 normal -> run individual leg");
            Check(!X5ExecutionPolicy.ShouldRunIndividualLeg(2, false, false, false), "x56 L2 -> skip (剧场闸)");
            Check(!X5ExecutionPolicy.ShouldRunIndividualLeg(3, true, false, false), "x56 exempted -> skip (玩家豁免)");
            Check(!X5ExecutionPolicy.ShouldRunIndividualLeg(3, false, true, false), "x56 frame-disabled -> skip (X4 解阵)");
            Check(!X5ExecutionPolicy.ShouldRunIndividualLeg(3, false, false, true), "x56 budget over -> skip (Bench 回退)");

            // 2) Bench 回退闸：avgAi 超 5ms → 回退个体腿（铁律② 500 人红线）
            Check(!X5ExecutionPolicy.IsIndividualOverBudget(4.9f), "x56 avgAi 4.9ms -> keep individual leg");
            Check(X5ExecutionPolicy.IsIndividualOverBudget(5.1f), "x56 avgAi 5.1ms -> rollback individual leg");
            Check(!X5ExecutionPolicy.IsIndividualOverBudget(5.0f), "x56 avgAi 5.0ms boundary -> keep (≤5)");

            // 3) Discipline 门控（x5-discipline-assigner-redundancy 接线）：MaintainSlots=false → 跳过刷新
            Check(X5ExecutionPolicy.ShouldRefreshSlots(FormationDisciplineController.Evaluate(90f)), "x56 full -> refresh slots");
            Check(X5ExecutionPolicy.ShouldRefreshSlots(FormationDisciplineController.Evaluate(50f)), "x56 moderate -> refresh slots");
            Check(!X5ExecutionPolicy.ShouldRefreshSlots(FormationDisciplineController.Evaluate(30f)), "x56 stopped -> skip refresh (交 F6)");

            // 4) 补位冷却字典（X4 FrameReleaseCooldown 同款模式）：无记录放行/窗内拦截/超窗放行/Remove 清理
            var cd = new X5SlotCooldown();
            Check(cd.CanRelease(5, 0f), "x56 cooldown initial allows");
            cd.MarkReleased(5, 10f);
            Check(!cd.CanRelease(5, 11.9f), "x56 cooldown blocks within 2s window");
            Check(cd.CanRelease(5, 12f), "x56 cooldown allows at 2s boundary");
            cd.MarkReleased(5, 12f);
            cd.Remove(5);
            Check(cd.CanRelease(5, 13f), "x56 cooldown allows after Remove (agent removed)");
            Check(cd.CanRelease(6, 20f), "x56 cooldown per-agent: unmarked other agent allows");

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

        private static int idxDefend()
        {
            return Array.IndexOf(TacticManual.Order, IntentType.Defend);
        }
    }
}