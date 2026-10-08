using HandHero.Core;
using UnityEngine;
using UnityEngine.InputSystem;

// Point-and-pinch menu selection (T7), controller-free: the aim hand's ray
// (HandGestureTracker, the same shoulder-anchored ray as aiming) hovers a
// HandMenuButton and a pinch presses it. While the hand isn't tracked the
// mouse stands in (editor / desktop): cursor = ray, left click = pinch.
// HandMenu enables this only while a menu panel is visible.
public class HandMenuPointer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional. Falls back to HandGestureTracker.Instance")]
    [SerializeField] private HandGestureTracker tracker;
    [Tooltip("Camera for the mouse fallback. Falls back to Camera.main")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("Optional ray visual, drawn from the hand to the hovered button")]
    [SerializeField] private LineRenderer ray;

    [Header("Hand")]
    [SerializeField] private bool useRightHand = true;
    [Tooltip("Pinch strength above this presses the hovered button")]
    [SerializeField] private float pinchPressThreshold = 0.8f;
    [Tooltip("Pinch strength below this re-arms the next press (lower than press = no flicker)")]
    [SerializeField] private float pinchReleaseThreshold = 0.5f;
    [SerializeField] private float maxDistance = 10f;

    [Header("Ray visual")]
    [Tooltip("Ray length when nothing is hovered")]
    [SerializeField] private float idleRayLength = 1.5f;
    [Tooltip("Ray start, meters from the shoulder along the ray (roughly at the hand)")]
    [SerializeField] private float rayStartOffset = 0.45f;

    [Header("Debug")]
    [Tooltip("Mouse cursor + left click stand in for the hand when it isn't tracked")]
    [SerializeField] private bool mouseFallback = true;

    private HysteresisGate _pinch;
    private bool _waitForOpenHand;
    private HandMenuButton _hovered;
    private float _rayWidth = -1f;
    private float _scale = 1f; // tabletop view: menus and hands are this many times larger in world units

    // A menu can appear under a pinch that is still held (e.g. the right hand
    // was firing when the match ended): that pinch must open before one counts.
    private void OnEnable()
    {
        _waitForOpenHand = true;
    }

    private void OnDisable()
    {
        Hover(null);
        _pinch.Reset();
        if (ray != null) ray.enabled = false;
    }

    private void Update()
    {
        HandGestureTracker t = tracker != null ? tracker : HandGestureTracker.Instance;
        _scale = t != null ? t.WorldScale : 1f;
        bool hasRay = TryGetRay(t, out Ray r, out bool pressed, out bool fromHand);

        HandMenuButton hit = null;
        float distance = idleRayLength * _scale;
        if (hasRay && Physics.Raycast(r, out RaycastHit info, maxDistance * _scale, ~0,
                QueryTriggerInteraction.Collide))
        {
            hit = info.collider.GetComponent<HandMenuButton>();
            if (hit != null) distance = info.distance;
        }

        Hover(hit);
        if (pressed && hit != null) hit.Press();

        if (ray != null)
        {
            // The mouse needs no visible ray: the cursor already shows where it points.
            ray.enabled = hasRay && fromHand;
            if (ray.enabled)
            {
                if (_rayWidth < 0f) _rayWidth = ray.widthMultiplier;
                ray.widthMultiplier = _rayWidth * _scale;
                float start = Mathf.Min(rayStartOffset * _scale, distance);
                ray.SetPosition(0, r.GetPoint(start));
                ray.SetPosition(1, r.GetPoint(distance));
            }
        }
    }

    private bool TryGetRay(HandGestureTracker t, out Ray r, out bool pressed, out bool fromHand)
    {
        r = default;
        pressed = false;
        fromHand = false;

        if (t != null)
        {
            HandGestureTracker.HandState hand = useRightHand ? t.Right : t.Left;
            if (hand.IsTracked && hand.AimRay.direction != Vector3.zero)
            {
                r = hand.AimRay;
                fromHand = true;
                if (_waitForOpenHand && hand.PinchStrength <= pinchReleaseThreshold) _waitForOpenHand = false;
                pressed = _pinch.Step(hand.PinchStrength, pinchPressThreshold, pinchReleaseThreshold)
                    == GateEdge.Rising && !_waitForOpenHand;
                return true;
            }
        }

        // Tracking lost: a pinch in progress ends here, never later somewhere else.
        _pinch.Reset();

        if (!mouseFallback) return false;
        Mouse mouse = Mouse.current;
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        if (mouse == null || cam == null) return false;

        r = cam.ScreenPointToRay(mouse.position.ReadValue());
        pressed = mouse.leftButton.wasPressedThisFrame;
        return true;
    }

    private void Hover(HandMenuButton button)
    {
        if (_hovered == button) return;
        if (_hovered != null) _hovered.SetHovered(false);
        _hovered = button;
        if (_hovered != null) _hovered.SetHovered(true);
    }
}
