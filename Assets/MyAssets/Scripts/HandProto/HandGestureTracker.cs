using System.Collections.Generic;
using HandHero.Core;
using UnityEngine;
using UnityEngine.XR.Hands;

// Central hand-tracking reader for the hands-first prototype.
// Wraps XRHandSubsystem (Unity XR Hands package) and exposes simple values
// the gameplay scripts consume:
//   FistStrength  (0..1) -> puppeteer clutch (left hand)
//   PinchStrength (0..1) -> menus; RawPinchStrength + Meta's pinch flag -> ASSIST fire (right hand)
//   GripStrength / IndexCurl (0..1) -> CURSOR aim: gun grip drags the marker, index fires
//   AimRay               -> shoulder-anchored pointing ray (stable while pinching)
// All poses are converted to WORLD space via the XR Origin transform,
// because XRHandSubsystem reports joints in XR Origin (session) space.
public class HandGestureTracker : MonoBehaviour
{
    public static HandGestureTracker Instance { get; private set; }

    public struct HandState
    {
        public bool IsTracked;
        public Vector3 PalmPosition;    // world space
        public Vector3 TrackingPalmPosition; // tracking space: physical meters, unaffected by the tabletop scale
        public Quaternion PalmRotation; // world space
        public float FistStrength;      // 0 = open hand, 1 = closed fist
        public float GripStrength;      // middle/ring/little only (CURSOR gun grip), 0..1
        public float IndexCurl;         // index finger alone (CURSOR trigger), 0..1
        public float PinchStrength;     // 0 = apart, 1 = thumb+index pinched
        public float RawPinchStrength;  // PinchStrength before smoothing (round 5: ASSIST fire/release)
        public Ray AimRay;              // world space pointing ray
        public bool SystemGesture;      // Meta system gesture (palm toward the headset): its pinch belongs to the OS
        // Meta Hand Tracking Aim (round 5, D1): the platform's own calibrated pinch.
        public bool HasMetaPinch;       // the aim state is valid this frame (Meta "Valid" flag)
        public bool MetaIndexPinching;  // Meta's index-pinching flag; false unless HasMetaPinch
        public float MetaPinchStrength; // Meta's pinchStrengthIndex, 0..1; 0 unless HasMetaPinch
    }

    [Header("References")]
    [SerializeField] private Transform xrOrigin;   // XR tracking space: the XR Origin's Camera Offset
    [SerializeField] private Transform headCamera; // Main Camera under the XR Origin

    [Header("Fist Detection (tip-to-palm distance, meters)")]
    [Tooltip("Average fingertip-to-palm distance when the hand is open")]
    [SerializeField] private float fingerOpenDistance = 0.10f;
    [Tooltip("Average fingertip-to-palm distance when the fist is closed")]
    [SerializeField] private float fingerClosedDistance = 0.05f;

    [Header("Pinch Detection (thumb-to-index distance, meters)")]
    [SerializeField] private float pinchOpenDistance = 0.06f;
    [SerializeField] private float pinchClosedDistance = 0.015f;

    [Header("Aim Ray")]
    [Tooltip("Shoulder estimate relative to the head, in head-local space. X flips for the left hand.")]
    [SerializeField] private Vector3 shoulderOffset = new Vector3(0.17f, -0.18f, 0f);

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothing = 25f;
    [SerializeField] private float valueSmoothing = 18f;

    // Uniform scale of the tracking space (1 in the VR arena, about 35 in the
    // passthrough tabletop). World-space hand distances are this many times the
    // physical ones, so physical thresholds use TrackingPalmPosition instead.
    public float WorldScale => xrOrigin != null ? xrOrigin.lossyScale.x : 1f;

    public HandState Left => _left;
    public HandState Right => _right;

    private XRHandSubsystem _subsystem;
    private HandState _left;
    private HandState _right;

    private static readonly XRHandJointID[] FingerTips =
    {
        XRHandJointID.IndexTip,
        XRHandJointID.MiddleTip,
        XRHandJointID.RingTip,
        XRHandJointID.LittleTip,
    };

