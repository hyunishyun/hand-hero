using HandHero.Core;
using TMPro;
using UnityEngine;

// Wrist pause button (T7): turn the open left palm toward your face and a
// small button appears just off the palm; pinch that hand to pause, again to
// resume. Matches the Quest system convention (left palm up + pinch = menu).
// Runs on unscaled time so it works while the game is paused.
public class WristMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchDirector director;
    [Tooltip("Optional. Falls back to HandGestureTracker.Instance")]
    [SerializeField] private HandGestureTracker tracker;
    [Tooltip("Head camera (palm-toward-face check, button facing). Falls back to Camera.main")]
    [SerializeField] private Transform head;
    [Tooltip("Button visual, shown and placed at the palm while the gesture is active")]
    [SerializeField] private Transform button;
    [SerializeField] private TMP_Text label;

    [Header("Gesture")]
    [SerializeField] private bool useLeftHand = true;
    [SerializeField] private WristMenuParams gesture = WristMenuParams.Default;
    [Tooltip("Palm-facing direction in the palm joint's local space (same convention as XRHandsInputSource)")]
    [SerializeField] private Vector3 palmNormalLocal = Vector3.down;
    [Tooltip("Button distance from the palm, meters along the palm normal")]
    [SerializeField] private float buttonOffset = 0.08f;

    [Header("Text")]
    [SerializeField] private string pauseText = "PAUSE";
    [SerializeField] private string resumeText = "RESUME";

    private readonly WristMenuGesture _gesture = new WristMenuGesture();

    private void Update()
    {
        HandGestureTracker t = tracker != null ? tracker : HandGestureTracker.Instance;
        Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
        MatchStateMachine m = director != null ? director.Match : null;

        // Nothing to pause in the menu (the main panel is already up).
        bool inMatch = m != null && m.Phase != MatchPhase.Menu && m.Phase != MatchPhase.Boot;
        bool tracked = t != null && h != null && inMatch;

        HandGestureTracker.HandState hand = default;
        Vector3 normal = Vector3.up;
        float facing = 0f;
        if (tracked)
        {
            hand = useLeftHand ? t.Left : t.Right;
            tracked = hand.IsTracked;
            normal = hand.PalmRotation * palmNormalLocal;
            facing = Vector3.Dot(normal.normalized, (h.position - hand.PalmPosition).normalized);
        }

        bool pressed = _gesture.Step(tracked, facing, hand.FistStrength, hand.PinchStrength,
            Time.unscaledDeltaTime, gesture);
        if (pressed) director.TogglePause();

        if (button == null) return;
        bool show = _gesture.IsVisible;
        if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
        if (!show) return;

        // The button lives in seat space (scaled with the tabletop view), so the offset scales too.
        button.position = hand.PalmPosition + normal.normalized * (buttonOffset * t.WorldScale);
        // TMP text reads correctly when its +Z points away from the viewer.
        button.rotation = Quaternion.LookRotation(button.position - h.position, Vector3.up);
        if (label != null) label.text = m.IsPaused ? resumeText : pauseText;
    }
}
