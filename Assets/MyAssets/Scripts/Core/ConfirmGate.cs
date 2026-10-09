namespace HandHero.Core
{
    // Two-press confirm for destructive buttons (round 4, S5: RESET PROGRESS).
    // The first press arms the gate for `window` seconds; a second press inside
    // the window confirms. Times are the caller's clock (unscaled: the pause
    // panel runs with Time.timeScale = 0).
    public class ConfirmGate
    {
        private float _armedUntil = float.NegativeInfinity;

        public ConfirmGate(float window)
        {
            Window = window;
        }

        public float Window { get; set; }

        public bool IsArmed(float now) => now <= _armedUntil;

        // True = confirmed (and disarmed); false = armed by this press.
        public bool Press(float now)
        {
            if (IsArmed(now))
            {
                Cancel();
                return true;
            }
            _armedUntil = now + Window;
            return false;
        }

        public void Cancel() => _armedUntil = float.NegativeInfinity;
    }
}
