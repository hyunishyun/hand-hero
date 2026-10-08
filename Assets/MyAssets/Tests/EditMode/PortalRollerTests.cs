using System;
using System.Collections.Generic;
using System.Linq;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Portals after a clear (design doc §2.2): 2 portals, sometimes 3 from
    // island 3 on, each = next island type + reward chest, never two identical,
    // never a chest that would roll empty.
    public class PortalRollerTests
    {
        private const int Seeds = 500;

        private static Inventory With(params string[] ids)
        {
            var inv = new Inventory(ItemCatalog.Get);
            foreach (string id in ids) Assert.IsTrue(inv.Add(id), id);
            return inv;
        }

        [Test]
        public void FirstIsland_IsArenaWithDamageChest()
        {
            Assert.AreEqual(IslandType.Arena, PortalRoller.FirstIsland.Island);
            Assert.AreEqual(ChestType.Damage, PortalRoller.FirstIsland.Chest);
        }

        [Test]
        public void EarlyIslands_TwoPortals()
        {
            for (int seed = 0; seed < Seeds; seed++)
                Assert.AreEqual(2, PortalRoller.Roll(2, With(), new Random(seed)).Count);
        }

        [Test]
        public void FromIslandThree_TwoOrThreePortals_BothHappen()
        {
            int twos = 0, threes = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                int count = PortalRoller.Roll(3, With(ItemCatalog.PowerCell), new Random(seed)).Count;
                if (count == 2) twos++;
                else if (count == 3) threes++;
                else Assert.Fail("count " + count);
            }
            Assert.Greater(twos, Seeds / 4);
            Assert.Greater(threes, Seeds / 4);
        }

        [Test]
        public void Portals_NeverIdentical()
        {
            for (int seed = 0; seed < Seeds; seed++)
            {
                List<Portal> portals = PortalRoller.Roll(6, With(ItemCatalog.PowerCell), new Random(seed));
                Assert.AreEqual(portals.Count, portals.Distinct().Count(), "seed " + seed);
            }
        }

        [Test]
        public void Portals_OnlyCombatIslands()
        {
            for (int seed = 0; seed < Seeds; seed++)
            foreach (Portal p in PortalRoller.Roll(4, With(), new Random(seed)))
                Assert.That(p.Island == IslandType.Arena || p.Island == IslandType.Horde
                    || p.Island == IslandType.Elite, p.ToString());
        }

        [Test]
        public void EliteIsland_RewardsEpicChest()
        {
            for (int seed = 0; seed < Seeds; seed++)
            foreach (Portal p in PortalRoller.Roll(4, With(), new Random(seed)))
                if (p.Island == IslandType.Elite) Assert.AreEqual(ChestType.Epic, p.Chest);
        }

        [Test]
        public void Portals_NeverOfferAnEmptyChest()
        {
            // Empty inventory: Upgrade would roll nothing. All relics owned: Relic would too.
            Inventory[] inventories =
            {
                With(),
                With(ItemCatalog.BigChests, ItemCatalog.Dividends, ItemCatalog.SecondWind),
            };
            foreach (Inventory inv in inventories)
            for (int seed = 0; seed < Seeds; seed++)
            foreach (Portal p in PortalRoller.Roll(5, inv, new Random(seed)))
                Assert.IsNotEmpty(ChestRoller.Pool(p.Chest, inv), p.ToString());
        }

        [Test]
        public void Portals_OfferVariety()
        {
            var islands = new HashSet<IslandType>();
            var chests = new HashSet<ChestType>();
            for (int seed = 0; seed < Seeds; seed++)
            foreach (Portal p in PortalRoller.Roll(4, With(ItemCatalog.PowerCell), new Random(seed)))
            {
                islands.Add(p.Island);
                chests.Add(p.Chest);
            }
            Assert.AreEqual(3, islands.Count);
            Assert.GreaterOrEqual(chests.Count, 6);
        }

        [Test]
        public void SameSeed_SamePortals()
        {
            Inventory inv = With(ItemCatalog.PowerCell);
            CollectionAssert.AreEqual(PortalRoller.Roll(4, inv, new Random(9)), PortalRoller.Roll(4, inv, new Random(9)));
        }
    }
}
