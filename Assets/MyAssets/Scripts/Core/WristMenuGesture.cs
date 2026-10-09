using System;
using UnityEngine;

namespace HandHero.Core
{
    [Serializable]
    public struct WristMenuParams
    {
        [Tooltip("Palm-toward-face value (dot of palm normal and palm->head) that shows the wrist button")]
        public float FacingShow;
        [Tooltip("Below this the wrist button hides (lower than FacingShow = no flicker)")]
        public float FacingHide;
        [Tooltip("Fist strength must be at or below this to show it: an open hand, not a clutch")]
        public float MaxFistStrength;
        [Tooltip("Seconds the open palm must face the head before the button shows (no flashes mid-fight)")]
        public float ShowDelay;
        [Tooltip("Pinch strength that presses the button")]
        public float PinchPress;
        [Tooltip("Pinch strength below this re-arms the next press")]
        public float PinchRelease;

        public static WristMenuParams Default => new WristMenuParams
        {
            FacingShow = 0.7f,
            FacingHide = 0.4f,
            MaxFistStrength = 0.35f,
            ShowDelay = 0.25f,
            PinchPress = 0.8f,
            PinchRelease = 0.5f,
        };
    }

    // Wrist menu (T7): turn the open palm toward the face and hold it there to
    // show the wrist button; pinch with the same hand to press it. Both the
    // facing and the pinch are hysteresis-gated (ADR 8). A pinch already held
    // when the button appears does not count — the hand must pinch again.
    public class WristMenuGesture
    {
        private HysteresisGate _facing;
        private HysteresisGate _pinch;
        private float _dwell;

        public bool IsVisible { get; private set; }

        // Returns true on the single step the button is pressed.
        public bool Step(bool tracked, float facing, float fistStrength, float pinchStrength, float dt,
            WristMenuParams p)
        {
            if (!tracked)
            {
                _facing.Reset();
                _pinch.Reset();
                _dwell = 0f;
                IsVisible = false;
                return false;
            }

            bool wasVisible = IsVisible;
            _facing.Step(facing, p.FacingShow, p.FacingHide);
            bool pinched = _pinch.Step(pinchStrength, p.PinchPress, p.PinchRelease) == GateEdge.Rising;

            if (!_facing.IsOn)
            {
                _dwell = 0f;
                IsVisible = false;
            }
            else if (!IsVisible)
            {
                _dwell = fistStrength <= p.MaxFistStrength ? _dwell + dt : 0f;
                IsVisible = _dwell >= p.ShowDelay;
            }

            // A fist closing for the clutch reads as a pinch too (BC-5): open hand only.
            return wasVisible && IsVisible && pinched && fistStrength <= p.MaxFistStrength;
        }
    }
}
