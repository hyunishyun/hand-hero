using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Inventory -> hero stats: multipliers multiply across items, capped stats
    // (chances, cuts, reductions) stay below 100%, Greed debuffs apply, and the
    // damage-taken floor holds.
    public class HeroStatsTests
    {
        private const float Eps = 1e-4f;

        private static Inventory With(params string[] ids)
        {
            var inv = new Inventory(ItemCatalog.Get);
            foreach (string id in ids) Assert.IsTrue(inv.Add(id), id);
            return inv;
        }

        [Test]
        public void Empty_IsNeutral()
        {
            HeroStats s = HeroStats.From(With());
            Assert.AreEqual(1f, s.DamageMult, Eps);
            Assert.AreEqual(1f, s.FireCooldownMult, Eps);
            Assert.AreEqual(0f, s.CritChance, Eps);
            Assert.AreEqual(1f, s.CritDamageMult, Eps);
            Assert.AreEqual(1f, s.ChargeDamageMult, Eps);
            Assert.AreEqual(1f, s.ChargeTimeMult, Eps);
            Assert.AreEqual(1f, s.ShockwaveRadiusMult, Eps);
            Assert.AreEqual(0f, s.StunDurationAdd, Eps);
            Assert.AreEqual(0f, s.MaxHealthAdd, Eps);
            Assert.AreEqual(1f, s.MaxHealthMult, Eps);
            Assert.AreEqual(0f, s.HealOnClear, Eps);
            Assert.AreEqual(1f, s.SpeedMult, Eps);
            Assert.AreEqual(1f, s.DamageTakenMult, Eps);
            Assert.AreEqual(1f, s.CrystalGainMult, Eps);
            Assert.AreEqual(0f, s.CrystalPerClear, Eps);
            Assert.AreEqual(0, s.ExtraChoices);
            Assert.AreEqual(0f, s.InterestRate, Eps);
            Assert.AreEqual(0, s.Revives);
        }

        [Test]
        public void Neutral_EqualsEmptyInventory()
        {
            Assert.AreEqual(HeroStats.Neutral, HeroStats.From(With()));
        }

        [Test]
        public void NullInventory_IsNeutral()
        {
            Assert.AreEqual(HeroStats.Neutral, HeroStats.From(null));
        }

        [Test]
        public void PowerCell_StacksLinearMultiplier()
        {
            Assert.AreEqual(1.45f, HeroStats.From(With(ItemCatalog.PowerCell, ItemCatalog.PowerCell,
                ItemCatalog.PowerCell)).DamageMult, Eps);
        }

        [Test]
        public void DamageMultipliers_MultiplyAcrossItems()
        {
            // Power Cell 1.15 x Glass Cannon 1.6
            HeroStats s = HeroStats.From(With(ItemCatalog.PowerCell, ItemCatalog.GlassCannon));
            Assert.AreEqual(1.15f * 1.6f, s.DamageMult, Eps);
        }

        [Test]
        public void RapidCoil_CutsCooldown_Hyperbolic()
        {
            Assert.AreEqual(0.88f, HeroStats.From(With(ItemCatalog.RapidCoil)).FireCooldownMult, Eps);
            HeroStats many = HeroStats.From(With(ItemCatalog.RapidCoil, ItemCatalog.RapidCoil,
                ItemCatalog.RapidCoil, ItemCatalog.RapidCoil, ItemCatalog.RapidCoil, ItemCatalog.RapidCoil,
                ItemCatalog.RapidCoil, ItemCatalog.RapidCoil, ItemCatalog.RapidCoil, ItemCatalog.RapidCoil));
            Assert.Greater(many.FireCooldownMult, 0f);
            Assert.Less(many.FireCooldownMult, 0.88f);
        }

        [Test]
        public void Critical_ChanceAndDamage()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.FocusLens, ItemCatalog.SharpFocus,
                ItemCatalog.SharpFocus));
            Assert.AreEqual(0.10f, s.CritChance, Eps);
            Assert.AreEqual(1.5f, s.CritDamageMult, Eps);
            // A crit hits for 2x, scaled by Sharp Focus.
            Assert.AreEqual(3f, s.CritHitMultiplier, Eps);
        }

        [Test]
        public void CritHitMultiplier_DefaultsToDouble()
        {
            Assert.AreEqual(HeroStats.BaseCritMultiplier, HeroStats.Neutral.CritHitMultiplier, Eps);
            Assert.AreEqual(2f, HeroStats.BaseCritMultiplier, Eps);
        }

        [Test]
        public void CritChance_NeverReachesOne()
        {
            var inv = new Inventory(ItemCatalog.Get);
            for (int i = 0; i < 200; i++) inv.Add(ItemCatalog.FocusLens);
            Assert.Less(HeroStats.From(inv).CritChance, 1f);
        }

        [Test]
        public void AbilityItems()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.Overcharge, ItemCatalog.QuickCharge,
                ItemCatalog.ShockAmp, ItemCatalog.ShockAmp, ItemCatalog.StunLock, ItemCatalog.StunLock));
            Assert.AreEqual(1.2f, s.ChargeDamageMult, Eps);
            Assert.AreEqual(0.85f, s.ChargeTimeMult, Eps);
            Assert.AreEqual(1.4f, s.ShockwaveRadiusMult, Eps);
            Assert.AreEqual(0.6f, s.StunDurationAdd, Eps);
        }

        [Test]
        public void HealthItems()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.HullPlating, ItemCatalog.HullPlating,
                ItemCatalog.NanoRepair));
            Assert.AreEqual(40f, s.MaxHealthAdd, Eps);
            Assert.AreEqual(10f, s.HealOnClear, Eps);
            Assert.AreEqual(1f, s.MaxHealthMult, Eps);
        }

        [Test]
        public void Afterburner_Speed()
        {
            Assert.AreEqual(1.2f, HeroStats.From(With(ItemCatalog.Afterburner, ItemCatalog.Afterburner)).SpeedMult,
                Eps);
        }

        [Test]
        public void Deflector_ReducesDamageTaken()
        {
            Assert.AreEqual(0.9f, HeroStats.From(With(ItemCatalog.Deflector)).DamageTakenMult, Eps);
        }

        [Test]
        public void DamageTaken_FlooredAtQuarter()
        {
            var inv = new Inventory(ItemCatalog.Get);
            for (int i = 0; i < 500; i++) inv.Add(ItemCatalog.Deflector);
            Assert.AreEqual(Scaling.DamageTakenFloor, HeroStats.From(inv).DamageTakenMult, Eps);
        }

        [Test]
        public void EconomyItems()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.CrystalMagnet, ItemCatalog.Paycheck,
                ItemCatalog.Paycheck, ItemCatalog.Dividends));
            Assert.AreEqual(1.25f, s.CrystalGainMult, Eps);
            Assert.AreEqual(40f, s.CrystalPerClear, Eps);
            Assert.AreEqual(0.10f, s.InterestRate, Eps);
        }

        [Test]
        public void Relics_RunStats()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.BigChests, ItemCatalog.SecondWind));
            Assert.AreEqual(1, s.ExtraChoices);
            Assert.AreEqual(1, s.Revives);
        }

        [Test]
        public void GlassCannon_CutsMaxHealth()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.GlassCannon));
            Assert.AreEqual(1.6f, s.DamageMult, Eps);
            Assert.AreEqual(0.7f, s.MaxHealthMult, Eps);
        }

        [Test]
        public void GlassCannon_StackedHealthStaysPositive()
        {
            var inv = new Inventory(ItemCatalog.Get);
            for (int i = 0; i < 10; i++) inv.Add(ItemCatalog.GlassCannon);
            Assert.Greater(HeroStats.From(inv).MaxHealthMult, 0f);
        }

        [Test]
        public void BloodPrice_MoreCrystalsMoreDamageTaken()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.BloodPrice));
            Assert.AreEqual(2f, s.CrystalGainMult, Eps);
            Assert.AreEqual(1.25f, s.DamageTakenMult, Eps);
        }

        [Test]
        public void BloodPrice_AndDeflector_Combine()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.BloodPrice, ItemCatalog.Deflector));
            Assert.AreEqual(1.25f * 0.9f, s.DamageTakenMult, Eps);
        }

        [Test]
        public void MaxHealth_AppliesAddThenMult()
        {
            HeroStats s = HeroStats.From(With(ItemCatalog.HullPlating, ItemCatalog.GlassCannon));
            Assert.AreEqual((100f + 20f) * 0.7f, s.MaxHealth(100f), Eps);
            Assert.AreEqual(100f, HeroStats.Neutral.MaxHealth(100f), Eps);
        }
    }
}
