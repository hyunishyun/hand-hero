using UnityEngine;

namespace HandHero.Core
{
    // Input-side half of the puppeteer clutch: turns an analog fist strength and
    // palm position into ClutchHeld + ClutchDelta for HandInputData.
    // Hysteresis on the fist (ADR 8); tracking loss opens the clutch.
    public class HandClutchSampler
    {
        private HysteresisGate _gate;
        private Vector3 _lastPalmPosition;

        public bool IsHeld => _gate.IsOn;

        public bool Step(bool handTracked, float fistStrength, Vector3 palmPosition,
            float grabThreshold, float releaseThreshold, out Vector3 delta)
        {
            delta = Vector3.zero;

            if (!handTracked)
            {
                _gate.Reset();
                return false;
            }

            if (_gate.Step(fistStrength, grabThreshold, releaseThreshold) == GateEdge.Rising)
                _lastPalmPosition = palmPosition;

            if (_gate.IsOn)
            {
                delta = palmPosition - _lastPalmPosition;
                _lastPalmPosition = palmPosition;
            }

            return _gate.IsOn;
        }
    }
}
