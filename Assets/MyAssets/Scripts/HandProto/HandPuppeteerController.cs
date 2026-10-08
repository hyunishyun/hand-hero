using UnityEngine;

// Left-hand puppeteer with a fist clutch and RELATIVE (mouse-style) mapping.
// While the fist is closed, hand movement deltas drive the character's target:
//   target += handDelta * positionScale
// Opening the fist releases the character to glide and lets the hand return
// to a comfortable position — exactly like lifting a mouse to reposition it.
//
// Relative mapping (instead of absolute hand->box mapping) was chosen because:
//  - no calibration of a neutral point is needed,
//  - a tracking glitch moves the character a little, not to a glitch position,
//  - the reachable arena is unlimited: clutch, drag, release, repeat.
public class HandPuppeteerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlyingCharacter character;

    [Header("Hand")]
    [SerializeField] private bool useLeftHand = true;

    [Header("Mapping")]
    [Tooltip("World meters the character target moves per meter of hand movement")]
    [SerializeField] private float positionScale = 60f;

    [Header("Clutch (fist) thresholds with hysteresis")]
    [Tooltip("Fist strength above this grabs the character")]
    [SerializeField] private float grabThreshold = 0.7f;
    [Tooltip("Fist strength below this releases it (lower than grab = no flicker)")]
    [SerializeField] private float releaseThreshold = 0.45f;

    [Header("Feedback (optional)")]
    [SerializeField] private Renderer clutchIndicator; // tinted while clutched
    [SerializeField] private Color clutchedColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color releasedColor = new Color(1f, 1f, 1f, 0.4f);

    private bool _clutched;
    private Vector3 _lastHandPosition;
    private Vector3 _targetPosition;

    private void Update()
    {
        var tracker = HandGestureTracker.Instance;
        if (tracker == null || character == null) return;

        HandGestureTracker.HandState hand = useLeftHand ? tracker.Left : tracker.Right;

        // Tracking loss releases the clutch — the character glides instead of
        // teleporting when the hand comes back somewhere else.
        if (!hand.IsTracked)
        {
            if (_clutched) Release();
            return;
        }

        if (!_clutched && hand.FistStrength >= grabThreshold)
        {
            Grab(hand.PalmPosition);
        }
        else if (_clutched && hand.FistStrength <= releaseThreshold)
        {
            Release();
        }

        if (_clutched)
        {
            Vector3 delta = hand.PalmPosition - _lastHandPosition;
            _lastHandPosition = hand.PalmPosition;

            _targetPosition += delta * positionScale;
            character.SetTarget(_targetPosition);
        }
    }

    private void Grab(Vector3 handPosition)
    {
        _clutched = true;
        _lastHandPosition = handPosition;
        // Start dragging from where the character currently is — no snap.
        _targetPosition = character.transform.position;

        if (clutchIndicator != null)
            clutchIndicator.material.color = clutchedColor;
    }

    private void Release()
    {
        _clutched = false;
        character.ClearTarget();

        if (clutchIndicator != null)
            clutchIndicator.material.color = releasedColor;
    }
}
