using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // D13 (ASSIST charge misfire): a pinch only starts charging after HoldDelay,
    // so a quick shot never slows the hero, shows the orb or fires a charge shot.
    public class ChargeHoldDelayTests
    {
        private const float Dt = 0.02f;
        private static readonly ChargeParams P = ChargeParams.Default;

        private static ChargeStep Hold(ChargeShotModel m, float seconds, ChargeParams p)
        {
            ChargeStep last = default;
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) last = m.Step(true, Dt, p);
            return last;
        }

        [Test]
        public void Defaults_HoldDelayQuarterSecond_MinChargeUnchanged()
        {
            Assert.AreEqual(0.25f, P.HoldDelay);
            Assert.AreEqual(0.3f, P.MinChargeTime);
        }

        [Test]
        public void WithinHoldDelay_NotCharging()
        {
            var m = new ChargeShotModel();
            ChargeStep s = Hold(m, 0.2f, P);

            Assert.IsFalse(s.Charging, "no slowdown, no orb");
            Assert.IsFalse(s.Ready);
            Assert.AreEqual(0f, s.Fraction);
        }

        [Test]
        public void QuickTap_ReleasesNoChargeShot()
        {
            var m = new ChargeShotModel();
            Hold(m, 0.4f, P);
            ChargeStep release = m.Step(false, Dt, P);

            Assert.IsFalse(release.Released);
            Assert.IsFalse(release.Charging);
        }

        [Test]
        public void DelayPlusMinCharge_ReleasesAChargeShot()
        {
            var m = new ChargeShotModel();
            ChargeStep held = Hold(m, 0.6f, P);
            Assert.IsTrue(held.Charging);
            Assert.IsTrue(held.Ready);

            ChargeStep release = m.Step(false, Dt, P);
            Assert.IsTrue(release.Released);
            Assert.AreEqual((0.35f - 0.3f) / 1.2f, release.Power, 0.02f, "min charge counts from the charge start");
        }

        [Test]
        public void ChargeFraction_CountsFromChargeStart()
        {
            var m = new ChargeShotModel();
            ChargeStep s = Hold(m, 0.25f + 0.6f, P);

            Assert.AreEqual(0.6f / 1.5f, s.Fraction, 0.02f);
        }

        [Test]
        public void Release_ReportsHoldDurationAndOutcome()
        {
            var tap = new ChargeShotModel();
            Hold(tap, 0.2f, P);
            ChargeStep a = tap.Step(false, Dt, P);
            Assert.IsTrue(a.HoldEnded);
            Assert.AreEqual(0.2f, a.HoldSeconds, 0.001f);
            Assert.IsFalse(a.ChargeStarted);
            Assert.IsFalse(a.Released);

            var started = new ChargeShotModel();
            Hold(started, 0.4f, P);
            ChargeStep b = started.Step(false, Dt, P);
            Assert.IsTrue(b.HoldEnded);
            Assert.IsTrue(b.ChargeStarted);
            Assert.IsFalse(b.Released);

            var charged = new ChargeShotModel();
            Hold(charged, 1f, P);
            ChargeStep c = charged.Step(false, Dt, P);
            Assert.IsTrue(c.HoldEnded && c.ChargeStarted && c.Released);
            Assert.AreEqual(1f, c.HoldSeconds, 0.001f);

            Assert.IsFalse(charged.Step(false, Dt, P).HoldEnded, "reported once");
        }

        [Test]
        public void Cancel_DuringDelay_RestartsTheDelay()
        {
            var m = new ChargeShotModel();
            Hold(m, 0.2f, P);
            m.Cancel();

            Assert.IsFalse(Hold(m, 0.2f, P).Charging);
        }

        [Test]
        public void ChargeItems_DoNotScaleTheHoldDelay()
        {
            var inv = new Inventory(ItemCatalog.Get);
            Assert.IsTrue(inv.Add(ItemCatalog.QuickCharge));
            HeroStats s = HeroStats.From(inv);
            Assert.Less(s.ChargeTimeMult, 1f);

            ChargeParams c = CombatMath.Charge(P, s);

            Assert.AreEqual(0.25f, c.HoldDelay);
            Assert.AreEqual(0.3f * s.ChargeTimeMult, c.MinChargeTime, 1e-5f);
        }

        [Test]
        public void HoldStats_CountChargeShotsAndQuickOnes()
        {
            var stats = new PinchHoldStats();
            stats.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.1f });
            stats.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.4f, ChargeStarted = true });
            stats.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.6f, ChargeStarted = true, Released = true });
            stats.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 1.2f, ChargeStarted = true, Released = true });
            stats.Add(new ChargeStep { Released = false }); // not a finished hold: ignored

            Assert.AreEqual(4, stats.Holds);
            Assert.AreEqual(3, stats.ChargesStarted);
            Assert.AreEqual(2, stats.ChargeShots);
            Assert.AreEqual(1, stats.QuickChargeShots(0.8f));
            Assert.AreEqual(4, stats.Durations.Count);
            Assert.AreEqual(0.4f, stats.Durations[1], 1e-6f);

            stats.Clear();
            Assert.AreEqual(0, stats.Holds);
            Assert.AreEqual(0, stats.Durations.Count);
        }
    }
}
