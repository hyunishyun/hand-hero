namespace HandHero.Core
{
    // Normal-shot cooldown with a one-shot input buffer. A pull during the
    // cooldown is remembered and fires the moment the cooldown ends instead of
    // being dropped, so rapid pulls never feel "missed" (the fire rate is the same).
    // Extra pulls during one cooldown still buffer only one shot.
    public class ShotCooldown
    {
        private float _lastShot = float.NegativeInfinity;
        private bool _pending;

        // Returns true when a normal shot should fire now.
        public bool Step(bool requested, float now, float cooldown)
        {
            bool ready = now - _lastShot >= cooldown;
            if (requested && !ready)
            {
                _pending = true;
                return false;
            }
            if (!ready || !(requested || _pending)) return false;

            _pending = false;
            _lastShot = now;
            return true;
        }

        // Drops a buffered shot (aim hand lost, hero dead, input switched off).
        public void ClearPending()
        {
            _pending = false;
        }

        // Another shot went off (charge shot): restart the cooldown, drop the buffer.
        public void MarkFired(float now)
        {
            _lastShot = now;
            _pending = false;
        }
    }
}
