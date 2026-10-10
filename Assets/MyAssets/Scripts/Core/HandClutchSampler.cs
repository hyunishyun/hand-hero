using UnityEngine;

namespace HandHero.Core
{
    // Input-side half of the puppeteer clutch: turns an analog fist strength and
    // palm position into ClutchHeld + ClutchDelta for HandInputData.
    // Hysteresis on the fist (ADR 8); tracking loss opens the clutch.
    //
    // Deep review DR-10: with a lost grace, a clutch held when the hand drops out
    // stays held (zero delta) for up to that long. A hand back inside the grace is
    // re-anchored where it is (no jump) and keeps the clutch if the fist is still
    // closed, so ClutchMapper keeps its target: a 1-2 frame dropout used to release
    // and regrab from the hero's position, which threw away the target's lead (up to
    // 40% of a drag). A longer loss opens the clutch as before (the hero glides).
    public class HandClutchSampler
    {
        private HysteresisGate _gate;
        private Vector3 _lastPalmPosition;
        private float _lostTime;
        private bool _lostWhileHeld;

        public bool IsHeld => _gate.IsOn;

        // Input switched back on (BR-2): a fist still closed regrabs from where
        // the hand is now instead of dragging by the gap.
        public void Reset()
        {
            _gate.Reset();
            _lostTime = 0f;
            _lostWhileHeld = false;
        }

        // No grace: the first untracked sample opens the clutch (rounds 1-5).
        public bool Step(bool handTracked, float fistStrength, Vector3 palmPosition,
            float grabThreshold, float releaseThreshold, out Vector3 delta)
            => Step(handTracked, fistStrength, palmPosition, grabThreshold, releaseThreshold, 0f, 0f, out delta);

        // lostGraceTime: seconds a held clutch survives while the hand is untracked
        // (0 = opens on the first untracked sample). dt: seconds since the last step.
        public bool Step(bool handTracked, float fistStrength, Vector3 palmPosition,
            float grabThreshold, float releaseThreshold, float lostGraceTime, float dt, out Vector3 delta)
        {
            delta = Vector3.zero;

            if (!handTracked)
            {
                if (!_gate.IsOn) return false;
                _lostTime += dt;
                if (lostGraceTime <= 0f || _lostTime > lostGraceTime)
                {
                    Reset();
                    return false;
                }
                _lostWhileHeld = true;
                return true;
            }

            _lostTime = 0f;
            GateEdge edge = _gate.Step(fistStrength, grabThreshold, releaseThreshold);
            // A new grab, or the hand back inside the grace: drag from where it is now.
            if (edge == GateEdge.Rising || _lostWhileHeld) _lastPalmPosition = palmPosition;
            _lostWhileHeld = false;

            if (_gate.IsOn)
            {
                delta = palmPosition - _lastPalmPosition;
                _lastPalmPosition = palmPosition;
            }

            return _gate.IsOn;
        }
    }
}
