using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Run inventory: picking an owned item levels it up; unique items (relics)
    // can be owned once; owned tags drive the required-tag filter.
    public class InventoryTests
    {
        private static Inventory NewInventory() => new Inventory(ItemCatalog.Get);

        [Test]
        public void Empty_HasNothing()
        {
            Inventory inv = NewInventory();
            Assert.AreEqual(0, inv.Level(ItemCatalog.PowerCell));
            Assert.AreEqual(0, inv.TotalLevels);
            Assert.IsFalse(inv.Owns(ItemCatalog.PowerCell));
            Assert.AreEqual(0, inv.OwnedTags.Count);
        }

        [Test]
        public void Add_NewItem_IsLevelOne()
        {
            Inventory inv = NewInventory();
            Assert.IsTrue(inv.Add(ItemCatalog.PowerCell));
            Assert.AreEqual(1, inv.Level(ItemCatalog.PowerCell));
            Assert.IsTrue(inv.Owns(ItemCatalog.PowerCell));
        }

        [Test]
        public void Add_Duplicate_LevelsUp()
        {
            Inventory inv = NewInventory();
            inv.Add(ItemCatalog.PowerCell);
            inv.Add(ItemCatalog.PowerCell);
            inv.Add(ItemCatalog.PowerCell);
            Assert.AreEqual(3, inv.Level(ItemCatalog.PowerCell));
            Assert.AreEqual(3, inv.TotalLevels);
        }

        [Test]
        public void TotalLevels_SumsAllItems()
        {
            Inventory inv = NewInventory();
            inv.Add(ItemCatalog.PowerCell);
            inv.Add(ItemCatalog.PowerCell);
            inv.Add(ItemCatalog.HullPlating);
            Assert.AreEqual(3, inv.TotalLevels);
            Assert.AreEqual(2, inv.Items.Count);
        }

        [Test]
        public void Add_UniqueTwice_IsRejected()
        {
            Inventory inv = NewInventory();
            Assert.IsTrue(inv.Add(ItemCatalog.BigChests));
            Assert.IsFalse(inv.Add(ItemCatalog.BigChests));
            Assert.AreEqual(1, inv.Level(ItemCatalog.BigChests));
        }

        [Test]
        public void Add_UnknownId_IsRejected()
        {
            Inventory inv = NewInventory();
            Assert.IsFalse(inv.Add("no_such_item"));
            Assert.AreEqual(0, inv.TotalLevels);
        }

        [Test]
        public void Add_AtMaxLevel_IsRejected()
        {
            var capped = new ItemDefinition("capped", "Capped", "Test item.", ItemCategory.Perk, ItemRarity.Common,
                ItemTheme.Health, 1f, new[] { "Health" }, new string[0], false, 2,
                new[] { new StatEffect(Stat.MaxHealth, 10f, ScalingType.Linear) });
            var inv = new Inventory(id => id == "capped" ? capped : null);
            Assert.IsTrue(inv.Add("capped"));
            Assert.IsTrue(inv.Add("capped"));
            Assert.IsFalse(inv.Add("capped"));
            Assert.AreEqual(2, inv.Level("capped"));
        }

        [Test]
        public void CanAdd_MatchesAdd()
        {
            Inventory inv = NewInventory();
            Assert.IsTrue(inv.CanAdd(ItemCatalog.SecondWind));
            inv.Add(ItemCatalog.SecondWind);
            Assert.IsFalse(inv.CanAdd(ItemCatalog.SecondWind));
            Assert.IsFalse(inv.CanAdd("no_such_item"));
        }

        [Test]
        public void OwnedTags_UnionOfOwnedItemTags()
        {
            Inventory inv = NewInventory();
            inv.Add(ItemCatalog.FocusLens);
            inv.Add(ItemCatalog.Dividends);
            var tags = new HashSet<string>(inv.OwnedTags);
            Assert.IsTrue(tags.Contains("Critical"));
            Assert.IsTrue(tags.Contains("Relic"));
            Assert.IsTrue(tags.Contains("Economy"));
            Assert.IsFalse(tags.Contains("Damage"));
        }

        [Test]
        public void HasRequiredTags_NeedsEveryRequiredTag()
        {
            Inventory inv = NewInventory();
            ItemDefinition sharp = ItemCatalog.Get(ItemCatalog.SharpFocus);
            Assert.IsFalse(inv.HasRequiredTags(sharp));
            inv.Add(ItemCatalog.FocusLens);
            Assert.IsTrue(inv.HasRequiredTags(sharp));
        }
    }
}
