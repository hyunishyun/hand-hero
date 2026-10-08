using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Whether this frame holds, releases or cancels the charge shot. A pinch
    // charge cancels (never fires) when the aim hand drops out or the input source
    // is switched off (pause, round end); the legacy palms charge keeps its
    // release-at-last-aim behaviour.
    public class ChargeCancelRuleTests
    {
        private static HandInputData Aiming(bool pinch)
        {
            return new HandInputData { HasAim = true, PinchHeld = pinch };
        }

        [Test]
        public void PinchHeld_WithAim_Holds()
        {
            Assert.AreEqual(ChargeInputAction.Hold, ChargeInputRule.Decide(Aiming(true), heroAlive: true));
        }

        [Test]
        public void NoPinch_WithAim_None()
        {
            Assert.AreEqual(ChargeInputAction.None, ChargeInputRule.Decide(Aiming(false), heroAlive: true));
        }

        [Test]
        public void AimLost_Cancels()
        {
            var input = new HandInputData { HasAim = false, PinchHeld = true };
            Assert.AreEqual(ChargeInputAction.Cancel, ChargeInputRule.Decide(input, heroAlive: true));
        }

        [Test]
        public void PalmsChargeHeld_AimLost_StillHolds()
        {
            var input = new HandInputData { HasAim = false, Gestures = HandGestures.ChargeHeld };
            Assert.AreEqual(ChargeInputAction.Hold, ChargeInputRule.Decide(input, heroAlive: true));
        }

        [Test]
        public void HeroDead_Cancels()
        {
            Assert.AreEqual(ChargeInputAction.Cancel, ChargeInputRule.Decide(Aiming(true), heroAlive: false));
        }

        [Test]
        public void DisabledSourceDefaultData_Cancels()
        {
            Assert.AreEqual(ChargeInputAction.Cancel, ChargeInputRule.Decide(default, heroAlive: true));
        }
    }
}
