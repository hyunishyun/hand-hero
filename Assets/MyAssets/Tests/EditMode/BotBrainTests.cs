using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // The bot drives the hero through the same HandInputData -> ClutchMapper ->
    // SpringFlightModel path as the player (fair: same speed cap, same mapping).
    public class BotBrainTests
    {
        private const float Dt = 1f / 72f;

        private static readonly FlightParams Flight = new FlightParams
        {
            Stiffness = 10f,
            Damping = 5f,
            MaxSpeed = 25f,
            GlideDrag = 0.8f,
        };

        private static readonly ArenaBounds Arena = new ArenaBounds(new Vector3(0f, 2f, 20f), new Vector3(35f, 20f, 35f));

        // Bot + the same simulation the scene runs (puppeteer mapping + flight).
        private class Sim
        {
            public readonly BotBrain Brain;
            public readonly BotParams Params;
            public readonly ClutchMapper Mapper = new ClutchMapper();
            public FlightState State;
            public Vector3 Target;
            public bool HasTarget;
            public HandInputData Last;

            public Sim(BotParams p, Vector3 start, int seed = 1)
            {
                Params = p;
                Brain = new BotBrain(p, seed);
                State = new FlightState { Position = start };
            }

            public HandInputData Step(Vector3 enemy, bool hasEnemy = true)
            {
                Last = Brain.Step(State.Position, hasEnemy, enemy, Arena, Dt);
                ClutchResult c = Mapper.Step(Last, State.Position, Params.PositionScale);
                if (c.Clutched)
                {
                    Target = Arena.Clamp(c.Target);
                    HasTarget = true;
                }
                if (c.JustReleased) HasTarget = false;
                State = SpringFlightModel.Step(State, HasTarget, Target, Flight, Arena, Dt);
                return Last;
            }
        }

        private static BotParams NoNoise()
        {
            BotParams p = BotParams.Default;
            p.ReactionTime = 0f;
            p.AimErrorDegrees = 0f;
            p.FireIntervalJitter = 0f;
            return p;
        }

        [Test]
        public void FirstStep_GrabsWithoutMoving()
        {
            var brain = new BotBrain(BotParams.Default, 1);
            HandInputData input = brain.Step(new Vector3(0f, 2f, 30f), true, new Vector3(0f, 2f, 5f), Arena, Dt);

            Assert.IsTrue(input.ClutchHeld);
            Assert.AreEqual(Vector3.zero, input.ClutchDelta);
        }

        [Test]
        public void HandSpeed_IsCappedLikeAHuman()
        {
            BotParams p = BotParams.Default;
            var brain = new BotBrain(p, 1);
            Vector3 self = new Vector3(15f, 2f, 35f);
            brain.Step(self, true, new Vector3(-15f, 2f, 5f), Arena, Dt);

            for (int i = 0; i < 30; i++)
            {
                HandInputData input = brain.Step(self, true, new Vector3(-15f, 2f, 5f), Arena, Dt);
                Assert.LessOrEqual(input.ClutchDelta.magnitude, p.MaxHandSpeed * Dt + 1e-5f);
            }
        }

        [Test]
        public void FarTarget_Approaches_IntoPreferredRange()
        {
            BotParams p = NoNoise();
            p.FireInterval = 1000f; // movement only
            Vector3 enemy = new Vector3(0f, 2f, 5f);
            var sim = new Sim(p, new Vector3(0f, 2f, 36f));

            sim.Step(enemy);
            Assert.AreEqual(BotState.Approach, sim.Brain.State);

            for (int i = 0; i < 72 * 6; i++) sim.Step(enemy);

            float d = Vector3.Distance(sim.State.Position, enemy);
            Assert.That(d, Is.InRange(p.PreferredRange - p.RangeTolerance, p.PreferredRange + p.RangeTolerance));
            Assert.AreEqual(BotState.Strafe, sim.Brain.State);
        }

        [Test]
        public void InRange_Strafes_Sideways()
        {
            BotParams p = NoNoise();
            p.FireInterval = 1000f;
            p.StrafeSwitchInterval = 1000f;
            Vector3 enemy = new Vector3(0f, 2f, 5f);
            Vector3 start = enemy + new Vector3(0f, 0f, p.PreferredRange);
            var sim = new Sim(p, start);

            for (int i = 0; i < 72; i++) sim.Step(enemy);

            Assert.AreEqual(BotState.Strafe, sim.Brain.State);
            Assert.Greater(Mathf.Abs(sim.State.Position.x - start.x), 1f, "moved sideways around the enemy");
        }

        [Test]
        public void StaysInsideArena()
        {
            BotParams p = BotParams.Default;
            Vector3 enemy = new Vector3(0f, 2f, 5f);
            var sim = new Sim(p, new Vector3(17f, 11f, 37f), seed: 7);

            for (int i = 0; i < 72 * 20; i++)
            {
                sim.Step(enemy);
                Assert.AreEqual(Arena.Clamp(sim.State.Position), sim.State.Position);
            }
        }

        [Test]
        public void Fires_AfterTelegraph_WithLockedAim()
        {
            BotParams p = NoNoise();
            p.FireInterval = 1f;
            p.TelegraphTime = 0.6f;
            Vector3 enemy = new Vector3(0f, 2f, 8f);
            var sim = new Sim(p, enemy + new Vector3(0f, 0f, p.PreferredRange));

            int telegraphFrames = 0;
            int fires = 0;
            Vector3 lockedAim = Vector3.zero;
            for (int i = 0; i < 72 * 2; i++)
            {
                HandInputData input = sim.Step(enemy);
                if (sim.Brain.IsTelegraphing)
                {
                    if (telegraphFrames == 0) lockedAim = sim.Brain.LockedAimPoint;
                    Assert.AreEqual(lockedAim, sim.Brain.LockedAimPoint, "aim locked during telegraph");
                    Assert.IsFalse(input.FireTriggered);
                    telegraphFrames++;
                }
                if (input.FireTriggered)
                {
                    fires++;
                    Assert.IsTrue(input.HasAim);
                    Vector3 expected = (lockedAim - input.AimOrigin).normalized;
                    Assert.That(Vector3.Distance(expected, input.AimDirection), Is.LessThan(1e-4f));
                }
            }

            Assert.AreEqual(1, fires);
            Assert.That(telegraphFrames * Dt, Is.EqualTo(p.TelegraphTime).Within(2f * Dt));
            Assert.That(Vector3.Distance(lockedAim, enemy), Is.LessThan(1e-3f), "no aim error configured");
        }

        [Test]
        public void TelegraphLocksAim_SoAMovingEnemyCanDodge()
        {
            BotParams p = NoNoise();
            p.FireInterval = 0.5f;
            Vector3 enemy = new Vector3(0f, 2f, 8f);
            var sim = new Sim(p, enemy + new Vector3(0f, 0f, p.PreferredRange));

            Vector3 lockedAim = Vector3.zero;
            bool fired = false;
            for (int i = 0; i < 72 * 2 && !fired; i++)
            {
                if (sim.Brain.IsTelegraphing) enemy += new Vector3(6f, 0f, 0f) * Dt; // dodge sideways
                HandInputData input = sim.Step(enemy);
                if (sim.Brain.IsTelegraphing) lockedAim = sim.Brain.LockedAimPoint;
                fired = input.FireTriggered;
            }

            Assert.IsTrue(fired);
            Assert.Greater(Vector3.Distance(lockedAim, enemy), 2f, "the shot goes where the enemy was");
        }

        [Test]
        public void AimError_StaysInsideCone()
        {
            BotParams p = BotParams.Default;
            p.ReactionTime = 0f;
            p.FireInterval = 0.1f;
            p.FireIntervalJitter = 0f;
            p.TelegraphTime = 0.1f;
            Vector3 enemy = new Vector3(0f, 2f, 8f);
            var brain = new BotBrain(p, 3);
            Vector3 self = enemy + new Vector3(0f, 0f, 12f);

            int shots = 0;
            float maxAngle = 0f;
            for (int i = 0; i < 72 * 10; i++)
            {
                HandInputData input = brain.Step(self, true, enemy, Arena, Dt);
                if (!input.FireTriggered) continue;
                shots++;
                maxAngle = Mathf.Max(maxAngle, Vector3.Angle(enemy - self, input.AimDirection));
            }

            Assert.Greater(shots, 10);
            Assert.LessOrEqual(maxAngle, p.AimErrorDegrees + 1e-3f);
            Assert.Greater(maxAngle, 0f, "some error is applied");
        }

        [Test]
        public void ReactionTime_LagsPerception()
        {
            BotParams p = BotParams.Default;
            p.ReactionTime = 0.5f;
            var brain = new BotBrain(p, 1);
            Vector3 self = new Vector3(0f, 2f, 30f);

            brain.Step(self, true, new Vector3(0f, 2f, 10f), Arena, Dt);
            brain.Step(self, true, new Vector3(10f, 2f, 10f), Arena, Dt); // enemy teleports

            Assert.Less(brain.PerceivedTarget.x, 1f, "one frame later the bot still sees the old spot");

            for (int i = 0; i < 72 * 3; i++) brain.Step(self, true, new Vector3(10f, 2f, 10f), Arena, Dt);
            Assert.That(brain.PerceivedTarget.x, Is.EqualTo(10f).Within(0.1f));
        }

        [Test]
        public void Hit_TriggersEvade_AndCancelsTelegraph()
        {
            BotParams p = NoNoise();
            p.FireInterval = 0.2f;
            p.TelegraphTime = 1f;
            Vector3 enemy = new Vector3(0f, 2f, 8f);
            var sim = new Sim(p, enemy + new Vector3(0f, 0f, p.PreferredRange));

            for (int i = 0; i < 72 && !sim.Brain.IsTelegraphing; i++) sim.Step(enemy);
            Assert.IsTrue(sim.Brain.IsTelegraphing);

            Vector3 before = sim.State.Position;
            sim.Brain.NotifyHit();
            sim.Step(enemy);
            Assert.AreEqual(BotState.Evade, sim.Brain.State);
            Assert.IsFalse(sim.Brain.IsTelegraphing);

            bool firedWhileEvading = false;
            while (sim.Brain.State == BotState.Evade)
                firedWhileEvading |= sim.Step(enemy).FireTriggered;

            Assert.IsFalse(firedWhileEvading);
            Assert.Greater(Vector3.Distance(before, sim.State.Position), 2f, "dodged away");
        }

        [Test]
        public void NoTarget_HoversWithoutAimOrFire()
        {
            var brain = new BotBrain(BotParams.Default, 1);
            Vector3 self = new Vector3(0f, 2f, 20f);
            for (int i = 0; i < 72 * 5; i++)
            {
                HandInputData input = brain.Step(self, false, Vector3.zero, Arena, Dt);
                Assert.IsFalse(input.HasAim);
                Assert.IsFalse(input.FireTriggered);
                Assert.AreEqual(Vector3.zero, input.ClutchDelta);
            }
            Assert.AreEqual(BotState.Idle, brain.State);
        }

        [Test]
        public void Reset_RegrabsFromCurrentPosition()
        {
            var brain = new BotBrain(BotParams.Default, 1);
            Vector3 enemy = new Vector3(0f, 2f, 5f);
            brain.Step(new Vector3(0f, 2f, 30f), true, enemy, Arena, Dt);
            brain.Step(new Vector3(0f, 2f, 30f), true, enemy, Arena, Dt);

            brain.Reset();
            HandInputData input = brain.Step(new Vector3(5f, 2f, 25f), true, enemy, Arena, Dt);
            Assert.IsTrue(input.ClutchHeld);
            Assert.AreEqual(Vector3.zero, input.ClutchDelta);
        }

        [Test]
        public void SameSeed_SameMatch()
        {
            var a = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), seed: 42);
            var b = new Sim(BotParams.Default, new Vector3(5f, 2f, 30f), seed: 42);
            Vector3 enemy = new Vector3(0f, 2f, 8f);
            for (int i = 0; i < 72 * 8; i++)
            {
                a.Step(enemy);
                b.Step(enemy);
            }
            Assert.AreEqual(a.State.Position, b.State.Position);
        }
    }
}
