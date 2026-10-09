using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // P12 (D15): the red edge flash on damage taken stays inside the comfort
    // limits (alpha <= 0.35, <= 0.25 s) whatever the inspector says.
    public class FlashEnvelopeTests
    {
        private const float Dt = 1f / 72f;

        [Test]
        public void NoTrigger_NoFlash()
        {
            var flash = new FlashEnvelope();
            Assert.AreEqual(0f, flash.Step(Dt, 0.3f, 0.2f));
            Assert.IsFalse(flash.IsActive);
        }

        [Test]
        public void Trigger_StartsAtMaxAlpha_FadesToZeroWithinTheDuration()
        {
            var flash = new FlashEnvelope();
            flash.Trigger();
            float previous = flash.Step(Dt, 0.3f, 0.2f);
            Assert.AreEqual(0.3f, previous, 1e-4f);

            float elapsed = Dt;
            while (elapsed < 0.2f)
            {
                float a = flash.Step(Dt, 0.3f, 0.2f);
                Assert.LessOrEqual(a, previous, "never brightens again");
                previous = a;
                elapsed += Dt;
            }
            Assert.AreEqual(0f, flash.Step(Dt, 0.3f, 0.2f));
            Assert.IsFalse(flash.IsActive);
        }

        [Test]
        public void Alpha_IsCappedForComfort()
        {
            var flash = new FlashEnvelope();
            flash.Trigger();
            Assert.AreEqual(FlashEnvelope.MaxAlpha, flash.Step(Dt, 1f, 0.2f), 1e-4f);
        }

        [Test]
        public void Duration_IsCappedForComfort()
        {
            var flash = new FlashEnvelope();
            flash.Trigger();
            flash.Step(FlashEnvelope.MaxDuration, 0.3f, 5f);
            Assert.AreEqual(0f, flash.Step(Dt, 0.3f, 5f), "a 5 s setting still ends at 0.25 s");
        }

        [Test]
        public void Retrigger_RestartsTheFlash()
        {
            var flash = new FlashEnvelope();
            flash.Trigger();
            flash.Step(0.15f, 0.3f, 0.2f);
            flash.Trigger();
            Assert.AreEqual(0.3f, flash.Step(Dt, 0.3f, 0.2f), 1e-4f);
        }

        [Test]
        public void HugeFrame_EndsTheFlash()
        {
            var flash = new FlashEnvelope();
            flash.Trigger();
            flash.Step(1f, 0.3f, 0.2f);
            Assert.AreEqual(0f, flash.Step(Dt, 0.3f, 0.2f), "a hitch never leaves the red edge on");
        }

        [Test]
        public void ZeroDuration_NoFlash_NoNaN()
        {
            var flash = new FlashEnvelope();
            flash.Trigger();
            Assert.AreEqual(0f, flash.Step(Dt, 0.3f, 0f));
        }

        [Test]
        public void Cancel_ClearsAtOnce()
        {
            var flash = new FlashEnvelope();
            flash.Trigger();
            flash.Cancel();
            Assert.AreEqual(0f, flash.Step(Dt, 0.3f, 0.2f));
        }
    }
}
