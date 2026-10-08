using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

// Central hand-tracking reader for the hands-first prototype.
// Wraps XRHandSubsystem (Unity XR Hands package) and exposes simple values
// the gameplay scripts consume:
//   FistStrength  (0..1) -> puppeteer clutch (left hand)
//   PinchStrength (0..1) -> fire trigger (right hand)
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
        public Quaternion PalmRotation; // world space
        public float FistStrength;      // 0 = open hand, 1 = closed fist
        public float PinchStrength;     // 0 = apart, 1 = thumb+index pinched
        public Ray AimRay;              // world space pointing ray
    }

    [Header("References")]
    [SerializeField] private Transform xrOrigin;   // XR Origin root transform
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

    private void Update()
    {
        if (_subsystem == null || !_subsystem.running)
        {
            FindSubsystem();
            if (_subsystem == null) return;
        }

        UpdateHand(_subsystem.leftHand, ref _left, isLeft: true);
        UpdateHand(_subsystem.rightHand, ref _right, isLeft: false);
    }

    private void FindSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems)
        {
            if (s.running)
            {
                _subsystem = s;
                return;
            }
        }
        if (subsystems.Count > 0) _subsystem = subsystems[0];
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

        state.IsTracked = true;

        // Session space -> world space.
        Vector3 palmWorld = xrOrigin != null ? xrOrigin.TransformPoint(palmPose.position) : palmPose.position;
        Quaternion palmRotWorld = xrOrigin != null ? xrOrigin.rotation * palmPose.rotation : palmPose.rotation;

        // Unscaled: hands (and the pause menu) keep working while the game is paused (timeScale 0).
        float posT = 1f - Mathf.Exp(-positionSmoothing * Time.unscaledDeltaTime);
        float valT = 1f - Mathf.Exp(-valueSmoothing * Time.unscaledDeltaTime);

        state.PalmPosition = Vector3.Lerp(state.PalmPosition, palmWorld, posT);
        state.PalmRotation = Quaternion.Slerp(state.PalmRotation, palmRotWorld, posT);

        // ---- Fist strength: average fingertip-to-palm distance ----
        float total = 0f;
        int counted = 0;
        foreach (var tipId in FingerTips)
        {
            if (hand.GetJoint(tipId).TryGetPose(out Pose tipPose))
            {
                total += Vector3.Distance(tipPose.position, palmPose.position);
                counted++;
            }
        }
        if (counted > 0)
        {
            float avg = total / counted;
            float rawFist = Mathf.InverseLerp(fingerOpenDistance, fingerClosedDistance, avg);
            state.FistStrength = Mathf.Lerp(state.FistStrength, Mathf.Clamp01(rawFist), valT);
        }

        // ---- Pinch strength: thumb tip to index tip ----
        if (hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose) &&
            hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexTipPose))
        {
            float d = Vector3.Distance(thumbPose.position, indexTipPose.position);
            float rawPinch = Mathf.InverseLerp(pinchOpenDistance, pinchClosedDistance, d);
            state.PinchStrength = Mathf.Lerp(state.PinchStrength, Mathf.Clamp01(rawPinch), valT);
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
            Vector3 shoulder = headCamera.position + headCamera.rotation * offset;

            Vector3 knuckleWorld = xrOrigin != null ? xrOrigin.TransformPoint(knucklePose.position) : knucklePose.position;
            Vector3 dir = (knuckleWorld - shoulder).normalized;

            Vector3 smoothedDir = state.AimRay.direction == Vector3.zero
                ? dir
                : Vector3.Slerp(state.AimRay.direction, dir, posT);
            state.AimRay = new Ray(shoulder, smoothedDir);
        }
    }
}
