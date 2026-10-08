using HandHero.Core;
using UnityEngine;

// Hand-tracking input: reads HandGestureTracker and turns analog fist/pinch
// strengths into HandInputData with hysteresis (ADR 8). Gesture recognition
// lives here; the simulation only sees the resulting flags.
// The thresholds moved here from HandPuppeteerController / PointingBeamController
// with the same names and defaults.
[DefaultExecutionOrder(HandInputSourceBehaviour.ExecutionOrder)]
public class XRHandsInputSource : HandInputSourceBehaviour
{
    [Header("References")]
    [Tooltip("Optional. Falls back to HandGestureTracker.Instance")]
    [SerializeField] private HandGestureTracker tracker;

    [Header("Hands")]
    [Tooltip("Puppeteer (clutch) hand is the left hand")]
    [SerializeField] private bool clutchUsesLeftHand = true;
    [Tooltip("Aim/fire hand is the right hand")]
    [SerializeField] private bool aimUsesRightHand = true;

    [Header("Clutch (fist) thresholds with hysteresis")]
    [Tooltip("Fist strength above this grabs the character")]
    [SerializeField] private float grabThreshold = 0.7f;
    [Tooltip("Fist strength below this releases it (lower than grab = no flicker)")]
    [SerializeField] private float releaseThreshold = 0.45f;

    [Header("Firing (pinch) thresholds with hysteresis")]
    [Tooltip("Pinch strength above this fires once")]
    [SerializeField] private float pinchFireThreshold = 0.8f;
    [Tooltip("Pinch strength below this re-arms the next shot")]
    [SerializeField] private float pinchResetThreshold = 0.5f;

    private readonly HandClutchSampler _clutch = new HandClutchSampler();
    private HysteresisGate _pinchGate;

    protected override HandInputData Sample()
    {
        HandGestureTracker t = tracker != null ? tracker : HandGestureTracker.Instance;
        if (t == null) return default;

        HandGestureTracker.HandState clutchHand = clutchUsesLeftHand ? t.Left : t.Right;
        HandGestureTracker.HandState aimHand = aimUsesRightHand ? t.Right : t.Left;

        var data = new HandInputData();

        // Tracking loss opens the clutch: the character glides instead of
        // teleporting when the hand comes back somewhere else.
        data.ClutchHeld = _clutch.Step(clutchHand.IsTracked, clutchHand.FistStrength, clutchHand.PalmPosition,
            grabThreshold, releaseThreshold, out data.ClutchDelta);

        if (!aimHand.IsTracked)
        {
            _pinchGate.Reset();
            return data; // HasAim = false: reticle freezes at the last aim point
        }

        Ray aim = aimHand.AimRay;
        data.HasAim = aim.direction != Vector3.zero;
        data.AimOrigin = aim.origin;
        data.AimDirection = aim.direction;

        // Edge-detected pinch: fire once per pinch.
        data.FireTriggered = _pinchGate.Step(aimHand.PinchStrength, pinchFireThreshold, pinchResetThreshold)
            == GateEdge.Rising;

        return data;
    }
}
