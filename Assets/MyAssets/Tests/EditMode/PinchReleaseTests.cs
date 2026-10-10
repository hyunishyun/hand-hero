using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Round 5 (D1, D2): the ASSIST pinch release. Round-4 device logs: ASSIST pinches
    // were held for seconds (median up to 7.9 s, max 22 s; CURSOR 0.125 s). A pointing
    // hand rests the thumb about 2.8 cm from the index, closer than the absolute reset
    // (strength 0.6 = 3.3 cm), so the old rule never saw the release. Strengths below
    // use HandGestureTracker's mapping: InverseLerp(6 cm, 1.5 cm, thumb-index distance).
    public class PinchReleaseTests
    {
        private const float Dt = 1f / 72f;
        private const float RestCm = 2.8f;  // pointing thumb after a pinch (device)
        private const float PinchCm = 1f;   // a closed pinch

        private static readonly PinchReleaseParams P = PinchReleaseParams.Default;

        private PinchTrigger _pinch;

        [SetUp]
        public void SetUp()
        {
            _pinch = new PinchTrigger();
        }

        // HandGestureTracker's unsmoothed strength for a thumb-index distance in cm.
        private static float Cm(float cm) => Mathf.Clamp01(Mathf.InverseLerp(6f, 1.5f, cm));

        private PinchState Step(float strength, bool tracked = true, bool hasMeta = false, bool metaPinching = false,
            bool systemGesture = false)
        {
            return _pinch.Step(new PinchSample
            {
                Tracked = tracked,
                Strength = strength,
                HasMeta = hasMeta,
                MetaPinching = metaPinching,
                SystemGesture = systemGesture,
            }, P);
        }

        // One quick tap from the resting pointing pose: close, hold briefly, open back to rest.
        private static void AddTap(List<float> frames, int heldFrames = 6, int restFrames = 8)
        {
            for (int i = 0; i < restFrames; i++) frames.Add(Cm(RestCm));
            frames.Add(Cm(2.2f));
            frames.Add(Cm(1.2f));
            for (int i = 0; i < heldFrames; i++) frames.Add(Cm(PinchCm));
            frames.Add(Cm(1.6f));
            frames.Add(Cm(2.2f));
            frames.Add(Cm(RestCm));
        }

        // The rounds 1-4 pipeline: smoothed strength (valueSmoothing 18) into the absolute rule (0.8 / 0.6).
        private static List<PinchState> OldPipeline(List<float> frames)
        {
            var pinch = new PinchTrigger();
            var states = new List<PinchState>();
            float smoothed = 0f;
            float t = 1f - Mathf.Exp(-18f * Dt);
            foreach (float raw in frames)
            {
                smoothed = Mathf.Lerp(smoothed, raw, t);
                states.Add(pinch.Step(true, smoothed, 0.8f, 0.6f, false, 0f, Dt));
            }
            return states;
        }

        private struct ChainResult
        {
            public int Fires;
            public int Holds;
            public int ChargeStarts;
            public int ChargeShots;
            public float FirstChargeHoldSeconds;
        }

        // The pinch rule into the charge rule and model, as PointingBeamController uses it.
        private ChainResult Chain(List<float> frames)
        {
            var charge = new ChargeShotModel();
            var result = new ChainResult { FirstChargeHoldSeconds = -1f };
            float held = 0f;
            foreach (float s in frames)
            {
                PinchState ps = Step(s);
                if (ps.FireTriggered) result.Fires++;
                ChargeStep step = charge.Step(ps.Held, Dt, ChargeParams.Default);
                held = ps.Held ? held + Dt : 0f;
                if (step.Charging && result.FirstChargeHoldSeconds < 0f) result.FirstChargeHoldSeconds = held;
                if (step.HoldEnded)
                {
                    result.Holds++;
                    if (step.ChargeStarted) result.ChargeStarts++;
                    if (step.Released) result.ChargeShots++;
                }
            }
            return result;
        }

        [Test]
        public void DeviceNumbers_ThumbRestsAt2_8cm()
        {
            Assert.AreEqual(0.711f, Cm(RestCm), 0.001f, "rest strength sits above the 0.6 reset");
            Assert.Greater(Cm(RestCm), P.ResetThreshold);
        }

        [Test]
        public void ThumbSettlesAt2_8cm_OldRuleNeverReleases()
        {
            var frames = new List<float>();
            AddTap(frames, restFrames: 4);
            for (int i = 0; i < 144; i++) frames.Add(Cm(RestCm)); // 2 s resting

            List<PinchState> old = OldPipeline(frames);
            Assert.IsTrue(old[old.Count - 1].Held, "the old rule still holds the pinch 2 s later (device bug)");
        }

        [Test]
        public void ThumbSettlesAt2_8cm_NewRuleReleasesWithin3Frames()
        {
            Assert.IsFalse(Step(Cm(4f)).FireTriggered);
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
            for (int i = 0; i < 5; i++) Assert.IsTrue(Step(Cm(PinchCm)).Held);

            // Opening: 1.6 -> 2.2 -> 2.8 cm, one frame each.
            float[] opening = { Cm(1.6f), Cm(2.2f), Cm(RestCm) };
            int releasedAt = -1;
            PinchRelease release = default;
            for (int i = 0; i < opening.Length && releasedAt < 0; i++)
            {
                PinchState s = Step(opening[i]);
                if (!s.Held)
                {
                    releasedAt = i;
                    release = s.Release;
                }
            }

            Assert.GreaterOrEqual(releasedAt, 0, "released while the thumb settles");
            Assert.LessOrEqual(releasedAt + 1, 3, "within 3 frames of opening");
            Assert.AreEqual(PinchReleaseBy.Relative, release.By);

            for (int i = 0; i < 144; i++)
            {
                PinchState s = Step(Cm(RestCm));
                Assert.IsFalse(s.Held || s.FireTriggered, $"rest frame {i}");
            }
        }

        [Test]
        public void TenQuickTaps_TenFires_NoChargeStarts()
        {
            var frames = new List<float>();
            for (int i = 0; i < 10; i++) AddTap(frames);
            frames.Add(Cm(RestCm));

            ChainResult r = Chain(frames);
            Assert.AreEqual(10, r.Fires);
            Assert.AreEqual(10, r.Holds);
            Assert.AreEqual(0, r.ChargeStarts);
            Assert.AreEqual(0, r.ChargeShots);
        }

        [Test]
        public void TenQuickTaps_OldRule_FiresOnceAndHolds()
        {
            var frames = new List<float>();
            for (int i = 0; i < 10; i++) AddTap(frames);

            int fires = 0;
            foreach (PinchState s in OldPipeline(frames))
                if (s.FireTriggered) fires++;
            Assert.AreEqual(1, fires, "the device bug: one shot, then one long hold");
        }

        [Test]
        public void DeliberateOneSecondHold_ChargesAfterHoldDelay_FiresOnRelease()
        {
            var frames = new List<float>();
            for (int i = 0; i < 6; i++) frames.Add(Cm(RestCm));
            frames.Add(Cm(1.2f));
            // 1 s held with tracking jitter (thumb wanders 1.0-1.7 cm).
            for (int i = 0; i < 72; i++) frames.Add(Cm(i % 3 == 0 ? 1.7f : i % 3 == 1 ? 1.0f : 1.4f));
            frames.Add(Cm(1.6f));
            frames.Add(Cm(2.2f));
            frames.Add(Cm(RestCm));

            ChainResult r = Chain(frames);
            Assert.AreEqual(1, r.Fires, "the press is one normal shot");
            Assert.AreEqual(1, r.Holds);
            Assert.AreEqual(1, r.ChargeStarts);
            Assert.AreEqual(1, r.ChargeShots, "release fires the charge shot");
            Assert.AreEqual(ChargeParams.Default.HoldDelay, r.FirstChargeHoldSeconds, 2f * Dt,
                "the charge starts after HoldDelay");
        }

        [Test]
        public void MetaFlag_ReleasesWhenTheThumbStaysClose()
        {
            // The thumb only opens to 1.8 cm (0.93): no strength rule releases.
            Assert.IsTrue(Step(Cm(PinchCm), hasMeta: true, metaPinching: true).FireTriggered);
            for (int i = 0; i < 10; i++) Step(Cm(PinchCm), hasMeta: true, metaPinching: true);

            PinchState first = Step(Cm(1.8f), hasMeta: true, metaPinching: false);
            Assert.IsTrue(first.Held, "one frame off is debounced");
            PinchState second = Step(Cm(1.8f), hasMeta: true, metaPinching: false);
            Assert.IsFalse(second.Held, "released after 2 frames off");
            Assert.AreEqual(PinchReleaseBy.Meta, second.Release.By);
            Assert.IsTrue(second.Release.MetaSeen);
        }

        [Test]
        public void SameThumb_WithoutMeta_StaysHeld()
        {
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
            for (int i = 0; i < 30; i++) Assert.IsTrue(Step(Cm(1.8f)).Held, $"frame {i}");
        }

        [Test]
        public void MetaFlag_OneFrameFlicker_DoesNotRelease()
        {
            Step(Cm(PinchCm), hasMeta: true, metaPinching: true);
            for (int i = 0; i < 20; i++)
            {
                bool flicker = i % 4 == 2;
                PinchState s = Step(Cm(PinchCm), hasMeta: true, metaPinching: !flicker);
                Assert.IsTrue(s.Held, $"frame {i}");
                Assert.IsFalse(s.FireTriggered, $"frame {i}");
            }
        }

        // A slow pinch crosses 0.8 before Meta's flag turns on (it needs full strength):
        // the flag being off then is not a release.
        [Test]
        public void MetaFlag_NotYetOn_DoesNotReleaseTheHold()
        {
            var charge = new ChargeShotModel();
            ChargeStep step = default;
            Assert.IsTrue(Step(Cm(2.3f), hasMeta: true, metaPinching: false).FireTriggered);
            for (int i = 0; i < 4; i++)
            {
                PinchState s = Step(Cm(2.0f - 0.2f * i), hasMeta: true, metaPinching: false);
                Assert.IsTrue(s.Held, $"closing frame {i}");
                step = charge.Step(s.Held, Dt, ChargeParams.Default);
            }
            for (int i = 0; i < 72; i++)
            {
                PinchState s = Step(Cm(PinchCm), hasMeta: true, metaPinching: true);
                Assert.IsTrue(s.Held, $"held frame {i}");
                step = charge.Step(s.Held, Dt, ChargeParams.Default);
            }
            Assert.IsTrue(step.Ready, "a 1 s hold readies the charge");
        }

        [Test]
        public void NoMeta_FallbackOnly_ReleasesByRelativeDrop()
        {
            Step(Cm(PinchCm));
            Step(Cm(PinchCm));
            PinchState s = Step(Cm(2.6f)); // 0.756: 0.24 below the peak, above the reset
            Assert.IsFalse(s.Held);
            Assert.AreEqual(PinchReleaseBy.Relative, s.Release.By);
            Assert.IsFalse(s.Release.MetaSeen);
        }

        [Test]
        public void FullOpen_IsLabelledAbsolute()
        {
            Step(Cm(PinchCm));
            PinchState s = Step(Cm(5f));
            Assert.IsFalse(s.Held);
            Assert.AreEqual(PinchReleaseBy.Absolute, s.Release.By, "the old rule would release here too");
        }

        [Test]
        public void JitterAroundTheReleasePoint_DoesNotDoubleFire()
        {
            int fires = 0;
            if (Step(1f).FireTriggered) fires++;
            float[] jitter = { 0.79f, 0.83f, 0.78f, 0.84f, 0.8f, 0.82f };
            for (int i = 0; i < 144; i++)
                if (Step(jitter[i % jitter.Length]).FireTriggered) fires++;
            Assert.AreEqual(1, fires);

            // A real pinch from there fires again.
            Assert.IsTrue(Step(1f).FireTriggered);
        }

        [Test]
        public void JitterAroundTheFirePoint_FiresOnce()
        {
            int fires = 0;
            float[] jitter = { 0.78f, 0.82f, 0.77f, 0.83f };
            for (int i = 0; i < 144; i++)
                if (Step(jitter[i % jitter.Length]).FireTriggered) fires++;
            Assert.AreEqual(1, fires);
        }

        [Test]
        public void TrackingLossMidHold_DropsThePinch_AndRequiresAReopen()
        {
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
            for (int i = 0; i < 20; i++) Step(Cm(PinchCm));

            PinchState lost = Step(Cm(PinchCm), tracked: false);
            Assert.IsFalse(lost.Held);
            Assert.AreEqual(PinchReleaseBy.Lost, lost.Release.By);

            for (int i = 0; i < 10; i++)
            {
                PinchState back = Step(Cm(PinchCm));
                Assert.IsFalse(back.FireTriggered || back.Held, $"back still pinched, frame {i}");
            }

            // Opening to the resting thumb (not below the reset) is a reopen.
            Step(Cm(2.2f));
            Step(Cm(RestCm));
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
        }

        [Test]
        public void RequireReopen_HeldPinchDoesNotFireUntilOpened()
        {
            _pinch.RequireReopen();
            for (int i = 0; i < 10; i++) Assert.IsFalse(Step(Cm(PinchCm)).Held);
            Step(Cm(RestCm));
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
        }

        [Test]
        public void SystemGesture_DropsThePinch_AndRequiresAReopenAfter()
        {
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
            PinchState during = Step(Cm(PinchCm), systemGesture: true);
            Assert.IsFalse(during.Held);
            Assert.AreEqual(PinchReleaseBy.Lost, during.Release.By);
            for (int i = 0; i < 20; i++) Assert.IsFalse(Step(Cm(PinchCm), systemGesture: true).Held);

            for (int i = 0; i < 20; i++)
            {
                PinchState after = Step(Cm(PinchCm));
                Assert.IsFalse(after.FireTriggered || after.Held, $"gesture over, still pinched, frame {i}");
            }
            Step(Cm(RestCm));
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
        }

        // The ASSIST chain as XRHandsInputSource wires it from round 5: a ready charge
        // held into the system gesture, through it and past its end never fires.
        [Test]
        public void ReadyChargeIntoSystemGesture_NeverFires()
        {
            var charge = new ChargeShotModel();
            bool released = false, shot = false, ready = false;

            void Frame(bool gesture, float strength)
            {
                PinchState ps = Step(strength, systemGesture: gesture);
                var input = new HandInputData
                {
                    HasAim = true,
                    FireTriggered = ps.FireTriggered,
                    PinchHeld = ps.Held,
                    AimSystemGesture = gesture,
                };
                ChargeInputAction action = ChargeInputRule.Decide(input, heroAlive: true);
                ChargeStep step = default;
                if (action == ChargeInputAction.Cancel) charge.Cancel();
                else step = charge.Step(action == ChargeInputAction.Hold, Dt, ChargeParams.Default);
                released |= step.Released;
                if (!gesture) ready = step.Ready;
                shot |= ps.FireTriggered;
            }

            Frame(false, 1f);
            Assert.IsTrue(shot);
            for (int i = 0; i < 72; i++) Frame(false, 1f);
            Assert.IsTrue(ready, "the charge was ready before the gesture");

            shot = false;
            for (int i = 0; i < 36; i++) Frame(true, 1f);
            for (int i = 0; i < 36; i++) Frame(false, 1f);
            Frame(false, Cm(RestCm));
            Assert.IsFalse(released, "no charge shot from the system gesture");
            Assert.IsFalse(shot, "no normal shot either");
        }

        [Test]
        public void Release_ReportsPeakMinAndReleaseStrength()
        {
            Step(0.9f, hasMeta: true, metaPinching: false);
            Step(1f, hasMeta: true, metaPinching: true);
            Step(0.85f, hasMeta: true, metaPinching: true);
            Step(0.95f, hasMeta: true, metaPinching: true);
            PinchState s = Step(0.75f, hasMeta: true, metaPinching: true);

            Assert.IsFalse(s.Held);
            Assert.AreEqual(PinchReleaseBy.Relative, s.Release.By);
            Assert.AreEqual(1f, s.Release.PeakStrength, 1e-5f);
            Assert.AreEqual(0.85f, s.Release.MinStrength, 1e-5f);
            Assert.AreEqual(0.75f, s.Release.ReleaseStrength, 1e-5f);
            Assert.IsTrue(s.Release.MetaSeen);
        }

        [Test]
        public void HeldSteps_HaveNoRelease()
        {
            Assert.IsFalse(Step(1f).Release.Ended);
            Assert.IsFalse(Step(1f).Release.Ended);
            Assert.IsTrue(Step(0.3f).Release.Ended);
            Assert.IsFalse(Step(0.3f).Release.Ended, "only the step the pinch ended");
        }

        [Test]
        public void RelativeAndMetaOff_IsTheAbsoluteRule()
        {
            var p = new PinchReleaseParams { FireThreshold = 0.8f, ResetThreshold = 0.6f };
            Assert.IsTrue(_pinch.Step(new PinchSample { Tracked = true, Strength = 1f }, p).FireTriggered);
            for (int i = 0; i < 30; i++)
                Assert.IsTrue(_pinch.Step(new PinchSample { Tracked = true, Strength = Cm(RestCm) }, p).Held);
            Assert.IsFalse(_pinch.Step(new PinchSample { Tracked = true, Strength = 0.6f }, p).Held);
            Assert.IsTrue(_pinch.Step(new PinchSample { Tracked = true, Strength = 0.8f }, p).FireTriggered);
        }

        [Test]
        public void DefaultParams_MatchTheDecision()
        {
            Assert.AreEqual(0.8f, P.FireThreshold);
            Assert.AreEqual(0.6f, P.ResetThreshold);
            Assert.AreEqual(0.2f, P.RelativeRelease);
            Assert.AreEqual(2, P.MetaReleaseFrames);
        }
    }
}
