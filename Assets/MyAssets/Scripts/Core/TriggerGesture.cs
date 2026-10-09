namespace HandHero.Core
{
    public struct TriggerState
    {
        // True for exactly one step per pull (fire a normal shot).
        public bool Fired;
        // Index finger is pulled (charge shot hold).
        public bool Held;
    }

    // CURSOR aim fire: a gun grip. Middle/ring/little hold the aim marker, the
    // index finger is the trigger, both with hysteresis (ADR 8). Measured on Quest
    // (2026-10-08 spike) the grip and the index read independently, but a full fist
    // closes both at once, in either order. So:
    //  - a pull within gripSettleTime after a grip change is ignored;
    //  - a pull without the grip waits gripSettleTime before it fires, and is
    //    dropped if the grip closes meanwhile (the index led a full fist);
    //  - a pull from a settled gun grip fires at once.
    // A dropped pull must open before it can fire. A pull already held when the
    // grip changes stays held (relaxing the grip mid-charge must not release the
    // charge shot). Firing does not require the grip.
    public class TriggerGesture
    {
        private HysteresisGate _grip;
        private HysteresisGate _index;
        private float _settle;
        private float _pending;
        private bool _mustReopen;

        public TriggerState Step(bool tracked, float indexCurl, float gripStrength,
            float pullThreshold, float releaseThreshold, float gripOnThreshold, float gripOffThreshold,
            float gripSettleTime, float dt)
        {
            if (!tracked)
            {
                RequireReopen();
                _grip.Reset();
                _settle = 0f;
                return default;
            }

            GateEdge gripEdge = _grip.Step(gripStrength, gripOnThreshold, gripOffThreshold);
            if (gripEdge != GateEdge.None)
                _settle = gripSettleTime;
            else if (_settle > 0f)
                _settle -= dt;

            if (_mustReopen)
            {
                if (indexCurl > releaseThreshold) return default;
                _mustReopen = false;
            }

            GateEdge edge = _index.Step(indexCurl, pullThreshold, releaseThreshold);
            if (edge == GateEdge.Rising)
            {
                if (_settle > 0f)
                {
                    RequireReopen(); // grip just changed: a full fist
                    return default;
                }
                if (!_grip.IsOn && gripSettleTime > 0f)
                {
                    _pending = gripSettleTime; // wait: the grip may still be closing
                    return default;
                }
                return new TriggerState { Fired = true, Held = true };
            }

            if (_pending > 0f)
            {
                if (!_index.IsOn)
                {
                    _pending = 0f; // let go before it counted
                    return default;
                }
                if (gripEdge == GateEdge.Rising)
                {
                    RequireReopen(); // the index led a full fist
                    return default;
                }
                _pending -= dt;
                if (_pending > 0f) return default;
                return new TriggerState { Fired = true, Held = true };
            }

            return new TriggerState { Fired = false, Held = _index.IsOn };
        }

        // Drops a pull in progress; a curled index must open before it fires or holds
        // (tracking loss, input source switched back on after pause).
        public void RequireReopen()
        {
            _index.Reset();
            _pending = 0f;
            _mustReopen = true;
        }
    }
}
