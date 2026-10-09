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
    }
}
