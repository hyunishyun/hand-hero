using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // T5 gestures: both palms together (charge) and palm push (shockwave), both hysteresis-gated.
    public class PalmsTogetherRecognizerTests
    {
        private const float Join = 0.10f;
        private const float Separate = 0.18f;
        private const float Grace = 0.3f;
        private const float Dt = 1f / 72f;

        private static bool Step(PalmsTogetherRecognizer r, float distance, bool leftTracked = true,
            bool rightTracked = true, float dt = Dt)
        {
            return r.Step(leftTracked, rightTracked, Vector3.zero, new Vector3(distance, 0f, 0f),
                Join, Separate, Grace, dt);
        }

        [Test]
        public void JoinsAtJoinDistance()
        {
            var r = new PalmsTogetherRecognizer();
            Assert.IsFalse(Step(r, 0.11f));
            Assert.IsTrue(Step(r, 0.10f));
        }

        [Test]
        public void SeparatesAtSeparateDistance()
        {
            var r = new PalmsTogetherRecognizer();
            Step(r, 0.05f);
            Assert.IsTrue(Step(r, 0.17f));
            Assert.IsFalse(Step(r, 0.18f));
        }

        [Test]
        public void DistanceOscillatingBetweenThresholds_NeverFlickers()
        {
            var r = new PalmsTogetherRecognizer();
            Step(r, 0.05f);
            for (int i = 0; i < 100; i++)
                Assert.IsTrue(Step(r, i % 2 == 0 ? 0.11f : 0.17f), $"held, step {i}");

            var off = new PalmsTogetherRecognizer();
            for (int i = 0; i < 100; i++)
                Assert.IsFalse(Step(off, i % 2 == 0 ? 0.11f : 0.17f), $"apart, step {i}");
        }

        [Test]
        public void ShortTrackingLoss_KeepsTheChargeHeld()
        {
            var r = new PalmsTogetherRecognizer();
            Step(r, 0.05f);
            Assert.IsTrue(Step(r, 0.05f, rightTracked: false, dt: 0.2f), "hands occlude each other briefly");
            Assert.IsTrue(Step(r, 0.05f), "tracking back, still together");
            Assert.IsTrue(Step(r, 0.05f, leftTracked: false, dt: 0.2f), "grace restarts after recovery");
        }

        [Test]
        public void LongTrackingLoss_Releases()
        {
            var r = new PalmsTogetherRecognizer();
            Step(r, 0.05f);
            Assert.IsTrue(Step(r, 0.05f, rightTracked: false, dt: 0.2f));
            Assert.IsFalse(Step(r, 0.05f, rightTracked: false, dt: 0.2f));
        }

        [Test]
        public void UntrackedHands_NeverJoin()
        {
            var r = new PalmsTogetherRecognizer();
            Assert.IsFalse(Step(r, 0.0f, leftTracked: false));
            Assert.IsFalse(Step(r, 0.0f, rightTracked: false));
        }
    }

    public class PalmPushRecognizerTests
    {
        private const float Dt = 0.02f;
        private static readonly PalmPushParams P = PalmPushParams.Default;
        private static readonly Vector3 Normal = Vector3.forward; // palm faces +Z

        // Moves the palm along +Z at the given speed (m/s) for one frame.
        private static bool Move(PalmPushRecognizer r, ref Vector3 pos, float speed, float fist = 0f,
            Vector3? normal = null)
        {
            pos += Vector3.forward * (speed * Dt);
            return r.Step(true, pos, normal ?? Normal, fist, Dt, P);
        }

        [Test]
        public void Defaults()
        {
            Assert.AreEqual(1.2f, P.PushSpeed);
            Assert.AreEqual(0.4f, P.RearmSpeed);
            Assert.AreEqual(0.35f, P.MaxFistStrength);
        }

        [Test]
        public void FastPushAlongPalm_TriggersOncePerPush()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            Assert.IsFalse(Move(r, ref pos, 0f), "first frame only seeds the position");

            int triggers = 0;
            for (int i = 0; i < 10; i++)
                if (Move(r, ref pos, 2f)) triggers++;
            Assert.AreEqual(1, triggers);
        }

        [Test]
        public void SlowPush_DoesNotTrigger()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(Move(r, ref pos, 1.0f), $"step {i}");
        }

        [Test]
        public void PullingBack_DoesNotTrigger()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(Move(r, ref pos, -3f), $"step {i}");
        }

        [Test]
        public void MovingSideways_DoesNotTrigger()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            for (int i = 0; i < 10; i++)
                Assert.IsFalse(Move(r, ref pos, 3f, normal: Vector3.right), $"step {i}");
        }

        [Test]
        public void ClosedFist_DoesNotTrigger_EvenLaterInTheSameMotion()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            Move(r, ref pos, 0f);
            Assert.IsFalse(Move(r, ref pos, 2f, fist: 0.9f), "a fist punch / clutch drag is not a push");
            Assert.IsFalse(Move(r, ref pos, 2f, fist: 0f), "opening mid-motion does not fire");
        }

        [Test]
        public void RearmsAfterSlowingDown()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            Move(r, ref pos, 0f);
            Assert.IsTrue(Move(r, ref pos, 2f));
            Assert.IsFalse(Move(r, ref pos, 0.5f), "still above rearm speed");
            Assert.IsFalse(Move(r, ref pos, 2f), "not re-armed yet");
            Move(r, ref pos, 0.3f);
            Assert.IsTrue(Move(r, ref pos, 2f), "re-armed");
        }

        [Test]
        public void SpeedOscillatingBetweenThresholds_NeverRetriggers()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            Move(r, ref pos, 0f);
            Assert.IsTrue(Move(r, ref pos, 2f));
            for (int i = 0; i < 100; i++)
                Assert.IsFalse(Move(r, ref pos, i % 2 == 0 ? 1.1f : 0.5f), $"step {i}");
        }

        [Test]
        public void TrackingLoss_DoesNotTurnTheJumpIntoAPush()
        {
            var r = new PalmPushRecognizer();
            Vector3 pos = Vector3.zero;
            Move(r, ref pos, 0f);
            Assert.IsFalse(r.Step(false, pos, Normal, 0f, Dt, P));

            // Hand reappears 30 cm further along its normal.
            pos += Vector3.forward * 0.3f;
            Assert.IsFalse(r.Step(true, pos, Normal, 0f, Dt, P), "reacquire only seeds the position");
            Assert.IsFalse(Move(r, ref pos, 0f));
        }
    }

    public class ChargeShotModelTests
    {
        private static readonly ChargeParams P = ChargeParams.Default;

        private static ChargeStep Hold(ChargeShotModel m, float seconds, float dt = 0.05f)
        {
            ChargeStep last = default;
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++) last = m.Step(true, dt, P);
            return last;
        }

        [Test]
        public void Defaults()
        {
            Assert.AreEqual(0.3f, P.MinChargeTime);
            Assert.AreEqual(1.5f, P.MaxChargeTime);
        }

        [Test]
        public void ShortHold_ReleasesNothing()
        {
            var m = new ChargeShotModel();
            ChargeStep held = Hold(m, 0.2f);
            Assert.IsTrue(held.Charging);
            Assert.IsFalse(held.Ready);

            ChargeStep release = m.Step(false, 0.05f, P);
            Assert.IsFalse(release.Released);
            Assert.IsFalse(release.Charging);
        }

        [Test]
        public void ReleaseJustAfterMinCharge_FiresAtLowPower()
        {
            var m = new ChargeShotModel();
            Assert.IsTrue(Hold(m, 0.35f).Ready);
            ChargeStep release = m.Step(false, 0.05f, P);
            Assert.IsTrue(release.Released);
            Assert.AreEqual(0.05f / 1.2f, release.Power, 0.01f);
        }

        [Test]
        public void PowerGrowsWithChargeTime_AndCapsAtMax()
        {
            var m = new ChargeShotModel();
            Hold(m, 0.9f);
            ChargeStep mid = m.Step(false, 0.05f, P);
            Assert.AreEqual(0.5f, mid.Power, 0.01f);

            Hold(m, 5f);
            Assert.AreEqual(1f, m.Step(true, 0.05f, P).Fraction, 1e-5f, "charge time caps at max");
            ChargeStep full = m.Step(false, 0.05f, P);
            Assert.IsTrue(full.Released);
            Assert.AreEqual(1f, full.Power, 1e-5f);
        }

        [Test]
        public void ReleaseHappensOnce_AndChargeRestartsFromZero()
        {
            var m = new ChargeShotModel();
            Hold(m, 1f);
            Assert.IsTrue(m.Step(false, 0.05f, P).Released);
            Assert.IsFalse(m.Step(false, 0.05f, P).Released);
            Assert.IsFalse(Hold(m, 0.1f).Ready, "new charge starts from zero");
        }

        [Test]
        public void Cancel_DropsTheChargeWithoutFiring()
        {
            var m = new ChargeShotModel();
            Hold(m, 1f);
            m.Cancel();
            Assert.IsFalse(m.Step(false, 0.05f, P).Released);
        }
    }
}
