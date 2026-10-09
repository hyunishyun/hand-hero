using System;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // P11 (D14): code-synthesized sound effects. Every event has a short recipe
    // that renders without clipping or clicks, and the whole bank fits the budget.
    public class SfxSynthTests
    {
        private const int Rate = SfxSynth.DefaultSampleRate;

        private static float[] Render(SfxRecipe r)
        {
            var buffer = new float[SfxSynth.SampleCount(r, Rate)];
            SfxSynth.Render(r, Rate, buffer);
            return buffer;
        }

        private static float Peak(float[] samples)
        {
            float peak = 0f;
            foreach (float s in samples) peak = Math.Max(peak, Math.Abs(s));
            return peak;
        }

        private static SfxId[] AllIds()
        {
            var values = (SfxId[])Enum.GetValues(typeof(SfxId));
            return Array.FindAll(values, id => id != SfxId.None);
        }

        [Test]
        public void EveryEvent_HasAShortRecipe()
        {
            foreach (SfxId id in AllIds())
            {
                SfxRecipe r = SfxRecipes.Get(id);
                Assert.Greater(r.Duration, 0f, id.ToString());
                Assert.LessOrEqual(r.Duration, 1f, id + " must stay short");
            }
        }

        [Test]
        public void EveryEvent_RendersWithoutClipping()
        {
            foreach (SfxId id in AllIds())
            {
                float[] s = Render(SfxRecipes.Get(id));
                float peak = Peak(s);
                Assert.LessOrEqual(peak, SfxSynth.MaxPeak + 1e-4f, id + " clips");
                Assert.Greater(peak, 0.05f, id + " is silent");
                foreach (float v in s) Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), id.ToString());
            }
        }

        [Test]
        public void EveryEvent_StartsAndEndsAtSilence_NoClicks()
        {
            foreach (SfxId id in AllIds())
            {
                float[] s = Render(SfxRecipes.Get(id));
                Assert.Less(Math.Abs(s[0]), 0.05f, id + " starts with a click");
                Assert.Less(Math.Abs(s[s.Length - 1]), 0.05f, id + " ends with a click");
            }
        }

        [Test]
        public void NothingIsLouderThanTheBeam()
        {
            float beam = SfxRecipes.Get(SfxId.BeamFire).Volume;
            foreach (SfxId id in AllIds())
                Assert.LessOrEqual(SfxRecipes.Get(id).Volume, beam, id + " is louder than the beam");
        }

        [Test]
        public void WholeBank_FitsTheMemoryBudget()
        {
            long bytes = 0;
            foreach (SfxId id in AllIds()) bytes += SfxSynth.SampleCount(SfxRecipes.Get(id), Rate) * sizeof(float);
            Assert.Less(bytes, SfxSynth.BudgetBytes);
        }

        [Test]
        public void SampleCount_FollowsDuration()
        {
            var r = new SfxRecipe { Wave = SfxWave.Sine, StartHz = 440f, EndHz = 440f, Duration = 0.5f, Volume = 0.5f };
            Assert.AreEqual(Rate / 2, SfxSynth.SampleCount(r, Rate));
        }

        [Test]
        public void Render_IsDeterministic()
        {
            SfxRecipe r = SfxRecipes.Get(SfxId.HitTaken); // has noise
            CollectionAssert.AreEqual(Render(r), Render(r));
        }

        [Test]
        public void Render_NormalizesToTheRecipeVolume()
        {
            var r = new SfxRecipe { Wave = SfxWave.Sine, StartHz = 440f, EndHz = 440f, Duration = 0.2f, Volume = 0.5f };
            Assert.AreEqual(0.5f, Peak(Render(r)), 1e-3f);
        }

        [Test]
        public void Render_VolumeAboveMaxPeak_IsCapped()
        {
            var r = new SfxRecipe { Wave = SfxWave.Square, StartHz = 300f, EndHz = 300f, Duration = 0.2f, Volume = 2f };
            Assert.AreEqual(SfxSynth.MaxPeak, Peak(Render(r)), 1e-3f);
        }

        [Test]
        public void Render_ConstantSine_HasTheRightPitch()
        {
            var r = new SfxRecipe { Wave = SfxWave.Sine, StartHz = 441f, EndHz = 441f, Duration = 1f, Volume = 0.5f };
            float[] s = Render(r);
            int crossings = 0;
            for (int i = 1; i < s.Length; i++)
                if ((s[i - 1] < 0f) != (s[i] < 0f)) crossings++;
            // Two zero crossings per cycle; the envelope fades only the edges.
            Assert.AreEqual(882, crossings, 6);
        }

        [Test]
        public void Render_Notes_SplitTheDurationIntoSeparateEnvelopes()
        {
            var r = new SfxRecipe
            {
                Wave = SfxWave.Sine, StartHz = 440f, EndHz = 440f, Duration = 0.4f, Volume = 0.5f,
                Notes = new[] { 0f, 12f },
            };
            float[] s = Render(r);
            // Each note fades out at its end, so the boundary between the notes is quiet.
            int mid = s.Length / 2;
            Assert.Less(Math.Abs(s[mid - 1]), 0.05f);
            Assert.Greater(Peak(s), 0.4f);
        }

        [Test]
        public void Render_ShortBuffer_Throws()
        {
            SfxRecipe r = SfxRecipes.Get(SfxId.MenuPoint);
            Assert.Throws<ArgumentException>(() => SfxSynth.Render(r, Rate, new float[1]));
        }
    }
}
