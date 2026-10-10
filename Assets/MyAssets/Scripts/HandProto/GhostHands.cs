using HandHero.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Hands;

// Demo mode (round 5, T4 / D6): both tracked hands drawn as translucent joint
// spheres and bone lines at the real hand poses, so a headset recording shows
// what the hands are doing. Joint poses come from HandGestureTracker's hand
// subsystem and go to world space through its tracking space, so they sit on the
// real hands in the VR arena and in the MR tabletop (sizes scale with
// WorldScale there). One shared material; each hand's color goes through one
// property block, re-applied only when its tint changes: the clutch hand tints
// while GRAB is held, the aim hand while PINCH (CURSOR: the trigger) is held.
// Everything is built once in Awake (no per-frame allocation) and drawn only
// while the demo is on. Keep this object unscaled (joint sizes are world sizes).
public class GhostHands : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Draws only while this demo is on")]
    [SerializeField] private DemoDirector demo;
    [Tooltip("Optional. Falls back to HandGestureTracker.Instance")]
    [SerializeField] private HandGestureTracker tracker;
    [Tooltip("The player's input source: GRAB and PINCH (CURSOR: the trigger) held, for the tints")]
    [SerializeField] private HandInputSourceBehaviour playerInput;
    [Tooltip("Optional. CURSOR fires with the index trigger, so the aim hand tints while that is held")]
    [SerializeField] private AimModeSetting aimModeSetting;
    [Tooltip("Translucent unlit material shared by every joint and bone; the color comes from a property block")]
    [SerializeField] private Material material;
    [Tooltip("Joint mesh (the built-in sphere); empty = taken from a temporary primitive at load")]
    [SerializeField] private Mesh jointMesh;

    [Header("Hands")]
    [Tooltip("The clutch (GRAB) hand is the left hand; keep in step with XRHandsInputSource.clutchUsesLeftHand")]
    [SerializeField] private bool clutchIsLeft = true;

    [Header("Look (physical meters, scaled with the tabletop view)")]
    [Tooltip("Finger joint sphere diameter")]
    [SerializeField] private float jointSize = 0.014f;
    [Tooltip("Wrist and palm sphere diameter")]
    [SerializeField] private float palmSize = 0.03f;
    [Tooltip("Bone line width")]
    [SerializeField] private float boneWidth = 0.006f;
    [Tooltip("Both hands at rest")]
    [SerializeField] private Color idleColor = new Color(0.8f, 0.92f, 1f, 0.35f);
    [Tooltip("The clutch hand while GRAB is held")]
    [SerializeField] private Color grabColor = new Color(0.35f, 1f, 0.5f, 0.7f);
    [Tooltip("The aim hand while PINCH (CURSOR: the trigger) is held")]
    [SerializeField] private Color pinchColor = new Color(1f, 0.9f, 0.3f, 0.75f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private sealed class Hand
    {
        public GameObject Root;
        public Transform[] Joints;
        public LineRenderer[] Bones;
        public Renderer[] Renderers;
        public Vector3[] World;
        public MaterialPropertyBlock Block;
        public bool Shown;
        public int Tint = -1; // -1 = never applied, 0 = idle, 1 = active
        public float Scale = -1f;
    }

    private Hand _left;
    private Hand _right;

    private void Awake()
    {
        Mesh mesh = jointMesh != null ? jointMesh : BuiltinSphere();
        _left = Build("GhostHand_L", mesh);
        _right = Build("GhostHand_R", mesh);
    }

    private void LateUpdate()
    {
        bool on = demo != null && demo.IsActive;
        HandGestureTracker t = tracker != null ? tracker : HandGestureTracker.Instance;
        XRHandSubsystem subsystem = on && t != null ? t.Subsystem : null;
        // A stopped subsystem's joint arrays are disposed: never read them.
        if (subsystem == null || !subsystem.running)
        {
            Hide(_left);
            Hide(_right);
            return;
        }

        HandInputData input = playerInput != null ? playerInput.Current : default;
        bool cursor = aimModeSetting != null && aimModeSetting.Mode == AimMode.Cursor;
        bool grab = input.ClutchHeld;
        bool pinch = cursor ? input.TriggerHeld : input.PinchHeld;
        Transform space = t.TrackingSpace;
        float scale = t.WorldScale;

        UpdateHand(_left, subsystem.leftHand, space, scale, clutchIsLeft ? grab : pinch,
            clutchIsLeft ? grabColor : pinchColor);
        UpdateHand(_right, subsystem.rightHand, space, scale, clutchIsLeft ? pinch : grab,
            clutchIsLeft ? pinchColor : grabColor);
    }

    private void UpdateHand(Hand hand, XRHand xrHand, Transform space, float scale, bool active, Color activeColor)
    {
        if (hand == null) return;
        if (!xrHand.isTracked)
        {
            Hide(hand);
            return;
        }

        // A joint without a valid pose this frame keeps its last position.
        for (int j = 0; j < HandSkeleton.JointCount; j++)
        {
            if (!xrHand.GetJoint(XRHandJointIDUtility.FromIndex(j)).TryGetPose(out Pose pose)) continue;
            Vector3 world = space != null ? space.TransformPoint(pose.position) : pose.position;
            hand.World[j] = world;
            hand.Joints[j].position = world;
        }

        if (!Mathf.Approximately(scale, hand.Scale))
        {
            hand.Scale = scale;
            for (int j = 0; j < HandSkeleton.JointCount; j++)
            {
                float size = j == HandSkeleton.Wrist || j == HandSkeleton.Palm ? palmSize : jointSize;
                hand.Joints[j].localScale = Vector3.one * (size * scale);
            }
            for (int c = 0; c < hand.Bones.Length; c++) hand.Bones[c].widthMultiplier = boneWidth * scale;
        }

        for (int c = 0; c < hand.Bones.Length; c++)
        {
            int[] chain = HandSkeleton.Chains[c];
            LineRenderer line = hand.Bones[c];
            for (int k = 0; k < chain.Length; k++) line.SetPosition(k, hand.World[chain[k]]);
        }

        ApplyTint(hand, active ? 1 : 0, active ? activeColor : idleColor);

        if (hand.Shown) return;
        hand.Shown = true;
        hand.Root.SetActive(true);
    }

    private static void ApplyTint(Hand hand, int tint, Color color)
    {
        if (tint == hand.Tint) return;
        hand.Tint = tint;
        hand.Block.SetColor(BaseColorId, color);
        hand.Block.SetColor(ColorId, color);
        for (int i = 0; i < hand.Renderers.Length; i++) hand.Renderers[i].SetPropertyBlock(hand.Block);
    }

    private static void Hide(Hand hand)
    {
        if (hand == null || !hand.Shown) return;
        hand.Shown = false;
        hand.Root.SetActive(false);
    }

    private Hand Build(string handName, Mesh mesh)
    {
        var hand = new Hand
        {
            Root = new GameObject(handName),
            Joints = new Transform[HandSkeleton.JointCount],
            Bones = new LineRenderer[HandSkeleton.Chains.Length],
            Renderers = new Renderer[HandSkeleton.JointCount + HandSkeleton.Chains.Length],
            World = new Vector3[HandSkeleton.JointCount],
            Block = new MaterialPropertyBlock(),
        };
        hand.Root.transform.SetParent(transform, false);

        for (int j = 0; j < HandSkeleton.JointCount; j++)
        {
            var go = new GameObject("Joint_" + j, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(hand.Root.transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            SetupRenderer(r);
            hand.Joints[j] = go.transform;
            hand.Renderers[j] = r;
        }

        for (int c = 0; c < HandSkeleton.Chains.Length; c++)
        {
            var go = new GameObject("Bones_" + c);
            go.transform.SetParent(hand.Root.transform, false);
            var line = go.AddComponent<LineRenderer>();
            SetupRenderer(line);
            line.useWorldSpace = true;
            line.positionCount = HandSkeleton.Chains[c].Length;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.startColor = Color.white;
            line.endColor = Color.white;
            hand.Bones[c] = line;
            hand.Renderers[HandSkeleton.JointCount + c] = line;
        }

        hand.Root.SetActive(false);
        return hand;
    }

    private void SetupRenderer(Renderer r)
    {
        r.sharedMaterial = material;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    // Fallback when the builder left jointMesh empty: the built-in sphere, without
    // ever leaving a collider in the scene (the aim ray must not hit the hands).
    private static Mesh BuiltinSphere()
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Mesh mesh = probe.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(probe);
        return mesh;
    }
}
