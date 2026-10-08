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
    // closes both at once — so a pull that starts within gripSettleTime of a grip
    // change is ignored and that curled index must open before it can fire. A pull
    // already held when the grip changes stays held (relaxing the grip mid-charge
    // must not release the charge shot). Firing does not require the grip.
    public class TriggerGesture
    {
        private HysteresisGate _grip;
        private HysteresisGate _index;
        private float _settle;
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

            if (_grip.Step(gripStrength, gripOnThreshold, gripOffThreshold) != GateEdge.None)
                _settle = gripSettleTime;
            else if (_settle > 0f)
                _settle -= dt;

            if (_mustReopen)
            {
                if (indexCurl > releaseThreshold) return default;
                _mustReopen = false;
            }

            GateEdge edge = _index.Step(indexCurl, pullThreshold, releaseThreshold);
            if (edge == GateEdge.Rising && _settle > 0f)
            {
                RequireReopen();
                return default;
            }

            return new TriggerState { Fired = edge == GateEdge.Rising, Held = _index.IsOn };
        }

        // Drops a pull in progress; a curled index must open before it fires or holds
        // (tracking loss, input source switched back on after pause).
        public void RequireReopen()
        {
            _index.Reset();
            _mustReopen = true;
        }
    }
}
