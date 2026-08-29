using System;
using CombatBrain.Movement;
using CombatBrain.Survival;
using CombatBrain.Cavalry;
using CombatBrain.Ranged;

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

            Console.WriteLine("== F6 MovementController ==");
            TestMovement();
            Console.WriteLine("== F7 SurvivalInstinct ==");
            TestSurvival();
            Console.WriteLine("== F8 CavalryWaveFSM ==");
            TestCavalry();
            Console.WriteLine("== F9 RangedBrain ==");
            TestRanged();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("ALL PASS");
                return 0;
            }
            Console.WriteLine(_failures + " FAILURE(S)");
            return 1;
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
    }
}