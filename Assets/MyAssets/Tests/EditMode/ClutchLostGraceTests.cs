using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Deep review DR-10: a one- or two-frame dropout of the clutch hand opened the
    // clutch, and the regrab on the next frame started from the hero's position
    // (ADR 6, no snap), so the target's lead over the hero (about 0.5 x drag speed)
    // was lost for good. Round-4 device log: 16 left-hand dropouts under 0.25 s in
    // fights, many 2 frames. The sampler now rides out losses up to lostGraceTime.
    public class ClutchLostGraceTests
    {
        private const float Dt = 1f / 72f;
        private const float Grab = 0.7f;
        private const float Release = 0.45f;
        private const float Scale = 60f;
        private const float Grace = 0.25f;

        private static readonly Vector3 CharacterPos = new Vector3(0f, 5f, 20f);

        // FlyingCharacter / scene flight values.
        private static readonly FlightParams Flight = new FlightParams
        {
            Stiffness = 10f,
            Damping = 5f,
            MaxSpeed = 25f,
            GlideDrag = 0.8f,
        };

        private HandClutchSampler _sampler;
        private ClutchMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _sampler = new HandClutchSampler();
            _mapper = new ClutchMapper();
        }

        private ClutchResult Step(float fist, Vector3 hand, bool tracked = true, float grace = Grace)
        {
            bool held = _sampler.Step(tracked, fist, hand, Grab, Release, grace, Dt, out Vector3 delta);
            return _mapper.Step(held, delta, CharacterPos, Scale);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ShortDropout_FistStillClosed_KeepsTheClutchAndTheTarget(int lostFrames)
        {
            var hand = new Vector3(0.3f, 1.1f, 0.4f);
            Step(0.9f, hand);
            hand += new Vector3(0.05f, 0f, 0f);
            Vector3 target = Step(0.9f, hand).Target;

            for (int i = 0; i < lostFrames; i++)
            {
                ClutchResult lost = Step(0.9f, hand, tracked: false);
                Assert.IsTrue(lost.Clutched, $"lost frame {i}: still held");
                Assert.IsFalse(lost.JustReleased, $"lost frame {i}");
                Assert.That(Vector3.Distance(lost.Target, target), Is.LessThan(1e-4f), $"lost frame {i}: no drag");
            }

            // Back 1 cm further along: re-anchored there, no jump and no regrab.
            hand += new Vector3(0.01f, 0f, 0f);
            ClutchResult back = Step(0.9f, hand);
            Assert.IsTrue(back.Clutched);
            Assert.IsFalse(back.JustGrabbed, "no regrab from the hero's position");
            Assert.IsFalse(back.JustReleased);
            Assert.That(Vector3.Distance(back.Target, target), Is.LessThan(1e-4f), "no jump on the reacquire frame");

            ClutchResult next = Step(0.9f, hand + new Vector3(0.01f, 0f, 0f));
            Assert.That(Vector3.Distance(next.Target, target + new Vector3(0.6f, 0f, 0f)), Is.LessThan(1e-3f),
                "the drag goes on from the reacquired palm");
        }

        [Test]
        public void ShortDropout_HandBackElsewhere_DoesNotJumpTheTarget()
        {
            Step(0.9f, Vector3.zero);
            Vector3 target = Step(0.9f, new Vector3(0.02f, 0f, 0f)).Target;
            Step(0.9f, Vector3.zero, tracked: false);

            ClutchResult back = Step(0.9f, new Vector3(0.3f, 0f, 0f));
            Assert.IsFalse(back.JustGrabbed);
            Assert.That(Vector3.Distance(back.Target, target), Is.LessThan(1e-4f));
        }

        [Test]
        public void ShortDropout_FistOpenOnReturn_Releases()
        {
            Step(0.9f, Vector3.zero);
            Step(0.9f, Vector3.zero, tracked: false);

            ClutchResult back = Step(0.2f, Vector3.zero);
            Assert.IsTrue(back.JustReleased);
            Assert.IsFalse(back.Clutched);
        }

        [Test]
        public void LossLongerThanTheGrace_ReleasesOnce_ThenRegrabsFromTheHero()
        {
            Step(0.9f, Vector3.zero);
            Step(0.9f, new Vector3(0.05f, 0f, 0f));

            int releases = 0;
            int lostSteps = Mathf.CeilToInt(0.4f / Dt);
            for (int i = 0; i < lostSteps; i++)
            {
                ClutchResult lost = Step(0.9f, Vector3.zero, tracked: false);
                if (lost.JustReleased)
                {
                    releases++;
                    float lostFor = (i + 1) * Dt;
                    Assert.GreaterOrEqual(lostFor, Grace - 0.5f * Dt, "not before the grace runs out");
                    Assert.LessOrEqual(lostFor, Grace + 1.5f * Dt, "on the first step past the grace");
                }
            }
            Assert.AreEqual(1, releases);
            Assert.IsFalse(_sampler.IsHeld);

            ClutchResult back = Step(0.9f, new Vector3(0.3f, 0f, 0f));
            Assert.IsTrue(back.JustGrabbed, "a long loss is a fresh grab (glide, no teleport)");
            Assert.AreEqual(CharacterPos, back.Target);
        }

        [Test]
        public void GraceRestartsAfterEachReacquire()
        {
            Step(0.9f, Vector3.zero);
            for (int round = 0; round < 3; round++)
            {
                for (int i = 0; i < 15; i++) // 0.21 s each time
                    Assert.IsTrue(Step(0.9f, Vector3.zero, tracked: false).Clutched, $"round {round}, lost {i}");
                Assert.IsTrue(Step(0.9f, Vector3.zero).Clutched, $"round {round}, back");
            }
        }

        [Test]
        public void OpenHand_Lost_StaysOpen()
        {
            Step(0.1f, Vector3.zero);
            ClutchResult lost = Step(0.1f, Vector3.zero, tracked: false);
            Assert.IsFalse(lost.Clutched);
            Assert.IsFalse(_sampler.IsHeld);

            ClutchResult grab = Step(0.9f, Vector3.zero);
            Assert.IsTrue(grab.JustGrabbed);
        }

        [Test]
        public void Reset_DuringTheGrace_OpensTheClutch()
        {
            Step(0.9f, Vector3.zero);
            Step(0.9f, Vector3.zero, tracked: false);
            _sampler.Reset();
            Assert.IsFalse(_sampler.IsHeld);
        }

        [Test]
        public void ZeroGrace_IsTheOldRule()
        {
            Step(0.9f, Vector3.zero, grace: 0f);
            ClutchResult lost = Step(0.9f, Vector3.zero, tracked: false, grace: 0f);
            Assert.IsTrue(lost.JustReleased);
            ClutchResult back = Step(0.9f, Vector3.zero, grace: 0f);
            Assert.IsTrue(back.JustGrabbed);
        }

        // The finding's simulation: drag at 10 m/s for 1 s, a 2-frame dropout half way.
        // Before: the hero ended near 6 m instead of 10 m.
        [Test]
        public void TwoFrameDropoutMidDrag_HeroKeepsItsLead()
        {
            float clean = DragDistance(-1);
            float dropped = DragDistance(36);
            Assert.Greater(clean, 9f, "sanity: the clean drag covers about 10 m");
            Assert.Less(clean - dropped, 0.5f, "only the 2 lost frames of hand motion are missing");
        }

        // Hero x after a 1 s drag at 10 m/s (hand 1/6 m/s at scale 60), then 1 s of follow-through.
        private static float DragDistance(int dropoutAt)
        {
            var sampler = new HandClutchSampler();
            var mapper = new ClutchMapper();
            var hero = new FlightState();
            bool hasTarget = false;
            Vector3 target = Vector3.zero;
            float handSpeed = 10f / Scale;
            int dragSteps = 72;
            for (int i = 0; i < dragSteps + 72; i++)
            {
                var palm = new Vector3(handSpeed * Dt * Mathf.Min(i, dragSteps), 0f, 0f);
                bool tracked = dropoutAt < 0 || i < dropoutAt || i >= dropoutAt + 2;
                bool held = sampler.Step(tracked, 0.9f, palm, Grab, Release, Grace, Dt, out Vector3 delta);
                ClutchResult r = mapper.Step(held, delta, hero.Position, Scale);
                if (r.Clutched)
                {
                    hasTarget = true;
                    target = r.Target;
                }
                if (r.JustReleased) hasTarget = false;
                hero = SpringFlightModel.Step(hero, hasTarget, target, Flight, default, Dt);
            }
            return hero.Position.x;
        }
    }
}
