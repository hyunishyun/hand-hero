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
            public PinchReleaseBy LastReleaseBy;
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

        // Chain with Meta's aim state valid on every frame: (strength, Meta's index-pinch flag).
        private ChainResult MetaChain(List<(float strength, bool flag)> frames)
        {
            var charge = new ChargeShotModel();
            var result = new ChainResult();
            foreach ((float strength, bool flag) in frames)
            {
                PinchState ps = Step(strength, hasMeta: true, metaPinching: flag);
                if (ps.FireTriggered) result.Fires++;
                if (ps.Release.Ended) result.LastReleaseBy = ps.Release.By;
                ChargeStep step = charge.Step(ps.Held, Dt, ChargeParams.Default);
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

        // Review fix (T0-R2-3): a release needs 2 agreeing samples, so the 3-frame
        // opening plus one more rest frame (was: within 3 frames).
        [Test]
        public void ThumbSettlesAt2_8cm_NewRuleReleasesWithin4Frames()
        {
            Assert.IsFalse(Step(Cm(4f)).FireTriggered);
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
            for (int i = 0; i < 5; i++) Assert.IsTrue(Step(Cm(PinchCm)).Held);

            // Opening: 1.6 -> 2.2 -> 2.8 cm, one frame each, then the thumb rests.
            float[] opening = { Cm(1.6f), Cm(2.2f), Cm(RestCm), Cm(RestCm) };
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
            Assert.LessOrEqual(releasedAt + 1, 4, "within 4 frames of opening");
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
            frames.Add(Cm(RestCm)); // the second resting sample confirms the release

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
            Assert.IsTrue(Step(Cm(2.6f)).Held, "one sample is not a release");
            PinchState s = Step(Cm(2.6f)); // 0.756: 0.24 below the peak, above the reset
            Assert.IsFalse(s.Held);
            Assert.AreEqual(PinchReleaseBy.Relative, s.Release.By);
            Assert.IsFalse(s.Release.MetaSeen);
        }

        [Test]
        public void FullOpen_IsLabelledAbsolute()
        {
            Step(Cm(PinchCm));
            Assert.IsTrue(Step(Cm(5f)).Held, "one sample is not a release");
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

            // Review fix (T0-R1-2): straddling the release point never gives 2 agreeing
            // low samples, so the pinch is still held; opening to rest releases it and
            // a real pinch from there fires again.
            Step(Cm(RestCm));
            Assert.IsFalse(Step(Cm(RestCm)).Held);
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

            // Opening to the resting thumb (not below the reset) for 2 samples is a reopen.
            Step(Cm(2.2f));
            Step(Cm(RestCm));
            Step(Cm(RestCm));
            Assert.IsTrue(Step(Cm(PinchCm)).FireTriggered);
        }

        [Test]
        public void RequireReopen_HeldPinchDoesNotFireUntilOpened()
        {
            _pinch.RequireReopen();
            for (int i = 0; i < 10; i++) Assert.IsFalse(Step(Cm(PinchCm)).Held);
            Step(Cm(RestCm));
            Assert.IsFalse(Step(Cm(PinchCm)).FireTriggered, "one resting sample is not a reopen");
            Step(Cm(RestCm));
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

        // Review fix (T0-R1-2): MinStrength counts every step reported Held, so the
        // first of the 2 release samples too. While Meta's flag is on the relative rule
        // does not count (D1), so the Meta report has its own test.
        [Test]
        public void Release_ReportsPeakMinAndReleaseStrength()
        {
            Step(0.9f);
            Step(1f);
            Step(0.85f);
            Step(0.95f);
            Assert.IsTrue(Step(0.75f).Held);
            PinchState s = Step(0.74f);

            Assert.IsFalse(s.Held);
            Assert.AreEqual(PinchReleaseBy.Relative, s.Release.By);
            Assert.AreEqual(1f, s.Release.PeakStrength, 1e-5f);
            Assert.AreEqual(0.75f, s.Release.MinStrength, 1e-5f);
            Assert.AreEqual(0.74f, s.Release.ReleaseStrength, 1e-5f);
            Assert.IsFalse(s.Release.MetaSeen);
        }

        [Test]
        public void Release_ByMeta_ReportsMetaSeen()
        {
            Step(0.9f, hasMeta: true, metaPinching: false);
            Step(1f, hasMeta: true, metaPinching: true);
            Step(0.85f, hasMeta: true, metaPinching: true);
            Step(0.95f, hasMeta: true, metaPinching: true);
            Assert.IsTrue(Step(0.75f, hasMeta: true, metaPinching: false).Held);
            PinchState s = Step(0.74f, hasMeta: true, metaPinching: false);

            Assert.IsFalse(s.Held);
            Assert.AreEqual(PinchReleaseBy.Meta, s.Release.By, "Meta names it when several rules agree");
            Assert.AreEqual(1f, s.Release.PeakStrength, 1e-5f);
            Assert.AreEqual(0.75f, s.Release.MinStrength, 1e-5f);
            Assert.AreEqual(0.74f, s.Release.ReleaseStrength, 1e-5f);
            Assert.IsTrue(s.Release.MetaSeen);
        }

        [Test]
        public void HeldSteps_HaveNoRelease()
        {
            Assert.IsFalse(Step(1f).Release.Ended);
            Assert.IsFalse(Step(1f).Release.Ended);
            Assert.IsFalse(Step(0.3f).Release.Ended, "one sample is not a release");
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

        // ---- Review fixes (round 5): T0-R1-1, T0-R1-2, T0-R2-1, T0-R2-3 ----

        // T0-R1-1 (a) / T0-R2-1 (A): a light tap only closes the thumb to 2.0 cm
        // (peak 0.889). 0.2 below that peak is 0.689, under the resting thumb (0.711),
        // so the peak drop alone never released it; the floor under FireThreshold does.
        [Test]
        public void LightTapTo2cm_ReleasesAtTheRestingThumb()
        {
            Assert.IsFalse(Step(Cm(4f)).FireTriggered);
            Assert.IsTrue(Step(Cm(2f)).FireTriggered);
            Assert.IsTrue(Step(Cm(RestCm)).Held, "one resting sample is not a release");
            PinchState s = Step(Cm(RestCm));
            Assert.IsFalse(s.Held, "released at the resting thumb");
            Assert.AreEqual(PinchReleaseBy.Relative, s.Release.By);
            Assert.AreEqual(Cm(2f), s.Release.PeakStrength, 1e-5f);

            for (int i = 0; i < 30; i++)
            {
                PinchState rest = Step(Cm(RestCm));
                Assert.IsFalse(rest.Held || rest.FireTriggered, $"rest frame {i}");
            }
        }

        [Test]
        public void LightTapsFromRest_EachFires_NoChargeEverBegins()
        {
            var frames = new List<float>();
            for (int t = 0; t < 5; t++) AddLightTap(frames, 2f);
            for (int i = 0; i < 72; i++) frames.Add(Cm(RestCm));

            ChainResult r = Chain(frames);
            Assert.AreEqual(5, r.Fires);
            Assert.AreEqual(5, r.Holds, "every tap ended");
            Assert.AreEqual(0, r.ChargeStarts);
            Assert.Less(r.FirstChargeHoldSeconds, 0f, "no charge ever began");
        }

        // T0-R1-1 (c): after a firm tap released at the resting thumb (0.711), the
        // re-arm used to need 0.911; a 2.0 cm tap (0.889) never fired.
        [Test]
        public void LightTapAfterAFirmTap_Fires()
        {
            var frames = new List<float>();
            AddTap(frames);
            AddLightTap(frames, 2f);
            for (int i = 0; i < 72; i++) frames.Add(Cm(RestCm));

            ChainResult r = Chain(frames);
            Assert.AreEqual(2, r.Fires, "the light tap fires too");
            Assert.AreEqual(2, r.Holds);
            Assert.Less(r.FirstChargeHoldSeconds, 0f, "no charge ever began");
        }

        // T0-R2-1 (B): a re-press that peaks at 1.85 cm (0.922), then the resting
        // thumb drifts 1 mm closer, to 2.7 cm (0.733): 0.2 below the peak is 0.722.
        [Test]
        public void RePressTo1_85cm_RestDriftingTo2_7cm_Releases()
        {
            var frames = new List<float>();
            AddTap(frames);
            AddLightTap(frames, 1.85f);
            for (int i = 0; i < 72; i++) frames.Add(Cm(2.7f));

            ChainResult r = Chain(frames);
            Assert.AreEqual(2, r.Fires);
            Assert.AreEqual(2, r.Holds, "the re-press ended at the drifted rest");
            Assert.Less(r.FirstChargeHoldSeconds, 0f, "no charge ever began");
        }

        // T0-R1-1 (b): rest jitter of +0.11 for one frame from an armed resting hand
        // reads as a press. The press stays instant (no added shot latency), so the
        // first spike may fire once, but it never holds into a charge and later
        // spikes of the same size don't fire again.
        [Test]
        public void OneFrameSpikesAtAnArmedRest_AtMostOneShot_NeverACharge()
        {
            var frames = new List<float> { 0.5f };
            for (int i = 0; i < 10; i++) frames.Add(Cm(RestCm));
            for (int spike = 0; spike < 6; spike++)
            {
                frames.Add(0.82f);
                for (int i = 0; i < 10; i++) frames.Add(Cm(RestCm));
            }

            ChainResult r = Chain(frames);
            Assert.LessOrEqual(r.Fires, 1, "at most the first spike fires");
            Assert.AreEqual(r.Fires, r.Holds, "a spike press ends at the resting thumb");
            Assert.Less(r.FirstChargeHoldSeconds, 0f, "no charge ever began");
        }

        // T0-R1-2 (a): one noisy frame 0.2 under the peak inside a firm hold used to
        // end it, and the next frame back at the peak fired again (with and without
        // Meta's flag on).
        [TestCase(false)]
        [TestCase(true)]
        public void OneFrameDipInAFirmHold_NoReleaseNoSecondShot(bool metaOn)
        {
            Assert.IsTrue(Step(1f, hasMeta: metaOn, metaPinching: metaOn).FireTriggered);
            for (int i = 0; i < 30; i++) Step(1f, hasMeta: metaOn, metaPinching: metaOn);

            PinchState dip = Step(0.79f, hasMeta: metaOn, metaPinching: metaOn);
            Assert.IsTrue(dip.Held, "one glitch sample is not a release");
            Assert.IsFalse(dip.Release.Ended);

            PinchState back = Step(1f, hasMeta: metaOn, metaPinching: metaOn);
            Assert.IsTrue(back.Held);
            Assert.IsFalse(back.FireTriggered, "no second shot");
        }

        // D1: the relative rule is the fallback for when Meta's flag is not on.
        [Test]
        public void MetaFlagOn_TheRelativeRuleWaits()
        {
            Assert.IsTrue(Step(1f, hasMeta: true, metaPinching: true).FireTriggered);
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(Step(0.75f, hasMeta: true, metaPinching: true).Held, $"frame {i}: Meta says pinched");

            Assert.IsTrue(Step(0.75f, hasMeta: true, metaPinching: false).Held);
            PinchState s = Step(0.75f, hasMeta: true, metaPinching: false);
            Assert.IsFalse(s.Held);
            Assert.AreEqual(PinchReleaseBy.Meta, s.Release.By);
        }

        // One frame below the reset (a deep tracking glitch) inside a hold: before,
        // it ended the hold and fully re-armed, so the next frame fired again.
        [Test]
        public void OneFrameDeepGlitchInAHold_NoReleaseNoSecondShot()
        {
            Assert.IsTrue(Step(1f).FireTriggered);
            for (int i = 0; i < 10; i++) Step(1f);

            Assert.IsTrue(Step(0.3f).Held, "one sample below the reset is not a release");
            PinchState back = Step(1f);
            Assert.IsTrue(back.Held);
            Assert.IsFalse(back.FireTriggered);
        }

        // T0-R1-2 (b): after a tracking loss one reacquire frame read low (0.78) and
        // the true value (1.0) fired at once; the rounds 1-4 pipeline needed a reopen.
        [Test]
        public void ReacquireFrameReadLow_ThenStillPinched_DoesNotFire()
        {
            Assert.IsTrue(Step(1f).FireTriggered);
            for (int i = 0; i < 20; i++) Step(1f);
            Assert.AreEqual(PinchReleaseBy.Lost, Step(1f, tracked: false).Release.By);

            Assert.IsFalse(Step(0.78f).FireTriggered, "the reacquire frame");
            for (int i = 0; i < 20; i++)
            {
                PinchState s = Step(1f);
                Assert.IsFalse(s.FireTriggered || s.Held, $"still pinched, frame {i}");
            }
        }

        // T0-R2-3: one tracking outlier at 3 cm (0.667) inside a 1 s hold used to end
        // it (charge shot early or thrown away) and fire an extra normal shot.
        [Test]
        public void OneFrame3cmSpikeInAOneSecondHold_OneShotOneChargeShot()
        {
            var frames = new List<float>();
            for (int i = 0; i < 6; i++) frames.Add(Cm(RestCm));
            frames.Add(Cm(1.2f));
            for (int i = 0; i < 30; i++) frames.Add(Cm(PinchCm));
            frames.Add(Cm(3f));
            for (int i = 0; i < 40; i++) frames.Add(Cm(PinchCm));
            frames.Add(Cm(1.6f));
            frames.Add(Cm(2.2f));
            frames.Add(Cm(RestCm));
            frames.Add(Cm(RestCm));

            ChainResult r = Chain(frames);
            Assert.AreEqual(1, r.Fires);
            Assert.AreEqual(1, r.Holds);
            Assert.AreEqual(1, r.ChargeStarts);
            Assert.AreEqual(1, r.ChargeShots);
        }

        // T0-R2-3: Meta's flag is on only at Meta strength 1.0; a 2-frame dip of the
        // flag while our strength stays at the press's peak (fingers still closed)
        // used to end the hold.
        [Test]
        public void MetaFlagTwoFrameDip_FingersStillClosed_KeepsTheHold()
        {
            Assert.IsTrue(Step(1f, hasMeta: true, metaPinching: true).FireTriggered);
            for (int i = 0; i < 20; i++) Step(1f, hasMeta: true, metaPinching: true);

            for (int i = 0; i < 2; i++)
                Assert.IsTrue(Step(1f, hasMeta: true, metaPinching: false).Held, $"flag off frame {i}");
            for (int i = 0; i < 20; i++)
            {
                PinchState s = Step(1f, hasMeta: true, metaPinching: true);
                Assert.IsTrue(s.Held, $"flag back on, frame {i}");
                Assert.IsFalse(s.FireTriggered, $"flag back on, frame {i}");
            }
        }

        // Deep review DR-6: the Meta rule tested only the current sample against its
        // drop. Once the flag had been off for 2 frames with the fingers still closed
        // (Q51: it needs Meta strength exactly 1.0), one 3 cm tracking outlier ended
        // the hold: the charge was thrown away (0.44 s held, not ready) and the
        // still-closed pinch stayed dead (re-arm 1.2). The strength rules need 2
        // samples (T0-R2-3); the Meta drop now does too.
        [TestCase(true)]
        [TestCase(false)]
        public void MetaFlagOff_FingersClosed_OneFrame3cmSpike_OneShotOneChargeShot(bool flagBackOn)
        {
            var frames = new List<(float, bool)>();
            for (int i = 0; i < 6; i++) frames.Add((Cm(RestCm), false));
            frames.Add((Cm(1.2f), true));
            for (int i = 0; i < 30; i++) frames.Add((Cm(PinchCm), true));
            frames.Add((Cm(PinchCm), false));
            frames.Add((Cm(3f), false)); // second flag-off frame: one outlier sample
            for (int i = 0; i < 40; i++) frames.Add((Cm(PinchCm), flagBackOn));
            frames.Add((Cm(1.6f), false));
            frames.Add((Cm(2.2f), false));
            frames.Add((Cm(RestCm), false));
            frames.Add((Cm(RestCm), false));

            ChainResult r = MetaChain(frames);
            Assert.AreEqual(1, r.Fires);
            Assert.AreEqual(1, r.Holds, "the outlier did not end the hold");
            Assert.AreEqual(1, r.ChargeStarts);
            Assert.AreEqual(1, r.ChargeShots, "the charge fired on the real release");
            Assert.AreEqual(PinchReleaseBy.Meta, r.LastReleaseBy, "the real opening is still Meta's release");
        }

        // DR-6: a light pinch (thumb to 1.8 cm, 0.93) under a flag flicker ended on
        // one sample 0.05 lower (about 2 mm of jitter).
        [Test]
        public void LightPinch_MetaFlagOff_OneLowerSample_KeepsTheHold()
        {
            Assert.IsTrue(Step(Cm(1.8f), hasMeta: true, metaPinching: true).FireTriggered);
            for (int i = 0; i < 30; i++) Step(Cm(1.8f), hasMeta: true, metaPinching: true);
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(Step(Cm(1.8f), hasMeta: true, metaPinching: false).Held, $"flag off frame {i}");

            PinchState jitter = Step(0.88f, hasMeta: true, metaPinching: false);
            Assert.IsTrue(jitter.Held, "one sample 0.05 under the peak is not a release");
            Assert.IsFalse(jitter.Release.Ended);
            for (int i = 0; i < 10; i++)
            {
                PinchState s = Step(Cm(1.8f), hasMeta: true, metaPinching: false);
                Assert.IsTrue(s.Held, $"back at the peak, frame {i}");
                Assert.IsFalse(s.FireTriggered, $"back at the peak, frame {i}");
            }
        }

        // DR-6: the confirm does not slow a real Meta release beyond the strength
        // rules' 2 samples: the thumb opens to 1.8 cm and stays there.
        [Test]
        public void MetaFlagOff_TwoSamplesDown_Releases()
        {
            Assert.IsTrue(Step(Cm(PinchCm), hasMeta: true, metaPinching: true).FireTriggered);
            for (int i = 0; i < 10; i++) Step(Cm(PinchCm), hasMeta: true, metaPinching: true);
            for (int i = 0; i < 3; i++) Step(Cm(PinchCm), hasMeta: true, metaPinching: false);

            Assert.IsTrue(Step(Cm(1.8f), hasMeta: true, metaPinching: false).Held, "one sample down");
            PinchState s = Step(Cm(1.8f), hasMeta: true, metaPinching: false);
            Assert.IsFalse(s.Held, "two samples down");
            Assert.AreEqual(PinchReleaseBy.Meta, s.Release.By);
        }

        // A light tap from the resting pointing pose: the thumb only closes to `closestCm`.
        private static void AddLightTap(List<float> frames, float closestCm)
        {
            for (int i = 0; i < 8; i++) frames.Add(Cm(RestCm));
            frames.Add(Cm(2.3f));
            frames.Add(Cm(closestCm));
            frames.Add(Cm(closestCm));
            frames.Add(Cm(2.3f));
        }

        [Test]
        public void DefaultParams_MatchTheDecision()
        {
            Assert.AreEqual(0.8f, P.FireThreshold);
            Assert.AreEqual(0.6f, P.ResetThreshold);
            Assert.AreEqual(0.2f, P.RelativeRelease);
            Assert.AreEqual(2, P.MetaReleaseFrames);
            Assert.AreEqual(0.05f, P.ReleaseFloorMargin);
            Assert.AreEqual(0.05f, P.RearmMargin);
            Assert.AreEqual(2, P.ConfirmFrames);
            Assert.AreEqual(0.05f, P.MetaReleaseDrop);
            Assert.LessOrEqual(Cm(RestCm), P.FireThreshold - P.ReleaseFloorMargin,
                "the resting pointing thumb is at or below the release floor");
        }
    }
}
