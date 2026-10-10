using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // HandMenuPointer's point-and-pinch press, moved to Core for deep review DR-8.
    // Thresholds are the scene values (press 0.8, release 0.5).
    public class MenuPinchPressTests
    {
        private const float Press = 0.8f;
        private const float Release = 0.5f;

        private MenuPinchPress _press;
        private int _presses;

        [SetUp]
        public void SetUp()
        {
            _press = new MenuPinchPress();
            _press.RequireReopen(); // HandMenuPointer.OnEnable
            _presses = 0;
        }

        private bool Step(float pinch, bool tracked = true)
        {
            bool pressed = _press.Step(tracked, pinch, Press, Release);
            if (pressed) _presses++;
            return pressed;
        }

        private void Steps(float pinch, int frames, bool tracked = true)
        {
            for (int i = 0; i < frames; i++) Step(pinch, tracked);
        }

        [Test]
        public void OpenHand_ThenPinch_PressesOncePerPinch()
        {
            Steps(0.2f, 5);
            Assert.IsTrue(Step(0.9f), "the pinch presses");
            Steps(0.95f, 30);
            Assert.AreEqual(1, _presses, "holding does not press again");

            Steps(0.3f, 3);
            Assert.IsTrue(Step(0.9f), "a new pinch presses");
            Assert.AreEqual(2, _presses);
        }

        [Test]
        public void BetweenThresholds_DoesNotPressAgain()
        {
            Steps(0.2f, 3);
            Step(0.9f);
            Steps(0.6f, 5);
            Steps(0.85f, 5);
            Assert.AreEqual(1, _presses, "0.6 is above the release: still the same pinch");
        }

        // A panel shown under a held pinch (e.g. firing when the match ended).
        [Test]
        public void PanelShownUnderAHeldPinch_MustOpenFirst()
        {
            Steps(1f, 30);
            Assert.AreEqual(0, _presses);

            Steps(0.4f, 2);
            Assert.IsTrue(Step(0.9f));
        }

        // Deep review DR-8: the right hand dropped out for 1-8 frames during a pinch and
        // came back still pinched (the tracker snaps the strength to the raw value): the
        // ray landed on the same button and it was pressed again. RESET PROGRESS
        // confirmed itself; REROLL rerolled and paid twice.
        [TestCase(1)]
        [TestCase(8)]
        public void PinchHeldThroughADropout_DoesNotPressAgain(int lostFrames)
        {
            Steps(0.2f, 5);
            Assert.IsTrue(Step(0.95f));
            Steps(1f, 10);

            Steps(1f, lostFrames, tracked: false);
            Steps(1f, 40);
            Assert.AreEqual(1, _presses, "one physical pinch, one press");

            Steps(0.3f, 2);
            Assert.IsTrue(Step(0.9f), "after opening, the next pinch presses");
        }

        // DR-8: a hand that comes into view already pinched presses nothing.
        [Test]
        public void HandReturnsAlreadyPinched_DoesNotPress()
        {
            Steps(0.2f, 5);
            Steps(0f, 30, tracked: false);
            Steps(1f, 20);
            Assert.AreEqual(0, _presses);
        }

        // DR-8: a dropout with the hand open costs nothing.
        [Test]
        public void HandReturnsOpen_NextPinchPresses()
        {
            Steps(0.2f, 5);
            Steps(0.2f, 3, tracked: false);
            Assert.IsFalse(Step(0.3f));
            Assert.IsTrue(Step(0.9f));
        }

        // The pinch in progress ends at the loss (never later somewhere else).
        [Test]
        public void TrackingLoss_DropsThePinch()
        {
            Steps(0.2f, 2);
            Step(0.9f);
            Assert.IsTrue(_press.IsPinched);
            Step(0.9f, tracked: false);
            Assert.IsFalse(_press.IsPinched);
        }
    }
}
