using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Pinch = fire edge + hold level (charge), with the aim-hand fist suppressing
    // both: a closed fist brings thumb and index close enough to read as a pinch.
    public class PinchTriggerTests
    {
        private const float Fire = 0.8f;
        private const float ResetT = 0.5f;
        private const float Suppress = 0.15f;
        private const float Dt = 0.02f;

        private PinchTrigger _pinch;

        [SetUp]
        public void SetUp()
        {
            _pinch = new PinchTrigger();
        }

        private PinchState Step(float pinch, bool fist = false, bool tracked = true)
        {
            return _pinch.Step(tracked, pinch, Fire, ResetT, fist, Suppress, Dt);
        }

        [Test]
        public void OnePinch_FiresOnce()
        {
            PinchState a = Step(0.9f);
            PinchState b = Step(0.9f);
            PinchState c = Step(0.9f);

            Assert.IsTrue(a.FireTriggered);
            Assert.IsFalse(b.FireTriggered);
            Assert.IsFalse(c.FireTriggered);
            Assert.IsTrue(a.Held && b.Held && c.Held);
        }

        [Test]
        public void ReleaseBelowReset_RearmsNextPinch()
        {
            Assert.IsTrue(Step(0.9f).FireTriggered);
            Assert.IsFalse(Step(0.4f).FireTriggered);
            Assert.IsTrue(Step(0.9f).FireTriggered);
        }

        [Test]
        public void BetweenThresholds_KeepsHeld()
        {
            Step(0.9f);
            PinchState s = Step(0.6f);
            Assert.IsTrue(s.Held);
            Assert.IsFalse(s.FireTriggered);
        }

        [Test]
        public void FistHeld_SuppressesFireAndHeld()
        {
            PinchState s = Step(0.9f, fist: true);
            Assert.IsFalse(s.FireTriggered);
            Assert.IsFalse(s.Held);
        }

        [Test]
        public void AfterFist_SuppressedForSuppressTime()
        {
            Step(0.2f, fist: true);
            for (int i = 0; i < 5; i++) // 0.10 s < 0.15 s
            {
                PinchState s = Step(0.9f);
                Assert.IsFalse(s.FireTriggered, $"step {i}");
                Assert.IsFalse(s.Held, $"step {i}");
            }
        }

        [Test]
        public void PinchClosedThroughSuppression_MustReopenBeforeFiring()
        {
            Step(0.9f, fist: true);
            for (int i = 0; i < 15; i++) // 0.3 s, well past the suppression
                Assert.IsFalse(Step(0.9f).FireTriggered, $"step {i}");

            Assert.IsFalse(Step(0.4f).FireTriggered);
            Assert.IsTrue(Step(0.9f).FireTriggered);
            Assert.IsFalse(Step(0.9f).FireTriggered);
        }

        [Test]
        public void TrackingLoss_ResetsGate()
        {
            Assert.IsTrue(Step(0.9f).FireTriggered);
            Assert.IsFalse(Step(0.9f, tracked: false).Held);
            Assert.IsTrue(Step(0.9f).FireTriggered);
        }
    }
}
