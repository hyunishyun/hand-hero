using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // P12 (D15): the shards of the kill burst fly out, slow down and shrink away.
    public class KillBurstMotionTests
    {
        [Test]
        public void Distance_GrowsAndEasesOut()
        {
            float early = KillBurstMotion.Distance(0.05f, 0.4f, 6f);
            float mid = KillBurstMotion.Distance(0.2f, 0.4f, 6f);
            float late = KillBurstMotion.Distance(0.4f, 0.4f, 6f);
            Assert.Greater(mid, early);
            Assert.Greater(late, mid);
            float first = KillBurstMotion.Distance(0.1f, 0.4f, 6f) - KillBurstMotion.Distance(0f, 0.4f, 6f);
            float last = KillBurstMotion.Distance(0.4f, 0.4f, 6f) - KillBurstMotion.Distance(0.3f, 0.4f, 6f);
            Assert.Less(last, first, "slower at the end");
            Assert.AreEqual(0f, KillBurstMotion.Distance(0f, 0.4f, 6f));
        }

        [Test]
        public void Distance_StopsAtTheLifetime()
        {
            Assert.AreEqual(KillBurstMotion.Distance(0.4f, 0.4f, 6f), KillBurstMotion.Distance(2f, 0.4f, 6f), 1e-5f);
        }

        [Test]
        public void Scale_ShrinksToZero()
        {
            Assert.AreEqual(1f, KillBurstMotion.Scale(0f, 0.4f), 1e-5f);
            Assert.AreEqual(0.5f, KillBurstMotion.Scale(0.2f, 0.4f), 1e-5f);
            Assert.AreEqual(0f, KillBurstMotion.Scale(0.5f, 0.4f));
        }

        [Test]
        public void ZeroLifetime_IsSafe()
        {
            Assert.AreEqual(0f, KillBurstMotion.Scale(0f, 0f));
            Assert.AreEqual(0f, KillBurstMotion.Distance(0.1f, 0f, 6f));
        }
    }
}
