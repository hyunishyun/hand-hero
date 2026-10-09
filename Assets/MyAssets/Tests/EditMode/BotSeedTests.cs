using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Freeze hunt (P6, D9): bots spawned together must not move and fire in
    // lock-step. Each spawn gets its own seed from the run, and the first shot
    // after a reset is jittered to FireInterval x U(0.5, 1.0).
    public class BotSeedTests
    {
        [Test]
        public void Reset_FirstShotDelayIsHalfToFullInterval()
        {
            BotParams p = BotParams.Default;
            p.FireInterval = 2f;
            for (int seed = 1; seed <= 50; seed++)
            {
                var brain = new BotBrain(p, seed);
                Assert.That(brain.TimeToNextShot, Is.InRange(1f, 2f));
            }
        }

        [Test]
        public void Reset_FirstShotDelayVariesAcrossSeeds()
        {
            BotParams p = BotParams.Default;
            var delays = new HashSet<float>();
            for (int seed = 1; seed <= 10; seed++) delays.Add(new BotBrain(p, seed).TimeToNextShot);
            Assert.Greater(delays.Count, 5);
        }

        [Test]
        public void FirstShotDelay_MapsUnitRandomToHalfThroughFull()
        {
            Assert.AreEqual(1f, BotBrain.FirstShotDelay(2f, 0.0), 1e-5f);
            Assert.AreEqual(1.5f, BotBrain.FirstShotDelay(2f, 0.5), 1e-5f);
            Assert.AreEqual(2f, BotBrain.FirstShotDelay(2f, 1.0), 1e-5f);
        }

        [Test]
        public void Reseed_BehavesLikeANewBrainWithThatSeed()
        {
            BotParams p = BotParams.Default;
            var reused = new BotBrain(p, 1);
            reused.Step(UnityEngine.Vector3.zero, false, UnityEngine.Vector3.zero,
                new ArenaBounds(UnityEngine.Vector3.zero, new UnityEngine.Vector3(10f, 10f, 10f)), 0.1f);
            reused.Reseed(77);
            var fresh = new BotBrain(p, 77);
            Assert.AreEqual(fresh.TimeToNextShot, reused.TimeToNextShot);
            Assert.AreEqual(BotState.Idle, reused.State);
        }

        [Test]
        public void SpawnSeeds_AreDeterministicPerRunSeedAndNonZero()
        {
            var a = new System.Random(123);
            var b = new System.Random(123);
            var seen = new HashSet<int>();
            for (int i = 0; i < 20; i++)
            {
                int s = BotBrain.SpawnSeed(a);
                Assert.AreEqual(s, BotBrain.SpawnSeed(b));
                Assert.AreNotEqual(0, s, "0 means 'random' on BotInputSource");
                seen.Add(s);
            }
            Assert.AreEqual(20, seen.Count, "two bots of one run never share a seed");
        }
    }
}
