using System.Collections.Generic;

namespace HandHero.Core
{
    // Finished pinch holds of one run (D13): how long each was held and whether it
    // became a charge, so HoldDelay can be tuned from data (run telemetry, P13).
    public class PinchHoldStats
    {
        private readonly List<float> _durations = new List<float>(256);
        private readonly List<bool> _shots = new List<bool>(256);
        private readonly List<bool> _started = new List<bool>(256);

        public int Holds => _durations.Count;
        public int ChargesStarted { get; private set; }
        public int ChargeShots { get; private set; }
        public IReadOnlyList<float> Durations => _durations;
        // Per hold, in the same order as Durations: did it fire a charge shot.
        public IReadOnlyList<bool> ChargeShotFlags => _shots;
        // Per hold: did a charge start (slowdown + orb), with or without a charge shot.
        public IReadOnlyList<bool> ChargeStartedFlags => _started;

        // Takes the step a held gesture ended on; any other step is ignored.
        public void Add(ChargeStep step)
        {
            if (!step.HoldEnded) return;
            _durations.Add(step.HoldSeconds);
            _shots.Add(step.Released);
            _started.Add(step.ChargeStarted);
            if (step.ChargeStarted) ChargesStarted++;
            if (step.Released) ChargeShots++;
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
            ChargesStarted = 0;
            ChargeShots = 0;
        }
    }
}
