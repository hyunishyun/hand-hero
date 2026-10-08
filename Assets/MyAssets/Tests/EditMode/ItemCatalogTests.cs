using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // The starter catalog (plan §5): ids are unique, relics are unique and max
    // level 1, Greed items carry a debuff and cannot be dropped.
    public class ItemCatalogTests
    {
        [Test]
        public void Catalog_HasNineteenItems()
        {
            Assert.AreEqual(19, ItemCatalog.All.Count);
        }

        [Test]
        public void Ids_AreUnique_AndLookupWorks()
        {
            var seen = new HashSet<string>();
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                Assert.IsTrue(seen.Add(item.Id), item.Id);
                Assert.AreSame(item, ItemCatalog.Get(item.Id));
            }
            Assert.IsNull(ItemCatalog.Get("no_such_item"));
        }

        [Test]
        public void EveryItem_HasNameDescriptionAndEffect()
        {
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(item.Name), item.Id);
                Assert.IsFalse(string.IsNullOrEmpty(item.Description), item.Id);
                Assert.Greater(item.Effects.Count, 0, item.Id);
                Assert.Greater(item.SpawnWeight, 0f, item.Id);
            }
        }

        [Test]
        public void Relics_AreUniqueLegendaryMaxLevelOne()
        {
            int relics = 0;
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                if (item.Category != ItemCategory.Relic) continue;
                relics++;
                Assert.IsTrue(item.Unique, item.Id);
                Assert.AreEqual(1, item.MaxLevel, item.Id);
                Assert.AreEqual(ItemRarity.Legendary, item.Rarity, item.Id);
                Assert.AreEqual(0.2f, item.SpawnWeight, 1e-4f, item.Id);
            }
            Assert.AreEqual(3, relics);
        }

        [Test]
        public void GreedItems_HaveDebuff_AndCannotBeDropped()
        {
            int greed = 0;
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                if (item.Rarity != ItemRarity.Greed) continue;
                greed++;
                Assert.Greater(item.Debuffs.Count, 0, item.Id);
                Assert.IsFalse(item.Droppable, item.Id);
            }
            Assert.AreEqual(2, greed);
        }

        [Test]
        public void NonGreedItems_AreDroppable()
        {
            Assert.IsTrue(ItemCatalog.Get(ItemCatalog.PowerCell).Droppable);
        }

        [Test]
        public void SynergyItems_RequireTheirTag()
        {
            CollectionAssert.AreEqual(new[] { "Critical" }, ItemCatalog.Get(ItemCatalog.SharpFocus).RequiredTags);
            CollectionAssert.AreEqual(new[] { "Charge" }, ItemCatalog.Get(ItemCatalog.QuickCharge).RequiredTags);
            CollectionAssert.AreEqual(new[] { "Shockwave" }, ItemCatalog.Get(ItemCatalog.StunLock).RequiredTags);
            Assert.AreEqual(0, ItemCatalog.Get(ItemCatalog.PowerCell).RequiredTags.Count);
        }
    }
}
