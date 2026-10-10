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
    [Tooltip("Deep review DR-10: seconds a held clutch survives while the clutch hand is untracked (zero drag meanwhile; back with the fist closed = same grab, re-anchored where the hand is). A 1-2 frame dropout used to release and regrab from the hero, losing the target's lead. A longer loss still opens the clutch (glide). 0 = open on the first untracked frame")]
    [SerializeField] private float clutchLostGraceTime = 0.25f;

    [Header("Firing (pinch) thresholds with hysteresis")]
    [Tooltip("Pinch strength (unsmoothed since round 5) at or above this fires once")]
    [SerializeField] private float pinchFireThreshold = 0.8f;
    [Tooltip("Pinch strength (unsmoothed) at or below this ends the pinch and fully re-arms the next shot. 0.6 (was 0.5, D13). A pointing thumb rests above it (about 2.8 cm = 0.71), so pinchRelativeRelease also ends a pinch")]
    [SerializeField] private float pinchResetThreshold = 0.6f;
    [Tooltip("Round 5 (D1): a pinch also ends when the strength falls this far below its peak in this press (0.2 = about 1 cm of thumb travel); the next press must then rise this far above the lowest point since. 0 = off (absolute reset only)")]
    [SerializeField] private float pinchRelativeRelease = 0.2f;
    [Tooltip("Round 5 review: a pinch also ends at pinchFireThreshold minus this, however deep the press went, so a light press (thumb only to 2 cm) ends at the resting pointing thumb (2.8 cm = 0.71). 0.05 = 0.75, about 2.6 cm. 0 = off (peak drop only)")]
    [SerializeField] private float pinchReleaseFloorMargin = 0.05f;
    [Tooltip("Round 5 review: once the thumb has settled at or below that floor after a release, the next press needs at most pinchFireThreshold plus this (0.85, about 2.2 cm), so a light tap after a firm one fires")]
    [SerializeField] private float pinchRearmMargin = 0.05f;
    [Tooltip("Round 5 review: the pinch strength rules count only when this many frames in a row agree, so one bad tracking frame neither ends a held charge nor fires an extra shot (adds 1 frame to a release). 1 = every frame counts")]
    [SerializeField] private int pinchConfirmFrames = 2;
    [Tooltip("Round 5 (D1): a pinch also ends when Meta's own index-pinch flag (Hand Tracking Aim) is off for this many frames after it was on in this press. 0 = ignore the Meta flag")]
    [SerializeField] private int metaPinchReleaseFrames = 2;
    [Tooltip("Round 5 review: Meta's flag ends a pinch only once the strength is also at least this far below the press's peak (the thumb moved) on pinchConfirmFrames frames in a row (deep review DR-6), so a flag flicker with the fingers still closed, plus one bad tracking frame, keeps the charge. 0 = the flag alone")]
    [SerializeField] private float metaPinchReleaseDrop = 0.05f;

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
        // teleporting when the hand comes back somewhere else. A short dropout
        // (clutchLostGraceTime, DR-10) keeps it, so the drag's lead is not lost.
        data.ClutchHeld = _clutch.Step(clutchHand.IsTracked, clutchHand.FistStrength, clutchHand.TrackingPalmPosition,
            grabThreshold, releaseThreshold, clutchLostGraceTime, Time.unscaledDeltaTime, out data.ClutchDelta);

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
        // Round 5 (D1): unsmoothed strength, released by Meta's pinch flag, the reset
        // threshold or a drop from the press's peak / the floor under the fire
        // threshold, the strength rules confirmed over 2 frames (PinchTrigger).
        // The Meta system gesture's pinch opens the OS menu, never a shot (CR-7): it
        // drops the pinch, a held charge is cancelled instead of released (round 4,
        // S3), and a pinch still closed afterwards must open before it fires.
        data.AimSystemGesture = aimHand.SystemGesture;
        PinchState pinch = _pinch.Step(new PinchSample
        {
            Tracked = aimHand.IsTracked,
            Strength = aimHand.RawPinchStrength,
            HasMeta = aimHand.HasMetaPinch,
            MetaPinching = aimHand.MetaIndexPinching,
            SystemGesture = aimHand.SystemGesture,
        }, new PinchReleaseParams
        {
            FireThreshold = pinchFireThreshold,
            ResetThreshold = pinchResetThreshold,
            RelativeRelease = pinchRelativeRelease,
            ReleaseFloorMargin = pinchReleaseFloorMargin,
            RearmMargin = pinchRearmMargin,
            ConfirmFrames = pinchConfirmFrames,
            MetaReleaseFrames = metaPinchReleaseFrames,
            MetaReleaseDrop = metaPinchReleaseDrop,
        });
        data.PinchRelease = pinch.Release;

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
