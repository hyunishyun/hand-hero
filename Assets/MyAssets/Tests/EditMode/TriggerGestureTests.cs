using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // CURSOR aim fire: middle/ring/little grip drags the marker, the index finger
    // is the trigger. A full fist (grip and index closing together) must not fire.
    public class TriggerGestureTests
    {
        private const float Pull = 0.7f;
        private const float Rel = 0.45f;
        private const float GripOn = 0.7f;
        private const float GripOff = 0.45f;
        private const float Settle = 0.15f;
        private const float Dt = 0.02f;

        private TriggerGesture _trigger;

        [SetUp]
        public void SetUp()
        {
            _trigger = new TriggerGesture();
        }

        private TriggerState Step(float index, float grip, bool tracked = true)
        {
            return _trigger.Step(tracked, index, grip, Pull, Rel, GripOn, GripOff, Settle, Dt);
        }

        private void SettledGunGrip()
        {
            for (int i = 0; i < 10; i++) Step(0f, 1f); // 0.2 s > settle
        }

        [Test]
        public void Pull_FiresOnce_HeldWhilePulled()
        {
            Step(0f, 0f);
            TriggerState a = Step(1f, 0f);
            TriggerState b = Step(1f, 0f);
            TriggerState c = Step(1f, 0f);

            Assert.IsTrue(a.Fired);
            Assert.IsFalse(b.Fired);
            Assert.IsFalse(c.Fired);
            Assert.IsTrue(a.Held && b.Held && c.Held);
        }

        [Test]
        public void ReleaseBelow_RearmsNextPull()
        {
            Assert.IsTrue(Step(1f, 0f).Fired);
            Assert.IsFalse(Step(0.3f, 0f).Fired);
            Assert.IsTrue(Step(1f, 0f).Fired);
        }

        [Test]
        public void GunGripThenPull_Fires()
        {
            SettledGunGrip();
            Assert.IsTrue(Step(1f, 1f).Fired);
        }

        [Test]
        public void FullFist_GripAndIndexTogether_DoesNotFire()
        {
            Step(0f, 0f);
            for (int i = 0; i < 20; i++)
            {
                TriggerState s = Step(1f, 1f);
                Assert.IsFalse(s.Fired, $"step {i}");
                Assert.IsFalse(s.Held, $"step {i}");
            }

            Step(0.3f, 1f);
            Assert.IsTrue(Step(1f, 1f).Fired);
        }

        [Test]
        public void PullWithoutGrip_Fires()
        {
            Step(0f, 0f);
            Assert.IsTrue(Step(1f, 0f).Fired);
        }

        [Test]
        public void GripRelease_DuringHold_SettleBlocksNewEdgeOnly()
        {
            SettledGunGrip();
            Assert.IsTrue(Step(1f, 1f).Fired);

            for (int i = 0; i < 5; i++)
            {
                TriggerState s = Step(1f, 0f);
                Assert.IsTrue(s.Held, $"step {i}");
                Assert.IsFalse(s.Fired, $"step {i}");
            }
        }

        [Test]
        public void TrackingLoss_RequiresReopen()
        {
            Assert.IsTrue(Step(1f, 0f).Fired);
            Step(1f, 0f, tracked: false);

            TriggerState back = Step(1f, 0f);
            Assert.IsFalse(back.Fired);
            Assert.IsFalse(back.Held);

            Step(0.3f, 0f);
            Assert.IsTrue(Step(1f, 0f).Fired);
        }

        [Test]
        public void RequireReopen_HeldTriggerDoesNotFire()
        {
            _trigger.RequireReopen();
            TriggerState s = Step(1f, 0f);
            Assert.IsFalse(s.Fired);
            Assert.IsFalse(s.Held);

            Step(0.3f, 0f);
            Assert.IsTrue(Step(1f, 0f).Fired);
        }
    }
}
