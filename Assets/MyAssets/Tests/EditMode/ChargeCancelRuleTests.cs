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

        // Round 4 (S3): turning the aim palm to the face for the Quest menu belongs
        // to the OS, so a held charge is dropped, never fired.
        [Test]
        public void AimSystemGesture_CancelsHeldCharge()
        {
            HandInputData input = Aiming(true);
            input.AimSystemGesture = true;
            Assert.AreEqual(ChargeInputAction.Cancel, ChargeInputRule.Decide(input, heroAlive: true));
        }

        [Test]
        public void AimSystemGesture_CancelsTriggerChargeToo()
        {
            var input = new HandInputData { HasAim = true, PinchHeld = true, TriggerHeld = true, AimSystemGesture = true };
            Assert.AreEqual(ChargeInputAction.Cancel, ChargeInputRule.Decide(input, heroAlive: true));
        }

        // The whole ASSIST chain as XRHandsInputSource wires it: system gesture gate
        // -> pinch trigger -> charge rule -> charge model. A ready charge held into
        // the system gesture, through it and past its end never fires, and the pinch
        // still closed after the gesture must open before it shoots or charges again.
        [Test]
        public void ReadyChargeIntoSystemGesture_NeverFires()
        {
            const float dt = 1f / 72f;
            const float fire = 0.8f, reset = 0.6f;
            var gate = new SystemGestureGate();
            var pinch = new PinchTrigger();
            var charge = new ChargeShotModel();
            ChargeParams p = ChargeParams.Default;

            bool released = false, shot = false, readyBeforeGesture = false;

            void Frame(bool gesture, float strength)
            {
                float s = gate.Step(gesture, strength, reset);
                PinchState ps = pinch.Step(true, s, fire, reset, false, 0f, dt);
                var input = new HandInputData
                {
                    HasAim = true,
                    FireTriggered = ps.FireTriggered,
                    PinchHeld = ps.Held,
                    AimSystemGesture = gate.Active,
                };
                ChargeInputAction action = ChargeInputRule.Decide(input, heroAlive: true);
                ChargeStep step = default;
                if (action == ChargeInputAction.Cancel) charge.Cancel();
                else step = charge.Step(action == ChargeInputAction.Hold, dt, p);
                released |= step.Released;
                if (!gesture) readyBeforeGesture = step.Ready;
                shot |= ps.FireTriggered;
            }

            Frame(false, 1f); // press: the normal shot
            Assert.IsTrue(shot);
            for (int i = 0; i < 72; i++) Frame(false, 1f); // 1 s hold: charge ready
            Assert.IsTrue(readyBeforeGesture, "the charge was ready before the gesture");

            shot = false;
            for (int i = 0; i < 36; i++) Frame(true, 1f);  // system gesture, pinch closed
            for (int i = 0; i < 36; i++) Frame(false, 1f); // gesture over, pinch still closed
            Frame(false, 0f);                               // let go
            Assert.IsFalse(released, "no charge shot from the system gesture");
            Assert.IsFalse(shot, "no normal shot either");
        }
    }
}
