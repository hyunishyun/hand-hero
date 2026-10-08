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

    // Simulation-side half of the puppeteer clutch: RELATIVE (mouse-style)
    // mapping (ADR 6). While clutched: target += clutchDelta * positionScale.
    // Grabbing starts from the character's current position (no snap), so a
    // release + hand reposition + regrab never moves the character.
    public class ClutchMapper
    {
        private bool _clutched;
        private Vector3 _target;

        public bool IsClutched => _clutched;

        public ClutchResult Step(bool clutchHeld, Vector3 clutchDelta, Vector3 characterPosition, float positionScale)
        {
            var result = new ClutchResult();

            if (clutchHeld && !_clutched)
            {
                _clutched = true;
                _target = characterPosition;
                result.JustGrabbed = true;
            }
            else if (!clutchHeld && _clutched)
            {
                _clutched = false;
                result.JustReleased = true;
            }

            if (_clutched)
            {
                _target += clutchDelta * positionScale;
                result.Clutched = true;
                result.Target = _target;
            }

            return result;
        }

        public ClutchResult Step(HandInputData input, Vector3 characterPosition, float positionScale)
            => Step(input.ClutchHeld, input.ClutchDelta, characterPosition, positionScale);
    }
}
