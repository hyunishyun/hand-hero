using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // RESET PROGRESS on the pause panel (round 4, S5 / D5): the first press only
    // arms it (CONFIRM RESET); a second press within the window resets.
    public class ConfirmGateTests
    {
        [Test]
        public void FirstPress_OnlyArms()
        {
            var gate = new ConfirmGate(3f);
            Assert.IsFalse(gate.IsArmed(0f));
            Assert.IsFalse(gate.Press(10f));
            Assert.IsTrue(gate.IsArmed(10f));
        }

        [Test]
        public void SecondPress_WithinTheWindow_Confirms_AndDisarms()
        {
            var gate = new ConfirmGate(3f);
            gate.Press(10f);
            Assert.IsTrue(gate.Press(12.9f));
            Assert.IsFalse(gate.IsArmed(12.9f));
            Assert.IsFalse(gate.Press(13f)); // a third press starts over
        }

        [Test]
        public void SecondPress_AfterTheWindow_ArmsAgain()
        {
            var gate = new ConfirmGate(3f);
            gate.Press(10f);
            Assert.IsFalse(gate.IsArmed(13.1f));
            Assert.IsFalse(gate.Press(13.1f));
            Assert.IsTrue(gate.IsArmed(13.1f));
            Assert.IsTrue(gate.Press(14f));
        }

        [Test]
        public void Cancel_Disarms()
        {
            var gate = new ConfirmGate(3f);
            gate.Press(10f);
            gate.Cancel();
            Assert.IsFalse(gate.IsArmed(10.5f));
            Assert.IsFalse(gate.Press(10.5f));
        }
    }
}
