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
    // frame. Every bot still flies through the player's clutch and flight model
    // with the same hand speed cap.
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

        [Test]
        public void Striker_Movement_ReproducesTodaysBot_FrameForFrame()
        {
            var today = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 9);
            var preset = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 9);
            preset.Brain.SetMovement(BotMovements.Striker, BotMovements.Striker);
            // An archetype entry serialized before round 5: every movement field 0.
            var unset = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), Arena, 9);
            unset.Brain.SetMovement(default, default);

            int shots = 0;
            var states = new HashSet<BotState>();
            for (int i = 0; i < 72 * 30; i++)
            {
                Vector3 enemy = new Vector3(Mathf.Sin(i * 0.02f) * 6f, 2f, 8f);
                if (i == 400 || i == 1200)
                {
                    today.Brain.NotifyHit();
                    preset.Brain.NotifyHit();
                    unset.Brain.NotifyHit();
                }
                HandInputData a = today.Step(enemy);
                foreach (Sim other in new[] { preset, unset })
                {
                    HandInputData b = other.Step(enemy);
                    Assert.IsTrue(a.ClutchDelta.Equals(b.ClutchDelta), $"move, frame {i}");
                    Assert.AreEqual(a.FireTriggered, b.FireTriggered, $"fire, frame {i}");
                    Assert.IsTrue(a.AimDirection.Equals(b.AimDirection), $"aim, frame {i}");
                    Assert.AreEqual(today.Brain.State, other.Brain.State, $"state, frame {i}");
                    Assert.IsTrue(today.Position.Equals(other.Position), $"position, frame {i}");
                }
                states.Add(today.Brain.State);
                if (a.FireTriggered) shots++;
            }

            Assert.Greater(shots, 5);
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

        // --- the boss ---

        [Test]
        public void Boss_MovesLikeTheGunner_ThenLikeTheLancer()
        {
            BotAttack gunner = BotArchetypes.Get(BotArchetypeId.Gunner).Attack;
            BotAttack lancer = BotArchetypes.Get(BotArchetypeId.Lancer).Attack;
            Sim sim = Make(BotArchetypeId.Boss, Quiet(), Center + new Vector3(0f, 0f, 9f), WideArena, 4);

            int gunnerAttacks = 0, lancerAttacks = 0;
            BotState lastMove = BotState.Idle; // the latest state other than Hold
            bool wasTelegraphing = false;
            for (int i = 0; i < 72 * 40; i++)
            {
                sim.Step(Center);
                BotBrain brain = sim.Brain;
                if (brain.IsTelegraphing && !wasTelegraphing)
                {
                    float mult = brain.ActiveAttack.TelegraphMult;
                    if (Mathf.Approximately(mult, gunner.TelegraphMult))
                    {
                        Assert.That(brain.State, Is.EqualTo(BotState.Strafe).Or.EqualTo(BotState.Approach),
                            $"Gunner pattern strafes, frame {i}");
                        gunnerAttacks++;
                    }
                    else
                    {
                        Assert.AreEqual(lancer.TelegraphMult, mult, 1e-5f);
                        Assert.AreEqual(BotState.Hold, brain.State, $"Lancer pattern holds, frame {i}");
                        Assert.AreEqual(BotState.Dash, lastMove, $"after a dash, frame {i}");
                        lancerAttacks++;
                    }
                }
                if (brain.State != BotState.Hold) lastMove = brain.State;
                wasTelegraphing = brain.IsTelegraphing;
            }
            Assert.GreaterOrEqual(gunnerAttacks, 2);
            Assert.GreaterOrEqual(lancerAttacks, 2);
        }

        // --- fairness: the player's arena and hand speed ---

        [Test]
        public void EveryArchetype_StaysInTheArena_UnderTheHandSpeedCap()
        {
            BotParams p = BotParams.Default;
            foreach (BotArchetypeId id in new[]
                     {
                         BotArchetypeId.Striker, BotArchetypeId.Sniper, BotArchetypeId.Gunner, BotArchetypeId.Lancer,
                         BotArchetypeId.Boss,
                     })
            {
                Sim sim = Make(id, p, new Vector3(17f, 11f, 37f), Arena, 7);
                for (int i = 0; i < 72 * 20; i++)
                {
                    if (i % 250 == 249) sim.Brain.NotifyHit();
                    Vector3 enemy = new Vector3(Mathf.Sin(i * 0.015f) * 12f, 2f + Mathf.Sin(i * 0.007f) * 5f, 6f);
                    HandInputData input = sim.Step(enemy);
                    Assert.LessOrEqual(input.ClutchDelta.magnitude, p.MaxHandSpeed * Dt + 1e-5f, $"{id}, frame {i}");
                    AssertInside(Arena, sim.Position, $"{id}, frame {i}");
                    if (sim.Brain.State == BotState.Dash || sim.Brain.State == BotState.Hold)
                        AssertInside(Arena, sim.Brain.DashSpot, $"{id}, frame {i}");
                }
            }
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
