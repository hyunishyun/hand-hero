using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // T7: left palm turned toward the face shows the wrist button; a pinch of that hand presses it.
    public class WristMenuGestureTests
    {
        private static readonly WristMenuParams P = WristMenuParams.Default;
        private const float Dt = 1f / 72f;

        private static bool Step(WristMenuGesture g, float facing, float fist = 0f, float pinch = 0f,
            bool tracked = true, float dt = Dt)
        {
            return g.Step(tracked, facing, fist, pinch, dt, P);
        }

        private static WristMenuGesture Shown()
        {
            var g = new WristMenuGesture();
            for (float t = 0f; t <= P.ShowDelay + 0.05f; t += Dt) Step(g, 1f);
            Assert.IsTrue(g.IsVisible);
            return g;
        }

        [Test]
        public void ShowsOnlyAfterTheDelay()
        {
            var g = new WristMenuGesture();
            Step(g, 1f);
            Assert.IsFalse(g.IsVisible);

            for (float t = 0f; t <= P.ShowDelay + 0.05f; t += Dt) Step(g, 1f);
            Assert.IsTrue(g.IsVisible);
        }

        [Test]
        public void ClosedFist_NeverShows()
        {
            var g = new WristMenuGesture();
            for (int i = 0; i < 200; i++) Step(g, 1f, fist: 0.8f);
            Assert.IsFalse(g.IsVisible);
        }

        [Test]
        public void FacingBetweenThresholds_DoesNotFlicker()
        {
            var g = Shown();
            float mid = (P.FacingShow + P.FacingHide) * 0.5f;
            for (int i = 0; i < 100; i++)
            {
                Step(g, i % 2 == 0 ? mid : P.FacingShow);
                Assert.IsTrue(g.IsVisible);
            }

            Step(g, P.FacingHide);
            Assert.IsFalse(g.IsVisible);

            for (int i = 0; i < 100; i++)
            {
                Step(g, mid);
                Assert.IsFalse(g.IsVisible);
            }
        }

        [Test]
        public void TurningAway_ResetsTheDelay()
        {
            var g = new WristMenuGesture();
            for (float t = 0f; t < P.ShowDelay * 0.8f; t += Dt) Step(g, 1f);
            Step(g, 0f);
            for (float t = 0f; t < P.ShowDelay * 0.8f; t += Dt) Step(g, 1f);
            Assert.IsFalse(g.IsVisible);
        }

        [Test]
        public void PinchWhileVisible_PressesOnce_UntilReleased()
        {
            var g = Shown();
            Assert.IsTrue(Step(g, 1f, pinch: 1f));
            for (int i = 0; i < 50; i++) Assert.IsFalse(Step(g, 1f, pinch: 1f));

            // Between the thresholds: still held, no re-arm.
            Assert.IsFalse(Step(g, 1f, pinch: (P.PinchPress + P.PinchRelease) * 0.5f));
            Assert.IsFalse(Step(g, 1f, pinch: 1f));

            Step(g, 1f, pinch: 0f);
            Assert.IsTrue(Step(g, 1f, pinch: 1f));
        }

        [Test]
        public void PinchHeldBeforeShowing_DoesNotPress()
        {
            var g = new WristMenuGesture();
            bool pressed = false;
            for (float t = 0f; t <= P.ShowDelay + 0.2f; t += Dt) pressed |= Step(g, 1f, pinch: 1f);
            Assert.IsTrue(g.IsVisible);
            Assert.IsFalse(pressed);
        }

        [Test]
        public void PinchWhileHidden_DoesNotPress()
        {
            var g = new WristMenuGesture();
            Assert.IsFalse(Step(g, 0f, pinch: 0f));
            Assert.IsFalse(Step(g, 0f, pinch: 1f));
            Assert.IsFalse(g.IsVisible);
        }

        [Test]
        public void TrackingLoss_HidesAndNeverPresses()
        {
            var g = Shown();
            Assert.IsFalse(Step(g, 1f, pinch: 1f, tracked: false));
            Assert.IsFalse(g.IsVisible);
        }
    }
}
