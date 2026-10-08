using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Text and layout of the RUN choice panels (portal, chest, shop).
    public class RunChoiceTextTests
    {
        private static Inventory NewInventory() => new Inventory(ItemCatalog.Get);

        [Test]
        public void PortalLabel_NamesIslandAndChest()
        {
            string label = RunChoiceText.PortalLabel(new Portal(IslandType.Horde, ChestType.Economy));
            StringAssert.Contains("HORDE", label);
            StringAssert.Contains("ECONOMY CHEST", label);
        }

        [Test]
        public void PortalHeader_ShowsNextIslandOfNine()
        {
            Assert.AreEqual("CHOOSE ISLAND 3/9", RunChoiceText.PortalHeader(3));
        }

        [Test]
        public void ChestHeader_SpikedWarnsAboutHealth()
        {
            StringAssert.Contains("DAMAGE CHEST", RunChoiceText.ChestHeader(ChestType.Damage));
            StringAssert.Contains("33%", RunChoiceText.ChestHeader(ChestType.Spiked));
        }

        [Test]
        public void ItemCard_ShowsNameLevelAfterPickRarityAndDescription()
        {
            Inventory inv = NewInventory();
            inv.Add(ItemCatalog.PowerCell);
            ItemDefinition item = ItemCatalog.Get(ItemCatalog.PowerCell);

            string card = RunChoiceText.ItemCard(item, inv);
            StringAssert.Contains("Power Cell", card);
            StringAssert.Contains("Lv 2", card);
            StringAssert.Contains("COMMON", card);
            StringAssert.Contains(item.Description, card);
        }

        [Test]
        public void ItemCard_NewItemIsLevelOne()
        {
            string card = RunChoiceText.ItemCard(ItemCatalog.Get(ItemCatalog.Deflector), NewInventory());
            StringAssert.Contains("Lv 1", card);
            StringAssert.Contains("EPIC", card);
        }

        [Test]
        public void ShopSlot_ShowsPriceUntilSold()
        {
            Inventory inv = NewInventory();
            var slot = new ShopSlot(ItemCatalog.Get(ItemCatalog.PowerCell), 105);
            StringAssert.Contains("105 CRYSTALS", RunChoiceText.ShopSlot(slot, inv));
            StringAssert.Contains("Lv 1", RunChoiceText.ShopSlot(slot, inv));

            var wallet = new CrystalWallet();
            wallet.Add(5000); // any rolled item, legendary included
            var shop = new Shop(inv, 0, new System.Random(1));
            Assert.IsTrue(shop.TryBuy(0, wallet));
            StringAssert.Contains("SOLD", RunChoiceText.ShopSlot(shop.Slots[0], inv));
            StringAssert.DoesNotContain("CRYSTALS", RunChoiceText.ShopSlot(shop.Slots[0], inv));
        }

        [Test]
        public void ShopSlot_MaxedRelicSaysOwned()
        {
            Inventory inv = NewInventory();
            inv.Add(ItemCatalog.SecondWind);
            var slot = new ShopSlot(ItemCatalog.Get(ItemCatalog.SecondWind), 800);
            StringAssert.Contains("OWNED", RunChoiceText.ShopSlot(slot, inv));
        }

        [Test]
        public void CanBuy_NeedsCrystalsUnsoldAndRoom()
        {
            Inventory inv = NewInventory();
            var wallet = new CrystalWallet();
            var slot = new ShopSlot(ItemCatalog.Get(ItemCatalog.PowerCell), 100);
            Assert.IsFalse(RunChoiceText.CanBuy(slot, inv, wallet));
            wallet.Add(100);
            Assert.IsTrue(RunChoiceText.CanBuy(slot, inv, wallet));

            inv.Add(ItemCatalog.SecondWind);
            var relic = new ShopSlot(ItemCatalog.Get(ItemCatalog.SecondWind), 50);
            Assert.IsFalse(RunChoiceText.CanBuy(relic, inv, wallet));
        }

        [Test]
        public void ShopHeaderAndReroll_ShowCrystals()
        {
            Assert.AreEqual("SHOP  -  350 CRYSTALS", RunChoiceText.ShopHeader(350));
            StringAssert.Contains("REROLL", RunChoiceText.RerollLabel(38));
            StringAssert.Contains("38 CRYSTALS", RunChoiceText.RerollLabel(38));
        }

        [Test]
        public void RarityColors_AreDistinct()
        {
            Color common = RunChoiceText.RarityColor(ItemRarity.Common);
            Assert.AreNotEqual(common, RunChoiceText.RarityColor(ItemRarity.Epic));
            Assert.AreNotEqual(common, RunChoiceText.RarityColor(ItemRarity.Legendary));
            Assert.AreNotEqual(common, RunChoiceText.RarityColor(ItemRarity.Greed));
            Assert.AreNotEqual(RunChoiceText.RarityColor(ItemRarity.Epic), RunChoiceText.RarityColor(ItemRarity.Legendary));
        }

        [Test]
        public void Layout_UpToThreeIsOneCenteredRow()
        {
            Vector2[] two = RunChoiceText.Layout(2, 1f, 0.5f);
            Assert.AreEqual(new Vector2(-0.5f, 0f), two[0]);
            Assert.AreEqual(new Vector2(0.5f, 0f), two[1]);

            Vector2[] three = RunChoiceText.Layout(3, 1f, 0.5f);
            Assert.AreEqual(new Vector2(-1f, 0f), three[0]);
            Assert.AreEqual(Vector2.zero, three[1]);
            Assert.AreEqual(new Vector2(1f, 0f), three[2]);

            Assert.AreEqual(Vector2.zero, RunChoiceText.Layout(1, 1f, 0.5f)[0]);
        }

        [Test]
        public void Layout_FourIsTwoByTwoGrid()
        {
            Vector2[] four = RunChoiceText.Layout(4, 1f, 0.6f);
            Assert.AreEqual(new Vector2(-0.5f, 0.3f), four[0]);
            Assert.AreEqual(new Vector2(0.5f, 0.3f), four[1]);
            Assert.AreEqual(new Vector2(-0.5f, -0.3f), four[2]);
            Assert.AreEqual(new Vector2(0.5f, -0.3f), four[3]);
        }
    }
}
