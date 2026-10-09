using System.Text;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HandHero.Tests
{
    // Freeze hunt (round 3, P1): the frame spike detector, the record ring
    // buffer, the line formatter and the flush rules behind PerfSpikeLogger.
    public class PerfLogTests
    {
        // ---- FrameSpikeDetector ----

        [Test]
        public void Detector_FirstFrame_IsNeverASpike()
        {
            var detector = new FrameSpikeDetector(50f);
            Assert.IsFalse(detector.Step(100.0));
            Assert.AreEqual(0f, detector.LastFrameMs);
        }

        [Test]
        public void Detector_MeasuresRealIntervals()
        {
            var detector = new FrameSpikeDetector(50f);
            detector.Step(10.0);

            Assert.IsFalse(detector.Step(10.011));
            Assert.AreEqual(11f, detector.LastFrameMs, 0.01f);
        }

        [Test]
        public void Detector_FlagsFramesOverThreshold()
        {
            var detector = new FrameSpikeDetector(50f);
            detector.Step(10.0);

            Assert.IsFalse(detector.Step(10.050), "exactly at the threshold is not a spike");
            Assert.IsTrue(detector.Step(10.151));
            Assert.AreEqual(101f, detector.LastFrameMs, 0.01f);
        }

        [Test]
        public void Detector_IsNotCappedLikeDeltaTime()
        {
            // Time.deltaTime stops at maximumDeltaTime (0.333 s); a 2 s freeze must read as 2000 ms.
            var detector = new FrameSpikeDetector(50f);
            detector.Step(5.0);

            Assert.IsTrue(detector.Step(7.0));
            Assert.AreEqual(2000f, detector.LastFrameMs, 0.01f);
        }

        [Test]
        public void Detector_Reset_ForgetsTheLastFrame()
        {
            var detector = new FrameSpikeDetector(50f);
            detector.Step(1.0);
            detector.Reset();

            Assert.IsFalse(detector.Step(9.0), "first frame after a reset");
        }

        // ---- RingBuffer ----

        [Test]
        public void Ring_KeepsItemsOldestFirst()
        {
            var ring = new RingBuffer<int>(4);
            ring.Add(1);
            ring.Add(2);
            ring.Add(3);

            Assert.AreEqual(3, ring.Count);
            Assert.AreEqual(1, ring[0]);
            Assert.AreEqual(3, ring[2]);
            Assert.AreEqual(0, ring.Dropped);
        }

        [Test]
        public void Ring_WrapsAroundAndCountsDropped()
        {
            var ring = new RingBuffer<int>(3);
            for (int i = 1; i <= 7; i++) ring.Add(i);

            Assert.AreEqual(3, ring.Count);
            Assert.AreEqual(5, ring[0], "oldest kept");
            Assert.AreEqual(6, ring[1]);
            Assert.AreEqual(7, ring[2], "newest");
            Assert.AreEqual(4, ring.Dropped);
        }

        [Test]
        public void Ring_Clear_EmptiesAndResetsDropped()
        {
            var ring = new RingBuffer<int>(2);
            ring.Add(1);
            ring.Add(2);
            ring.Add(3);
            ring.Clear();

            Assert.AreEqual(0, ring.Count);
            Assert.AreEqual(0, ring.Dropped);
            ring.Add(9);
            Assert.AreEqual(9, ring[0]);
        }

        [Test]
        public void Ring_IndexOutOfRange_Throws()
        {
            var ring = new RingBuffer<int>(2);
            ring.Add(1);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => { int _ = ring[1]; });
        }

        // ---- TrackedEdge ----

        [Test]
        public void Edge_FirstObservationIsTheBaseline()
        {
            var edge = new TrackedEdge();
            Assert.AreEqual(0, edge.Step(true));
            Assert.AreEqual(0, edge.Step(true));
        }

        [Test]
        public void Edge_ReportsLostAndFound()
        {
            var edge = new TrackedEdge();
            edge.Step(true);

            Assert.AreEqual(-1, edge.Step(false));
            Assert.AreEqual(0, edge.Step(false));
            Assert.AreEqual(1, edge.Step(true));
        }

        // ---- PerfLogFormatter ----

        private static PerfSample SpikeSample()
        {
            return new PerfSample
            {
                Kind = PerfRecordKind.Spike,
                Time = 1234.5678,
                FrameMs = 87.34f,
                CpuMs = 12.06f,
                RenderMs = 3.2f,
                GpuMs = -1f,
                GcCollections = 1,
                HeapBytes = 47395635, // 45.2 MB
                Match = MatchPhase.Run,
                MatchPaused = false,
                Run = RunPhase.Island,
                Island = 3,
                BotsAlive = 2,
                TimeScale = 1f,
                Focused = true,
                LeftTracked = true,
                RightTracked = false,
                HeadTracked = true,
            };
        }

        [Test]
        public void Formatter_WritesOneFullLine()
        {
            var sb = new StringBuilder();
            // 21:04:00.000 at realtime 0, so t = 1234.5678 s is 21:24:34.567.
            double clockAtZero = 21 * 3600 + 4 * 60;

            PerfLogFormatter.AppendLine(sb, SpikeSample(), clockAtZero);

            Assert.AreEqual(
                "21:24:34.567 t=1234.568 SPIKE frame=87.3 cpu=12.1 rt=3.2 gpu=-1.0 gc=+1 heap=45.2MB " +
                "match=Run run=Island isl=3 bots=2 ts=1.00 focus=1 L=1 R=0 head=1\n",
                sb.ToString());
        }

        [Test]
        public void Formatter_MarksGamePauseAndHandDetail()
        {
            var sb = new StringBuilder();
            PerfSample s = SpikeSample();
            s.Kind = PerfRecordKind.HandLost;
            s.Detail = 1;
            s.MatchPaused = true;
            s.TimeScale = 0f;

            PerfLogFormatter.AppendLine(sb, s, 0.0);

            StringAssert.Contains(" HAND_LOST hand=R ", sb.ToString());
            StringAssert.Contains(" match=Run(paused) ", sb.ToString());
            StringAssert.Contains(" ts=0.00 ", sb.ToString());
        }

        [Test]
        public void Formatter_WrapsClockPastMidnight()
        {
            var sb = new StringBuilder();
            PerfSample s = SpikeSample();
            s.Time = 30.0;

            PerfLogFormatter.AppendLine(sb, s, 86400.0 - 10.0); // started at 23:59:50

            StringAssert.StartsWith("00:00:20.000 ", sb.ToString());
        }

        [Test]
        public void Formatter_HandlesNegativeClockOffset()
        {
            var sb = new StringBuilder();
            PerfSample s = SpikeSample();
            s.Time = 100.0;

            PerfLogFormatter.AppendLine(sb, s, -40.0); // realtime 0 was 40 s before midnight

            StringAssert.StartsWith("00:01:00.000 ", sb.ToString());
        }

        [Test]
        public void Formatter_AppendFixed_RoundsAndPads()
        {
            var sb = new StringBuilder();
            PerfLogFormatter.AppendFixed(sb, 3.05f, 2);
            sb.Append(' ');
            PerfLogFormatter.AppendFixed(sb, -0.04f, 1);
            sb.Append(' ');
            PerfLogFormatter.AppendFixed(sb, 120f, 0);
            sb.Append(' ');
            PerfLogFormatter.AppendFixed(sb, 0.999f, 2);

            Assert.AreEqual("3.05 -0.0 120 1.00", sb.ToString());
        }

        [Test]
        public void Formatter_DoesNotAllocateAfterWarmUp()
        {
            var sb = new StringBuilder(4096);
            PerfSample s = SpikeSample();
            PerfLogFormatter.AppendLine(sb, s, 0.0); // warm up the name tables
            sb.Clear();

            Assert.That(() =>
            {
                for (int i = 0; i < 20; i++) PerfLogFormatter.AppendLine(sb, s, 0.0);
                sb.Clear();
            }, Is.Not.AllocatingGCMemory());
        }

        // ---- PerfFlushPolicy ----

        [Test]
        public void Policy_CombatIsFightTutorialAndRunIslands()
        {
            Assert.IsTrue(PerfFlushPolicy.InCombat(MatchPhase.Fight, RunPhase.Idle, false));
            Assert.IsTrue(PerfFlushPolicy.InCombat(MatchPhase.Tutorial, RunPhase.Idle, false));
            Assert.IsTrue(PerfFlushPolicy.InCombat(MatchPhase.Run, RunPhase.Island, false));

            Assert.IsFalse(PerfFlushPolicy.InCombat(MatchPhase.Run, RunPhase.Shop, false));
            Assert.IsFalse(PerfFlushPolicy.InCombat(MatchPhase.Menu, RunPhase.Idle, false));
            Assert.IsFalse(PerfFlushPolicy.InCombat(MatchPhase.Countdown, RunPhase.Idle, false));
            Assert.IsFalse(PerfFlushPolicy.InCombat(MatchPhase.Fight, RunPhase.Idle, true), "paused");
        }

        [Test]
        public void Policy_PeriodicFlushOnlyOutOfCombatAfterInterval()
        {
            Assert.IsFalse(PerfFlushPolicy.PeriodicFlushDue(false, 15.0, 10.0, 10.0));
            Assert.IsTrue(PerfFlushPolicy.PeriodicFlushDue(false, 20.0, 10.0, 10.0));
            Assert.IsFalse(PerfFlushPolicy.PeriodicFlushDue(true, 60.0, 10.0, 10.0), "never mid-fight");
        }
    }
}
