using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Enemy movement personalities (round 5, T2 / D4): Sniper keeps far and moves
    // after each shot, Gunner keeps close and strafes wide, Lancer dashes to a new
    // spot, holds still through its telegraph and shot, then dashes again; the
    // boss moves like the pattern it is in. Striker is today's bot frame for
    // frame. Dash and hold spots stay inside the arena and out of the terrain
    // pieces (review T2-P4). Every bot still flies through the player's clutch
    // and flight model with the same hand speed cap.
    public class BotMovementTests
    {
        private const float Dt = 1f / 72f;

        private static readonly FlightParams Flight = new FlightParams
        {
            Stiffness = 10f,
            Damping = 5f,
            MaxSpeed = 25f,
            GlideDrag = 0.8f,
        };

        // Today's arena, and a wide one where the walls never cut a range sphere.
        private static readonly ArenaBounds Arena = new ArenaBounds(new Vector3(0f, 2f, 20f), new Vector3(35f, 20f, 35f));
        private static readonly ArenaBounds WideArena = new ArenaBounds(new Vector3(0f, 2f, 20f), new Vector3(80f, 20f, 80f));
        private static readonly Vector3 Center = new Vector3(0f, 2f, 20f);

        // Bot + the scene's simulation (puppeteer clutch mapping + flight model).
        private class Sim
        {
            public readonly BotBrain Brain;
            private readonly BotParams _p;
            private readonly ArenaBounds _arena;
            private readonly ClutchMapper _mapper = new ClutchMapper();
            private FlightState _state;
            private Vector3 _target;
            private bool _hasTarget;

            public Sim(BotParams p, Vector3 start, ArenaBounds arena, int seed)
            {
                _p = p;
                _arena = arena;
                Brain = new BotBrain(p, seed);
                _state = new FlightState { Position = start };
            }

            public Vector3 Position => _state.Position;

            public HandInputData Step(Vector3 enemy, bool hasEnemy = true)
            {
                HandInputData input = Brain.Step(_state.Position, hasEnemy, enemy, _arena, Dt);
                ClutchResult c = _mapper.Step(input, _state.Position, _p.PositionScale);
                if (c.Clutched)
                {
                    _target = _arena.Clamp(c.Target);
                    _hasTarget = true;
                }
                if (c.JustReleased) _hasTarget = false;
                _state = SpringFlightModel.Step(_state, _hasTarget, _target, Flight, _arena, Dt);
                return input;
            }
        }

        // Like RunBot.Activate: the archetype's attacks and movement, then a reseed.
        private static Sim Make(BotArchetypeId id, BotParams p, Vector3 start, ArenaBounds arena, int seed = 1)
        {
            var sim = new Sim(p, start, arena, seed);
            BotArchetype a = BotArchetypes.Get(id);
            sim.Brain.SetAttacks(a.Attack, a.AltAttack, a.SwitchEvery);
            sim.Brain.SetMovement(a.Movement, a.AltMovement);
            sim.Brain.Reseed(seed);
            return sim;
        }

        private static BotParams Quiet()
        {
            BotParams p = BotParams.Default;
            p.ReactionTime = 0f;
            p.AimErrorDegrees = 0f;
            p.FireIntervalJitter = 0f;
            p.FireInterval = 1f;
            p.TelegraphTime = 0.5f;
            return p;
        }

        // No shots: movement only.
        private static BotParams MoveOnly()
        {
            BotParams p = Quiet();
            p.FireInterval = 1000f;
            return p;
        }

        // Inside the arena box (a float-rounding margin, not a gameplay one).
        private static void AssertInside(ArenaBounds arena, Vector3 p, string message)
        {
            Vector3 local = p - arena.Center;
            Vector3 half = arena.Size * 0.5f + Vector3.one * 1e-3f;
            Assert.IsTrue(Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z,
                $"{message}: {p} is outside the arena");
        }

        // --- the table ---

        [Test]
        public void ZeroMovement_ReadsAsStriker()
        {
            BotMovement zero = default;
            Assert.AreEqual(1f, zero.RangeScale);
            Assert.AreEqual(1f, zero.StrafeScale);
            Assert.AreEqual(1f, zero.SwitchScale);
            Assert.AreEqual(1f, zero.WeaveScale);
            Assert.AreEqual(BotMovement.DefaultArriveRadius, zero.ArriveDistance);
            Assert.AreEqual(BotMovement.DefaultDashTimeout, zero.DashTime);
            Assert.IsFalse(zero.KeepRange);
            Assert.IsFalse(zero.DashBeforeAttack);
            Assert.IsFalse(zero.DashAfterAttack);

            BotMovement striker = BotMovements.Striker;
            Assert.AreEqual(zero.RangeScale, striker.RangeScale);
            Assert.AreEqual(zero.StrafeScale, striker.StrafeScale);
            Assert.AreEqual(zero.SwitchScale, striker.SwitchScale);
            Assert.AreEqual(zero.WeaveScale, striker.WeaveScale);
            Assert.IsFalse(striker.KeepRange);
            Assert.AreEqual(0f, striker.DashDistance);
            Assert.AreEqual(0f, striker.DashLeadTime);
            Assert.IsFalse(striker.DashBeforeAttack);
            Assert.IsFalse(striker.DashAfterAttack);
        }

        [Test]
        public void Archetypes_PairEachPatternWithItsMovement()
        {
            Assert.AreEqual(BotMovements.Striker, BotArchetypes.Get(BotArchetypeId.Striker).Movement);

            BotMovement sniper = BotArchetypes.Get(BotArchetypeId.Sniper).Movement;
            Assert.AreEqual(1.5f, sniper.RangeMult, 1e-5f, "keeps far: 1.5x today's range");
            Assert.Less(sniper.StrafeMult, 1f, "small strafe");
            Assert.IsTrue(sniper.DashAfterAttack, "moves after each shot");
            Assert.IsFalse(sniper.DashBeforeAttack);
            Assert.Greater(sniper.DashDistance, 0f);

            BotMovement gunner = BotArchetypes.Get(BotArchetypeId.Gunner).Movement;
            Assert.AreEqual(0.7f, gunner.RangeMult, 1e-5f, "keeps close: 0.7x today's range");
            Assert.Greater(gunner.StrafeMult, 1f, "wide strafe");
            Assert.IsFalse(gunner.DashBeforeAttack);
            Assert.IsFalse(gunner.DashAfterAttack);

            BotMovement lancer = BotArchetypes.Get(BotArchetypeId.Lancer).Movement;
            Assert.IsTrue(lancer.DashBeforeAttack);
            Assert.IsTrue(lancer.DashAfterAttack);
            Assert.Greater(lancer.DashDistance, 0f);
            Assert.Greater(lancer.DashLeadTime, 0f, "sets off before the attack is due");

            BotArchetype boss = BotArchetypes.Get(BotArchetypeId.Boss);
            Assert.AreEqual(BotMovements.Gunner, boss.Movement, "Gunner bursts, Gunner movement");
            Assert.AreEqual(BotMovements.Lancer, boss.AltMovement, "Lancer shots, Lancer movement");

            foreach (BotArchetypeId id in new[]
                     { BotArchetypeId.Striker, BotArchetypeId.Sniper, BotArchetypeId.Gunner, BotArchetypeId.Lancer })
            {
                BotArchetype a = BotArchetypes.Get(id);
                Assert.AreEqual(a.Movement, a.AltMovement, $"{id}: one pattern, one movement");
            }

            BotArchetype[] all = BotArchetypes.Defaults();
            for (int i = 0; i < all.Length; i++)
                Assert.AreEqual(BotMovements.For((BotArchetypeId)i), all[i].Movement, $"{(BotArchetypeId)i}");
        }

        // --- Striker = today's bot ---

        // Golden trace (review T2-P1): round 4's BotBrain (ae71d0f, before T2) run
        // on exactly this test's inputs, recorded once from a frozen copy of that
        // class in the Unity editor (Mono). The three sims below agreeing with
        // each other only shows T2 agrees with itself; these numbers tie it to
        // round 4. Shot frames must match exactly. Positions are compared to
        // 0.1 mm and hand deltas to 1e-6 m: the same code run on .NET CoreCLR
        // drifts by up to 5e-6 m in float math, while a change to the shared
        // movement path (an extra random draw, a 1 cm range offset) moves them by
        // millimeters or more.
        private const float GoldenPositionTolerance = 1e-4f;
        private const float GoldenDeltaTolerance = 1e-6f;
        private static readonly int[] GoldenFrames = { 100, 400, 800, 1600, 2159 };
        private static readonly Vector3[] GoldenPositions =
        {
            new Vector3(8.04682922f, 3.415941f, 19.8173542f),
            new Vector3(11.1934624f, 0.0933405161f, 16.6704769f),
            new Vector3(9.73603058f, 8.58107948f, 17.2473698f),
            new Vector3(5.23593521f, 0.56882453f, 16.7121201f),
            new Vector3(7.75130606f, 2.04798889f, 9.25252056f),
        };
        private static readonly Vector3[] GoldenDeltas =
        {
            new Vector3(0.00189838407f, 0.000867160154f, -0.00136849086f),
            new Vector3(0.0151270293f, 0.00113466091f, -0.0142798228f),
            new Vector3(-0.0023621479f, -0.00109109085f, 0.00058193208f),
            new Vector3(-0.00267634401f, 0.000937700272f, 0.000577831292f),
            new Vector3(-5.45422226E-05f, 0.000670214475f, -0.00201986637f),
        };
        private static readonly BotState[] GoldenStates =
            { BotState.Strafe, BotState.Evade, BotState.Strafe, BotState.Strafe, BotState.Strafe };
        private static readonly int[] GoldenShotFrames = { 177, 581, 828, 1065, 1381, 1647, 1910 };

        private static void AssertGolden(Vector3 expected, Vector3 actual, float tolerance, string message)
        {
            Assert.IsTrue(Mathf.Abs(expected.x - actual.x) <= tolerance && Mathf.Abs(expected.y - actual.y) <= tolerance
                          && Mathf.Abs(expected.z - actual.z) <= tolerance,
                $"{message}: expected {expected.x:G9}, {expected.y:G9}, {expected.z:G9} " +
                $"but was {actual.x:G9}, {actual.y:G9}, {actual.z:G9}");
        }

        [Test]
        public void Striker_Movement_ReproducesRound4sBot_FrameForFrame()
        {
            // No SetMovement call (a brain nobody gave a movement), the Striker
            // preset, and an archetype entry serialized before round 5 (all zero).
            var none = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 9);
            var preset = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 9);
            preset.Brain.SetMovement(BotMovements.Striker, BotMovements.Striker);
            var unset = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 9);
            unset.Brain.SetMovement(default, default);

            var shotFrames = new List<int>();
            var states = new HashSet<BotState>();
            int golden = 0;
            for (int i = 0; i < 72 * 30; i++)
            {
                Vector3 enemy = new Vector3(Mathf.Sin(i * 0.02f) * 6f, 2f, 8f);
                if (i == 400 || i == 1200)
                {
                    none.Brain.NotifyHit();
                    preset.Brain.NotifyHit();
                    unset.Brain.NotifyHit();
                }
                HandInputData a = none.Step(enemy);
                foreach (Sim other in new[] { preset, unset })
                {
                    HandInputData b = other.Step(enemy);
                    Assert.IsTrue(a.ClutchDelta.Equals(b.ClutchDelta), $"move, frame {i}");
                    Assert.AreEqual(a.FireTriggered, b.FireTriggered, $"fire, frame {i}");
                    Assert.IsTrue(a.AimDirection.Equals(b.AimDirection), $"aim, frame {i}");
                    Assert.AreEqual(none.Brain.State, other.Brain.State, $"state, frame {i}");
                    Assert.IsTrue(none.Position.Equals(other.Position), $"position, frame {i}");
                }
                states.Add(none.Brain.State);
                if (a.FireTriggered) shotFrames.Add(i);

                if (golden < GoldenFrames.Length && i == GoldenFrames[golden])
                {
                    AssertGolden(GoldenPositions[golden], none.Position, GoldenPositionTolerance, $"round 4 position, frame {i}");
                    AssertGolden(GoldenDeltas[golden], a.ClutchDelta, GoldenDeltaTolerance, $"round 4 hand delta, frame {i}");
                    Assert.AreEqual(GoldenStates[golden], none.Brain.State, $"round 4 state, frame {i}");
                    golden++;
                }
            }

            Assert.AreEqual(GoldenFrames.Length, golden);
            CollectionAssert.AreEqual(GoldenShotFrames, shotFrames, "round 4's shots, frame for frame");
            Assert.IsTrue(states.Contains(BotState.Strafe) && states.Contains(BotState.Evade));
            Assert.IsFalse(states.Contains(BotState.Dash) || states.Contains(BotState.Hold), "Striker never dashes");
        }

        // --- distance and strafe ---

        private struct MoveStats
        {
            public float MeanDistance;
            public float MeanSideSpeed;    // m/s across the line to the enemy
            public float MeanAngularSpeed; // rad/s around the enemy
        }

        private static MoveStats Measure(BotArchetypeId id, int seed)
        {
            Sim sim = Make(id, MoveOnly(), Center + new Vector3(0f, 0f, 12f), WideArena, seed);
            float distance = 0f, side = 0f, angular = 0f;
            int n = 0;
            Vector3 prev = sim.Position;
            for (int i = 0; i < 72 * 20; i++)
            {
                sim.Step(Center);
                if (i >= 72 * 6)
                {
                    Vector3 offset = sim.Position - Center;
                    float d = offset.magnitude;
                    Vector3 v = (sim.Position - prev) / Dt;
                    Vector3 across = Vector3.ProjectOnPlane(v, offset / d);
                    distance += d;
                    side += across.magnitude;
                    angular += across.magnitude / d;
                    n++;
                }
                prev = sim.Position;
            }
            return new MoveStats { MeanDistance = distance / n, MeanSideSpeed = side / n, MeanAngularSpeed = angular / n };
        }

        [Test]
        public void Sniper_KeepsFar_Gunner_KeepsClose()
        {
            float range = BotParams.Default.PreferredRange;
            foreach (int seed in new[] { 1, 7 })
            {
                MoveStats striker = Measure(BotArchetypeId.Striker, seed);
                MoveStats sniper = Measure(BotArchetypeId.Sniper, seed);
                MoveStats gunner = Measure(BotArchetypeId.Gunner, seed);

                Assert.That(sniper.MeanDistance, Is.EqualTo(range * 1.5f).Within(1.5f), $"Sniper, seed {seed}");
                Assert.That(gunner.MeanDistance, Is.EqualTo(range * 0.7f).Within(1.5f), $"Gunner, seed {seed}");
                Assert.Greater(sniper.MeanDistance, striker.MeanDistance * 1.3f, $"seed {seed}");
                Assert.Less(gunner.MeanDistance, striker.MeanDistance * 0.8f, $"seed {seed}");
            }
        }

        [Test]
        public void Gunner_StrafesWide_Sniper_StrafesSmall()
        {
            MoveStats striker = Measure(BotArchetypeId.Striker, 3);
            MoveStats sniper = Measure(BotArchetypeId.Sniper, 3);
            MoveStats gunner = Measure(BotArchetypeId.Gunner, 3);

            Assert.Greater(gunner.MeanAngularSpeed, striker.MeanAngularSpeed * 1.4f, "sweeps around the player faster");
            Assert.GreaterOrEqual(gunner.MeanSideSpeed, striker.MeanSideSpeed);
            Assert.Less(sniper.MeanSideSpeed, striker.MeanSideSpeed * 0.6f, "barely strafes");
        }

        // --- Sniper: moves after each shot ---

        [Test]
        public void Sniper_MovesToANewSpot_AfterEachShot()
        {
            float range = BotParams.Default.PreferredRange * 1.5f;
            Sim sim = Make(BotArchetypeId.Sniper, Quiet(), Center + new Vector3(0f, 0f, range), WideArena, 2);

            int moves = 0;
            for (int i = 0; i < 72 * 12; i++)
            {
                if (!sim.Step(Center).FireTriggered) continue;
                Vector3 shotAt = sim.Position;

                sim.Step(Center);
                Assert.AreEqual(BotState.Dash, sim.Brain.State, "dashes right after the shot");
                AssertInside(WideArena, sim.Brain.DashSpot, "the spot is inside the arena");
                for (int j = 0; j < 72 * 2 && sim.Brain.State == BotState.Dash; j++) sim.Step(Center);

                Assert.AreNotEqual(BotState.Dash, sim.Brain.State, "the dash ends");
                Assert.Greater(Vector3.Distance(shotAt, sim.Position), 4f, "a new spot");
                Assert.That(Vector3.Distance(sim.Position, Center), Is.EqualTo(range).Within(1.5f), "still far");
                moves++;
            }
            Assert.GreaterOrEqual(moves, 3);
        }

        // --- Lancer: dash, hold through the telegraph and shot, dash again ---

        [Test]
        public void Lancer_DashesThenHoldsStill_ThroughItsTelegraphAndShot_ThenDashesAgain()
        {
            Sim sim = Make(BotArchetypeId.Lancer, Quiet(), Center + new Vector3(0f, 0f, 12f), WideArena, 1);
            float lead = BotMovements.Lancer.DashLeadTime;

            BotState lastMove = BotState.Idle; // the latest state other than Hold
            int holdFrames = 0;
            bool wasTelegraphing = false;
            bool expectDash = false;
            Vector3 midTelegraph = Vector3.zero;
            bool pastHalf = false;
            int attacks = 0;
            for (int i = 0; i < 72 * 25; i++)
            {
                HandInputData input = sim.Step(Center);
                BotBrain brain = sim.Brain;

                if (expectDash)
                {
                    Assert.AreEqual(BotState.Dash, brain.State, $"dashes again after the shot, frame {i}");
                    expectDash = false;
                }
                if (brain.IsTelegraphing && !wasTelegraphing)
                {
                    Assert.AreEqual(BotState.Dash, lastMove, $"a dash right before the telegraph, frame {i}");
                    // Set off DashLeadTime early: any wait in place before the telegraph is shorter.
                    Assert.LessOrEqual(holdFrames * Dt, lead, $"waits in place at most the lead, frame {i}");
                    pastHalf = false;
                }
                if (brain.IsTelegraphing)
                {
                    Assert.AreEqual(BotState.Hold, brain.State, $"holds while telegraphing, frame {i}");
                    if (!pastHalf && brain.TelegraphProgress >= 0.5f)
                    {
                        pastHalf = true;
                        midTelegraph = sim.Position;
                    }
                }
                if (input.FireTriggered)
                {
                    Assert.AreEqual(BotState.Hold, brain.State, "holds while firing");
                    Assert.IsTrue(pastHalf);
                    Assert.Less(Vector3.Distance(midTelegraph, sim.Position), 0.5f, "still for the shot");
                    expectDash = true;
                    attacks++;
                }

                if (brain.State != BotState.Hold)
                {
                    lastMove = brain.State;
                    holdFrames = 0;
                }
                else if (!brain.IsTelegraphing && !input.FireTriggered)
                {
                    holdFrames++;
                }
                wasTelegraphing = brain.IsTelegraphing;
            }
            Assert.GreaterOrEqual(attacks, 4);
        }

        // The dash before an attack sets off early, so the Lancer fires as often
        // as round 4's Lancer (same attack, Striker movement).
        [Test]
        public void Lancer_KeepsItsAttackRate()
        {
            BotAttack lancer = BotArchetypes.Get(BotArchetypeId.Lancer).Attack;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                int Shots(BotMovement move)
                {
                    var sim = new Sim(Quiet(), Center + new Vector3(0f, 0f, 12f), WideArena, seed);
                    sim.Brain.SetAttacks(lancer, lancer, 0);
                    sim.Brain.SetMovement(move, move);
                    sim.Brain.Reseed(seed);
                    int shots = 0;
                    for (int i = 0; i < 72 * 60; i++)
                        if (sim.Step(Center).FireTriggered) shots++;
                    return shots;
                }

                int roundFour = Shots(BotMovements.Striker);
                int dashing = Shots(BotMovements.Lancer);
                Assert.Greater(roundFour, 10);
                Assert.That(dashing, Is.InRange(roundFour - 1, roundFour + 1), $"seed {seed}");
            }
        }

        [Test]
        public void Lancer_HoldsOnlyForAnAttack()
        {
            float lead = BotMovements.Lancer.DashLeadTime;
            foreach (int seed in new[] { 1, 5, 11 })
            {
                Sim sim = Make(BotArchetypeId.Lancer, BotParams.Default, new Vector3(5f, 4f, 30f), Arena, seed);
                int holds = 0;
                for (int i = 0; i < 72 * 30; i++)
                {
                    if (i % 300 == 299) sim.Brain.NotifyHit();
                    Vector3 enemy = new Vector3(Mathf.Sin(i * 0.01f) * 8f, 2f, 10f);
                    HandInputData input = sim.Step(enemy);
                    if (sim.Brain.State != BotState.Hold) continue;
                    holds++;
                    // Telegraphing, firing, or in place just before the attack is due.
                    Assert.IsTrue(sim.Brain.IsTelegraphing || input.FireTriggered || sim.Brain.TimeToNextShot <= lead,
                        $"seed {seed}, frame {i}");
                }
                Assert.Greater(holds, 0, $"seed {seed}");
            }
        }

        [Test]
        public void Lancer_HitDuringTheHold_Evades_ThenDashesBeforeItsNextTelegraph()
        {
            Sim sim = Make(BotArchetypeId.Lancer, Quiet(), Center + new Vector3(0f, 0f, 12f), WideArena, 3);
            for (int i = 0; i < 72 * 5 && !(sim.Brain.IsTelegraphing && sim.Brain.TelegraphProgress >= 0.3f); i++)
                sim.Step(Center);
            Assert.AreEqual(BotState.Hold, sim.Brain.State);

            sim.Brain.NotifyHit();
            sim.Step(Center);
            Assert.AreEqual(BotState.Evade, sim.Brain.State);
            Assert.IsFalse(sim.Brain.IsTelegraphing);

            BotState lastMove = sim.Brain.State; // the latest state other than Hold
            for (int i = 0; i < 72 * 10; i++)
            {
                HandInputData input = sim.Step(Center);
                Assert.IsFalse(input.FireTriggered, "no shot without a new telegraph");
                if (sim.Brain.IsTelegraphing)
                {
                    Assert.AreEqual(BotState.Dash, lastMove, "dashed to a new spot first");
                    return;
                }
                if (sim.Brain.State != BotState.Hold) lastMove = sim.Brain.State;
            }
            Assert.Fail("no telegraph after the hit");
        }

        [Test]
        public void Lancer_OutOfRange_NeitherDashesNorHolds()
        {
            BotParams p = Quiet();
            p.MaxFireRange = 5f; // the enemy is always farther
            Sim sim = Make(BotArchetypeId.Lancer, p, Center + new Vector3(0f, 0f, 12f), WideArena, 1);
            for (int i = 0; i < 72 * 10; i++)
            {
                Assert.IsFalse(sim.Step(Center).FireTriggered);
                Assert.AreNotEqual(BotState.Dash, sim.Brain.State, $"frame {i}");
                Assert.AreNotEqual(BotState.Hold, sim.Brain.State, $"frame {i}");
            }
        }

        // Enemy near the right wall: a dash that way would leave the arena, so
        // every seed takes the free side.
        [Test]
        public void DashSpot_TakesTheSideAWallDoesNotCut()
        {
            Vector3 enemy = new Vector3(10f, 2f, 20f);
            Vector3 self = enemy + new Vector3(0f, 0f, 12f);
            for (int seed = 1; seed <= 20; seed++)
            {
                var brain = new BotBrain(Quiet(), seed);
                BotArchetype lancer = BotArchetypes.Get(BotArchetypeId.Lancer);
                brain.SetAttacks(lancer.Attack, lancer.AltAttack, lancer.SwitchEvery);
                brain.SetMovement(lancer.Movement, lancer.AltMovement);
                brain.Reseed(seed);

                for (int i = 0; i < 72 * 5 && brain.State != BotState.Dash; i++) brain.Step(self, true, enemy, Arena, Dt);
                Assert.AreEqual(BotState.Dash, brain.State, $"seed {seed}");
                Assert.Less(brain.DashSpot.x, enemy.x - 1f, $"seed {seed}: away from the wall");
                AssertInside(Arena, brain.DashSpot, $"seed {seed}");
            }
        }

        // --- terrain pieces (review T2-P4) ---

        private const float Clearance = 1f;

        private static BotBrain LancerBrain(int seed, BotParams p)
        {
            var brain = new BotBrain(p, seed);
            BotArchetype lancer = BotArchetypes.Get(BotArchetypeId.Lancer);
            brain.SetAttacks(lancer.Attack, lancer.AltAttack, lancer.SwitchEvery);
            brain.SetMovement(lancer.Movement, lancer.AltMovement);
            brain.Reseed(seed);
            return brain;
        }

        // Inside `box` grown by `grow` on every side (the test's own check, not the brain's).
        private static bool Inside(Bounds box, float grow, Vector3 p)
        {
            Vector3 d = p - box.center;
            Vector3 e = box.extents + Vector3.one * grow;
            return Mathf.Abs(d.x) <= e.x && Mathf.Abs(d.y) <= e.y && Mathf.Abs(d.z) <= e.z;
        }

        // A Lancer's first dash with no pieces, then the same seed with a low wall
        // (T3's 6 x 3 x 1) right on that spot: it takes another spot, clear of the
        // wall and the hero's radius, and it still dashes there (not a hold in place).
        [Test]
        public void DashSpot_KeepsOutOfATerrainPiece()
        {
            Vector3 enemy = Center;
            Vector3 self = Center + new Vector3(0f, 0f, 12f);
            int moved = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                BotBrain free = LancerBrain(seed, Quiet());
                for (int i = 0; i < 72 * 5 && free.State != BotState.Dash; i++) free.Step(self, true, enemy, WideArena, Dt);
                Assert.AreEqual(BotState.Dash, free.State, $"seed {seed}");
                Vector3 spot = free.DashSpot;

                var wall = new Bounds(spot, new Vector3(6f, 3f, 1f));
                BotBrain brain = LancerBrain(seed, Quiet());
                brain.SetObstacles(new[] { wall }, 1, Clearance);
                for (int i = 0; i < 72 * 5 && brain.State != BotState.Dash; i++) brain.Step(self, true, enemy, WideArena, Dt);

                Assert.AreEqual(BotState.Dash, brain.State, $"seed {seed}: still dashes");
                Assert.IsFalse(Inside(wall, Clearance, brain.DashSpot), $"seed {seed}: {brain.DashSpot} is in the wall");
                Assert.IsTrue(brain.InsideObstacle(spot), $"seed {seed}: the brain sees the wall");
                // The other side first, the full arc (8.9 m to the side; half of it would be 4.9 m).
                Assert.Less((brain.DashSpot.x - Center.x) * (spot.x - Center.x), 0f, $"seed {seed}: the other side");
                Assert.Greater(Mathf.Abs(brain.DashSpot.x - Center.x), 7f, $"seed {seed}: the full arc");
                AssertInside(WideArena, brain.DashSpot, $"seed {seed}");
                moved++;
            }
            Assert.AreEqual(20, moved);
        }

        // Every spot it could dash to is inside a piece: no dash, it holds where
        // it is (clear of both pieces), and the attack still comes.
        [Test]
        public void DashSpot_EverySpotBlocked_HoldsInPlace_AndStillAttacks()
        {
            Vector3 enemy = Center;
            Vector3 self = Center + new Vector3(0f, 0f, 12f);
            // Both sides of the range sphere, full and half arcs, all heights; not the bot.
            Bounds[] pieces =
            {
                new Bounds(Center + new Vector3(-7f, 0f, 9.5f), new Vector3(8f, 10f, 6f)),
                new Bounds(Center + new Vector3(7f, 0f, 9.5f), new Vector3(8f, 10f, 6f)),
            };
            for (int seed = 1; seed <= 10; seed++)
            {
                BotBrain brain = LancerBrain(seed, Quiet());
                brain.SetObstacles(pieces, pieces.Length, Clearance);
                Assert.IsFalse(brain.InsideObstacle(self), "the bot itself is clear");

                bool held = false, fired = false;
                for (int i = 0; i < 72 * 6 && !fired; i++)
                {
                    HandInputData input = brain.Step(self, true, enemy, WideArena, Dt);
                    Assert.AreNotEqual(BotState.Dash, brain.State, $"seed {seed}, frame {i}: no spot to dash to");
                    if (brain.State == BotState.Hold)
                    {
                        held = true;
                        Assert.AreEqual(self, brain.DashSpot, $"seed {seed}: holds in place");
                    }
                    fired |= input.FireTriggered;
                }
                Assert.IsTrue(held, $"seed {seed}: holds for its attack");
                Assert.IsTrue(fired, $"seed {seed}: the attack still comes");
            }
        }

        // Pieces on both full-arc spots but not nearer in: it dashes half the arc.
        [Test]
        public void DashSpot_BothFullArcsBlocked_DashesHalfTheArc()
        {
            Vector3 enemy = Center;
            Vector3 self = Center + new Vector3(0f, 0f, 12f);
            // The 10 m arc on the 12 m sphere ends 8.9 m to either side; half of it 4.9 m.
            Bounds[] pieces =
            {
                new Bounds(Center + new Vector3(-9f, 0f, 8f), new Vector3(3f, 10f, 3f)),
                new Bounds(Center + new Vector3(9f, 0f, 8f), new Vector3(3f, 10f, 3f)),
            };
            for (int seed = 1; seed <= 10; seed++)
            {
                BotBrain brain = LancerBrain(seed, Quiet());
                brain.SetObstacles(pieces, pieces.Length, Clearance);
                for (int i = 0; i < 72 * 5 && brain.State != BotState.Dash; i++) brain.Step(self, true, enemy, WideArena, Dt);

                Assert.AreEqual(BotState.Dash, brain.State, $"seed {seed}: still dashes");
                float side = Mathf.Abs(brain.DashSpot.x - Center.x);
                Assert.That(side, Is.InRange(3.5f, 6f), $"seed {seed}: half the arc, {brain.DashSpot}");
                foreach (Bounds piece in pieces)
                    Assert.IsFalse(Inside(piece, Clearance, brain.DashSpot), $"seed {seed}");
            }
        }

        // A dash the bot cannot finish in time (a hand speed cap far below the
        // player's) ends at its timeout and holds where it stopped; when that
        // place is inside a terrain piece it keeps making for its clear spot.
        [Test]
        public void Dash_CutShortByItsTimeout_HoldsWhereItStopped_ButNotInsideAPiece()
        {
            BotParams slow = Quiet();
            slow.MaxHandSpeed = 0.08f; // about 5 m/s of hero travel: a 10 m dash needs about 2 s
            BotMovement lancer = BotMovements.Lancer;
            Vector3 start = Center + new Vector3(0f, 0f, 12f);

            // Runs until the first dash ends; returns where the bot was when it ended.
            Vector3 RunFirstDash(Sim sim, out Vector3 spotBefore, out int dashFrames)
            {
                dashFrames = 0;
                spotBefore = Vector3.zero;
                for (int i = 0; i < 72 * 10; i++)
                {
                    Vector3 before = sim.Position;
                    Vector3 spot = sim.Brain.DashSpot;
                    BotState prev = sim.Brain.State;
                    sim.Step(Center);
                    if (sim.Brain.State == BotState.Dash) dashFrames++;
                    else if (prev == BotState.Dash)
                    {
                        spotBefore = spot;
                        Assert.AreEqual(BotState.Hold, sim.Brain.State, "the dash before an attack turns into a hold");
                        return before;
                    }
                }
                Assert.Fail("no dash ended");
                return default;
            }

            Sim open = Make(BotArchetypeId.Lancer, slow, start, WideArena, 6);
            Vector3 stopped = RunFirstDash(open, out Vector3 target, out int frames);
            Assert.That((frames + 1) * Dt, Is.EqualTo(lancer.DashTime).Within(Dt * 1.01f), "ended by its timeout");
            Assert.Greater(Vector3.Distance(stopped, target), lancer.ArriveDistance, "short of its spot");
            Assert.AreEqual(stopped, open.Brain.DashSpot, "holds where it stopped");

            // The same run with a piece around the place it stops (clear of its start and its spot).
            var piece = new Bounds(stopped, Vector3.one);
            Assert.IsFalse(Inside(piece, Clearance, start) || Inside(piece, Clearance, target), $"{start} {stopped} {target}");
            Sim blocked = Make(BotArchetypeId.Lancer, slow, start, WideArena, 6);
            blocked.Brain.SetObstacles(new[] { piece }, 1, Clearance);
            Vector3 stoppedAgain = RunFirstDash(blocked, out Vector3 targetAgain, out _);
            Assert.AreEqual(target, targetAgain, "the piece is not on its spot");
            Assert.IsTrue(Inside(piece, Clearance, stoppedAgain));
            Assert.AreEqual(target, blocked.Brain.DashSpot, "not inside the piece: keeps making for its spot");
        }

        // Today's two pillars plus T3-sized pieces around the player: through 40 s
        // of fighting (hits included) a Lancer, and the boss in its Lancer pattern,
        // never hold or dash to a spot inside a piece.
        [Test]
        public void Lancer_NeverStopsInsideATerrainPiece()
        {
            Bounds[] pieces =
            {
                new Bounds(Center + new Vector3(-7f, -5f, 4f), new Vector3(2f, 10f, 2f)),   // Pillar_L
                new Bounds(Center + new Vector3(9f, -4f, 9f), new Vector3(2f, 12f, 2f)),    // Pillar_R
                new Bounds(Center + new Vector3(-4f, -6.5f, -9f), new Vector3(6f, 3f, 1f)), // low wall
                new Bounds(Center + new Vector3(6f, 3f, -8f), new Vector3(4f, 0.6f, 4f)),   // platform
                new Bounds(Center + new Vector3(-11f, -3f, -3f), new Vector3(1f, 14f, 1f)), // thin pillar
                new Bounds(Center + new Vector3(0f, 0f, 11f), new Vector3(6f, 3f, 1f)),     // low wall, on the ring
            };
            int holds = 0;
            foreach (BotArchetypeId id in new[] { BotArchetypeId.Lancer, BotArchetypeId.Boss })
            foreach (int seed in new[] { 1, 2, 3 })
            {
                Sim sim = Make(id, BotParams.Default, Center + new Vector3(3f, 0f, 12f), Arena, seed);
                sim.Brain.SetObstacles(pieces, pieces.Length, Clearance);
                for (int i = 0; i < 72 * 40; i++)
                {
                    if (i % 400 == 399) sim.Brain.NotifyHit();
                    Vector3 enemy = Center + new Vector3(Mathf.Sin(i * 0.01f) * 5f, Mathf.Sin(i * 0.006f) * 3f, Mathf.Cos(i * 0.008f) * 5f);
                    sim.Step(enemy);
                    BotState state = sim.Brain.State;
                    if (state != BotState.Dash && state != BotState.Hold) continue;
                    if (state == BotState.Hold) holds++;
                    foreach (Bounds piece in pieces)
                        Assert.IsFalse(Inside(piece, Clearance, sim.Brain.DashSpot),
                            $"{id}, seed {seed}, frame {i}: {state} spot {sim.Brain.DashSpot} is in a piece at {piece.center}");
                }
            }
            Assert.Greater(holds, 100);
        }

        // --- the boss ---

        [Test]
        public void Boss_MovesLikeTheGunner_ThenLikeTheLancer()
        {
            BotAttack gunner = BotArchetypes.Get(BotArchetypeId.Gunner).Attack;
            BotAttack lancer = BotArchetypes.Get(BotArchetypeId.Lancer).Attack;
            Sim sim = Make(BotArchetypeId.Boss, Quiet(), Center + new Vector3(0f, 0f, 9f), WideArena, 4);
            float gunnerRange = BotParams.Default.PreferredRange * BotMovements.Gunner.RangeScale;
            float lancerRange = BotParams.Default.PreferredRange * BotMovements.Lancer.RangeScale;

            // Review T2-P3: the attack pattern alone decides the dash and hold, so
            // also check what only the movement decides, the range: at every Gunner
            // telegraph the boss is nearer than halfway to the Lancer's range, and
            // between two attacks of the same pattern it strafes at that pattern's
            // range (the first Gunner attack after the Lancer pair overshoots
            // inward while it closes in, so single frames are not checked to ±1.5).
            float halfway = (gunnerRange + lancerRange) * 0.5f;
            int gunnerAttacks = 0, lancerAttacks = 0, gunnerGaps = 0, lancerGaps = 0;
            BotState lastMove = BotState.Idle; // the latest state other than Hold
            bool wasTelegraphing = false;
            int lastPattern = -1; // 0 = Gunner, 1 = Lancer
            float strafeDistance = 0f; // Strafe frames since the latest telegraph start
            int strafeFrames = 0;
            for (int i = 0; i < 72 * 40; i++)
            {
                sim.Step(Center);
                BotBrain brain = sim.Brain;
                float distance = Vector3.Distance(sim.Position, Center);
                if (brain.IsTelegraphing && !wasTelegraphing)
                {
                    float mult = brain.ActiveAttack.TelegraphMult;
                    int pattern;
                    if (Mathf.Approximately(mult, gunner.TelegraphMult))
                    {
                        pattern = 0;
                        Assert.That(brain.State, Is.EqualTo(BotState.Strafe).Or.EqualTo(BotState.Approach),
                            $"Gunner pattern strafes, frame {i}");
                        Assert.Less(distance, halfway, $"Gunner pattern keeps in close, frame {i}");
                        gunnerAttacks++;
                    }
                    else
                    {
                        pattern = 1;
                        Assert.AreEqual(lancer.TelegraphMult, mult, 1e-5f);
                        Assert.AreEqual(BotState.Hold, brain.State, $"Lancer pattern holds, frame {i}");
                        Assert.AreEqual(BotState.Dash, lastMove, $"after a dash, frame {i}");
                        lancerAttacks++;
                    }
                    if (pattern == lastPattern)
                    {
                        float range = pattern == 0 ? gunnerRange : lancerRange;
                        Assert.Greater(strafeFrames, 0, $"strafes between the attacks, frame {i}");
                        Assert.That(strafeDistance / strafeFrames, Is.EqualTo(range).Within(1.5f),
                            $"{(pattern == 0 ? "Gunner" : "Lancer")} pattern strafes at its range, frame {i}");
                        if (pattern == 0) gunnerGaps++;
                        else lancerGaps++;
                    }
                    lastPattern = pattern;
                    strafeDistance = 0f;
                    strafeFrames = 0;
                }
                if (brain.State == BotState.Strafe)
                {
                    strafeDistance += distance;
                    strafeFrames++;
                }
                if (brain.State != BotState.Hold) lastMove = brain.State;
                wasTelegraphing = brain.IsTelegraphing;
            }
            Assert.GreaterOrEqual(gunnerAttacks, 2);
            Assert.GreaterOrEqual(lancerAttacks, 2);
            Assert.GreaterOrEqual(gunnerGaps, 2);
            Assert.GreaterOrEqual(lancerGaps, 2);
        }

        // --- fairness: dash spots inside the arena, every dash ends ---

        // Review T2-P2: the flight model already clamps the hero to the arena and
        // BotBrain.Step already caps the hand speed, so asserting those proved
        // nothing about T2. This checks what the dash code itself decides: each
        // spot is inside the arena and on (or, cut by a wall, inside) the range
        // sphere it was picked on, and each dash ends by arriving or by its timeout.
        [Test]
        public void EveryArchetype_DashSpots_StayInTheArena_AndEveryDashEnds()
        {
            BotParams p = BotParams.Default;
            int dashes = 0, ended = 0;
            foreach (BotArchetypeId id in new[]
                     {
                         BotArchetypeId.Striker, BotArchetypeId.Sniper, BotArchetypeId.Gunner, BotArchetypeId.Lancer,
                         BotArchetypeId.Boss,
                     })
            {
                BotArchetype archetype = BotArchetypes.Get(id);
                bool canDash = archetype.Movement.DashDistance > 0f || archetype.AltMovement.DashDistance > 0f;
                foreach (int seed in new[] { 7, 8 })
                {
                    Sim sim = Make(id, p, new Vector3(17f, 11f, 37f), Arena, seed);
                    BotState prev = sim.Brain.State;
                    Vector3 prevPerceived = sim.Brain.PerceivedTarget;
                    Vector3 prevSpot = sim.Brain.DashSpot;
                    int dashFrames = 0;
                    for (int i = 0; i < 72 * 60; i++)
                    {
                        if (i % 500 == 499) sim.Brain.NotifyHit();
                        Vector3 enemy = new Vector3(Mathf.Sin(i * 0.015f) * 12f, 2f + Mathf.Sin(i * 0.007f) * 5f, 6f);
                        Vector3 before = sim.Position;
                        sim.Step(enemy);
                        BotBrain brain = sim.Brain;
                        BotState state = brain.State;
                        string at = $"{id}, seed {seed}, frame {i}";

                        bool starts = (state == BotState.Dash && prev != BotState.Dash)
                                      || (state == BotState.Hold && prev != BotState.Hold && prev != BotState.Dash);
                        if (starts)
                        {
                            Assert.IsTrue(canDash, $"{at}: this archetype never dashes");
                            // Picked on the previous frame, from that frame's perceived target.
                            AssertInside(Arena, brain.DashSpot, at);
                            BotMovement m = DashMovement(archetype);
                            float reach = p.PreferredRange * m.RangeScale + p.VerticalWeave * m.WeaveScale;
                            Assert.LessOrEqual(Vector3.Distance(brain.DashSpot, prevPerceived), reach + 1e-3f,
                                $"{at}: on or inside its range sphere");
                            dashes++;
                        }

                        if (state == BotState.Dash) dashFrames++;
                        if (prev == BotState.Dash && state != BotState.Dash)
                        {
                            // A hit or a lost enemy cancels a dash; otherwise it ended by itself.
                            if (state != BotState.Evade && state != BotState.Idle)
                            {
                                BotMovement m = DashMovement(archetype);
                                bool arrived = Vector3.Distance(before, prevSpot) <= m.ArriveDistance + 1e-4f;
                                bool timedOut = (dashFrames + 1) * Dt >= m.DashTime - 1e-4f;
                                Assert.IsTrue(arrived || timedOut, $"{at}: a dash ends by arriving or by its timeout");
                                ended++;
                            }
                            Assert.LessOrEqual(dashFrames * Dt, DashMovement(archetype).DashTime + 1e-4f,
                                $"{at}: no dash outlasts its timeout");
                            dashFrames = 0;
                        }
                        if (state == BotState.Dash || state == BotState.Hold)
                            AssertInside(Arena, brain.DashSpot, at);

                        prev = state;
                        prevPerceived = brain.PerceivedTarget;
                        prevSpot = brain.DashSpot;
                    }
                }
            }
            Assert.Greater(dashes, 40);
            Assert.Greater(ended, 30);
        }

        // Every archetype's two movements share their range and dash settings
        // except the boss's; for the boss, the Lancer pattern is the one that dashes.
        private static BotMovement DashMovement(BotArchetype archetype)
        {
            return archetype.AltMovement.DashDistance > 0f ? archetype.AltMovement : archetype.Movement;
        }

        // --- pace (deep review DR-1) ---

        [Test]
        public void Paced_ScalesWhatSetsTheFlightSpeed_AndNeverSpeedsUp()
        {
            BotParams run = BotParams.Default;
            foreach (float same in new[] { 1f, 2f, 0f, -1f })
                Assert.AreEqual(run, run.Paced(same), $"pace {same}: the difficulty's bot");

            BotParams slow = run.Paced(0.6f);
            Assert.AreEqual(run.StrafeLead * 0.6f, slow.StrafeLead, 1e-5f);
            Assert.AreEqual(run.EvadeDistance * 0.6f, slow.EvadeDistance, 1e-5f);
            Assert.AreEqual(run.VerticalWeave * 0.6f, slow.VerticalWeave, 1e-5f);
            Assert.AreEqual(run.MaxHandSpeed * 0.6f, slow.MaxHandSpeed, 1e-5f, "the fairness cap goes down with it");

            // Everything else is the difficulty's: range, aim, shots, reaction.
            BotParams rest = slow;
            rest.StrafeLead = run.StrafeLead;
            rest.EvadeDistance = run.EvadeDistance;
            rest.VerticalWeave = run.VerticalWeave;
            rest.MaxHandSpeed = run.MaxHandSpeed;
            Assert.AreEqual(run, rest);

            Assert.AreEqual(run.StrafeLead * BotParams.MinPace, run.Paced(0.001f).StrafeLead, 1e-5f, "floor");
        }

        // Mean flight speed over 60 s against a moving enemy, after the first 3 s;
        // `hits` = a hit (an evade) every 2 s.
        private static float MeanSpeed(BotParams p, int seed, bool hits)
        {
            Sim sim = Make(BotArchetypeId.Striker, p, Center + new Vector3(6f, 3f, 12f), Arena, seed);
            float sum = 0f;
            int n = 0;
            Vector3 prev = sim.Position;
            for (int i = 0; i < 72 * 60; i++)
            {
                if (hits && i % 144 == 143) sim.Brain.NotifyHit();
                Vector3 enemy = Center + new Vector3(Mathf.Sin(i * 0.01f) * 6f, Mathf.Sin(i * 0.007f) * 2f,
                    Mathf.Cos(i * 0.013f) * 4f);
                sim.Step(enemy);
                if (i >= 72 * 3)
                {
                    sum += (sim.Position - prev).magnitude / Dt;
                    n++;
                }
                prev = sim.Position;
            }
            return sum / n;
        }

        // Review DR-1: the demo scaled only the hand speed cap (1.5 x 60 = 90 m/s
        // of clutch travel, far above the ~10 m/s the flight spring settles at
        // while strafing), so its "slow" Strikers flew exactly as fast as a run's.
        // Same hero max speed for both here: the brain alone has to slow it.
        [Test]
        public void DemoStrikers_FlySlowerThanTheRunsStriker()
        {
            float pace = DemoRules.SpeedScale(DemoParams.Default);
            Assert.Less(pace, 0.9f);
            BotParams demo = BotParams.Default.Paced(pace);
            foreach (bool hits in new[] { false, true })
            {
                float run = 0f, slow = 0f;
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    run += MeanSpeed(BotParams.Default, seed, hits);
                    slow += MeanSpeed(demo, seed, hits);
                }
                string speeds = $"hits {hits}: run {run / 3f:F2} m/s, demo {slow / 3f:F2} m/s";
                Assert.Less(slow, run * (pace + 0.15f), speeds);
                Assert.Greater(slow, run * (pace - 0.2f), $"{speeds}: still flies");
            }
        }

        // --- the seat (deep review DR-2) ---

        // The VR seat: the eye at world (0, 1.2, 0) (arena-local (0, -0.8, -20)),
        // facing the arena center; BotInputSource's defaults.
        private static readonly Vector3 SeatEye = new Vector3(0f, 1.2f, 0f);
        private static readonly BotSeat Seat = BotSeat.At(SeatEye, Vector3.forward, 45f, 8f);

        // Degrees off the seat's forward and level meters from the eye.
        private static float SeatYaw(Vector3 p) => Mathf.Abs(Mathf.Atan2(p.x - SeatEye.x, p.z - SeatEye.z)) * Mathf.Rad2Deg;

        private static float SeatDistance(Vector3 p) => new Vector2(p.x - SeatEye.x, p.z - SeatEye.z).magnitude;

        private static float LevelDistance(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        // `meters` from the eye, `degrees` to the right of the seat's forward (level).
        private static Vector3 FromSeat(float degrees, float meters, float up = 0f)
        {
            float r = degrees * Mathf.Deg2Rad;
            return SeatEye + new Vector3(Mathf.Sin(r) * meters, up, Mathf.Cos(r) * meters);
        }

        [Test]
        public void Seat_AllowsOnlyWhatTheSeatLooksAt()
        {
            Assert.IsTrue(Seat.Allows(FromSeat(0f, 20f)), "straight ahead");
            Assert.IsTrue(Seat.Allows(FromSeat(40f, 20f)), "40 degrees right");
            Assert.IsTrue(Seat.Allows(FromSeat(-40f, 9f)), "40 degrees left, 9 m");
            Assert.IsFalse(Seat.Allows(FromSeat(50f, 20f)), "50 degrees right");
            Assert.IsFalse(Seat.Allows(FromSeat(-50f, 20f)), "50 degrees left");
            Assert.IsFalse(Seat.Allows(FromSeat(0f, 5f)), "in the player's face");
            Assert.IsFalse(Seat.Allows(FromSeat(180f, 10f)), "behind the seat");
            Assert.IsTrue(Seat.Allows(FromSeat(10f, 9f, 9f)), "heights are free");
            Assert.IsFalse(Seat.Allows(FromSeat(0f, 5f, 9f)), "distance is level");

            BotSeat none = default;
            Assert.IsTrue(none.Allows(FromSeat(0f, 1f)), "no seat: no limit");
            Assert.AreEqual(FromSeat(90f, 3f), none.Limit(FromSeat(90f, 3f), Center));
        }

        [Test]
        public void Seat_Limit_TurnsAboutThePivot_ToTheNearestBearingInView()
        {
            Vector3 hero = Center; // 20 m in front of the eye, the hero's start
            foreach (float side in new[] { 1f, -1f })
            {
                // On the Sniper's 18 m circle around the hero, beside the seat.
                Vector3 p = hero + new Vector3(side * 17f, 1.5f, -6f);
                Assert.Greater(SeatYaw(p), 50f);
                Vector3 limited = Seat.Limit(p, hero);
                Assert.IsTrue(Seat.Allows(limited), $"side {side}: {limited}");
                Assert.That(SeatYaw(limited), Is.InRange(44f, 45f), $"side {side}: at the edge of the view");
                Assert.AreEqual(LevelDistance(p, hero), LevelDistance(limited, hero), 1e-3f, "same circle");
                Assert.AreEqual(p.y, limited.y, 1e-5f, "same height");
                Assert.Greater(limited.x * side, 0f, "turned the short way, not across to the other edge");
                Assert.Less(Vector3.Distance(p, limited), 5f, "the nearest edge (4 m round the circle)");
            }

            // In the player's face: the circle round a hero nearer the seat is in view farther out.
            Vector3 near = SeatEye + new Vector3(0f, 0.8f, 8f);
            Vector3 face = SeatEye + new Vector3(1f, 0.5f, 3f);
            Vector3 out1 = Seat.Limit(face, near);
            Assert.IsTrue(Seat.Allows(out1), $"{out1}");
            Assert.GreaterOrEqual(SeatDistance(out1), 8f - 1e-3f);
            Assert.AreEqual(LevelDistance(face, near), LevelDistance(out1, near), 1e-3f, "same circle");

            Vector3 inView = FromSeat(20f, 15f);
            Assert.AreEqual(inView, Seat.Limit(inView, hero), "in view: unchanged");
        }

        [Test]
        public void Seat_Limit_NoBearingInViewOnTheCircle_GoesToTheEdgeOfTheView()
        {
            // A hero in the near right corner: its small circle never comes within 45 degrees.
            Vector3 hero = SeatEye + new Vector3(17f, 0f, 2.5f);
            Vector3 p = hero + new Vector3(-3f, 2f, 0f);
            Vector3 limited = Seat.Limit(p, hero);
            Assert.IsTrue(Seat.Allows(limited), $"{limited}");
            Assert.That(SeatYaw(limited), Is.InRange(44f, 45f), "the edge of the view");
            Assert.Greater(limited.x, 0f, "on the hero's side");
            Assert.GreaterOrEqual(SeatDistance(limited), 8f - 1e-3f);
            Assert.AreEqual(p.y, limited.y, 1e-5f, "same height");
        }

        // Review DR-2: the near wall is 2.5 m in front of the eye, so the Sniper's
        // 18 m circle around a hero at its start never met a wall it could turn at
        // and orbited beside the seat (39% of the time over 45 degrees off, 2.5 m
        // from the eye). Every range keeper, with shots and hits, a hero at its
        // start, higher, nearer the seat and moving about: in the seat's view and
        // never in the player's face. Its dash and hold spots are in view.
        [Test]
        public void RangeKeepers_StayInTheSeatsView()
        {
            Vector3[] heroes = { Center, Center + new Vector3(0f, 3f, 0f), Center + new Vector3(0f, 0f, -8f) };
            foreach (BotArchetypeId id in new[]
                         { BotArchetypeId.Sniper, BotArchetypeId.Gunner, BotArchetypeId.Lancer, BotArchetypeId.Boss })
            foreach (Vector3 hero in heroes)
            foreach (int seed in new[] { 1, 2 })
            {
                Sim sim = Make(id, BotParams.Default, Center + new Vector3(6f, 3f, 12f), Arena, seed);
                sim.Brain.SetSeat(Seat);
                int frames = 0, wide = 0;
                float widest = 0f, nearest = float.MaxValue, range = 0f;
                for (int i = 0; i < 72 * 60; i++)
                {
                    if (i % 216 == 215) sim.Brain.NotifyHit();
                    Vector3 enemy = hero + new Vector3(Mathf.Sin(i * 0.01f) * 3f, Mathf.Sin(i * 0.007f) * 2f,
                        Mathf.Cos(i * 0.013f) * 3f);
                    sim.Step(enemy);
                    string at = $"{id}, hero {hero - Center}, seed {seed}, frame {i}";
                    BotState state = sim.Brain.State;
                    if (state == BotState.Dash || state == BotState.Hold)
                        Assert.IsTrue(Seat.Allows(sim.Brain.DashSpot), $"{at}: {state} spot {sim.Brain.DashSpot}");

                    float yaw = SeatYaw(sim.Position);
                    frames++;
                    if (yaw > 47f) wide++;
                    widest = Mathf.Max(widest, yaw);
                    nearest = Mathf.Min(nearest, SeatDistance(sim.Position));
                    range += Vector3.Distance(sim.Position, enemy);
                }
                string run = $"{id}, hero {hero - Center}, seed {seed}";
                Assert.Less(wide, frames / 100, $"{run}: {wide} of {frames} frames over 47 degrees");
                Assert.Less(widest, 52f, run);
                Assert.Greater(nearest, 7f, $"{run}: in the player's face");
                if (id == BotArchetypeId.Sniper && hero == Center)
                    Assert.Greater(range / frames, 15f, $"{run}: still keeps far");
            }
        }

        // The seat is for range keepers only: Striker (round 4's bot, and the
        // demo's) moves exactly as without one, also where the seat would turn it.
        [Test]
        public void Striker_IgnoresTheSeat()
        {
            Vector3 hero = Center + new Vector3(0f, 0f, -8f);
            Sim free = Make(BotArchetypeId.Striker, BotParams.Default, Center + new Vector3(6f, 3f, 12f), Arena, 5);
            Sim seated = Make(BotArchetypeId.Striker, BotParams.Default, Center + new Vector3(6f, 3f, 12f), Arena, 5);
            seated.Brain.SetSeat(Seat);
            int outside = 0;
            for (int i = 0; i < 72 * 30; i++)
            {
                if (i % 216 == 215)
                {
                    free.Brain.NotifyHit();
                    seated.Brain.NotifyHit();
                }
                HandInputData a = free.Step(hero);
                HandInputData b = seated.Step(hero);
                Assert.IsTrue(a.ClutchDelta.Equals(b.ClutchDelta), $"move, frame {i}");
                Assert.AreEqual(a.FireTriggered, b.FireTriggered, $"fire, frame {i}");
                Assert.IsTrue(free.Position.Equals(seated.Position), $"position, frame {i}");
                if (!Seat.Allows(free.Position)) outside++;
            }
            Assert.Greater(outside, 0, "the seat would have turned a range keeper here");
        }

        [Test]
        public void SameSeed_SameMovement()
        {
            foreach (BotArchetypeId id in new[] { BotArchetypeId.Sniper, BotArchetypeId.Lancer, BotArchetypeId.Boss })
            {
                Sim a = Make(id, BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 42);
                Sim b = Make(id, BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 42);
                for (int i = 0; i < 72 * 15; i++)
                {
                    Vector3 enemy = new Vector3(Mathf.Sin(i * 0.02f) * 6f, 2f, 8f);
                    a.Step(enemy);
                    b.Step(enemy);
                }
                Assert.AreEqual(a.Position, b.Position, $"{id}");
            }
        }
    }
}
