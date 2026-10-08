using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Run stats -> combat numbers (autonomous plan R7). Neutral stats must give
    // exactly today's values so Quick Match and the bot are unchanged.
    public class CombatMathTests
    {
        private const float Eps = 1e-4f;

        private static HeroStats With(params string[] ids)
        {
            var inv = new Inventory(ItemCatalog.Get);
            foreach (string id in ids) Assert.IsTrue(inv.Add(id), id);
            return HeroStats.From(inv);
        }

        [Test]
        public void Neutral_KeepsEveryBaseValue()
        {
            HeroStats s = HeroStats.Neutral;
            ShotDamage shot = CombatMath.Shot(20f, false, s, 0.0);
            Assert.AreEqual(20f, shot.Damage, Eps);
            Assert.IsFalse(shot.Crit);
            Assert.AreEqual(70f, CombatMath.Shot(70f, true, s, 0.0).Damage, Eps);
            Assert.AreEqual(0.35f, CombatMath.FireCooldown(0.35f, s), Eps);
            Assert.AreEqual(25f, CombatMath.DamageTaken(25f, s), Eps);
            Assert.AreEqual(6f, CombatMath.ShockwaveRadius(6f, s), Eps);
            Assert.AreEqual(1.2f, CombatMath.StunDuration(1.2f, s), Eps);

            ChargeParams c = CombatMath.Charge(ChargeParams.Default, s);
            Assert.AreEqual(0.3f, c.MinChargeTime, Eps);
            Assert.AreEqual(1.5f, c.MaxChargeTime, Eps);
        }

        [Test]
        public void DamageMult_ScalesNormalAndChargedShots()
        {
            HeroStats s = With(ItemCatalog.PowerCell);
            Assert.AreEqual(23f, CombatMath.Shot(20f, false, s, 0.99).Damage, Eps);
            Assert.AreEqual(23f, CombatMath.Shot(20f, true, s, 0.99).Damage, Eps);
        }

        [Test]
        public void ChargeDamage_OnlyScalesChargedShots()
        {
            HeroStats s = With(ItemCatalog.Overcharge);
            Assert.AreEqual(20f, CombatMath.Shot(20f, false, s, 0.99).Damage, Eps);
            Assert.AreEqual(s.ChargeDamageMult * 70f, CombatMath.Shot(70f, true, s, 0.99).Damage, Eps);
            Assert.Greater(s.ChargeDamageMult, 1f);
        }

        [Test]
        public void Crit_WhenRollBelowChance()
        {
            HeroStats s = With(ItemCatalog.FocusLens);
            Assert.Greater(s.CritChance, 0f);

            ShotDamage crit = CombatMath.Shot(20f, false, s, s.CritChance * 0.5);
            Assert.IsTrue(crit.Crit);
            Assert.AreEqual(20f * HeroStats.BaseCritMultiplier, crit.Damage, Eps);

            ShotDamage miss = CombatMath.Shot(20f, false, s, s.CritChance + 0.01);
            Assert.IsFalse(miss.Crit);
            Assert.AreEqual(20f, miss.Damage, Eps);
        }

        [Test]
        public void CritDamageMult_RaisesCritHits()
        {
            HeroStats s = With(ItemCatalog.FocusLens, ItemCatalog.SharpFocus);
            ShotDamage crit = CombatMath.Shot(20f, false, s, 0.0);
            Assert.IsTrue(crit.Crit);
            Assert.AreEqual(20f * s.CritHitMultiplier, crit.Damage, Eps);
            Assert.Greater(s.CritHitMultiplier, HeroStats.BaseCritMultiplier);
        }

        [Test]
        public void FireCooldown_ShrinksWithRapidCoil()
        {
            HeroStats s = With(ItemCatalog.RapidCoil);
            Assert.AreEqual(0.35f * s.FireCooldownMult, CombatMath.FireCooldown(0.35f, s), Eps);
            Assert.Less(CombatMath.FireCooldown(0.35f, s), 0.35f);
        }

        [Test]
        public void ChargeTime_ScalesMinAndMax()
        {
            HeroStats s = With(ItemCatalog.Overcharge, ItemCatalog.QuickCharge);
            ChargeParams c = CombatMath.Charge(ChargeParams.Default, s);
            Assert.AreEqual(0.3f * s.ChargeTimeMult, c.MinChargeTime, Eps);
            Assert.AreEqual(1.5f * s.ChargeTimeMult, c.MaxChargeTime, Eps);
            Assert.Less(c.MaxChargeTime, 1.5f);
        }

        [Test]
        public void DamageTaken_UsesFlooredMultiplier()
        {
            HeroStats deflect = With(ItemCatalog.Deflector);
            Assert.Less(CombatMath.DamageTaken(20f, deflect), 20f);

            HeroStats blood = With(ItemCatalog.BloodPrice);
            Assert.AreEqual(25f, CombatMath.DamageTaken(20f, blood), Eps);
        }

        [Test]
        public void Shockwave_RadiusMultipliesAndStunAdds()
        {
            HeroStats s = With(ItemCatalog.ShockAmp, ItemCatalog.StunLock);
            Assert.AreEqual(6f * s.ShockwaveRadiusMult, CombatMath.ShockwaveRadius(6f, s), Eps);
            Assert.AreEqual(1.2f + s.StunDurationAdd, CombatMath.StunDuration(1.2f, s), Eps);
            Assert.Greater(s.StunDurationAdd, 0f);
        }

        [Test]
        public void MaxHealth_UsesAddThenMult()
        {
            Assert.AreEqual(120f, CombatMath.MaxHealth(100f, With(ItemCatalog.HullPlating)), Eps);
            Assert.AreEqual(100f, CombatMath.MaxHealth(100f, HeroStats.Neutral), Eps);
            Assert.Less(CombatMath.MaxHealth(100f, With(ItemCatalog.GlassCannon)), 100f);
        }
    }
}
