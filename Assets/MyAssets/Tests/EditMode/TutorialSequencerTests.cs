using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // T8: grab -> drag into the ring -> let go and glide -> point -> pinch the target -> dodge.
    public class TutorialSequencerTests
    {
        private static readonly TutorialParams P = TutorialParams.Default;
        private const float Dt = 1f / 72f;

        private static void Run(TutorialSequencer s, float seconds, TutorialObservation o)
        {
            int steps = Mathf.CeilToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) s.Tick(Dt, o);
        }

        // Finishes the current step and its short "nice" pause.
        private static void Pass(TutorialSequencer s, TutorialObservation o, float seconds)
        {
            TutorialStep before = s.Step;
            Run(s, seconds, o);
            Assert.IsTrue(s.IsCelebrating, $"{before} should have succeeded");
            Run(s, P.CelebrateTime + 0.05f, default);
            Assert.AreNotEqual(before, s.Step);
        }

        private static TutorialSequencer AtStep(TutorialStep target)
        {
            var s = new TutorialSequencer(P);
            if (target == TutorialStep.Grab) return s;
            Pass(s, new TutorialObservation { ClutchHeld = true }, P.GrabHoldTime + 0.05f);
            if (target == TutorialStep.DragToRing) return s;
            Pass(s, new TutorialObservation { ClutchHeld = true, HeroInRing = true }, Dt * 2f);
            if (target == TutorialStep.Glide) return s;
            Pass(s, new TutorialObservation { HeroMoving = true }, P.GlideTime + 0.05f);
            if (target == TutorialStep.Aim) return s;
            Pass(s, new TutorialObservation { AimOnTarget = true }, P.AimHoldTime + 0.05f);
            if (target == TutorialStep.Shoot) return s;
            Pass(s, new TutorialObservation { TargetHit = true }, Dt);
            Assert.AreEqual(TutorialStep.Dodge, s.Step);
            return s;
        }

        [Test]
        public void StartsAtGrab()
        {
            var s = new TutorialSequencer(P);
            Assert.AreEqual(TutorialStep.Grab, s.Step);
            Assert.IsFalse(s.IsDone);
        }

        [Test]
        public void Grab_NeedsAHeldFist_NotAFlicker()
        {
            var s = new TutorialSequencer(P);
            for (int i = 0; i < 200; i++)
                s.Tick(Dt, new TutorialObservation { ClutchHeld = i % 2 == 0 });
            Assert.AreEqual(TutorialStep.Grab, s.Step);
            Assert.IsFalse(s.IsCelebrating);

            Run(s, P.GrabHoldTime + 0.05f, new TutorialObservation { ClutchHeld = true });
            Assert.IsTrue(s.IsCelebrating);
        }

        [Test]
        public void HoldSteps_ReportProgress()
        {
            var s = new TutorialSequencer(P);
            Run(s, P.GrabHoldTime * 0.5f, new TutorialObservation { ClutchHeld = true });
            Assert.AreEqual(0.5f, s.StepProgress, 0.1f);
        }

        [Test]
        public void DragToRing_CountsOnlyWhileHolding()
        {
            var s = AtStep(TutorialStep.DragToRing);
            Run(s, 1f, new TutorialObservation { ClutchHeld = false, HeroInRing = true });
            Assert.IsFalse(s.IsCelebrating);
            s.Tick(Dt, new TutorialObservation { ClutchHeld = true, HeroInRing = true });
            Assert.IsTrue(s.IsCelebrating);
        }

        [Test]
        public void Glide_NeedsReleasedAndMoving()
        {
            var s = AtStep(TutorialStep.Glide);
            Run(s, 2f, new TutorialObservation { ClutchHeld = true, HeroMoving = true });
            Assert.IsFalse(s.IsCelebrating);
            Run(s, 2f, new TutorialObservation { ClutchHeld = false, HeroMoving = false });
            Assert.IsFalse(s.IsCelebrating);
            Run(s, P.GlideTime + 0.05f, new TutorialObservation { HeroMoving = true });
            Assert.IsTrue(s.IsCelebrating);
        }

        [Test]
        public void Aim_ResetsWhenTheRayLeavesTheTarget()
        {
            var s = AtStep(TutorialStep.Aim);
            Run(s, P.AimHoldTime * 0.8f, new TutorialObservation { AimOnTarget = true });
            s.Tick(Dt, default);
            Run(s, P.AimHoldTime * 0.8f, new TutorialObservation { AimOnTarget = true });
            Assert.IsFalse(s.IsCelebrating);
        }

        [Test]
        public void Shoot_SucceedsOnAHit()
        {
            var s = AtStep(TutorialStep.Shoot);
            Run(s, 3f, default);
            Assert.IsFalse(s.IsCelebrating);
            s.Tick(Dt, new TutorialObservation { TargetHit = true });
            Assert.IsTrue(s.IsCelebrating);
        }

        [Test]
        public void Dodge_TelegraphLocksOnTheHero_ThenFires()
        {
            var s = AtStep(TutorialStep.Dodge);
            var hero = new Vector3(1f, 2f, 3f);
            Assert.IsFalse(s.IsTelegraphing);

            Run(s, P.DodgeWaitTime + Dt, new TutorialObservation { HeroPosition = hero });
            Assert.IsTrue(s.IsTelegraphing);
            Assert.AreEqual(hero, s.LockedPoint);
        }

        [Test]
        public void Dodge_StayingPut_IsHit_AndRetries()
        {
            var s = AtStep(TutorialStep.Dodge);
            var o = new TutorialObservation { HeroPosition = Vector3.zero };
            int hits = 0;
            s.DodgeShotFired += hit => { if (hit) hits++; };

            Run(s, P.DodgeWaitTime + P.DodgeTelegraphTime + 0.1f, o);

            Assert.AreEqual(1, hits);
            Assert.AreEqual(TutorialStep.Dodge, s.Step);
            Assert.IsFalse(s.IsCelebrating);
            Assert.IsFalse(s.IsTelegraphing);
        }

        [Test]
        public void Dodge_MovingAwayBeforeTheShot_Succeeds()
        {
            var s = AtStep(TutorialStep.Dodge);
            bool? result = null;
            s.DodgeShotFired += hit => result = hit;

            Run(s, P.DodgeWaitTime + Dt, new TutorialObservation { HeroPosition = Vector3.zero });
            Assert.IsTrue(s.IsTelegraphing);
            Run(s, P.DodgeTelegraphTime, new TutorialObservation
                { HeroPosition = new Vector3(P.DodgeHitRadius + 0.5f, 0f, 0f) });

            Assert.AreEqual(false, result);
            Assert.IsTrue(s.IsCelebrating);
            Run(s, P.CelebrateTime + 0.05f, default);
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void Skip_FinishesAtOnce()
        {
            var s = AtStep(TutorialStep.Aim);
            s.Skip();
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void StepChanged_FiresForEveryStep()
        {
            var s = new TutorialSequencer(P);
            var seen = new System.Collections.Generic.List<TutorialStep>();
            s.StepChanged += seen.Add;

            Pass(s, new TutorialObservation { ClutchHeld = true }, P.GrabHoldTime + 0.05f);
            s.Skip();

            CollectionAssert.AreEqual(new[] { TutorialStep.DragToRing, TutorialStep.Done }, seen);
        }

        [Test]
        public void HappyPath_FitsInThirtySeconds()
        {
            // Minimum time with instant player reactions: holds + one dodge cycle + celebrations.
            float minimum = P.GrabHoldTime + P.GlideTime + P.AimHoldTime + P.DodgeWaitTime + P.DodgeTelegraphTime
                + 6 * P.CelebrateTime;
            Assert.Less(minimum, 15f, "leaves at least half of 30 s for the player to actually move");
        }
    }
}
