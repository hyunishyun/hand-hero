using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Freeze hunt (P5): the reuse contract behind the BeamImpact and RunBot pools.
    public class ObjectPoolTests
    {
        private class Item
        {
            public bool Active;
            public int Gets;
            public int Releases;
        }

        private static ObjectPool<Item> MakePool(int maxSize, List<Item> created = null)
        {
            return new ObjectPool<Item>(
                () => { var i = new Item(); created?.Add(i); return i; },
                maxSize,
                i => { i.Active = true; i.Gets++; },
                i => { i.Active = false; i.Releases++; });
        }

        [Test]
        public void Prewarm_CreatesInactiveItems()
        {
            var created = new List<Item>();
            var pool = MakePool(8, created);
            pool.Prewarm(3);
            Assert.AreEqual(3, created.Count);
            Assert.AreEqual(3, pool.CountAll);
            Assert.AreEqual(3, pool.CountInactive);
            Assert.AreEqual(0, pool.CountActive);
            foreach (Item i in created)
            {
                Assert.IsFalse(i.Active);
                Assert.AreEqual(1, i.Releases, "prewarm puts each item in the released state");
            }
        }

        [Test]
        public void Prewarm_NeverExceedsMaxSize()
        {
            var pool = MakePool(4);
            pool.Prewarm(10);
            Assert.AreEqual(4, pool.CountAll);
        }

        [Test]
        public void Get_ReusesReleasedItemBeforeCreating()
        {
            var created = new List<Item>();
            var pool = MakePool(8, created);
            pool.Prewarm(1);
            Item a = pool.Get();
            Assert.AreSame(created[0], a);
            Assert.IsTrue(a.Active);
            Assert.AreEqual(1, created.Count);

            Assert.IsTrue(pool.Release(a));
            Item b = pool.Get();
            Assert.AreSame(a, b);
            Assert.AreEqual(1, created.Count);
            Assert.AreEqual(2, b.Gets);
        }

        [Test]
        public void Get_GrowsUpToMaxSizeThenReturnsNull()
        {
            var pool = MakePool(2);
            Item a = pool.Get();
            Item b = pool.Get();
            Assert.IsNotNull(a);
            Assert.IsNotNull(b);
            Assert.AreNotSame(a, b);
            Assert.IsNull(pool.Get(), "at the cap the caller skips the effect");
            Assert.AreEqual(2, pool.CountActive);
        }

        [Test]
        public void Release_TwiceIsIgnored()
        {
            var pool = MakePool(4);
            Item a = pool.Get();
            Assert.IsTrue(pool.Release(a));
            Assert.IsFalse(pool.Release(a));
            Assert.AreEqual(1, a.Releases);
            Assert.AreEqual(1, pool.CountInactive);

            // The same item must not come back twice.
            Item first = pool.Get();
            Item second = pool.Get();
            Assert.AreNotSame(first, second);
        }

        [Test]
        public void Release_ForeignOrNullItemIsIgnored()
        {
            var pool = MakePool(4);
            Assert.IsFalse(pool.Release(new Item()));
            Assert.IsFalse(pool.Release(null));
            Assert.AreEqual(0, pool.CountAll);
            Assert.AreEqual(0, pool.CountInactive);
        }

        [Test]
        public void Counts_TrackActiveAndInactive()
        {
            var pool = MakePool(4);
            pool.Prewarm(2);
            Item a = pool.Get();
            Item b = pool.Get();
            Item c = pool.Get();
            Assert.AreEqual(3, pool.CountAll);
            Assert.AreEqual(3, pool.CountActive);
            pool.Release(b);
            Assert.AreEqual(2, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);
            Assert.IsNotNull(a);
            Assert.IsNotNull(c);
        }

        [Test]
        public void ReleaseAll_ReturnsEveryActiveItem()
        {
            var pool = MakePool(4);
            Item a = pool.Get();
            Item b = pool.Get();
            pool.ReleaseAll();
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(2, pool.CountInactive);
            Assert.IsFalse(a.Active);
            Assert.IsFalse(b.Active);
        }

        [Test]
        public void Constructor_MaxSizeBelowOneIsTreatedAsOne()
        {
            var pool = MakePool(0);
            Assert.IsNotNull(pool.Get());
            Assert.IsNull(pool.Get());
        }
    }
}
