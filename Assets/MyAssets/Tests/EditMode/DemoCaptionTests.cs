using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Round 5, T4 (D6): gesture captions beside the hands in demo mode - which
    // caption, when it fades, where it goes and which hand pushed.
    public class DemoCaptionTests
    {
        private static readonly DemoCaptionParams P = DemoCaptionParams.Default;
        private static readonly DemoCaptionLayoutParams L = DemoCaptionLayoutParams.Default;
        private const float Dt = 1f / 72f;

        private static DemoCaptionModel Synced(DemoGestureFrame baseline = default)
        {
            var m = new DemoCaptionModel();
            m.Step(baseline, Dt, P);
            Assert.AreEqual(DemoCaption.None, m.Clutch);
            Assert.AreEqual(DemoCaption.None, m.Aim);
            return m;
        }

        private static void Hold(DemoCaptionModel m, DemoGestureFrame frame, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) m.Step(frame, Dt, P);
        }

        [Test]
        public void Texts_MatchThePlan_CursorSaysTrigger()
        {
            Assert.AreEqual("GRAB", DemoCaptionText.Text(DemoCaption.Grab, false));
            Assert.AreEqual("PINCH = FIRE", DemoCaptionText.Text(DemoCaption.Fire, false));
            Assert.AreEqual("HOLD = CHARGE", DemoCaptionText.Text(DemoCaption.Charge, false));
            Assert.AreEqual("PUSH = SHOCKWAVE", DemoCaptionText.Text(DemoCaption.Shockwave, false));
            Assert.AreEqual("", DemoCaptionText.Text(DemoCaption.None, false));

            Assert.AreEqual("TRIGGER = FIRE", DemoCaptionText.Text(DemoCaption.Fire, true));
            Assert.AreEqual("GRAB", DemoCaptionText.Text(DemoCaption.Grab, true));
            Assert.AreEqual("HOLD = CHARGE", DemoCaptionText.Text(DemoCaption.Charge, true));
        }

        [Test]
        public void Grab_ShowsBesideTheClutchHand_OncePerClose()
        {
            var m = Synced();
            var fist = new DemoGestureFrame { ClutchHeld = true };
            m.Step(fist, Dt, P);
            Assert.AreEqual(DemoCaption.Grab, m.Clutch);
            Assert.AreEqual(DemoCaption.None, m.Aim);
            Assert.AreEqual(1f, m.ClutchAlpha);

            // Holding the fist never shows it again.
            Hold(m, fist, P.ShowTime + P.FadeTime + 0.1f);
            Assert.AreEqual(DemoCaption.None, m.Clutch);
            Assert.AreEqual(0f, m.ClutchAlpha);

            m.Step(default, Dt, P);
            m.Step(fist, Dt, P);
            Assert.AreEqual(DemoCaption.Grab, m.Clutch, "a new close shows it again");
        }

        [Test]
        public void NormalShot_ShowsFire_BesideTheAimHand()
        {
            var m = Synced();
            m.Step(new DemoGestureFrame { ShotsFired = 1 }, Dt, P);
            Assert.AreEqual(DemoCaption.Fire, m.Aim);
            Assert.AreEqual(DemoCaption.None, m.Clutch);
        }

        [Test]
        public void ChargedShot_ShowsCharge_NotFire()
        {
            var m = Synced();
            m.Step(new DemoGestureFrame { ShotsFired = 1, ChargeShotsFired = 1 }, Dt, P);
            Assert.AreEqual(DemoCaption.Charge, m.Aim);
        }

        [Test]
        public void ChargeStart_ShowsCharge_AndReplacesTheShotCaption()
        {
            var m = Synced();
            var shot = new DemoGestureFrame { ShotsFired = 1 };
            m.Step(shot, Dt, P);
            Assert.AreEqual(DemoCaption.Fire, m.Aim);

            // Still holding the pinch: the charge starts after HoldDelay.
            Hold(m, shot, 0.25f);
            var charging = new DemoGestureFrame { ShotsFired = 1, Charging = true };
            m.Step(charging, Dt, P);
            Assert.AreEqual(DemoCaption.Charge, m.Aim);

            // Charging on is a level, not a new edge: the caption still fades.
            Hold(m, charging, P.ShowTime + P.FadeTime + 0.1f);
            Assert.AreEqual(DemoCaption.None, m.Aim);
        }

        [Test]
        public void Shockwave_ShowsBesideThePushingHand()
        {
            var m = Synced();
            m.Step(new DemoGestureFrame { Shockwave = true, ShockwaveByClutchHand = true }, Dt, P);
            Assert.AreEqual(DemoCaption.Shockwave, m.Clutch);
            Assert.AreEqual(DemoCaption.None, m.Aim);

            m = Synced();
            m.Step(new DemoGestureFrame { Shockwave = true, ShockwaveByClutchHand = false }, Dt, P);
            Assert.AreEqual(DemoCaption.Shockwave, m.Aim);
            Assert.AreEqual(DemoCaption.None, m.Clutch);
        }

        [Test]
        public void SameFrame_ShockwaveBeatsGrabAndFire_ChargeBeatsFire()
        {
            var m = Synced();
            m.Step(new DemoGestureFrame { ClutchHeld = true, Shockwave = true, ShockwaveByClutchHand = true }, Dt, P);
            Assert.AreEqual(DemoCaption.Shockwave, m.Clutch);

            m = Synced();
            m.Step(new DemoGestureFrame { ShotsFired = 1, Shockwave = true }, Dt, P);
            Assert.AreEqual(DemoCaption.Shockwave, m.Aim);

            m = Synced();
            m.Step(new DemoGestureFrame { ShotsFired = 1, Charging = true }, Dt, P);
            Assert.AreEqual(DemoCaption.Charge, m.Aim);
        }

        [Test]
        public void Caption_ShowsFull_ThenFades_ThenGoes()
        {
            var m = Synced();
            var shot = new DemoGestureFrame { ShotsFired = 1 };
            m.Step(shot, Dt, P);

            m.Step(shot, P.ShowTime * 0.9f, P);
            Assert.AreEqual(1f, m.AimAlpha);

            m.Step(shot, P.ShowTime * 0.1f + P.FadeTime * 0.5f, P);
            Assert.AreEqual(DemoCaption.Fire, m.Aim);
            Assert.AreEqual(0.5f, m.AimAlpha, 0.01f);

            m.Step(shot, P.FadeTime * 0.6f, P);
            Assert.AreEqual(DemoCaption.None, m.Aim);
            Assert.AreEqual(0f, m.AimAlpha);
        }

        [Test]
        public void NewShot_RestartsTheCaptionTime()
        {
            var m = Synced();
            m.Step(new DemoGestureFrame { ShotsFired = 1 }, Dt, P);
            m.Step(new DemoGestureFrame { ShotsFired = 1 }, P.ShowTime * 0.9f, P);
            m.Step(new DemoGestureFrame { ShotsFired = 2 }, Dt, P);
            m.Step(new DemoGestureFrame { ShotsFired = 2 }, P.ShowTime * 0.9f, P);
            Assert.AreEqual(DemoCaption.Fire, m.Aim);
            Assert.AreEqual(1f, m.AimAlpha, "rapid fire keeps the caption up");
        }

        [Test]
        public void ShotCountersReset_ReBaseWithoutACaption()
        {
            var m = Synced(new DemoGestureFrame { ShotsFired = 5, ChargeShotsFired = 2 });
            m.Step(new DemoGestureFrame { ShotsFired = 0, ChargeShotsFired = 0 }, Dt, P);
            Assert.AreEqual(DemoCaption.None, m.Aim);

            m.Step(new DemoGestureFrame { ShotsFired = 1, ChargeShotsFired = 0 }, Dt, P);
            Assert.AreEqual(DemoCaption.Fire, m.Aim);
        }

        [Test]
        public void FirstStep_OnlyTakesTheBaseline()
        {
            var m = new DemoCaptionModel();
            var busy = new DemoGestureFrame { ClutchHeld = true, Charging = true, ShotsFired = 7, ChargeShotsFired = 3 };
            m.Step(busy, Dt, P);
            Assert.AreEqual(DemoCaption.None, m.Clutch);
            Assert.AreEqual(DemoCaption.None, m.Aim);

            m.Step(busy, Dt, P);
            Assert.AreEqual(DemoCaption.None, m.Clutch, "a fist held since the demo started is no new GRAB");
            Assert.AreEqual(DemoCaption.None, m.Aim);
        }

        [Test]
        public void Reset_ClearsCaptions_AndTakesANewBaseline()
        {
            var m = Synced();
            var fist = new DemoGestureFrame { ClutchHeld = true, ShotsFired = 1 };
            m.Step(fist, Dt, P);
            Assert.AreEqual(DemoCaption.Grab, m.Clutch);

            m.Reset();
            Assert.AreEqual(DemoCaption.None, m.Clutch);
            Assert.AreEqual(DemoCaption.None, m.Aim);
            Assert.AreEqual(0f, m.ClutchAlpha);

            m.Step(fist, Dt, P);
            m.Step(fist, Dt, P);
            Assert.AreEqual(DemoCaption.None, m.Clutch);
            Assert.AreEqual(DemoCaption.None, m.Aim);
        }

        [Test]
        public void Alpha_NoCaptionOrNoFade_IsZeroAfterTheShowTime()
        {
            Assert.AreEqual(0f, DemoCaptionModel.Alpha(DemoCaption.None, 0f, P));
            var noFade = new DemoCaptionParams { ShowTime = 0.5f, FadeTime = 0f };
            Assert.AreEqual(1f, DemoCaptionModel.Alpha(DemoCaption.Grab, 0.5f, noFade));
            Assert.AreEqual(0f, DemoCaptionModel.Alpha(DemoCaption.Grab, 0.51f, noFade));
        }

        // ---- Placement ----

        private static readonly Vector3 Head = new Vector3(0f, 1.2f, 0f);
        private static readonly Vector3 LeftPalm = new Vector3(-0.2f, 0.9f, 0.4f);
        private static readonly Vector3 RightPalm = new Vector3(0.2f, 0.9f, 0.4f);

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.Less(Vector3.Distance(expected, actual), 1e-4f, $"expected {expected}, got {actual}");
        }

        [Test]
        public void Place_OuterSideOfEachHand_AboveThePalm()
        {
            Vector3 left = DemoCaptionLayout.Place(Head, LeftPalm, Vector3.right, true, false, Vector3.zero, 1f, L);
            AssertNear(LeftPalm + new Vector3(-L.SideOffset, L.UpOffset, 0f), left);

            Vector3 right = DemoCaptionLayout.Place(Head, RightPalm, Vector3.right, false, false, Vector3.zero, 1f, L);
            AssertNear(RightPalm + new Vector3(L.SideOffset, L.UpOffset, 0f), right);
        }

        [Test]
        public void Place_ScalesWithTheTabletopView()
        {
            const float scale = 35f;
            Vector3 palm = RightPalm * scale;
            Vector3 at = DemoCaptionLayout.Place(Head * scale, palm, Vector3.right, false, false, Vector3.zero, scale, L);
            AssertNear(palm + new Vector3(L.SideOffset, L.UpOffset, 0f) * scale, at);
        }

        [Test]
        public void Place_TiltedHead_KeepsCaptionsLevel()
        {
            Vector3 tilted = new Vector3(1f, 1f, 0f).normalized;
            Vector3 at = DemoCaptionLayout.Place(Head, RightPalm, tilted, false, false, Vector3.zero, 1f, L);
            Assert.AreEqual(RightPalm.y + L.UpOffset, at.y, 1e-4f);
            Assert.Greater(at.x, RightPalm.x);
        }

        [Test]
        public void Place_StepsDownOffTheHero()
        {
            Vector3 normal = DemoCaptionLayout.Place(Head, RightPalm, Vector3.right, false, false, Vector3.zero, 1f, L);
            // The hero far behind the middle of the caption, as seen from the head.
            Vector3 middle = normal + Vector3.right * (L.LabelWidth * 0.5f);
            Vector3 hero = Head + (middle - Head) * 30f;
            Assert.IsTrue(DemoCaptionLayout.CoversHero(Head, normal, Vector3.right, hero, 1f, L));

            Vector3 at = DemoCaptionLayout.Place(Head, RightPalm, Vector3.right, false, true, hero, 1f, L);
            Assert.IsFalse(DemoCaptionLayout.CoversHero(Head, at, Vector3.right, hero, 1f, L));
            Assert.Less(at.y, normal.y);
            Assert.AreEqual(normal.x, at.x, 1e-4f, "stays beside the same hand");
        }

        [Test]
        public void Place_HeroElsewhere_LeavesTheCaptionBesideThePalm()
        {
            Vector3 normal = DemoCaptionLayout.Place(Head, LeftPalm, Vector3.right, true, false, Vector3.zero, 1f, L);
            Vector3 at = DemoCaptionLayout.Place(Head, LeftPalm, Vector3.right, true, true, new Vector3(15f, 8f, 25f),
                1f, L);
            AssertNear(normal, at);
        }

        [Test]
        public void Place_GivesUpAfterMaxSteps()
        {
            DemoCaptionLayoutParams none = L;
            none.MaxSteps = 0;
            Vector3 normal = DemoCaptionLayout.Place(Head, RightPalm, Vector3.right, false, false, Vector3.zero, 1f, none);
            Vector3 hero = Head + (normal - Head) * 30f;
            AssertNear(normal, DemoCaptionLayout.Place(Head, RightPalm, Vector3.right, false, true, hero, 1f, none));
        }

        [Test]
        public void CoversHero_HeroAtTheHead_IsFalse()
        {
            Assert.IsFalse(DemoCaptionLayout.CoversHero(Head, RightPalm, Vector3.right, Head, 1f, L));
        }

        // ---- Which hand pushed ----

        [Test]
        public void PushHand_OnlyTrackedHand_ElseTheFasterOne()
        {
            Assert.IsTrue(DemoPushHand.IsLeft(true, 0.2f, false, 3f, false));
            Assert.IsFalse(DemoPushHand.IsLeft(false, 3f, true, 0.2f, true));
            Assert.IsTrue(DemoPushHand.IsLeft(true, 2.5f, true, 0.4f, false));
            Assert.IsFalse(DemoPushHand.IsLeft(true, 0.4f, true, 2.5f, true));
        }

        [Test]
        public void PushHand_TieOrNoHand_UsesTheFallback()
        {
            Assert.IsTrue(DemoPushHand.IsLeft(true, 1f, true, 1f, true));
            Assert.IsFalse(DemoPushHand.IsLeft(true, 1f, true, 1f, false));
            Assert.IsTrue(DemoPushHand.IsLeft(false, 0f, false, 0f, true));
            Assert.IsFalse(DemoPushHand.IsLeft(false, 0f, false, 0f, false));
        }
    }
}
