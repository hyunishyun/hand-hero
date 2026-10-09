using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Meta system gesture (palm toward the headset, CR-7): its pinch belongs to the
    // OS menu, so the game reads it as open, and a pinch still closed when the
    // gesture ends must open once before it counts.
    public class SystemGestureGateTests
    {
        private const float ResetT = 0.5f;

        private SystemGestureGate _gate;

        [SetUp]
        public void SetUp()
        {
            _gate = new SystemGestureGate();
        }

        [Test]
        public void NoGesture_PassesPinchThrough()
        {
            Assert.AreEqual(0.9f, _gate.Step(false, 0.9f, ResetT), 1e-6f);
            Assert.AreEqual(0.2f, _gate.Step(false, 0.2f, ResetT), 1e-6f);
            Assert.AreEqual(0, _gate.Edge);
            Assert.IsFalse(_gate.Active);
        }

        [Test]
        public void DuringGesture_PinchReadsOpen()
        {
            Assert.AreEqual(0f, _gate.Step(true, 1f, ResetT));
            Assert.AreEqual(0f, _gate.Step(true, 1f, ResetT));
            Assert.IsTrue(_gate.Active);
        }

        [Test]
        public void PinchHeldThroughGestureEnd_StaysOpenUntilReleased()
        {
            _gate.Step(true, 1f, ResetT);

            Assert.AreEqual(0f, _gate.Step(false, 1f, ResetT));
            Assert.AreEqual(0f, _gate.Step(false, 0.7f, ResetT));
            Assert.AreEqual(0.4f, _gate.Step(false, 0.4f, ResetT), 1e-6f);
            Assert.AreEqual(0.9f, _gate.Step(false, 0.9f, ResetT), 1e-6f);
        }

        [Test]
        public void OpenHandAtGestureEnd_PassesAtOnce()
        {
            _gate.Step(true, 0f, ResetT);

            Assert.AreEqual(0.3f, _gate.Step(false, 0.3f, ResetT), 1e-6f);
        }

        [Test]
        public void Edge_ReportsStartAndEndOnce()
        {
            _gate.Step(false, 0f, ResetT);
            Assert.AreEqual(0, _gate.Edge);

            _gate.Step(true, 0f, ResetT);
            Assert.AreEqual(1, _gate.Edge);
            _gate.Step(true, 0f, ResetT);
            Assert.AreEqual(0, _gate.Edge);

            _gate.Step(false, 0f, ResetT);
            Assert.AreEqual(-1, _gate.Edge);
            _gate.Step(false, 0f, ResetT);
            Assert.AreEqual(0, _gate.Edge);
        }

        [Test]
        public void ThroughPinchTrigger_HeldPinchNeverFires()
        {
            var pinch = new PinchTrigger();
            bool fired = false;
            for (int i = 0; i < 10; i++)
                fired |= pinch.Step(true, _gate.Step(true, 1f, ResetT), 0.8f, ResetT, false, 0f, 0.02f).FireTriggered;
            for (int i = 0; i < 10; i++)
                fired |= pinch.Step(true, _gate.Step(false, 1f, ResetT), 0.8f, ResetT, false, 0f, 0.02f).FireTriggered;

            Assert.IsFalse(fired);

            pinch.Step(true, _gate.Step(false, 0f, ResetT), 0.8f, ResetT, false, 0f, 0.02f);
            Assert.IsTrue(pinch.Step(true, _gate.Step(false, 1f, ResetT), 0.8f, ResetT, false, 0f, 0.02f).FireTriggered);
        }

        [Test]
        public void Reset_ForgetsGestureAndPendingReopen()
        {
            _gate.Step(true, 1f, ResetT);
            _gate.Reset();

            Assert.AreEqual(1f, _gate.Step(false, 1f, ResetT));
            Assert.AreEqual(0, _gate.Edge);
            Assert.IsFalse(_gate.Active);
        }
    }
}
