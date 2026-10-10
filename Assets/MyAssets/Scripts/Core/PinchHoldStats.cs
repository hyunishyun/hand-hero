using System.Collections.Generic;

namespace HandHero.Core
{
    // Finished pinch holds of one run (D13): how long each was held and whether it
    // became a charge, so HoldDelay can be tuned from data (run telemetry, P13).
    // Since deep review DR-9 (second pass) also the aim hand's time per strength.
    public class PinchHoldStats
    {
        private readonly List<float> _durations = new List<float>(256);
        private readonly List<bool> _shots = new List<bool>(256);
        private readonly List<bool> _started = new List<bool>(256);
        private readonly List<float> _peak = new List<float>(256);
        private readonly List<float> _min = new List<float>(256);
        private readonly List<float> _releaseStrength = new List<float>(256);
        private readonly List<PinchReleaseBy> _releaseBy = new List<PinchReleaseBy>(256);
        private readonly List<bool> _metaSeen = new List<bool>(256);

        // Off outside a run (round 4, S3): Quick Match holds are not run telemetry,
        // so the lists never grow there. A new stats object records.
        public bool Recording { get; set; } = true;

        public int Holds => _durations.Count;
        public int ChargesStarted { get; private set; }
        public int ChargeShots { get; private set; }
        public IReadOnlyList<float> Durations => _durations;
        // Per hold, in the same order as Durations: did it fire a charge shot.
        public IReadOnlyList<bool> ChargeShotFlags => _shots;
        // Per hold: did a charge start (slowdown + orb), with or without a charge shot.
        public IReadOnlyList<bool> ChargeStartedFlags => _started;
        // Per hold (round 5, D2): the pinch that ended it (ASSIST); None for CURSOR
        // trigger holds and anything without pinch diagnostics. Unsmoothed strengths.
        public IReadOnlyList<float> PeakStrengths => _peak;
        public IReadOnlyList<float> MinStrengths => _min;
        public IReadOnlyList<float> ReleaseStrengths => _releaseStrength;
        public IReadOnlyList<PinchReleaseBy> ReleasedBy => _releaseBy;
        public IReadOnlyList<bool> MetaSeenFlags => _metaSeen;
        // Deep review DR-9, second pass: the aim hand's time per unsmoothed strength
        // (tracked ASSIST frames), so the resting thumb can be read from the run log.
        public PinchStrengthTime StrengthTime { get; } = new PinchStrengthTime();

        // Takes the step a held gesture ended on; any other step, or any step while
        // not recording, is ignored.
        public void Add(ChargeStep step)
        {
            Add(step, default);
        }

        // `release`: the pinch diagnostics of this frame (HandInputData.PinchRelease).
        public void Add(ChargeStep step, PinchRelease release)
        {
            if (!Recording || !step.HoldEnded) return;
            _durations.Add(step.HoldSeconds);
            _shots.Add(step.Released);
            _started.Add(step.ChargeStarted);
            _peak.Add(release.PeakStrength);
            _min.Add(release.MinStrength);
            _releaseStrength.Add(release.ReleaseStrength);
            _releaseBy.Add(release.By);
            _metaSeen.Add(release.MetaSeen);
            if (step.ChargeStarted) ChargesStarted++;
            if (step.Released) ChargeShots++;
        }

        // One aim-hand frame (PointingBeamController, ASSIST): `dt` real seconds at this
        // unsmoothed strength. Ignored while not recording, like the holds.
        public void AddStrength(float strength, float dt)
        {
            if (!Recording) return;
            StrengthTime.Add(strength, dt);
        }

        // Charge shots from holds shorter than this: likely meant as normal shots.
        public int QuickChargeShots(float maxHoldSeconds)
        {
            int n = 0;
            for (int i = 0; i < _durations.Count; i++)
                if (_shots[i] && _durations[i] < maxHoldSeconds) n++;
            return n;
        }

        public void Clear()
        {
            _durations.Clear();
            _shots.Clear();
            _started.Clear();
            _peak.Clear();
            _min.Clear();
            _releaseStrength.Clear();
            _releaseBy.Clear();
            _metaSeen.Clear();
            StrengthTime.Clear();
            ChargesStarted = 0;
            ChargeShots = 0;
        }
    }
}
