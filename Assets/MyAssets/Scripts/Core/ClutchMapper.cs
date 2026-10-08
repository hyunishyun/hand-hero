using UnityEngine;

namespace HandHero.Core
{
    public struct ClutchResult
    {
        public bool Clutched;
        public bool JustGrabbed;
        public bool JustReleased;
        public Vector3 Target; // valid while Clutched
    }

    // Fist clutch + RELATIVE (mouse-style) mapping for the puppeteer hand (ADR 6).
    // While clutched: target += handDelta * positionScale. Grabbing starts from the
    // character's current position (no snap); tracking loss releases the clutch so
    // the character glides instead of jumping when the hand reappears elsewhere.
    public class ClutchMapper
    {
        private HysteresisGate _gate;
        private Vector3 _lastHandPosition;
        private Vector3 _target;

        public bool IsClutched => _gate.IsOn;

        public ClutchResult Step(bool handTracked, float fistStrength, Vector3 handPosition,
            Vector3 characterPosition, float grabThreshold, float releaseThreshold, float positionScale)
        {
            var result = new ClutchResult();

            if (!handTracked)
            {
                result.JustReleased = _gate.Reset() == GateEdge.Falling;
                return result;
            }

            GateEdge edge = _gate.Step(fistStrength, grabThreshold, releaseThreshold);
            if (edge == GateEdge.Rising)
            {
                _lastHandPosition = handPosition;
                _target = characterPosition;
                result.JustGrabbed = true;
            }
            else if (edge == GateEdge.Falling)
            {
                result.JustReleased = true;
            }

            if (_gate.IsOn)
            {
                Vector3 delta = handPosition - _lastHandPosition;
                _lastHandPosition = handPosition;
                _target += delta * positionScale;

                result.Clutched = true;
                result.Target = _target;
            }

            return result;
        }
    }
}
