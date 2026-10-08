using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Normal-shot cooldown with a one-shot input buffer: a pull during the
    // cooldown is remembered and fires the moment the cooldown ends, instead of
    // being silently dropped (rapid trigger pulls felt "missed" on device).
    public class ShotCooldownTests
    {
        private const float Cooldown = 0.35f;

        private ShotCooldown _shots;

        [SetUp]
        public void SetUp()
        {
            _shots = new ShotCooldown();
        }

        [Test]
        public void FirstPull_FiresImmediately()
        {
            Assert.IsTrue(_shots.Step(true, 1.0f, Cooldown));
        }

        [Test]
        public void PullDuringCooldown_FiresWhenCooldownEnds()
        {
            _shots.Step(true, 1.0f, Cooldown);

            Assert.IsFalse(_shots.Step(true, 1.1f, Cooldown), "buffered, not fired yet");
            Assert.IsFalse(_shots.Step(false, 1.3f, Cooldown));
            Assert.IsTrue(_shots.Step(false, 1.36f, Cooldown), "fires once the cooldown is over");
            Assert.IsFalse(_shots.Step(false, 1.8f, Cooldown), "only once");
        }

        [Test]
        public void SeveralPullsDuringCooldown_BufferOneShot()
        {
            _shots.Step(true, 1.0f, Cooldown);
            _shots.Step(true, 1.05f, Cooldown);
            _shots.Step(true, 1.1f, Cooldown);
            _shots.Step(true, 1.2f, Cooldown);

            Assert.IsTrue(_shots.Step(false, 1.36f, Cooldown));
            Assert.IsFalse(_shots.Step(false, 1.72f, Cooldown), "no second buffered shot");
        }

        [Test]
        public void BufferedShot_RespectsCooldownFromItself()
        {
            _shots.Step(true, 1.0f, Cooldown);
            _shots.Step(true, 1.1f, Cooldown);
            Assert.IsTrue(_shots.Step(false, 1.36f, Cooldown));

            Assert.IsFalse(_shots.Step(true, 1.5f, Cooldown), "the buffered shot restarted the cooldown");
            Assert.IsTrue(_shots.Step(false, 1.72f, Cooldown));
        }

        [Test]
        public void PullAfterCooldown_FiresImmediately()
        {
            _shots.Step(true, 1.0f, Cooldown);
            Assert.IsTrue(_shots.Step(true, 1.4f, Cooldown));
        }

        [Test]
        public void ClearPending_DropsBufferedShot()
        {
            _shots.Step(true, 1.0f, Cooldown);
            _shots.Step(true, 1.1f, Cooldown);

            _shots.ClearPending();
            Assert.IsFalse(_shots.Step(false, 1.4f, Cooldown));
        }

        [Test]
        public void MarkFired_StartsCooldownAndDropsBuffer()
        {
            _shots.Step(true, 1.0f, Cooldown);
            _shots.Step(true, 1.1f, Cooldown);

            _shots.MarkFired(1.2f); // e.g. a charge shot went off
            Assert.IsFalse(_shots.Step(false, 1.4f, Cooldown));
            Assert.IsFalse(_shots.Step(true, 1.5f, Cooldown));
            Assert.IsTrue(_shots.Step(false, 1.56f, Cooldown));
        }
    }
}
