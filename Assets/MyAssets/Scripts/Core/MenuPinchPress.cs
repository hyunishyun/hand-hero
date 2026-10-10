namespace HandHero.Core
{
    // Point-and-pinch menu press (HandMenuPointer, T7): one press per pinch, on the
    // step the strength rises through the press threshold, with hysteresis (ADR 8).
    // A pinch already closed when counting starts must open to the release
    // threshold before it can press: a panel shown under a held pinch and, since
    // deep review DR-8, a hand back from a tracking loss. A pinch held through a
    // short dropout came back still closed (the tracker snaps the strength to the
    // raw value) with the ray on the same button and pressed it again: RESET
    // PROGRESS confirmed itself, REROLL rerolled and paid twice.
    public class MenuPinchPress
    {
        private HysteresisGate _gate;
        private bool _waitForOpen;

        public bool IsPinched => _gate.IsOn;
        public bool WaitingForOpen => _waitForOpen;

        // Drops any pinch in progress; one still closed must open before it presses.
        public void RequireReopen()
        {
            _gate.Reset();
            _waitForOpen = true;
        }

        // Returns true on the step the pinch presses. tracked = false: no hand ray this frame.
        public bool Step(bool tracked, float pinchStrength, float pressThreshold, float releaseThreshold)
        {
            if (!tracked)
            {
                // Tracking lost: a pinch in progress ends here, never later somewhere
                // else, and one still closed when the hand is back must open first.
                RequireReopen();
                return false;
            }

            if (_waitForOpen && pinchStrength <= releaseThreshold) _waitForOpen = false;
            return _gate.Step(pinchStrength, pressThreshold, releaseThreshold) == GateEdge.Rising && !_waitForOpen;
        }
    }
}
