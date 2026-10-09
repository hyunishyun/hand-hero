namespace HandHero.Core
{
    // Flags frames whose real interval is over a threshold (freeze hunt, P1).
    // Feed it Time.realtimeSinceStartupAsDouble once per frame, never
    // Time.deltaTime: deltaTime stops at maximumDeltaTime and reads 0 while
    // Time.timeScale is 0, so a 2 s freeze would log as 333 ms or not at all.
    public class FrameSpikeDetector
    {
        private double _last = double.NaN;

        public float ThresholdMs { get; set; }

        // Interval in ms between the last two Step calls (0 on the first).
        public float LastFrameMs { get; private set; }

        public FrameSpikeDetector(float thresholdMs)
        {
            ThresholdMs = thresholdMs;
        }

        // Returns true when the interval since the previous call is a spike.
        public bool Step(double now)
        {
            if (double.IsNaN(_last))
            {
                _last = now;
                LastFrameMs = 0f;
                return false;
            }
            LastFrameMs = (float)((now - _last) * 1000.0);
            _last = now;
            return LastFrameMs > ThresholdMs;
        }

        public void Reset()
        {
            _last = double.NaN;
            LastFrameMs = 0f;
        }
    }

    // Small hitches (round 4, S1): round 3's device run dropped frames that were
    // well under the 50 ms spike threshold. Counts frames over a lower threshold
    // per flush and flags the first one after each phase change (and after the
    // start), so the log shows where a run of small hitches began.
    public class HitchCounter
    {
        private bool _armed = true;

        public float ThresholdMs { get; set; }
        public int Count { get; private set; }
        public float WorstMs { get; private set; }

        public HitchCounter(float thresholdMs)
        {
            ThresholdMs = thresholdMs;
        }

        // Feed it every frame's real interval. True = the first hitch since the
        // start or the last PhaseChanged: worth its own log record.
        public bool Step(float frameMs)
        {
            if (frameMs <= ThresholdMs) return false;
            Count++;
            if (frameMs > WorstMs) WorstMs = frameMs;
            if (!_armed) return false;
            _armed = false;
            return true;
        }

        public void PhaseChanged() => _armed = true;

        // After a flush; a phase change still waiting for its first hitch keeps waiting.
        public void ResetCounts()
        {
            Count = 0;
            WorstMs = 0f;
        }
    }

    // Edge detector for a tracked / focused / present flag. The first value is
    // only the baseline, so starting up never logs a fake "lost" or "found".
    public class TrackedEdge
    {
        private bool _known;
        private bool _value;

        // +1 = became true, -1 = became false, 0 = no change.
        public int Step(bool value)
        {
            if (!_known)
            {
                _known = true;
                _value = value;
                return 0;
            }
            if (value == _value) return 0;
            _value = value;
            return value ? 1 : -1;
        }
    }

    // When PerfSpikeLogger may write its buffer to disk: never mid-fight, where
    // a file write would add a hitch of its own.
    public static class PerfFlushPolicy
    {
        // The tutorial counts as combat too (the player is flying and shooting).
        public static bool InCombat(MatchPhase match, RunPhase run, bool paused)
        {
            if (paused) return false;
            return match == MatchPhase.Fight || match == MatchPhase.Tutorial
                || (match == MatchPhase.Run && run == RunPhase.Island);
        }

        public static bool PeriodicFlushDue(bool inCombat, double now, double lastFlush, double interval)
        {
            return !inCombat && now - lastFlush >= interval;
        }

        // The header carries the GPU warmup time (S1), so a routine flush before the
        // header waits for the warmup's few frames. Pause, focus loss and quit
        // (forced) write at once.
        public static bool WaitForWarmup(bool headerWritten, bool warmupRunning, bool forced)
        {
            return !headerWritten && warmupRunning && !forced;
        }
    }
}
