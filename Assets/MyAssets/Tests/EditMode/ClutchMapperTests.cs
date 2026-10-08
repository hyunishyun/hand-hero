using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    public class ClutchMapperTests
    {
        private const float Grab = 0.7f;
        private const float Release = 0.45f;
        private const float Scale = 60f;

        private static readonly Vector3 CharacterPos = new Vector3(0f, 5f, 20f);

        private static ClutchResult Step(ClutchMapper m, float fist, Vector3 hand, bool tracked = true)
            => m.Step(tracked, fist, hand, CharacterPos, Grab, Release, Scale);

        [Test]
        public void OpenHand_DoesNotClutch()
        {
            var m = new ClutchMapper();
            ClutchResult r = Step(m, 0.2f, Vector3.zero);
            Assert.IsFalse(r.Clutched);
            Assert.IsFalse(r.JustGrabbed);
        }

        [Test]
        public void Grab_StartsAtCharacterPosition_NoSnap()
        {
            var m = new ClutchMapper();
            ClutchResult r = Step(m, 0.9f, new Vector3(0.3f, 1.1f, 0.4f));

            Assert.IsTrue(r.JustGrabbed);
            Assert.IsTrue(r.Clutched);
            Assert.AreEqual(CharacterPos, r.Target);
        }

        [Test]
        public void WhileClutched_TargetMovesByHandDeltaTimesScale()
        {
            var m = new ClutchMapper();
            var hand = new Vector3(0.3f, 1.1f, 0.4f);
            Step(m, 0.9f, hand);

            ClutchResult r = Step(m, 0.9f, hand + new Vector3(0.01f, 0f, 0f));
            Assert.That(Vector3.Distance(r.Target, CharacterPos + new Vector3(0.6f, 0f, 0f)), Is.LessThan(1e-4f));

            r = Step(m, 0.9f, hand + new Vector3(0.01f, 0.02f, 0f));
            Assert.That(Vector3.Distance(r.Target, CharacterPos + new Vector3(0.6f, 1.2f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void ReleaseAndRegrab_HandRepositionDoesNotMoveTarget()
        {
            var m = new ClutchMapper();
            Step(m, 0.9f, Vector3.zero);
            Step(m, 0.9f, new Vector3(0.1f, 0f, 0f));

            ClutchResult released = Step(m, 0.1f, new Vector3(0.1f, 0f, 0f));
            Assert.IsTrue(released.JustReleased);
            Assert.IsFalse(released.Clutched);

            // Hand moves back while open (repositioning), then grabs again.
            ClutchResult regrab = Step(m, 0.9f, new Vector3(-0.2f, 0f, 0f));
            Assert.IsTrue(regrab.JustGrabbed);
            Assert.AreEqual(CharacterPos, regrab.Target);
        }

        [Test]
        public void FistBetweenThresholds_KeepsClutch()
        {
            var m = new ClutchMapper();
            Step(m, 0.9f, Vector3.zero);
            ClutchResult r = Step(m, 0.5f, Vector3.zero);
            Assert.IsTrue(r.Clutched);
            Assert.IsFalse(r.JustReleased);
        }

        [Test]
        public void TrackingLoss_ReleasesClutchOnce()
        {
            var m = new ClutchMapper();
            Step(m, 0.9f, Vector3.zero);

            ClutchResult lost = Step(m, 0.9f, Vector3.zero, tracked: false);
            Assert.IsTrue(lost.JustReleased);
            Assert.IsFalse(lost.Clutched);
            Assert.IsFalse(m.IsClutched);

            ClutchResult stillLost = Step(m, 0.9f, Vector3.zero, tracked: false);
            Assert.IsFalse(stillLost.JustReleased);
        }
    }
}
