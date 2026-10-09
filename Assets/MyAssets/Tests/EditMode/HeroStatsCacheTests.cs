using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Stats folded from the inventory only when it changes (round 3, P3 / GM-9, RF-6).
    public class HeroStatsCacheTests
    {
        [Test]
        public void NoInventory_IsNeutral()
        {
            var cache = new HeroStatsCache();
            Assert.AreEqual(HeroStats.Neutral, cache.Get(null));
        }

        [Test]
        public void FollowsItemsAdded()
        {
            var cache = new HeroStatsCache();
            var inv = new Inventory(ItemCatalog.Get);
            Assert.AreEqual(HeroStats.Neutral, cache.Get(inv));
            inv.Add(ItemCatalog.FocusLens);
            Assert.AreEqual(HeroStats.From(inv), cache.Get(inv));
            Assert.AreNotEqual(HeroStats.Neutral, cache.Get(inv));
        }

        [Test]
        public void ReplacedInventoryWithSameLevelCount_Recomputes()
        {
            var cache = new HeroStatsCache();
            var first = new Inventory(ItemCatalog.Get);
            first.Add(ItemCatalog.FocusLens);
            cache.Get(first);

            var second = new Inventory(ItemCatalog.Get); // a new run with one other item: same TotalLevels
            second.Add(ItemCatalog.HullPlating);
            Assert.AreEqual(HeroStats.From(second), cache.Get(second));
            Assert.AreNotEqual(HeroStats.From(first), cache.Get(second));
        }

        [Test]
        public void Invalidate_ForgetsCachedStats()
        {
            var cache = new HeroStatsCache();
            var inv = new Inventory(ItemCatalog.Get);
            inv.Add(ItemCatalog.FocusLens);
            cache.Get(inv);
            cache.Invalidate();
            Assert.AreEqual(HeroStats.From(inv), cache.Get(inv));
        }

        [Test]
        public void RunStateMachineStats_FollowPickedItems()
        {
            var run = new RunStateMachine(RunParams.Default, new System.Random(1));
            Assert.AreEqual(HeroStats.Neutral, run.Stats);
            run.Inventory.Add(ItemCatalog.FocusLens);
            Assert.AreEqual(HeroStats.From(run.Inventory), run.Stats);
        }
    }
}
