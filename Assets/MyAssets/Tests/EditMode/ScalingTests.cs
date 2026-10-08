using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Item level scaling (design doc §1.5, §6.2): four curves, and a global floor
    // on damage taken.
    public class ScalingTests
    {
        private const float Eps = 1e-4f;

        [Test]
        public void Linear_AddsBasePerLevel()
        {
            Assert.AreEqual(0f, Scaling.Evaluate(ScalingType.Linear, 20f, 0), Eps);
            Assert.AreEqual(20f, Scaling.Evaluate(ScalingType.Linear, 20f, 1), Eps);
            Assert.AreEqual(60f, Scaling.Evaluate(ScalingType.Linear, 20f, 3), Eps);
        }

        [Test]
        public void LinearMultiplier_IsOnePlusBasePerLevel()
        {
            Assert.AreEqual(1f, Scaling.Evaluate(ScalingType.LinearMultiplier, 0.15f, 0), Eps);
            Assert.AreEqual(1.15f, Scaling.Evaluate(ScalingType.LinearMultiplier, 0.15f, 1), Eps);
            Assert.AreEqual(1.45f, Scaling.Evaluate(ScalingType.LinearMultiplier, 0.15f, 3), Eps);
        }

        [Test]
        public void Hyperbolic_LevelOneIsBase()
        {
            Assert.AreEqual(0f, Scaling.Evaluate(ScalingType.Hyperbolic, 0.12f, 0), Eps);
            Assert.AreEqual(0.12f, Scaling.Evaluate(ScalingType.Hyperbolic, 0.12f, 1), Eps);
        }

        [Test]
        public void Hyperbolic_GrowsWithDiminishingReturns()
        {
            float l1 = Scaling.Evaluate(ScalingType.Hyperbolic, 0.1f, 1);
            float l2 = Scaling.Evaluate(ScalingType.Hyperbolic, 0.1f, 2);
            float l3 = Scaling.Evaluate(ScalingType.Hyperbolic, 0.1f, 3);
            Assert.Greater(l2, l1);
            Assert.Greater(l3, l2);
            Assert.Less(l3 - l2, l2 - l1);
        }

        [Test]
        public void Hyperbolic_StaysBelowOne()
        {
            Assert.Less(Scaling.Evaluate(ScalingType.Hyperbolic, 0.5f, 1000), 1f);
            Assert.Less(Scaling.Evaluate(ScalingType.Hyperbolic, 1f, 5), 1f);
        }

        [Test]
        public void Exponential_GrowsFasterEachLevel()
        {
            Assert.AreEqual(0f, Scaling.Evaluate(ScalingType.Exponential, 0.2f, 0), Eps);
            Assert.AreEqual(0.2f, Scaling.Evaluate(ScalingType.Exponential, 0.2f, 1), Eps);
            // k = 1: base * (2^level - 1)
            Assert.AreEqual(0.6f, Scaling.Evaluate(ScalingType.Exponential, 0.2f, 2), Eps);
            Assert.AreEqual(1.4f, Scaling.Evaluate(ScalingType.Exponential, 0.2f, 3), Eps);
        }

        [Test]
        public void DamageTaken_NoReduction_IsFull()
        {
            Assert.AreEqual(1f, Scaling.DamageTakenMultiplier(0f), Eps);
        }

        [Test]
        public void DamageTaken_SubtractsReduction()
        {
            Assert.AreEqual(0.7f, Scaling.DamageTakenMultiplier(0.3f), Eps);
        }

        [Test]
        public void DamageTaken_NeverBelowFloor()
        {
            Assert.AreEqual(0.25f, Scaling.DamageTakenMultiplier(0.9f), Eps);
            Assert.AreEqual(Scaling.DamageTakenFloor, Scaling.DamageTakenMultiplier(2f), Eps);
        }
    }
}
