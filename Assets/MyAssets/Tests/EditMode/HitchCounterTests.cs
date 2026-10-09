using System.Text;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Round 4 (S1, D1): small hitches. Round 3's device log showed stale frames
    // under the 50 ms spike threshold, so PerfSpikeLogger also counts frames over
    // 25 ms per flush and records the first one after each phase change.
    public class HitchCounterTests
    {
        [Test]
        public void Counter_CountsOnlyFramesOverTheThreshold()
        {
            var hitches = new HitchCounter(25f);
            hitches.Step(14f);
            hitches.Step(25f); // exactly at the threshold is not a hitch, like the spike detector
            hitches.Step(31.2f);
            hitches.Step(27f);

            Assert.AreEqual(2, hitches.Count);
            Assert.AreEqual(31.2f, hitches.WorstMs, 0.001f);
        }

        [Test]
        public void Counter_ReportsTheFirstHitchAfterStartOnly()
        {
            var hitches = new HitchCounter(25f);

            Assert.IsFalse(hitches.Step(14f));
            Assert.IsTrue(hitches.Step(30f), "first hitch of the session");
            Assert.IsFalse(hitches.Step(40f), "later hitches are only counted");
        }

        [Test]
        public void Counter_ReportsTheFirstHitchAfterEachPhaseChange()
        {
            var hitches = new HitchCounter(25f);
            hitches.Step(30f);

            hitches.PhaseChanged();
            Assert.IsFalse(hitches.Step(20f), "a smooth frame keeps it armed");
            Assert.IsTrue(hitches.Step(26f));
            Assert.IsFalse(hitches.Step(26f));
        }

        [Test]
        public void Counter_ResetCountsKeepsThePendingFirstHitch()
        {
            var hitches = new HitchCounter(25f);
            hitches.Step(30f);
            hitches.PhaseChanged();

            hitches.ResetCounts(); // a flush in between
            Assert.AreEqual(0, hitches.Count);
            Assert.AreEqual(0f, hitches.WorstMs);
            Assert.IsTrue(hitches.Step(28f), "the phase change is still waiting for its first hitch");
            Assert.AreEqual(1, hitches.Count);
        }

        [Test]
        public void Counter_FollowsAThresholdChange()
        {
            var hitches = new HitchCounter(25f) { ThresholdMs = 40f };
            hitches.Step(30f);

            Assert.AreEqual(0, hitches.Count);
        }

        [Test]
        public void Formatter_NamesTheHitchRecord()
        {
            Assert.AreEqual("HITCH", PerfLogFormatter.KindName(PerfRecordKind.Hitch));
        }

        [Test]
        public void Formatter_FlushSummaryCarriesHitches()
        {
            var sb = new StringBuilder();
            PerfLogFormatter.AppendFlushSummary(sb, "menu", 4, 0, 2, 1, 138.5f, 3, 31.24f);

            Assert.AreEqual(
                "--- flush (menu) records=4 dropped=0 frames=2 spikes=1 worst=138.5ms hitches=3 worst_hitch=31.2ms\n",
                sb.ToString());
        }

        [Test]
        public void Policy_RoutineFlushesWaitForTheWarmupBeforeTheHeader()
        {
            // The header carries the warmup time, so the first routine flush waits for it.
            Assert.IsTrue(PerfFlushPolicy.WaitForWarmup(headerWritten: false, warmupRunning: true, forced: false));
            Assert.IsFalse(PerfFlushPolicy.WaitForWarmup(false, true, forced: true), "pause / focus loss / quit never wait");
            Assert.IsFalse(PerfFlushPolicy.WaitForWarmup(false, false, false), "warmup done");
            Assert.IsFalse(PerfFlushPolicy.WaitForWarmup(true, true, false), "header already written");
        }
    }
}