    private void Awake()
    {
        Instance = this;

        if (headCamera == null && Camera.main != null)
            headCamera = Camera.main.transform;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Round 5 T4: GhostHands (demo mode) reads every joint. The running hand
    // subsystem or null (check .running before reading joints: a stopped one's
    // joint arrays are disposed), and the tracking space that maps joint poses to
    // world space (null = joints are already world space).
    public XRHandSubsystem Subsystem => _subsystem;
    public Transform TrackingSpace => xrOrigin;

    private void Update()
    {
        if (_subsystem == null || !_subsystem.running)
        {
            _subsystem = null;
            // Searching allocates and calls native code: at most every 0.5 s while none runs (GC-2).
            if (Time.unscaledTime >= _nextFindTime)
            {
                _nextFindTime = Time.unscaledTime + SubsystemRetryInterval;
                FindSubsystem();
            }
            if (_subsystem == null)
            {
                // A stopped subsystem's joint arrays are disposed; reading them throws.
                // Treat "no running subsystem" exactly like tracking loss.
                _left.IsTracked = false;
                _right.IsTracked = false;
                _left.SystemGesture = false;
                _right.SystemGesture = false;
                ClearMetaPinch(ref _left);
                ClearMetaPinch(ref _right);
                return;
            }
        }

        UpdateHand(_subsystem.leftHand, ref _left, isLeft: true);
        UpdateHand(_subsystem.rightHand, ref _right, isLeft: false);
        UpdateAimState(Handedness.Left, ref _left, 0);
        UpdateAimState(Handedness.Right, ref _right, 1);
    }

    private static void ClearMetaPinch(ref HandState state)
    {
        state.HasMetaPinch = false;
        state.MetaIndexPinching = false;
        state.MetaPinchStrength = 0f;
    }

    // Meta Hand Tracking Aim (XR_FB_hand_tracking_aim, on for Android and Standalone)
    // flags the system gesture per hand (CR-7) and reports the platform's own index
    // pinch (round 5, D1). Without the extension the aim state is invalid: the system
    // gesture stays off and HasMetaPinch false. System gesture edges go to the perf log.
    // Structs only: nothing allocates.
    private void UpdateAimState(Handedness handedness, ref HandState state, int hand)
    {
        bool active = false;
        ClearMetaPinch(ref state);
        if (state.IsTracked && _subsystem.TryGetAimState(handedness, out XRHandAimState aim))
        {
            MetaAimFlags flags = new MetaAimHandState(in aim).aimFlags;
            active = (flags & MetaAimFlags.SystemGesture) != 0;
            if ((flags & MetaAimFlags.Valid) != 0)
            {
                state.HasMetaPinch = true;
                state.MetaIndexPinching = (flags & MetaAimFlags.IndexPinching) != 0;
                state.MetaPinchStrength = aim.pinchStrengthIndex;
            }
        }

        if (active == state.SystemGesture) return;
        state.SystemGesture = active;
        PerfSpikeLogger.Mark(active ? PerfRecordKind.SystemGestureStart : PerfRecordKind.SystemGestureEnd, hand);
    }

    private const float SubsystemRetryInterval = 0.5f;
    private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();
    private float _nextFindTime;
    private long _lastSubsystemSignature = -1;

    private void FindSubsystem()
    {
        _subsystem = null;
        SubsystemManager.GetSubsystems(Subsystems);
        long signature = Subsystems.Count;
        for (int i = 0; i < Subsystems.Count; i++)
        {
            XRHandSubsystem s = Subsystems[i];
            if (s.running)
            {
                if (_subsystem == null) _subsystem = s;
                if (i < 31) signature |= 1L << (i + 32);
            }
        }
        // The report string is built only when the count or a running flag changed.
        if (signature != _lastSubsystemSignature)
        {
            _lastSubsystemSignature = signature;
            ReportSubsystems(Subsystems);
        }
    }

    // Diagnostic: logs once per change so a missing/stopped hand subsystem is visible in the Console.
    private static void ReportSubsystems(List<XRHandSubsystem> subsystems)
    {
        string report;
        if (subsystems.Count == 0)
        {
            report = "no XRHandSubsystem exists (OpenXR Hand Tracking feature off for this platform, or XR not initialized)";
        }
        else
        {
            var parts = new List<string>();
            foreach (var s in subsystems)
                parts.Add($"{s.subsystemDescriptor.id} running={s.running}");
            report = string.Join(", ", parts);
        }

        HHLog.Info($"[HandGestureTracker] hand subsystems: {report}");
    }

    private void UpdateHand(XRHand hand, ref HandState state, bool isLeft)
    {
        if (!hand.isTracked)
        {
            // Keep last pose but flag as untracked; gameplay scripts decide
            // how to degrade (clutch releases, aim freezes).
            state.IsTracked = false;
            return;
        }

        if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose))
        {
            state.IsTracked = false;
            return;
        }

