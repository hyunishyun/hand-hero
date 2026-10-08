using System;
using System.Collections.Generic;
using System.Linq;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Chest rolling (design doc §1.3, §1.4, §2.3, §6.3): pool filters per chest
    // type, required tags, unique exclusion, owned-item weight bonus, weighted
    // sampling without replacement, seeded and deterministic.
    public class ChestRollerTests
    {
        private const int Seeds = 300;

        private static Inventory With(params string[] ids)
        {
            var inv = new Inventory(ItemCatalog.Get);
            foreach (string id in ids) Assert.IsTrue(inv.Add(id), id);
            return inv;
        }

        private static IEnumerable<List<ItemDefinition>> RollMany(ChestType type, Inventory inv)
        {
            for (int seed = 0; seed < Seeds; seed++) yield return ChestRoller.Roll(type, inv, new Random(seed));
        }

        [Test]
        public void ChoiceCount_PerChestType()
        {
            Assert.AreEqual(3, ChestRoller.ChoiceCount(ChestType.Damage, 0));
            Assert.AreEqual(3, ChestRoller.ChoiceCount(ChestType.Random, 0));
            Assert.AreEqual(3, ChestRoller.ChoiceCount(ChestType.Spiked, 0));
            Assert.AreEqual(2, ChestRoller.ChoiceCount(ChestType.Upgrade, 0));
            Assert.AreEqual(1, ChestRoller.ChoiceCount(ChestType.Relic, 0));
            Assert.AreEqual(1, ChestRoller.ChoiceCount(ChestType.Greed, 0));
            Assert.AreEqual(4, ChestRoller.ChoiceCount(ChestType.Damage, 1));
        }

        [Test]
        public void Roll_DefaultsToThreeChoices()
        {
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Random, With()))
                Assert.AreEqual(3, roll.Count);
        }

        [Test]
        public void BigChests_AddsAChoice()
        {
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Random, With(ItemCatalog.BigChests)))
                Assert.AreEqual(4, roll.Count);
        }

        [Test]
        public void Roll_NeverRepeatsAnItem()
        {
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Random, With(ItemCatalog.BigChests)))
                Assert.AreEqual(roll.Count, roll.Select(i => i.Id).Distinct().Count());
        }

        [Test]
        public void SameSeed_SameRoll()
        {
            Inventory inv = With(ItemCatalog.PowerCell);
            List<string> a = ChestRoller.Roll(ChestType.Random, inv, new Random(42)).Select(i => i.Id).ToList();
            List<string> b = ChestRoller.Roll(ChestType.Random, inv, new Random(42)).Select(i => i.Id).ToList();
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void DamageChest_OnlyDamageAndCriticalThemes()
        {
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Damage, With(ItemCatalog.FocusLens)))
            foreach (ItemDefinition item in roll)
            {
                Assert.That(item.Theme == ItemTheme.Damage || item.Theme == ItemTheme.Critical, item.Id);
                Assert.AreNotEqual(ItemRarity.Greed, item.Rarity, item.Id);
                Assert.AreNotEqual(ItemCategory.Relic, item.Category, item.Id);
            }
        }

        [Test]
        public void ThemeChests_MatchTheirThemes()
        {
            foreach (ItemDefinition item in ChestRoller.Pool(ChestType.Ability, With()))
                Assert.AreEqual(ItemTheme.Ability, item.Theme, item.Id);
            foreach (ItemDefinition item in ChestRoller.Pool(ChestType.Economy, With()))
                Assert.AreEqual(ItemTheme.Economy, item.Theme, item.Id);
            foreach (ItemDefinition item in ChestRoller.Pool(ChestType.Health, With()))
                Assert.That(item.Theme == ItemTheme.Health || item.Theme == ItemTheme.Defense
                    || item.Theme == ItemTheme.Speed, item.Id);
            Assert.IsNotEmpty(ChestRoller.Pool(ChestType.Health, With()));
        }

        [Test]
        public void RequiredTags_HideItemsUntilOwned()
        {
            List<string> before = ChestRoller.Pool(ChestType.Damage, With()).Select(i => i.Id).ToList();
            CollectionAssert.DoesNotContain(before, ItemCatalog.SharpFocus);
            CollectionAssert.Contains(before, ItemCatalog.FocusLens);

            List<string> after = ChestRoller.Pool(ChestType.Damage, With(ItemCatalog.FocusLens))
                .Select(i => i.Id).ToList();
            CollectionAssert.Contains(after, ItemCatalog.SharpFocus);
        }

        [Test]
        public void RequiredTags_NeverRolledWithoutTheTag()
        {
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Ability, With()))
            {
                Assert.IsFalse(roll.Any(i => i.Id == ItemCatalog.QuickCharge));
                Assert.IsFalse(roll.Any(i => i.Id == ItemCatalog.StunLock));
            }
        }

        [Test]
        public void UniqueOwned_IsExcluded()
        {
            List<string> pool = ChestRoller.Pool(ChestType.Relic, With(ItemCatalog.BigChests))
                .Select(i => i.Id).ToList();
            CollectionAssert.DoesNotContain(pool, ItemCatalog.BigChests);
            CollectionAssert.Contains(pool, ItemCatalog.Dividends);
            CollectionAssert.Contains(pool, ItemCatalog.SecondWind);
        }

        [Test]
        public void RelicChest_OneRelic()
        {
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Relic, With()))
            {
                Assert.AreEqual(1, roll.Count);
                Assert.AreEqual(ItemCategory.Relic, roll[0].Category);
            }
        }

        [Test]
        public void RelicChest_AllOwned_IsEmpty()
        {
            Inventory inv = With(ItemCatalog.BigChests, ItemCatalog.Dividends, ItemCatalog.SecondWind);
            Assert.IsEmpty(ChestRoller.Roll(ChestType.Relic, inv, new Random(1)));
        }

        [Test]
        public void GreedChest_OneGreedItem()
        {
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Greed, With()))
            {
                Assert.AreEqual(1, roll.Count);
                Assert.AreEqual(ItemRarity.Greed, roll[0].Rarity);
            }
        }

        [Test]
        public void GreedItems_OnlyInGreedChest()
        {
            foreach (ChestType type in Enum.GetValues(typeof(ChestType)))
            {
                if (type == ChestType.Greed) continue;
                foreach (ItemDefinition item in ChestRoller.Pool(type, With(ItemCatalog.GlassCannon)))
                    Assert.AreNotEqual(ItemRarity.Greed, item.Rarity, type + " " + item.Id);
            }
        }

        [Test]
        public void EpicChest_EpicOrBetter()
        {
            Inventory inv = With(ItemCatalog.FocusLens, ItemCatalog.ShockAmp);
            List<ItemDefinition> pool = ChestRoller.Pool(ChestType.Epic, inv);
            Assert.IsNotEmpty(pool);
            foreach (ItemDefinition item in pool)
                Assert.That(item.Rarity == ItemRarity.Epic || item.Rarity == ItemRarity.Legendary, item.Id);
        }

        [Test]
        public void SpikedChest_UsesTheEpicPool()
        {
            Inventory inv = With(ItemCatalog.FocusLens);
            CollectionAssert.AreEquivalent(
                ChestRoller.Pool(ChestType.Epic, inv).Select(i => i.Id).ToList(),
                ChestRoller.Pool(ChestType.Spiked, inv).Select(i => i.Id).ToList());
        }

        [Test]
        public void UpgradeChest_OwnedItemsOnly_TwoChoices()
        {
            Inventory inv = With(ItemCatalog.PowerCell, ItemCatalog.HullPlating, ItemCatalog.Afterburner);
            foreach (List<ItemDefinition> roll in RollMany(ChestType.Upgrade, inv))
            {
                Assert.AreEqual(2, roll.Count);
                foreach (ItemDefinition item in roll) Assert.IsTrue(inv.Owns(item.Id), item.Id);
            }
        }

        [Test]
        public void UpgradeChest_EmptyInventory_IsEmpty()
        {
            Assert.IsEmpty(ChestRoller.Roll(ChestType.Upgrade, With(), new Random(3)));
        }

        [Test]
        public void UpgradeChest_SkipsMaxedRelicsAndGreed()
        {
            Inventory inv = With(ItemCatalog.SecondWind, ItemCatalog.GlassCannon, ItemCatalog.PowerCell);
            List<string> pool = ChestRoller.Pool(ChestType.Upgrade, inv).Select(i => i.Id).ToList();
            CollectionAssert.AreEqual(new[] { ItemCatalog.PowerCell }, pool);
        }

        [Test]
        public void Roll_PoolSmallerThanChoices_ReturnsWholePool()
        {
            // Economy theme: Crystal Magnet and Paycheck only.
            List<ItemDefinition> roll = ChestRoller.Roll(ChestType.Economy, With(), new Random(5));
            Assert.AreEqual(2, roll.Count);
        }

        [Test]
        public void Weight_OwnedBonus()
        {
            ItemDefinition cell = ItemCatalog.Get(ItemCatalog.PowerCell);
            Assert.AreEqual(1f, ChestRoller.Weight(cell, With()), 1e-5f);
            Assert.AreEqual(1.75f, ChestRoller.Weight(cell, With(ItemCatalog.PowerCell)), 1e-5f);
            Assert.AreEqual(1.75f, ChestRoller.OwnedWeightBonus, 1e-5f);
        }

        [Test]
        public void OwnedItems_ShowUpMoreOften()
        {
            const int n = 3000;
            int Count(Inventory inv)
            {
                int hits = 0;
                for (int seed = 0; seed < n; seed++)
                    if (ChestRoller.Roll(ChestType.Random, inv, new Random(seed)).Any(i => i.Id == ItemCatalog.Afterburner))
                        hits++;
                return hits;
            }

            // Both inventories own one item so the pool size matches.
            int notOwned = Count(With(ItemCatalog.HullPlating));
            int owned = Count(With(ItemCatalog.Afterburner));
            Assert.Greater(owned, notOwned * 1.25f, $"owned {owned} vs not owned {notOwned}");
        }
    }
}
