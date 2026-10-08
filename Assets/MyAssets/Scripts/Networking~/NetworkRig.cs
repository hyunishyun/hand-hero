using Fusion;
using UnityEngine;

// Networked avatar (head + both hands) so remote players can see each other.
// Pattern follows the Fusion VR samples: hardware rig (local, reads devices)
// is separate from the network rig (this, replicated to everyone).
//
// Prefab setup: an "AvatarRoot" child on the bike prefab, placed at the SAME
// local offset as XROriginSync.positionOffset (e.g. 0, 1.5, -0.5). The poses
// in BikeInputData are relative to the XR Origin, so with matching offsets
// the avatar lines up with where the player's body actually is.
public class NetworkRig : NetworkBehaviour
{
    [Header("Avatar Transforms (children of AvatarRoot)")]
    [SerializeField] private Transform head;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    [Header("Local Player")]
    [Tooltip("Hidden for the local player so the head mesh doesn't block the camera")]
    [SerializeField] private Renderer[] hiddenForLocalPlayer;

    // ---- Networked rig pose (written from input, read by everyone) ----
    [Networked] private Vector3 HeadPos { get; set; }
    [Networked] private Quaternion HeadRot { get; set; }
    [Networked] private Vector3 LeftPos { get; set; }
    [Networked] private Quaternion LeftRot { get; set; }
    [Networked] private Vector3 RightPos { get; set; }
    [Networked] private Quaternion RightRot { get; set; }

    public override void Spawned()
    {
        if (HasInputAuthority && hiddenForLocalPlayer != null)
        {
            foreach (Renderer r in hiddenForLocalPlayer)
                if (r != null) r.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Input authority + server both receive input; networked props replicate
        // the pose to every other client automatically.
        if (GetInput(out BikeInputData input))
        {
            HeadPos = input.HeadPosition;
            HeadRot = input.HeadRotation;
            LeftPos = input.LeftHandPosition;
            LeftRot = input.LeftHandRotation;
            RightPos = input.RightHandPosition;
            RightRot = input.RightHandRotation;
        }
    }

    public override void Render()
    {
        // Apply replicated pose every rendered frame. Light smoothing hides
        // the tick-rate stepping on remote avatars.
        const float smoothing = 20f;
        float t = smoothing * Time.deltaTime;

        Apply(head, HeadPos, HeadRot, t);
        Apply(leftHand, LeftPos, LeftRot, t);
        Apply(rightHand, RightPos, RightRot, t);
    }

    private static void Apply(Transform target, Vector3 pos, Quaternion rot, float t)
    {
        if (target == null) return;
        target.localPosition = Vector3.Lerp(target.localPosition, pos, t);
        if (rot.x != 0f || rot.y != 0f || rot.z != 0f || rot.w != 0f)
            target.localRotation = Quaternion.Slerp(target.localRotation, rot, t);
    }
}
