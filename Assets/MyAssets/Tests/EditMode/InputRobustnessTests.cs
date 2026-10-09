using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // P7 (D5, D6): hitch recovery, clutch clamped to the arena, and stale
    // gesture state dropped when the input source is switched back on.
    public class InputRobustnessTests
    {
        private static readonly FlightParams Flight = new FlightParams
        {
            Stiffness = 10f,
            Damping = 5f,
            MaxSpeed = 25f,
            GlideDrag = 0.8f,
        };

        private static readonly ArenaBounds Bounds =
            new ArenaBounds(new Vector3(0f, 10f, 20f), new Vector3(35f, 20f, 35f));

        // ---- Spring: frame-rate independent (BC-3, BR-12) ----

        [Test]
        public void HitchStep_AtRest_MovesTowardTargetWithoutOvershoot()
        {
            var target = new Vector3(2f, 0f, 0f);
            FlightState next = SpringFlightModel.Step(new FlightState(), true, target, Flight, default, 0.33f);

            Assert.Greater(next.Position.x, 0f, "moves toward the target");
            Assert.Less(next.Position.x, target.x, "does not overshoot");
            Assert.Greater(next.Velocity.x, 0f, "velocity still points at the target");
        }

        [Test]
        public void HitchStep_MovingTowardTarget_VelocityNeverFlipsBackward()
        {
            var s = new FlightState { Velocity = new Vector3(10f, 0f, 0f) };
            var target = new Vector3(10f, 0f, 0f);
            FlightState next = SpringFlightModel.Step(s, true, target, Flight, default, 0.33f);

            Assert.Greater(next.Velocity.x, 0f);
            Assert.Greater(next.Position.x, 0f);
        }

        [Test]
        public void HitchStep_Glide_SlowsButNeverReverses()
        {
            var s = new FlightState { Velocity = new Vector3(10f, 0f, 0f) };
            FlightState next = SpringFlightModel.Step(s, false, Vector3.zero, new FlightParams
            {
                Stiffness = 10f, Damping = 5f, MaxSpeed = 25f, GlideDrag = 8f,
            }, default, 0.33f);

            Assert.Greater(next.Velocity.x, 0f);
            Assert.Less(next.Velocity.x, 10f);
        }

        [Test]
        public void SameTimeInOneHitchOrManyFrames_EndsNearTheSamePlace()
        {
            var target = new Vector3(3f, 1f, -2f);
            FlightState oneStep = SpringFlightModel.Step(new FlightState(), true, target, Flight, default, 0.1f);

            FlightState many = new FlightState();
            for (int i = 0; i < 9; i++)
                many = SpringFlightModel.Step(many, true, target, Flight, default, 0.1f / 9f);

            Assert.Less(Vector3.Distance(oneStep.Position, many.Position), 0.05f);
        }

        [Test]
        public void SubSteps_AreCapped()
        {
            Assert.AreEqual(1, SpringFlightModel.SubStepCount(1f / 72f));
            Assert.AreEqual(1, SpringFlightModel.SubStepCount(1f / 60f));
            Assert.AreEqual(2, SpringFlightModel.SubStepCount(0.02f));
            Assert.AreEqual(SpringFlightModel.MaxSubSteps, SpringFlightModel.SubStepCount(0.33f));
            Assert.AreEqual(1, SpringFlightModel.SubStepCount(0f));
        }

        // ---- Clutch target clamped to the arena (BR-13, BC-4) ----

        [Test]
        public void ClutchPastWall_ReversingMovesInwardAtOnce()
        {
            var mapper = new ClutchMapper();
            Vector3 start = Bounds.Center;
            mapper.Step(true, Vector3.zero, start, 60f, Bounds);

            // Drag 1 m of hand = 60 m of target, far past the +X wall (17.5 m away).
            ClutchResult past = mapper.Step(true, new Vector3(1f, 0f, 0f), start, 60f, Bounds);
            Assert.AreEqual(Bounds.Center.x + 17.5f, past.Target.x, 1e-4f);

            // Back 1 cm: the target leaves the wall right away (no dead zone).
            ClutchResult back = mapper.Step(true, new Vector3(-0.01f, 0f, 0f), start, 60f, Bounds);
            Assert.AreEqual(Bounds.Center.x + 17.5f - 0.6f, back.Target.x, 1e-4f);
        }

        [Test]
        public void ClutchWithoutBounds_IsUnclamped()
        {
            var mapper = new ClutchMapper();
            mapper.Step(true, Vector3.zero, Vector3.zero, 60f);
            ClutchResult r = mapper.Step(true, new Vector3(1f, 0f, 0f), Vector3.zero, 60f);
            Assert.AreEqual(60f, r.Target.x, 1e-4f);
        }

        [Test]
        public void CursorPastWall_ReversingMovesInwardAtOnce()
        {
            var cursor = new AimCursorModel();
            cursor.Reset(Bounds.Center);
            cursor.Step(true, Vector3.zero, 60f, Bounds);
            cursor.Step(true, new Vector3(1f, 0f, 0f), 60f, Bounds);

            Vector3 p = cursor.Step(true, new Vector3(-0.01f, 0f, 0f), 60f, Bounds);
            Assert.AreEqual(Bounds.Center.x + 17.5f - 0.6f, p.x, 1e-4f);
        }

        // ---- Reset on re-enable (BR-2) ----

        [Test]
        public void ClutchSampler_ResetThenStep_GivesNoStaleDelta()
        {
            var sampler = new HandClutchSampler();
            sampler.Step(true, 0.9f, Vector3.zero, 0.7f, 0.45f, out _);
            Assert.IsTrue(sampler.IsHeld);

            // Input source switched off (pause) while the hand moved 30 cm.
            sampler.Reset();
            Assert.IsFalse(sampler.IsHeld);

            bool held = sampler.Step(true, 0.9f, new Vector3(0.3f, 0f, 0f), 0.7f, 0.45f, out Vector3 delta);
            Assert.IsTrue(held, "a fist still closed regrabs");
            Assert.AreEqual(Vector3.zero, delta, "the regrab frame carries no motion");
        }

        [Test]
        public void PalmPush_ResetThenStep_NoSpuriousPush()
        {
            var push = new PalmPushRecognizer();
            PalmPushParams p = PalmPushParams.Default;
            push.Step(true, Vector3.zero, Vector3.forward, 0f, 0.02f, p);

            push.Reset();

            // The palm is now 30 cm further along its normal: not a push.
            Assert.IsFalse(push.Step(true, new Vector3(0f, 0f, 0.3f), Vector3.forward, 0f, 0.02f, p));
            Assert.IsFalse(push.Step(true, new Vector3(0f, 0f, 0.3f), Vector3.forward, 0f, 0.02f, p));
        }

        [Test]
        public void PalmPush_ResetRearmsTheGate()
        {
            var push = new PalmPushRecognizer();
            PalmPushParams p = PalmPushParams.Default;
            Vector3 pos = Vector3.zero;
            push.Step(true, pos, Vector3.forward, 0f, 0.02f, p);
            pos.z += 0.04f;
            Assert.IsTrue(push.Step(true, pos, Vector3.forward, 0f, 0.02f, p));

            push.Reset();
            push.Step(true, pos, Vector3.forward, 0f, 0.02f, p);
            pos.z += 0.04f;
            Assert.IsTrue(push.Step(true, pos, Vector3.forward, 0f, 0.02f, p), "a fresh push after the reset fires");
        }

        [Test]
        public void PalmsTogether_ResetDropsTheCharge()
        {
            var palms = new PalmsTogetherRecognizer();
            Assert.IsTrue(palms.Step(true, true, Vector3.zero, new Vector3(0.05f, 0f, 0f), 0.1f, 0.18f, 0.3f, 0.02f));

            palms.Reset();
            Assert.IsFalse(palms.IsHeld);
            // Lost hands right after the reset: no grace from the old charge.
            Assert.IsFalse(palms.Step(false, true, Vector3.zero, Vector3.zero, 0.1f, 0.18f, 0.3f, 0.02f));
        }

        // ---- Long frames never read as palm speed (CR-8) ----

        [Test]
        public void PalmPush_LongFrame_IsNotReadAsSpeed()
        {
            var push = new PalmPushRecognizer();
            PalmPushParams p = PalmPushParams.Default;
            push.Step(true, Vector3.zero, Vector3.forward, 0f, 0.02f, p);

            // 0.15 s stall, palm moved 20 cm: the hand pose is smoothed across the
            // stall, so the speed from one long frame is not trusted.
            Assert.IsFalse(push.Step(true, new Vector3(0f, 0f, 0.2f), Vector3.forward, 0f, 0.15f, p));
            // The next normal frame measures from the post-stall position.
            Assert.IsFalse(push.Step(true, new Vector3(0f, 0f, 0.205f), Vector3.forward, 0f, 0.02f, p));
        }
    }
}
