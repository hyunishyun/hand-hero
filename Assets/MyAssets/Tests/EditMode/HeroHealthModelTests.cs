using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // ADR 3: hit = health down + 2 s at 50 % speed; 0 HP = dead, respawn after 3 s. No knockback.
    public class HeroHealthModelTests
    {
        private static HeroHealthModel NewModel() => new HeroHealthModel(HealthParams.Default);

        [Test]
        public void Defaults_MatchAdr3()
        {
            HealthParams p = HealthParams.Default;
            Assert.AreEqual(100f, p.MaxHealth);
            Assert.AreEqual(2f, p.SlowDuration);
            Assert.AreEqual(0.5f, p.SlowMultiplier);
            Assert.AreEqual(3f, p.RespawnDelay);
        }

        [Test]
        public void Hit_LowersHealth_AndSlowsForSlowDuration()
        {
            var m = NewModel();
            Assert.AreEqual(1f, m.SpeedMultiplier);

            Assert.AreEqual(HitOutcome.Damaged, m.ApplyDamage(20f));
            Assert.AreEqual(80f, m.CurrentHealth);
            Assert.AreEqual(0.5f, m.SpeedMultiplier);
            Assert.AreEqual(1, m.DamageCount);

            m.Tick(1.9f);
            Assert.AreEqual(0.5f, m.SpeedMultiplier);
            m.Tick(0.2f);
            Assert.AreEqual(1f, m.SpeedMultiplier);
        }

        [Test]
        public void SecondHit_RestartsSlow()
        {
            var m = NewModel();
            m.ApplyDamage(10f);
            m.Tick(1.5f);
            m.ApplyDamage(10f);
            m.Tick(1.5f);
            Assert.AreEqual(0.5f, m.SpeedMultiplier, "slow timer restarted by the second hit");
        }

        [Test]
        public void ZeroHealth_Dies_ThenRespawnsAfterDelay_AtFullHealth()
        {
            var m = NewModel();
            m.ApplyDamage(60f);
            Assert.AreEqual(HitOutcome.Killed, m.ApplyDamage(60f));
            Assert.IsTrue(m.IsDead);
            Assert.AreEqual(0f, m.CurrentHealth);

            Assert.IsFalse(m.Tick(2.9f));
            Assert.IsTrue(m.IsDead);
            Assert.IsTrue(m.Tick(0.2f), "respawn reported once");
            Assert.IsFalse(m.IsDead);
            Assert.AreEqual(100f, m.CurrentHealth);
            Assert.AreEqual(1f, m.SpeedMultiplier, "respawn clears the slow");
            Assert.IsFalse(m.Tick(5f));
        }

        [Test]
        public void HitsWhileDead_AreIgnored()
        {
            var m = NewModel();
            m.ApplyDamage(100f);
            int count = m.DamageCount;
            Assert.AreEqual(HitOutcome.Ignored, m.ApplyDamage(10f));
            Assert.AreEqual(count, m.DamageCount);
        }

        [Test]
        public void ZeroOrNegativeDamage_IsIgnored()
        {
            var m = NewModel();
            Assert.AreEqual(HitOutcome.Ignored, m.ApplyDamage(0f));
            Assert.AreEqual(HitOutcome.Ignored, m.ApplyDamage(-5f));
            Assert.AreEqual(100f, m.CurrentHealth);
            Assert.AreEqual(1f, m.SpeedMultiplier);
        }

        [Test]
        public void Reset_RestoresFullHealth()
        {
            var m = NewModel();
            m.ApplyDamage(100f);
            m.Reset();
            Assert.IsFalse(m.IsDead);
            Assert.AreEqual(100f, m.CurrentHealth);
            Assert.AreEqual(1f, m.SpeedMultiplier);
        }

        [Test]
        public void Normalized_IsFraction()
        {
            var m = NewModel();
            m.ApplyDamage(25f);
            Assert.AreEqual(0.75f, m.Normalized, 1e-6f);
        }
    }
}
