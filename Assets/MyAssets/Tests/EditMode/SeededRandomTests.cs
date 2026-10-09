using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Round 4 (S3): a pooled bot keeps one random stream and reseeds it per
    // spawn instead of allocating a System.Random each time.
    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextDouble(), b.NextDouble());
        }

        [Test]
        public void Reseed_RestartsLikeANewStream()
        {
            var reused = new SeededRandom(1);
            for (int i = 0; i < 17; i++) reused.NextDouble();

            reused.Reseed(77);
            var fresh = new SeededRandom(77);
            for (int i = 0; i < 100; i++) Assert.AreEqual(fresh.NextDouble(), reused.NextDouble());
        }

        [Test]
        public void NextDouble_IsInUnitInterval()
        {
            var r = new SeededRandom(3);
            for (int i = 0; i < 10000; i++)
            {
                double u = r.NextDouble();
                Assert.That(u, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }

        [Test]
        public void NearbySeeds_StartDifferently()
        {
            var firsts = new HashSet<double>();
            for (int seed = 1; seed <= 20; seed++) firsts.Add(new SeededRandom(seed).NextDouble());
            Assert.AreEqual(20, firsts.Count);
        }

        [Test]
        public void NextDouble_SpreadsEvenly()
        {
            var r = new SeededRandom(9);
            var buckets = new int[10];
            const int n = 20000;
            for (int i = 0; i < n; i++) buckets[(int)(r.NextDouble() * 10)]++;
            foreach (int count in buckets) Assert.That(count, Is.InRange(n / 10 * 0.9, n / 10 * 1.1));
        }
    }
}
