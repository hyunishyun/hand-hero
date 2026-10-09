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
    [Tooltip("Pinch strength below this re-arms the next shot and ends a charge hold. 0.6 (was 0.5): the smoothed strength reaches it sooner, so a quick shot reads as released sooner (D13)")]
    [SerializeField] private float pinchResetThreshold = 0.6f;

    [Header("CURSOR trigger (index finger) with hysteresis")]
    [Tooltip("Index curl above this pulls the trigger (fires once, holding charges)")]
    [SerializeField] private float triggerPullThreshold = 0.7f;
    [Tooltip("Index curl below this re-arms the trigger")]
    [SerializeField] private float triggerReleaseThreshold = 0.45f;
    [Tooltip("A pull within this many seconds of the grip (middle/ring/little) changing is ignored: a full fist closes both at once")]
    [SerializeField] private float gripSettleTime = 0.15f;

    [Header("Charge shot (both palms together) with hysteresis")]
    [Tooltip("Legacy charge gesture (both palms together). Off: the charge shot is a held right-hand pinch")]
    [SerializeField] private bool palmsTogetherCharges = false;
    [Tooltip("Palms closer than this (meters) start charging")]
    [SerializeField] private float chargeJoinDistance = 0.10f;
    [Tooltip("Palms farther apart than this (meters) release the charge shot (larger than join = no flicker)")]
    [SerializeField] private float chargeSeparateDistance = 0.18f;
    [Tooltip("Seconds the charge survives while a hand drops out of tracking (touching hands occlude each other)")]
    [SerializeField] private float chargeLostGraceTime = 0.3f;

    [Header("Shockwave (palm push) with hysteresis")]
    [SerializeField] private bool pushWithLeftHand = true;
    [SerializeField] private bool pushWithRightHand = true;
    [Tooltip("Push speed / re-arm speed (m/s along the palm normal) and the open-hand limit")]
    [SerializeField] private PalmPushParams push = PalmPushParams.Default;
    [Tooltip("Palm-facing direction in the palm joint's local space. XR Hands: palm faces -Y (back of hand is +Y). Flip if pushes fire on pulls.")]
    [SerializeField] private Vector3 palmNormalLocal = Vector3.down;

    private readonly HandClutchSampler _clutch = new HandClutchSampler();
    private readonly HandClutchSampler _aimClutch = new HandClutchSampler();
    private readonly PinchTrigger _pinch = new PinchTrigger();
    private readonly TriggerGesture _trigger = new TriggerGesture();
    private readonly PalmsTogetherRecognizer _palmsTogether = new PalmsTogetherRecognizer();
    private readonly PalmPushRecognizer _leftPush = new PalmPushRecognizer();
    private readonly PalmPushRecognizer _rightPush = new PalmPushRecognizer();
    private readonly SystemGestureGate _systemGesture = new SystemGestureGate();

    // Switched back on (resume from pause, round start): the pinch that pressed
    // RESUME (or a pulled trigger) is still closed and must open before it fires or charges.
    // Clutch and gesture history from before the switch-off is stale (BR-2): the
    // hand moved meanwhile, which would read as a drag or a palm push.
    private void OnEnable()
    {
        _pinch.RequireReopen();
        _trigger.RequireReopen();
        _clutch.Reset();
        _aimClutch.Reset();
        _leftPush.Reset();
        _rightPush.Reset();
        _palmsTogether.Reset();
        _systemGesture.Reset();
    }

    protected override HandInputData Sample()
    {
        HandGestureTracker t = tracker != null ? tracker : HandGestureTracker.Instance;
        if (t == null) return default;

        HandGestureTracker.HandState clutchHand = clutchUsesLeftHand ? t.Left : t.Right;
        HandGestureTracker.HandState aimHand = aimUsesRightHand ? t.Right : t.Left;

        var data = new HandInputData();
        data.AimHandLost = !aimHand.IsTracked;
        data.ClutchHandLost = !clutchHand.IsTracked;

        // Tracking loss opens the clutch: the character glides instead of
        // teleporting when the hand comes back somewhere else.
        data.ClutchHeld = _clutch.Step(clutchHand.IsTracked, clutchHand.FistStrength, clutchHand.TrackingPalmPosition,
            grabThreshold, releaseThreshold, out data.ClutchDelta);

        // Aim-hand gun grip (middle/ring/little) = CURSOR aim drag, with the
        // puppeteer's fist thresholds; the index stays free as the trigger.
        // Sampled before the tracking check so a lost hand opens it.
        data.AimClutchHeld = _aimClutch.Step(aimHand.IsTracked, aimHand.GripStrength, aimHand.TrackingPalmPosition,
            grabThreshold, releaseThreshold, out data.AimClutchDelta);

        TriggerState trigger = _trigger.Step(aimHand.IsTracked, aimHand.IndexCurl, aimHand.GripStrength,
            triggerPullThreshold, triggerReleaseThreshold, grabThreshold, releaseThreshold, gripSettleTime,
            Time.deltaTime);
        data.TriggerFired = trigger.Fired;
        data.TriggerHeld = trigger.Held;

        bool palmsCharging = SampleGestures(t, ref data);

        // Pinch edge = normal shot, pinch level = charge hold (ASSIST aim; CURSOR fires
        // with the index trigger). No fist block: pointing with the other fingers
        // curled reads as a full fist and blocked every ASSIST pinch on device.
        // Keeps stepping through a palms charge so a pinch held through it doesn't
        // fire afterwards.
        // The Meta system gesture's pinch opens the OS menu, never a shot (CR-7).
        float pinchStrength = _systemGesture.Step(aimHand.SystemGesture, aimHand.PinchStrength, pinchResetThreshold);
        PinchState pinch = _pinch.Step(aimHand.IsTracked, pinchStrength, pinchFireThreshold,
            pinchResetThreshold, false, 0f, Time.deltaTime);

        if (!aimHand.IsTracked)
            return data; // HasAim = false: reticle freezes at the last aim point

        Ray aim = aimHand.AimRay;
        data.HasAim = aim.direction != Vector3.zero;
        data.AimOrigin = aim.origin;
        data.AimDirection = aim.direction;

        data.FireTriggered = pinch.FireTriggered && !palmsCharging;
        data.PinchHeld = pinch.Held && !palmsCharging;

        return data;
    }

    // Legacy palms-together charge (off by default) and shockwave (palm push).
    // Returns true while a palms charge is active.
    // Distances and speeds are physical (tracking space), so the thresholds hold in
    // the tabletop mode too. The XR Origin never rotates, so the world palm normal
    // is also the tracking-space one.
    private bool SampleGestures(HandGestureTracker t, ref HandInputData data)
    {
        HandGestureTracker.HandState left = t.Left;
        HandGestureTracker.HandState right = t.Right;
        // The hand poses are smoothed with the unscaled frame time, so palm speed
        // uses it too (CR-8); paused (timeScale 0) no gesture advances.
        float dt = Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;

        // Always stepped (state continuity); only charges when enabled.
        bool joined = _palmsTogether.Step(left.IsTracked, right.IsTracked, left.TrackingPalmPosition,
            right.TrackingPalmPosition,
            chargeJoinDistance, chargeSeparateDistance, chargeLostGraceTime, dt);
        bool charging = joined && palmsTogetherCharges;

        // Both recognizers always step so their speed history stays continuous.
        bool leftPush = _leftPush.Step(left.IsTracked, left.TrackingPalmPosition, left.PalmRotation * palmNormalLocal,
            left.FistStrength, dt, push) && pushWithLeftHand;
        bool rightPush = _rightPush.Step(right.IsTracked, right.TrackingPalmPosition, right.PalmRotation * palmNormalLocal,
            right.FistStrength, dt, push) && pushWithRightHand;

        if (charging) data.Gestures |= HandGestures.ChargeHeld;
        // Joined hands win: pushing both palms forward together is not a shockwave.
        else if (!joined && (leftPush || rightPush)) data.Gestures |= HandGestures.Shockwave;

        return charging;
    }
}
