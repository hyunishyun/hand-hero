using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    public class HysteresisGateTests
    {
        private const float On = 0.7f;
        private const float Off = 0.45f;

        [Test]
        public void StartsOff()
        {
            var gate = new HysteresisGate();
            Assert.IsFalse(gate.IsOn);
        }

        [Test]
        public void TurnsOnAtOnThreshold_WithRisingEdge()
        {
            var gate = new HysteresisGate();
            Assert.AreEqual(GateEdge.None, gate.Step(0.69f, On, Off));
            Assert.AreEqual(GateEdge.Rising, gate.Step(0.7f, On, Off));
            Assert.IsTrue(gate.IsOn);
        }

        [Test]
        public void TurnsOffAtOffThreshold_WithFallingEdge()
        {
            var gate = new HysteresisGate();
            gate.Step(1f, On, Off);
            Assert.AreEqual(GateEdge.None, gate.Step(0.46f, On, Off));
            Assert.AreEqual(GateEdge.Falling, gate.Step(0.45f, On, Off));
            Assert.IsFalse(gate.IsOn);
        }

        [Test]
        public void ValueOscillatingBetweenThresholds_NeverFlickers()
        {
            var gate = new HysteresisGate();
            gate.Step(1f, On, Off);

            for (int i = 0; i < 100; i++)
            {
                float v = i % 2 == 0 ? 0.68f : 0.5f;
                Assert.AreEqual(GateEdge.None, gate.Step(v, On, Off), $"step {i}");
                Assert.IsTrue(gate.IsOn);
            }
        }

        [Test]
        public void ValueOscillatingBetweenThresholds_StaysOffWhenOff()
        {
            var gate = new HysteresisGate();
            for (int i = 0; i < 100; i++)
            {
                float v = i % 2 == 0 ? 0.69f : 0.46f;
                Assert.AreEqual(GateEdge.None, gate.Step(v, On, Off), $"step {i}");
            }
            Assert.IsFalse(gate.IsOn);
        }

        [Test]
        public void HeldOn_RisingEdgeFiresOnce()
        {
            var gate = new HysteresisGate();
            int rising = 0;
            for (int i = 0; i < 10; i++)
                if (gate.Step(1f, On, Off) == GateEdge.Rising) rising++;
            Assert.AreEqual(1, rising);
        }

        [Test]
        public void Reset_TurnsOffAndReportsFallingOnlyWhenOn()
        {
            var gate = new HysteresisGate();
            Assert.AreEqual(GateEdge.None, gate.Reset());

            gate.Step(1f, On, Off);
            Assert.AreEqual(GateEdge.Falling, gate.Reset());
            Assert.IsFalse(gate.IsOn);
        }
    }
}
