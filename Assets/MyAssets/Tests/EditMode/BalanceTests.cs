using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Round 4 (S2, D2): round 3's device run took 9m48s with 166 damage taken.
    // Horde islands get shorter, the boss less tanky and every run bot hits harder.
    // Quick Match and the tutorial bot never read RunParams, so they keep their damage.
    public class BalanceTests
    {
        [Test]
        public void Defaults_FollowD2()
        {
            RunParams p = RunParams.Default;

            Assert.AreEqual(30f, p.HordeTime, 1e-5f);
            Assert.AreEqual(5f, p.BossHealthMult, 1e-5f);
            Assert.AreEqual(1.15f, p.EnemyDamageMult, 1e-5f);
        }

        [Test]
        public void EnemyDamageMult_AppliesToEveryIslandType()
        {
            RunParams p = RunParams.Default;
            p.EnemyDamageMult = 1.2f;

            Assert.AreEqual(1.2f, RunRules.Island(2, IslandType.Arena, p).DamageMult, 1e-5f);
            Assert.AreEqual(1.2f, RunRules.Island(3, IslandType.Horde, p).DamageMult, 1e-5f);
            Assert.AreEqual(p.EliteDamageMult * 1.2f, RunRules.Island(7, IslandType.Elite, p).DamageMult, 1e-5f);
            Assert.AreEqual(1.2f, RunRules.Island(9, IslandType.Boss, p).DamageMult, 1e-5f);
        }

        [Test]
        public void EnemyDamageMult_ZeroReadsAsOne()
        {
            // A RunParams serialized before round 4 has no value (0): bots must still hit.
            RunParams p = RunParams.Default;
            p.EnemyDamageMult = 0f;

            Assert.AreEqual(1f, RunRules.Island(1, IslandType.Arena, p).DamageMult, 1e-5f);
            Assert.AreEqual(p.EliteDamageMult, RunRules.Island(7, IslandType.Elite, p).DamageMult, 1e-5f);
        }
    }
}
