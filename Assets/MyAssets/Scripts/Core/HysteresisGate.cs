namespace HandHero.Core
{
    public enum GateEdge
    {
        None,
        Rising,  // just turned on this step
        Falling, // just turned off this step
    }

    // Two-threshold on/off gate for analog gesture values (fist, pinch, ...).
    // Turns on at value >= onThreshold, off at value <= offThreshold. Values in
    // between keep the current state, so a hand hovering at a boundary never flickers
    // (ADR 8). Thresholds are passed per step so inspector tuning applies live.
    public struct HysteresisGate
    {
        public bool IsOn { get; private set; }

        public GateEdge Step(float value, float onThreshold, float offThreshold)
        {
            if (!IsOn && value >= onThreshold)
            {
                IsOn = true;
                return GateEdge.Rising;
            }
            if (IsOn && value <= offThreshold)
            {
                IsOn = false;
                return GateEdge.Falling;
            }
            return GateEdge.None;
        }

        // Forces the gate off (e.g. tracking loss). Returns Falling if it was on.
        public GateEdge Reset()
        {
            if (!IsOn) return GateEdge.None;
            IsOn = false;
            return GateEdge.Falling;
        }
    }
}
