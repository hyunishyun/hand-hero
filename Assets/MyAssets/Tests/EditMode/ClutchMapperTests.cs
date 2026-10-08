using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Sampler (input side) + mapper (simulation side) together reproduce the
    // original HandPuppeteerController behaviour.
    public class ClutchMapperTests
    {
        private const float Grab = 0.7f;
        private const float Release = 0.45f;
        private const float Scale = 60f;

        private static readonly Vector3 CharacterPos = new Vector3(0f, 5f, 20f);

        private HandClutchSampler _sampler;
        private ClutchMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _sampler = new HandClutchSampler();
            _mapper = new ClutchMapper();
        }

        private ClutchResult Step(float fist, Vector3 hand, bool tracked = true)
        {
            bool held = _sampler.Step(tracked, fist, hand, Grab, Release, out Vector3 delta);
            return _mapper.Step(held, delta, CharacterPos, Scale);
        }

        [Test]
        public void OpenHand_DoesNotClutch()
        {
            ClutchResult r = Step(0.2f, Vector3.zero);
            Assert.IsFalse(r.Clutched);
            Assert.IsFalse(r.JustGrabbed);
        }

        [Test]
        public void Grab_StartsAtCharacterPosition_NoSnap()
        {
            ClutchResult r = Step(0.9f, new Vector3(0.3f, 1.1f, 0.4f));

            Assert.IsTrue(r.JustGrabbed);
            Assert.IsTrue(r.Clutched);
            Assert.AreEqual(CharacterPos, r.Target);
        }

        [Test]
        public void WhileClutched_TargetMovesByHandDeltaTimesScale()
        {
            var hand = new Vector3(0.3f, 1.1f, 0.4f);
            Step(0.9f, hand);

            ClutchResult r = Step(0.9f, hand + new Vector3(0.01f, 0f, 0f));
            Assert.That(Vector3.Distance(r.Target, CharacterPos + new Vector3(0.6f, 0f, 0f)), Is.LessThan(1e-4f));

            r = Step(0.9f, hand + new Vector3(0.01f, 0.02f, 0f));
            Assert.That(Vector3.Distance(r.Target, CharacterPos + new Vector3(0.6f, 1.2f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void ReleaseAndRegrab_HandRepositionDoesNotMoveTarget()
        {
            Step(0.9f, Vector3.zero);
            Step(0.9f, new Vector3(0.1f, 0f, 0f));

            ClutchResult released = Step(0.1f, new Vector3(0.1f, 0f, 0f));
            Assert.IsTrue(released.JustReleased);
            Assert.IsFalse(released.Clutched);

            // Hand moves back while open (repositioning), then grabs again.
            ClutchResult regrab = Step(0.9f, new Vector3(-0.2f, 0f, 0f));
            Assert.IsTrue(regrab.JustGrabbed);
            Assert.AreEqual(CharacterPos, regrab.Target);
        }

        [Test]
        public void FistBetweenThresholds_KeepsClutch()
        {
            Step(0.9f, Vector3.zero);
            ClutchResult r = Step(0.5f, Vector3.zero);
            Assert.IsTrue(r.Clutched);
            Assert.IsFalse(r.JustReleased);
        }

        [Test]
        public void TrackingLoss_ReleasesClutchOnce()
        {
            Step(0.9f, Vector3.zero);

            ClutchResult lost = Step(0.9f, Vector3.zero, tracked: false);
            Assert.IsTrue(lost.JustReleased);
            Assert.IsFalse(lost.Clutched);
            Assert.IsFalse(_mapper.IsClutched);
            Assert.IsFalse(_sampler.IsHeld);

            ClutchResult stillLost = Step(0.9f, Vector3.zero, tracked: false);
            Assert.IsFalse(stillLost.JustReleased);
        }

        [Test]
        public void TrackingReturnsElsewhere_DoesNotJumpTarget()
        {
            Step(0.9f, Vector3.zero);
            Step(0.9f, Vector3.zero, tracked: false);

            // Hand reappears 30 cm away, still a fist: fresh grab, no jump.
            ClutchResult back = Step(0.9f, new Vector3(0.3f, 0f, 0f));
            Assert.IsTrue(back.JustGrabbed);
            Assert.AreEqual(CharacterPos, back.Target);
        }

        [Test]
        public void Reset_HeldFistRegrabsFromNewPosition()
        {
            _mapper.Step(true, Vector3.zero, CharacterPos, Scale);
            _mapper.Step(true, new Vector3(0.1f, 0f, 0f), CharacterPos, Scale);

            _mapper.Reset();
            var respawn = new Vector3(0f, 2f, 30f);
            ClutchResult r = _mapper.Step(true, new Vector3(0.01f, 0f, 0f), respawn, Scale);

            Assert.IsTrue(r.JustGrabbed);
            Assert.IsFalse(r.JustReleased);
            Assert.That(Vector3.Distance(r.Target, respawn + new Vector3(0.6f, 0f, 0f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void Sampler_DeltaIsZeroWhenOpen()
        {
            _sampler.Step(true, 0.1f, Vector3.zero, Grab, Release, out _);
            bool held = _sampler.Step(true, 0.1f, Vector3.one, Grab, Release, out Vector3 delta);
            Assert.IsFalse(held);
            Assert.AreEqual(Vector3.zero, delta);
        }
    }
}
