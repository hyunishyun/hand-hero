namespace HandHero.Core
{
    // Meta system gesture (palm toward the headset; CR-7). Its pinch opens the OS
    // menu, so the game reads that hand's pinch as open while the gesture is on.
    // A pinch still closed when the gesture ends reads open until it drops to the
    // reset threshold, so letting go of the system menu never fires a shot.
    public class SystemGestureGate
    {
        private bool _active;
        private bool _mustReopen;

        public bool Active => _active;
        // +1 = gesture started this step, -1 = ended, 0 = no change.
        public int Edge { get; private set; }

        public float Step(bool systemGesture, float pinchStrength, float resetThreshold)
        {
            Edge = systemGesture == _active ? 0 : systemGesture ? 1 : -1;
            _active = systemGesture;

            if (systemGesture)
            {
                _mustReopen = true;
                return 0f;
            }

            if (_mustReopen)
            {
                if (pinchStrength > resetThreshold) return 0f;
                _mustReopen = false;
            }
            return pinchStrength;
        }

        public void Reset()
        {
            _active = false;
            _mustReopen = false;
            Edge = 0;
        }
    }
}
