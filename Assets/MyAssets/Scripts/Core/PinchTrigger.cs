namespace HandHero.Core
{
    public struct PinchState
    {
        // True for exactly one step per pinch (fire a normal shot).
        public bool FireTriggered;
        // Pinch is closed (charge shot hold).
        public bool Held;
    }

    // Aim-hand pinch: fire edge + hold level with hysteresis (ADR 8).
    // A closed fist brings thumb and index tips close enough to read as a pinch,
    // so while the aim-hand fist is held (CURSOR aim drag), and for a short time
    // after it opens, the pinch is ignored. A pinch still closed when that ends
    // must open once before it can fire.
    public class PinchTrigger
    {
        private HysteresisGate _gate;
        private float _suppressTimer;
        private bool _mustReopen;

        public PinchState Step(bool tracked, float pinchStrength, float fireThreshold, float resetThreshold,
            bool fistHeld, float fistSuppressTime, float dt)
        {
            if (!tracked)
            {
                Reset();
                return default;
            }

            if (fistHeld) _suppressTimer = fistSuppressTime;
            else if (_suppressTimer > 0f) _suppressTimer -= dt;

            if (fistHeld || _suppressTimer > 0f)
            {
                _gate.Reset();
                _mustReopen = true;
                return default;
            }

            if (_mustReopen)
            {
                if (pinchStrength > resetThreshold) return default;
                _mustReopen = false;
            }

            bool fired = _gate.Step(pinchStrength, fireThreshold, resetThreshold) == GateEdge.Rising;
            return new PinchState { FireTriggered = fired, Held = _gate.IsOn };
        }

        public void Reset()
        {
            _gate.Reset();
            _suppressTimer = 0f;
            _mustReopen = false;
        }
    }
}
