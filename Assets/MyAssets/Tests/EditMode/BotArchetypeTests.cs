using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Enemy variety, first slice (round 4, S6 / D7): four bot archetypes that
    // differ only in looks and attack pattern, and the per-island spawn table.
    public class BotArchetypeTests
    {
        // The player's quick-shot cooldown (PointingBeamController.fireCooldown).
        private const float PlayerShotCooldown = 0.35f;

        private static BotArchetype Get(BotArchetypeId id) => BotArchetypes.Get(id);

        [Test]
        public void Striker_IsTodaysBot()
        {
            BotArchetype striker = Get(BotArchetypeId.Striker);
            AssertNeutral(striker.Attack);
            Assert.AreEqual(0, striker.SwitchEvery);
            Assert.AreEqual(BotShape.None, striker.Shapes);
            Assert.AreEqual(0f, striker.BodyColor.a, "keeps the bot material's color");
        }

        private static void AssertNeutral(BotAttack a)
        {
            Assert.AreEqual(1f, a.TelegraphMult);
            Assert.AreEqual(1f, a.IntervalMult);
            Assert.AreEqual(1f, a.DamageMult);
            Assert.AreEqual(1, a.BurstCount);
            Assert.AreEqual(1f, a.BeamWidthMult);
            Assert.AreEqual(1f, a.BeamDurationMult);
            Assert.AreEqual(1f, a.TelegraphWidthMult);
            Assert.AreEqual(1f, a.FirePitch);
            Assert.AreEqual(0f, a.BeamColor.a, "keeps the bot's beam color");
        }

        [Test]
        public void Sniper_LongTelegraph_SlowHeavyThinLongBeam()
        {
            BotAttack a = Get(BotArchetypeId.Sniper).Attack;
            Assert.AreEqual(1.6f, a.TelegraphMult, 1e-5f);
            Assert.AreEqual(1.8f, a.IntervalMult, 1e-5f);
            Assert.AreEqual(1.8f, a.DamageMult, 1e-5f);
            Assert.AreEqual(1, a.BurstCount);
            Assert.Less(a.BeamWidthMult, 1f);
            Assert.Greater(a.BeamDurationMult, 1f);
        }

        [Test]
        public void Gunner_ThreeShotBurst_LowDamagePerShot()
        {
            BotAttack a = Get(BotArchetypeId.Gunner).Attack;
            Assert.AreEqual(3, a.BurstCount);
            Assert.AreEqual(0.45f, a.DamageMult, 1e-5f);
            Assert.LessOrEqual(a.TelegraphMult, 1f, "one short telegraph");
            // Bots use the player's rules: never faster than the player's quick shots.
            Assert.GreaterOrEqual(a.BurstGap, PlayerShotCooldown);
            Assert.Less(a.BurstGap, 0.5f);
        }

        [Test]
        public void Lancer_WideHeavySlowShot()
        {
            BotAttack a = Get(BotArchetypeId.Lancer).Attack;
            Assert.AreEqual(2.2f, a.DamageMult, 1e-5f);
            Assert.AreEqual(2.5f, a.IntervalMult, 1e-5f);
            Assert.Greater(a.TelegraphMult, 1.5f, "long telegraph: forces a dodge");
            Assert.AreEqual(4f, a.BeamWidthMult, 1e-5f, "the charge shot's full width");
            Assert.Greater(a.TelegraphWidthMult, 1f);
        }

        [Test]
        public void Boss_AlternatesGunnerBurstsAndLancerShots()
        {
            BotArchetype boss = Get(BotArchetypeId.Boss);
            Assert.AreEqual(Get(BotArchetypeId.Gunner).Attack.BurstCount, boss.Attack.BurstCount);
            Assert.AreEqual(Get(BotArchetypeId.Gunner).Attack.DamageMult, boss.Attack.DamageMult);
            Assert.AreEqual(Get(BotArchetypeId.Lancer).Attack.DamageMult, boss.AltAttack.DamageMult);
            Assert.GreaterOrEqual(boss.SwitchEvery, 1);
            Assert.AreEqual(BotShape.Block | BotShape.Lance, boss.Shapes);
        }

        [Test]
        public void Looks_AreDistinct_ByShapeAndColor()
        {
            var ids = new[] { BotArchetypeId.Sniper, BotArchetypeId.Gunner, BotArchetypeId.Lancer, BotArchetypeId.Boss };
            var shapes = new HashSet<BotShape> { BotShape.None }; // Striker
            var colors = new List<Color>();
            foreach (BotArchetypeId id in ids)
            {
                BotArchetype a = Get(id);
                Assert.AreEqual(id, a.Id);
                Assert.IsTrue(shapes.Add(a.Shapes), $"{id} shape");
                Assert.AreEqual(1f, a.BodyColor.a, $"{id} has its own color");
                foreach (Color other in colors) Assert.AreNotEqual(other, a.BodyColor, $"{id} color");
                colors.Add(a.BodyColor);
                Assert.AreNotEqual(1f, a.Attack.FirePitch, $"{id} sounds different");
            }
        }

        [Test]
        public void Defaults_HoldEveryArchetypeOnce_InIdOrder()
        {
            BotArchetype[] all = BotArchetypes.Defaults();
            Assert.AreEqual(5, all.Length);
            for (int i = 0; i < all.Length; i++) Assert.AreEqual((BotArchetypeId)i, all[i].Id);
        }

        [Test]
        public void Names_ForTelemetry()
        {
            Assert.AreEqual("Striker", BotArchetypes.Name(BotArchetypeId.Striker));
            Assert.AreEqual("Sniper", BotArchetypes.Name(BotArchetypeId.Sniper));
            Assert.AreEqual("Gunner", BotArchetypes.Name(BotArchetypeId.Gunner));
            Assert.AreEqual("Lancer", BotArchetypes.Name(BotArchetypeId.Lancer));
            Assert.AreEqual("Boss", BotArchetypes.Name(BotArchetypeId.Boss));
        }

        // --- spawn table (RunRules) ---

        private static HashSet<BotArchetypeId> Seen(int island, IslandType type, RunParams p, int draws = 300)
        {
            var rng = new System.Random(11);
            var seen = new HashSet<BotArchetypeId>();
            for (int i = 0; i < draws; i++) seen.Add(RunRules.PickArchetype(island, type, p, rng));
            return seen;
        }

        [Test]
        public void SpawnTable_GrowsWithTheIsland()
        {
            RunParams p = RunParams.Default;
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker }, Seen(1, IslandType.Arena, p));
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker }, Seen(2, IslandType.Horde, p));
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker, BotArchetypeId.Gunner },
                Seen(3, IslandType.Arena, p));
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker, BotArchetypeId.Gunner },
                Seen(4, IslandType.Horde, p));
            CollectionAssert.AreEquivalent(
                new[] { BotArchetypeId.Striker, BotArchetypeId.Gunner, BotArchetypeId.Sniper },
                Seen(6, IslandType.Arena, p));
            CollectionAssert.AreEquivalent(
                new[] { BotArchetypeId.Striker, BotArchetypeId.Gunner, BotArchetypeId.Sniper, BotArchetypeId.Lancer },
                Seen(8, IslandType.Arena, p));
        }

        [Test]
        public void Elite_IsOneArchetypeFromTheIslandsTable_BossIsTheBoss()
        {
            RunParams p = RunParams.Default;
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker, BotArchetypeId.Gunner },
                Seen(4, IslandType.Elite, p));
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Boss }, Seen(9, IslandType.Boss, p));
        }

        [Test]
        public void SpawnTable_SameSeed_SameBots()
        {
            var a = new System.Random(5);
            var b = new System.Random(5);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(RunRules.PickArchetype(8, IslandType.Arena, RunParams.Default, a),
                    RunRules.PickArchetype(8, IslandType.Arena, RunParams.Default, b));
        }

        [Test]
        public void SpawnTable_ThresholdsAreRunParams_ZeroReadsAsTheDefault()
        {
            RunParams early = RunParams.Default;
            early.GunnerFromIsland = 1;
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker, BotArchetypeId.Gunner },
                Seen(1, IslandType.Arena, early));

            RunParams unset = RunParams.Default;
            unset.GunnerFromIsland = 0;
            unset.SniperFromIsland = 0;
            unset.LancerFromIsland = 0;
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker }, Seen(2, IslandType.Arena, unset));
            CollectionAssert.AreEquivalent(new[] { BotArchetypeId.Striker, BotArchetypeId.Gunner },
                Seen(3, IslandType.Arena, unset));
        }
    }
}
