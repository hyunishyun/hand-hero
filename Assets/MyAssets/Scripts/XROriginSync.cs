using UnityEngine;

// Updated version of the original XROriginSync.
// Changes from your single-player version:
//  - SetMotorcycle() added: the bike no longer exists at scene load (it is
//    network-spawned), so LocalPlayerBinder assigns it at runtime.
//  - Everything else (offset, smoothing, Y-only rotation for comfort) is kept.
public class XROriginSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform motorcycle; // assigned at runtime via SetMotorcycle

    [Header("Offset Settings")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 1.5f, -0.5f);
    [SerializeField] private float syncSmoothTime = 0.05f;

    private Vector3 velocityReference;
    private CharacterController characterController;

    public Vector3 PositionOffset => positionOffset; // NetworkRig avatar uses the same offset

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = false;
    }

    // Called by LocalPlayerBinder when the local bike spawns (or respawns).
    public void SetMotorcycle(Transform bike)
    {
        motorcycle = bike;

        // Snap immediately on assignment — no smooth flight across the map.
        if (motorcycle != null)
        {
            transform.position = motorcycle.position + motorcycle.TransformDirection(positionOffset);
            transform.rotation = Quaternion.Euler(0f, motorcycle.eulerAngles.y, 0f);
        }
    }

    private void LateUpdate()
    {
        if (motorcycle == null) return;

        // Position: smoothed follow with local-space offset.
        Vector3 targetPosition = motorcycle.position + motorcycle.TransformDirection(positionOffset);
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPosition, ref velocityReference, syncSmoothTime);

        // Rotation: Y-axis only — keeps the horizon level to prevent VR sickness.
        // This is also where the agreed hit design lives: the bike body may pitch
        // or get knocked by physics, but the player's view NEVER inherits it.
        transform.rotation = Quaternion.Euler(0f, motorcycle.eulerAngles.y, 0f);
    }
}
