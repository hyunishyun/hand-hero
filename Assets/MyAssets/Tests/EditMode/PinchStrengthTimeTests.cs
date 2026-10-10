using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Deep review DR-9, second pass: report A5 retunes the ASSIST release to the
    // player's resting thumb strength r, but nothing in the run log showed r. A stuck
    // light tap ends only once 2 samples reach the release level (0.75), and the first
    // of them counts as held, so its "lowest while held" is always at or under 0.75;
    // after a reopen the same taps never fire and leave no hold at all. The run log now
    // keeps the aim hand's time per unsmoothed strength (PinchStrengthTime), and the
    // resting thumb is where most of that time sits.
    public class PinchStrengthTimeTests
    {
        private const float Dt = 1f / 72f;
        private static readonly PinchReleaseParams P = PinchReleaseParams.Default;

        // HandGestureTracker's unsmoothed strength for a thumb-index distance in cm.
        private static float Cm(float cm) => Mathf.Clamp01(Mathf.InverseLerp(6f, 1.5f, cm));

        private const float CloseRestCm = 2.535f; // r = 0.77: rests 2.7 mm closer than the tuned 2.8 cm
        private const float LightTapCm = 1.8f;    // 0.933: a light tap, thumb not on the index

        [TestCase(0f, "<0.50")]
        [TestCase(0.494f, "<0.50")]
        [TestCase(0.495f, "0.50")]
        [TestCase(0.711f, "0.71")]
        [TestCase(0.7749f, "0.77")]
        [TestCase(0.9333f, "0.93")]
        [TestCase(1f, "1.00")]
        [TestCase(1.2f, "1.00")]
        public void Bins_RoundToHundredths_From050To100(float strength, string label)
        {
            Assert.AreEqual(label, PinchStrengthTime.Label(PinchStrengthTime.BinOf(strength)));
        }

        [Test]
        public void Bins_CoverUnder050AndEveryHundredthTo100()
        {
            Assert.AreEqual(52, PinchStrengthTime.Bins);
            Assert.AreEqual("<0.50", PinchStrengthTime.Label(0));
            Assert.AreEqual("0.50", PinchStrengthTime.Label(1));
            Assert.AreEqual("1.00", PinchStrengthTime.Label(PinchStrengthTime.Bins - 1));
        }

        [Test]
        public void Add_SumsSecondsPerBin_AndIgnoresBadSamples()
        {
            var time = new PinchStrengthTime();
            time.Add(0.711f, 0.5f);
            time.Add(0.709f, 0.25f);
            time.Add(0.3f, 1f);
            time.Add(0.8f, 0f);
            time.Add(0.8f, -1f);
            time.Add(float.NaN, 1f);
            time.Add(0.8f, float.NaN);

            Assert.AreEqual(0.75f, time.Seconds[PinchStrengthTime.BinOf(0.71f)], 1e-6f);
            Assert.AreEqual(1f, time.Seconds[0], 1e-6f);
            Assert.AreEqual(0f, time.Seconds[PinchStrengthTime.BinOf(0.8f)]);
            Assert.AreEqual(1.75f, time.TotalSeconds, 1e-6f);

            time.Clear();
            Assert.AreEqual(0f, time.TotalSeconds);
            Assert.AreEqual(0f, time.Seconds[PinchStrengthTime.BinOf(0.71f)]);
        }

        [Test]
        public void PinchHoldStats_AddStrength_OnlyWhileRecording_ClearedWithTheHolds()
        {
            var holds = new PinchHoldStats();
            holds.AddStrength(0.71f, 1f);
            holds.Recording = false;
            holds.AddStrength(0.71f, 5f); // Quick Match / demo: not run telemetry
            Assert.AreEqual(1f, holds.StrengthTime.TotalSeconds, 1e-6f);

            holds.Clear();
            Assert.AreEqual(0f, holds.StrengthTime.TotalSeconds);
        }

        // The bin in 0.50-0.95 with the most time (run_summary.py reads the resting thumb there).
        private static string MostTimeAtRest(PinchStrengthTime time)
        {
            int best = PinchStrengthTime.BinOf(0.5f);
            for (int bin = best; bin <= PinchStrengthTime.BinOf(0.95f); bin++)
                if (time.Seconds[bin] > time.Seconds[best]) best = bin;
            return PinchStrengthTime.Label(best);
        }

        private static void Rest(List<float> frames, float cm, int count)
        {
            for (int i = 0; i < count; i++) frames.Add(Cm(cm));
        }

        // A light tap from the resting pose: 2.2 cm (0.844, fires), 1.8 cm, back to rest.
        private static void LightTap(List<float> frames, float restCm)
        {
            frames.Add(Cm(2.2f));
            Rest(frames, LightTapCm, 4);
            frames.Add(Cm(2.2f));
            frames.Add(Cm(restCm));
        }

        private struct Played
        {
            public int Fires;
            public List<PinchRelease> Releases;
        }

        private static Played Play(PinchTrigger pinch, PinchStrengthTime time, List<float> frames)
        {
            var played = new Played { Releases = new List<PinchRelease>() };
            foreach (float v in frames)
            {
                PinchState s = pinch.Step(new PinchSample { Tracked = true, Strength = v }, P);
                time.Add(v, Dt);
                if (s.FireTriggered) played.Fires++;
                if (s.Release.Ended) played.Releases.Add(s.Release);
            }
            return played;
        }

        // DR-9 failure 1: a light tap fires, then the hold sticks at the resting thumb
        // (0.77 is above the 0.75 release level) until the hand opens.
        [Test]
        public void StuckLightTap_LowestWhileHeldIsNotTheRestingThumb_TheTimeIs()
        {
            var frames = new List<float>();
            Rest(frames, CloseRestCm, 72);
            LightTap(frames, CloseRestCm);
            Rest(frames, CloseRestCm, 144); // 2 s stuck at rest: reads as a charge
            frames.Add(Cm(3f));             // the hand opens
            frames.Add(Cm(4f));
            Rest(frames, 5f, 10);

            var time = new PinchStrengthTime();
            Played played = Play(new PinchTrigger(), time, frames);

            Assert.AreEqual(1, played.Fires);
            Assert.AreEqual(1, played.Releases.Count);
            Assert.Greater(played.Releases[0].PeakStrength, 0.93f);
            Assert.LessOrEqual(played.Releases[0].MinStrength, 0.75f,
                "a stuck hold's lowest while held is at or under the release level, never r 0.77");
            Assert.AreEqual("0.77", MostTimeAtRest(time));
        }

        // DR-9 failure 2: after a reopen request (island start, resume) the re-arm needs
        // r + 0.2 = 0.97, so light taps (0.93) never fire and no hold is logged at all.
        [Test]
        public void TapsThatNeverFireAfterAReopen_NoHolds_TheTimeShowsTheRestingThumb()
        {
            var frames = new List<float>();
            Rest(frames, CloseRestCm, 36);
            for (int i = 0; i < 10; i++)
            {
                LightTap(frames, CloseRestCm);
                Rest(frames, CloseRestCm, 20);
            }

            var pinch = new PinchTrigger();
            pinch.RequireReopen();
            var time = new PinchStrengthTime();
            Played played = Play(pinch, time, frames);

            Assert.AreEqual(0, played.Fires);
            Assert.AreEqual(0, played.Releases.Count);
            Assert.AreEqual("0.77", MostTimeAtRest(time));
        }
    }
}
