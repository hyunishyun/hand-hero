using System;
using System.Collections.Generic;
using System.Globalization;

namespace HandHero.Core
{
    // Deep review DR-9, second pass: where the aim-hand thumb rests, so the ASSIST
    // release can be retuned to a player (report section 2, A5).
    //
    // The release floor (0.75) and the re-arm sit just above the resting pointing
    // thumb they were tuned for (0.71 = 2.8 cm). A thumb that rests closer (r above
    // 0.75) makes light taps stick as charges, or never fire after a reopen. The
    // per-hold numbers cannot show r: a stuck hold ends only once 2 samples reach the
    // release level and the first of them counts as held, so its lowest strength while
    // held is at or under that level; taps that never fire leave no hold at all.
    // So the run log keeps the time the aim hand spent at each unsmoothed strength
    // (tracked ASSIST fight frames of a run, held or not, real seconds); the resting thumb is
    // where most of that time sits (run_summary.py, 0.50-0.95).
    //
    // Bins: the strength rounded to 0.01, from 0.50 to 1.00, plus one bin for
    // everything that rounds under 0.50 (an open hand).
    public class PinchStrengthTime
    {
        private const int LowestPercent = 50;
        // [0] = under 0.50, [1] = 0.50 ... [51] = 1.00.
        public const int Bins = 100 - LowestPercent + 2;

        private readonly float[] _seconds = new float[Bins];

        public IReadOnlyList<float> Seconds => _seconds;
        public float TotalSeconds { get; private set; }

        public void Add(float strength, float dt)
        {
            if (!(dt > 0f) || float.IsInfinity(dt) || float.IsNaN(strength)) return;
            _seconds[BinOf(strength)] += dt;
            TotalSeconds += dt;
        }

        public static int BinOf(float strength)
        {
            int percent = (int)Math.Floor(strength * 100f + 0.5f);
            if (percent < LowestPercent) return 0;
            if (percent > 100) percent = 100;
            return percent - LowestPercent + 1;
        }

        // Run-log key of a bin (RunRecordJson "pinch_strength_s"): "<0.50", "0.50" ... "1.00".
        public static string Label(int bin)
        {
            if (bin <= 0) return "<0.50";
            int percent = LowestPercent + Math.Min(bin, Bins - 1) - 1;
            return (percent / 100).ToString(CultureInfo.InvariantCulture) + "." +
                   (percent % 100).ToString("00", CultureInfo.InvariantCulture);
        }

        public void Clear()
        {
            Array.Clear(_seconds, 0, _seconds.Length);
            TotalSeconds = 0f;
        }
    }
}