        // Reacquired (BR-3): the stored pose is from before the loss. Snap to the
        // raw values on this frame; smoothing from the stale pose would drag the
        // clutch and read as a palm push. Smoothing resumes next frame.
        bool reacquired = !state.IsTracked;
        state.IsTracked = true;

        // Session space -> world space.
        Vector3 palmWorld = xrOrigin != null ? xrOrigin.TransformPoint(palmPose.position) : palmPose.position;
        Quaternion palmRotWorld = xrOrigin != null ? xrOrigin.rotation * palmPose.rotation : palmPose.rotation;

        // Unscaled: hands (and the pause menu) keep working while the game is paused (timeScale 0).
        float posT = reacquired ? 1f : 1f - Mathf.Exp(-positionSmoothing * Time.unscaledDeltaTime);
        float valT = reacquired ? 1f : 1f - Mathf.Exp(-valueSmoothing * Time.unscaledDeltaTime);

        state.PalmPosition = Vector3.Lerp(state.PalmPosition, palmWorld, posT);
        state.TrackingPalmPosition = Vector3.Lerp(state.TrackingPalmPosition, palmPose.position, posT);
        state.PalmRotation = Quaternion.Slerp(state.PalmRotation, palmRotWorld, posT);

        // ---- Fist strength: average fingertip-to-palm distance ----
        // Also split into the index alone (trigger) and the other three (grip):
        // measured on Quest, they read independently in a gun grip.
        float total = 0f;
        int counted = 0;
        float gripTotal = 0f;
        int gripCounted = 0;
        float indexDistance = -1f;
        foreach (var tipId in FingerTips)
        {
            if (hand.GetJoint(tipId).TryGetPose(out Pose tipPose))
            {
                float d = Vector3.Distance(tipPose.position, palmPose.position);
                total += d;
                counted++;
                if (tipId == XRHandJointID.IndexTip)
                {
                    indexDistance = d;
                }
                else
                {
                    gripTotal += d;
                    gripCounted++;
                }
            }
        }
        if (counted > 0)
        {
            float avg = total / counted;
            float rawFist = Mathf.InverseLerp(fingerOpenDistance, fingerClosedDistance, avg);
            state.FistStrength = Mathf.Lerp(state.FistStrength, Mathf.Clamp01(rawFist), valT);
        }
        if (gripCounted > 0)
        {
            float rawGrip = Mathf.InverseLerp(fingerOpenDistance, fingerClosedDistance, gripTotal / gripCounted);
            state.GripStrength = Mathf.Lerp(state.GripStrength, Mathf.Clamp01(rawGrip), valT);
        }
        if (indexDistance >= 0f)
        {
            float rawIndex = Mathf.InverseLerp(fingerOpenDistance, fingerClosedDistance, indexDistance);
            state.IndexCurl = Mathf.Lerp(state.IndexCurl, Mathf.Clamp01(rawIndex), valT);
        }

        // ---- Pinch strength: thumb tip to index tip ----
        if (hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose) &&
            hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexTipPose))
        {
            float d = Vector3.Distance(thumbPose.position, indexTipPose.position);
            float rawPinch = Mathf.Clamp01(Mathf.InverseLerp(pinchOpenDistance, pinchClosedDistance, d));
            state.RawPinchStrength = rawPinch;
            state.PinchStrength = Mathf.Lerp(state.PinchStrength, rawPinch, valT);
        }

        // ---- Aim ray: shoulder-anchored, through the index knuckle ----
        // Origin sits near the shoulder and the ray passes through the index
        // proximal joint. This is the standard system-pointer trick: the
        // knuckle barely moves when you pinch, so the reticle doesn't jump
        // at the moment of firing (an index-direction ray would).
        if (headCamera != null &&
            hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose knucklePose))
        {
            Vector3 offset = shoulderOffset;
            if (isLeft) offset.x = -offset.x;
            Vector3 shoulder = headCamera.position + headCamera.rotation * (offset * WorldScale);

            Vector3 knuckleWorld = xrOrigin != null ? xrOrigin.TransformPoint(knucklePose.position) : knucklePose.position;
            Vector3 dir = (knuckleWorld - shoulder).normalized;

            Vector3 smoothedDir = state.AimRay.direction == Vector3.zero || reacquired
                ? dir
                : Vector3.Slerp(state.AimRay.direction, dir, posT);
            state.AimRay = new Ray(shoulder, smoothedDir);
        }
    }
}
