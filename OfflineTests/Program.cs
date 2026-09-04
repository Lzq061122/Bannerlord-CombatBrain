using System;
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

        private static int idxDefend()
        {
            return Array.IndexOf(TacticManual.Order, IntentType.Defend);
        }
    }
}