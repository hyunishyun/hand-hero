using System;
using System.Linq;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Crystals, prices and the shop (autonomous plan R6, design doc §4, §6.4–6.5).
    // Every number hangs off Economy.CommonBasePrice.
    public class EconomyTests
    {
        private static Inventory With(params string[] ids)
        {
            var inv = new Inventory(ItemCatalog.Get);
            foreach (string id in ids) Assert.IsTrue(inv.Add(id), id);
            return inv;
        }

        [Test]
        public void Price_RarityTimesInflation_RoundedUp()
        {
            Assert.AreEqual(100, Economy.Price(ItemCatalog.Get(ItemCatalog.PowerCell), 0));
            Assert.AreEqual(105, Economy.Price(ItemCatalog.Get(ItemCatalog.PowerCell), 1));
            Assert.AreEqual(360, Economy.Price(ItemCatalog.Get(ItemCatalog.Deflector), 4));   // Epic 3 x 1.2
            Assert.AreEqual(880, Economy.Price(ItemCatalog.Get(ItemCatalog.BigChests), 2));   // Legendary 8 x 1.1
            Assert.AreEqual(200, Economy.Price(ItemCatalog.Get(ItemCatalog.GlassCannon), 0)); // Greed 2
        }

        [Test]
        public void RerollPrice_GrowsByHalfEachReroll()
        {
            Assert.AreEqual(25, Economy.RerollPrice(0, 0));
            Assert.AreEqual(38, Economy.RerollPrice(1, 0)); // 37.5
            Assert.AreEqual(57, Economy.RerollPrice(2, 0)); // 56.25
            Assert.AreEqual(30, Economy.RerollPrice(0, 4)); // 25 x 1.2
            Assert.AreEqual(45, Economy.RerollPrice(1, 4)); // 30 x 1.5
        }

        [Test]
        public void KillReward_TenTimesCrystalGain()
        {
            Assert.AreEqual(10, Economy.KillReward(HeroStats.Neutral));
            Assert.AreEqual(13, Economy.KillReward(HeroStats.From(With(ItemCatalog.CrystalMagnet)))); // 12.5
            Assert.AreEqual(20, Economy.KillReward(HeroStats.From(With(ItemCatalog.BloodPrice))));
        }

        [Test]
        public void ClearReward_BasePlusPaycheckPlusInterest()
        {
            Assert.AreEqual(25, Economy.ClearReward(HeroStats.Neutral, 500));
            Assert.AreEqual(45, Economy.ClearReward(HeroStats.From(With(ItemCatalog.Paycheck)), 500));
            Assert.AreEqual(45, Economy.ClearReward(HeroStats.From(With(ItemCatalog.Dividends)), 200)); // 25 + 10% of 200
            Assert.AreEqual(25, Economy.ClearReward(HeroStats.From(With(ItemCatalog.Dividends)), 0));
        }

        [Test]
        public void Spiked_CostsAThirdOfMaxHealth_NeverKills()
        {
            Assert.AreEqual(67f, Economy.SpikedHealthAfter(100f, 100f), 1e-4f);
            Assert.AreEqual(1f, Economy.SpikedHealthAfter(20f, 100f), 1e-4f);
            Assert.AreEqual(1f, Economy.SpikedHealthAfter(1f, 100f), 1e-4f);
            Assert.AreEqual(70.5f, Economy.SpikedHealthAfter(120f, 150f), 1e-3f);
        }

        [Test]
        public void Wallet_SpendOnlyWhatYouHave()
        {
            var w = new CrystalWallet();
            w.Add(50);
            Assert.IsFalse(w.TrySpend(60));
            Assert.AreEqual(50, w.Balance);
            Assert.IsTrue(w.TrySpend(50));
            Assert.AreEqual(0, w.Balance);
            w.Add(-10);
            Assert.AreEqual(0, w.Balance);
        }

        [Test]
        public void Shop_FourPedestalsFromRandomPool_PricedByEconomy()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var shop = new Shop(With(), 4, new Random(seed));
                Assert.AreEqual(Shop.PedestalCount, shop.Slots.Count);
                Assert.AreEqual(shop.Slots.Count, shop.Slots.Select(s => s.Item.Id).Distinct().Count());
                foreach (ShopSlot slot in shop.Slots)
                {
                    Assert.AreNotEqual(ItemRarity.Greed, slot.Item.Rarity);
                    Assert.AreEqual(Economy.Price(slot.Item, 4), slot.Price);
                    Assert.IsFalse(slot.Sold);
                }
            }
        }

        [Test]
        public void Shop_Buy_AddsLevel_DeductsCrystals_SlotSoldOut()
        {
            Inventory inv = With();
            var shop = new Shop(inv, 0, new Random(3));
            var wallet = new CrystalWallet();
            wallet.Add(5000);
            ShopSlot first = shop.Slots[0];

            Assert.IsTrue(shop.TryBuy(0, wallet));
            Assert.AreEqual(5000 - first.Price, wallet.Balance);
            Assert.AreEqual(1, inv.Level(first.Item.Id));
            Assert.IsTrue(shop.Slots[0].Sold);

            Assert.IsFalse(shop.TryBuy(0, wallet)); // sold out
            Assert.AreEqual(1, inv.Level(first.Item.Id));
            Assert.IsFalse(shop.TryBuy(9, wallet));
        }

        [Test]
        public void Shop_CannotBuyWithoutEnoughCrystals()
        {
            Inventory inv = With();
            var shop = new Shop(inv, 0, new Random(3));
            var wallet = new CrystalWallet();
            wallet.Add(shop.Slots[0].Price - 1);
            Assert.IsFalse(shop.TryBuy(0, wallet));
            Assert.AreEqual(shop.Slots[0].Price - 1, wallet.Balance);
            Assert.AreEqual(0, inv.TotalLevels);
            Assert.IsFalse(shop.Slots[0].Sold);
        }

        [Test]
        public void Shop_Reroll_ChargesGrowingPrice_AndRefills()
        {
            var shop = new Shop(With(), 0, new Random(5));
            var wallet = new CrystalWallet();
            wallet.Add(25 + 38 - 1);

            var wealthy = new CrystalWallet();
            wealthy.Add(1000);
            shop.TryBuy(0, wealthy);
            Assert.IsTrue(shop.Slots[0].Sold);

            Assert.AreEqual(25, shop.RerollPrice);
            Assert.IsTrue(shop.TryReroll(wallet));
            Assert.AreEqual(38 - 1, wallet.Balance);
            Assert.AreEqual(1, shop.Rerolls);
            Assert.AreEqual(38, shop.RerollPrice);
            Assert.AreEqual(Shop.PedestalCount, shop.Slots.Count);
            Assert.IsFalse(shop.Slots.Any(s => s.Sold));

            Assert.IsFalse(shop.TryReroll(wallet)); // 37 < 38
            Assert.AreEqual(1, shop.Rerolls);
        }

        [Test]
        public void Shop_MaxedItemsNotOffered()
        {
            // Relics are max level 1: owned relics never show up again.
            Inventory inv = With(ItemCatalog.BigChests, ItemCatalog.Dividends, ItemCatalog.SecondWind);
            for (int seed = 0; seed < 100; seed++)
            {
                var shop = new Shop(inv, 0, new Random(seed));
                Assert.IsFalse(shop.Slots.Any(s => s.Item.Category == ItemCategory.Relic), "seed " + seed);
            }
        }

        // --- run integration ---

        private static void Advance(RunStateMachine run, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.1f) run.Tick(0.1f);
        }

        [Test]
        public void Run_KillsAndClearsPayCrystals()
        {
            var run = new RunStateMachine(RunParams.Default, new Random(1));
            run.StartRun();
            Assert.AreEqual(0, run.Crystals.Balance);
            Advance(run, run.Params.IntroTime + 0.2f);
            run.ReportBotKilled(); // island 1: one bot -> cleared
            Assert.AreEqual(10 + 25, run.Crystals.Balance);
        }

        [Test]
        public void Run_ShopIsOpenBeforeIslandFive_AndBuysThroughTheRun()
        {
            var run = new RunStateMachine(RunParams.Default, new Random(2));
            run.StartRun();
            for (int island = 1; island <= 4; island++)
            {
                Advance(run, run.Params.IntroTime + 0.2f);
                if (run.Spec.Type == IslandType.Horde) Advance(run, run.Params.HordeTime + 0.5f);
                else for (int i = 0; i < run.Spec.BotCount; i++) run.ReportBotKilled();
                Advance(run, run.Params.ClearedTime + 0.2f);
                Assert.IsTrue(run.PickChestItem(0));
                if (island < 4) Assert.IsTrue(run.ChoosePortal(0));
            }

            Assert.AreEqual(RunPhase.Shop, run.Phase);
            Assert.IsNotNull(run.CurrentShop);
            Assert.AreEqual(Economy.Price(run.CurrentShop.Slots[0].Item, 4), run.CurrentShop.Slots[0].Price);

            run.Crystals.Add(10000);
            int before = run.Inventory.TotalLevels;
            int balance = run.Crystals.Balance;
            Assert.IsTrue(run.BuyShopItem(0));
            Assert.AreEqual(before + 1, run.Inventory.TotalLevels);
            Assert.AreEqual(balance - run.CurrentShop.Slots[0].Price, run.Crystals.Balance);
            Assert.IsTrue(run.RerollShop());

            Assert.IsTrue(run.LeaveShop());
            Assert.IsNull(run.CurrentShop);
            Assert.IsFalse(run.BuyShopItem(0));
            Assert.AreEqual(RunPhase.ChoosePortal, run.Phase);
        }

        [Test]
        public void Run_StartResetsCrystals()
        {
            var run = new RunStateMachine(RunParams.Default, new Random(1));
            run.StartRun();
            run.Crystals.Add(300);
            run.ReturnToMenu();
            run.StartRun();
            Assert.AreEqual(0, run.Crystals.Balance);
        }
    }
}
