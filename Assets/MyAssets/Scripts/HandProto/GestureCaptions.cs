using HandHero.Core;
using TMPro;
using UnityEngine;

// Demo mode (round 5, T4 / D6): a small world-space label beside each hand names
// the gesture just made: GRAB (the clutch closes), PINCH = FIRE (a shot; CURSOR:
// TRIGGER = FIRE), HOLD = CHARGE (a charge starts or a charged shot fires),
// PUSH = SHOCKWAVE (beside the hand that pushed). Each shows for
// DemoCaptionParams.ShowTime, then fades. The rules live in Core
// (DemoCaptionModel, DemoCaptionLayout, DemoPushHand); this reads the player's
// controllers, places the two labels on the outer side of the hands (stepped
// down off the hero), faces them to the head and keeps them readable in a
// recording (bold white; the dark outline is the labels' shared TMP material,
// DemoCaption, made by the scene builder). The labels live in seat space, so
// they keep their physical size in the tabletop view. Only while the demo is on.
public class GestureCaptions : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Shows captions only while this demo is on")]
    [SerializeField] private DemoDirector demo;
    [Tooltip("Optional. Falls back to HandGestureTracker.Instance")]
    [SerializeField] private HandGestureTracker tracker;
    [Tooltip("Head camera: captions face it and step off the hero as seen from it. Falls back to Camera.main")]
    [SerializeField] private Transform head;
    [Tooltip("The player's input source (GRAB = the clutch closing)")]
    [SerializeField] private HandInputSourceBehaviour playerInput;
    [Tooltip("The player's beam: shots, charged shots and the charge start")]
    [SerializeField] private PointingBeamController playerAim;
    [Tooltip("The player's shockwave (palm push)")]
    [SerializeField] private ShockwaveController playerShockwave;
    [Tooltip("Optional. CURSOR fires with the index trigger, so its caption says TRIGGER = FIRE")]
    [SerializeField] private AimModeSetting aimModeSetting;
    [Tooltip("The player's hero: a caption never sits over it")]
    [SerializeField] private Transform playerHero;
    [Tooltip("Caption beside the left hand (seat space)")]
    [SerializeField] private TMP_Text leftLabel;
    [Tooltip("Caption beside the right hand (seat space)")]
    [SerializeField] private TMP_Text rightLabel;

    [Header("Hands")]
    [Tooltip("The clutch (GRAB) hand is the left hand; keep in step with XRHandsInputSource.clutchUsesLeftHand")]
    [SerializeField] private bool clutchIsLeft = true;
    [Tooltip("Palm-facing direction in the palm joint's local space (same as XRHandsInputSource): tells which hand pushed")]
    [SerializeField] private Vector3 palmNormalLocal = Vector3.down;

    [Header("Timing and placement")]
    [Tooltip("Seconds at full strength, then the fade")]
    [SerializeField] private DemoCaptionParams timing = DemoCaptionParams.Default;
    [Tooltip("Offsets from the palm (physical meters) and the hero clearance")]
    [SerializeField] private DemoCaptionLayoutParams layout = DemoCaptionLayoutParams.Default;

    [Header("Look")]
    [Tooltip("Caption text color (the outline width and color are on the labels' DemoCaption material)")]
    [SerializeField] private Color textColor = Color.white;

    private sealed class Label
    {
        public TMP_Text Text;
        public DemoCaption Shown;
        public bool Cursor;
        public float Alpha = -1f;
    }

    private readonly DemoCaptionModel _model = new DemoCaptionModel();
    private Label _left;
    private Label _right;
    private bool _running;
    private bool _shockwaveFired;
    private Vector3 _lastLeftPalm;
    private Vector3 _lastRightPalm;
    private bool _hadLeft;
    private bool _hadRight;
    private float _leftPushSpeed;
    private float _rightPushSpeed;
    private bool _warmup;
    private Vector3 _warmupScale = Vector3.one;

    private void Awake()
    {
        _left = SetupLabel(leftLabel, true);
        _right = SetupLabel(rightLabel, false);
    }

    private void OnEnable()
    {
        if (playerShockwave != null) playerShockwave.Fired += OnShockwave;
    }

    private void OnDisable()
    {
        if (playerShockwave != null) playerShockwave.Fired -= OnShockwave;
    }

    private void OnShockwave() => _shockwaveFired = true;

    private void LateUpdate()
    {
        if (_warmup) return;
        if (demo == null || !demo.IsActive)
        {
            if (_running)
            {
                _running = false;
                HideLabels();
            }
            _shockwaveFired = false;
            return;
        }
        if (!_running)
        {
            _running = true;
            _model.Reset();
            _hadLeft = false;
            _hadRight = false;
        }

        HandGestureTracker t = tracker != null ? tracker : HandGestureTracker.Instance;
        // Unscaled: captions finish fading while the game is paused.
        float dt = Time.unscaledDeltaTime;
        if (t != null && dt > 0f)
        {
            _leftPushSpeed = PushSpeed(t.Left, ref _lastLeftPalm, ref _hadLeft, dt);
            _rightPushSpeed = PushSpeed(t.Right, ref _lastRightPalm, ref _hadRight, dt);
        }

        HandInputData input = playerInput != null ? playerInput.Current : default;
        var frame = new DemoGestureFrame
        {
            ClutchHeld = input.ClutchHeld,
            Charging = playerAim != null && playerAim.IsCharging,
            ShotsFired = playerAim != null ? playerAim.ShotsFired : 0,
            ChargeShotsFired = playerAim != null ? playerAim.ChargeShotsFired : 0,
            Shockwave = _shockwaveFired,
        };
        if (_shockwaveFired)
        {
            _shockwaveFired = false;
            bool pushedLeft = DemoPushHand.IsLeft(t != null && t.Left.IsTracked, _leftPushSpeed,
                t != null && t.Right.IsTracked, _rightPushSpeed, clutchIsLeft);
            frame.ShockwaveByClutchHand = pushedLeft == clutchIsLeft;
        }
        _model.Step(frame, dt, timing);

        Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
        if (t == null || h == null)
        {
            HideLabels();
            return;
        }

        bool cursor = aimModeSetting != null && aimModeSetting.Mode == AimMode.Cursor;
        float scale = t.WorldScale;
        UpdateLabel(_left, t.Left, true, clutchIsLeft ? _model.Clutch : _model.Aim,
            clutchIsLeft ? _model.ClutchAlpha : _model.AimAlpha, cursor, h, scale);
        UpdateLabel(_right, t.Right, false, clutchIsLeft ? _model.Aim : _model.Clutch,
            clutchIsLeft ? _model.AimAlpha : _model.ClutchAlpha, cursor, h, scale);
    }

    // Palm speed along the palm normal (m/s, tracking space), the push recognizers'
    // measure. The XR Origin never rotates, so the world palm rotation is also the
    // tracking-space one.
    private float PushSpeed(HandGestureTracker.HandState hand, ref Vector3 last, ref bool had, float dt)
    {
        if (!hand.IsTracked)
        {
            had = false;
            return 0f;
        }
        Vector3 now = hand.TrackingPalmPosition;
        Vector3 normal = (hand.PalmRotation * palmNormalLocal).normalized;
        float speed = had ? Vector3.Dot(now - last, normal) / dt : 0f;
        last = now;
        had = true;
        return speed;
    }

    private void UpdateLabel(Label label, HandGestureTracker.HandState hand, bool leftHand, DemoCaption caption,
        float alpha, bool cursor, Transform h, float scale)
    {
        if (label == null || label.Text == null) return;
        bool show = caption != DemoCaption.None && alpha > 0f && hand.IsTracked;
        GameObject go = label.Text.gameObject;
        if (go.activeSelf != show) go.SetActive(show);
        if (!show) return;

        // Text and alpha change only when they change (TMP rebuilds its mesh on each set).
        if (caption != label.Shown || cursor != label.Cursor)
        {
            label.Shown = caption;
            label.Cursor = cursor;
            label.Text.text = DemoCaptionText.Text(caption, cursor);
        }
        float stepped = Mathf.Round(alpha * 50f) / 50f;
        if (stepped != label.Alpha)
        {
            label.Alpha = stepped;
            label.Text.alpha = stepped;
        }

        bool hasHero = playerHero != null && playerHero.gameObject.activeInHierarchy;
        Vector3 at = DemoCaptionLayout.Place(h.position, hand.PalmPosition, h.right, leftHand, hasHero,
            hasHero ? playerHero.position : Vector3.zero, scale, layout);
        Transform tr = label.Text.transform;
        tr.position = at;
        // TMP text reads correctly when its +Z points away from the viewer.
        Vector3 away = at - h.position;
        if (away.sqrMagnitude > 1e-8f) tr.rotation = Quaternion.LookRotation(away, Vector3.up);
    }

    private void HideLabels()
    {
        if (_left != null && _left.Text != null && _left.Text.gameObject.activeSelf) _left.Text.gameObject.SetActive(false);
        if (_right != null && _right.Text != null && _right.Text.gameObject.activeSelf)
            _right.Text.gameObject.SetActive(false);
    }

    // The text runs outward from the anchor DemoCaptionLayout returns: the left
    // caption ends at it, the right caption starts at it. Only vertex color and
    // layout here: the outline lives on the shared material, so no label gets a
    // material instance and no label has to be switched on at load (pre-review
    // T4-P1: TMP's outlineWidth setter never turns on the OUTLINE_ON keyword).
    private Label SetupLabel(TMP_Text text, bool left)
    {
        var label = new Label { Text = text };
        if (text == null) return label;
        text.rectTransform.pivot = new Vector2(left ? 1f : 0f, 0.5f);
        text.horizontalAlignment = left ? HorizontalAlignmentOptions.Right : HorizontalAlignmentOptions.Left;
        text.color = textColor;
        return label;
    }

    // Round 4 S1 warmup (pre-review T4-P4): RenderWarmup draws the right label
    // with its outline material for a few frames at scene load, small and far
    // ahead, so the first caption of a demo builds no pipeline state mid-recording.
    // Called every warmup frame after this LateUpdate; off restores the label.
    public void ShowWarmup(bool on, Vector3 at, Quaternion rotation, float scale)
    {
        Label label = _right;
        if (label == null || label.Text == null) return;
        Transform tr = label.Text.transform;
        if (on)
        {
            if (!_warmup)
            {
                _warmup = true;
                _warmupScale = tr.localScale;
                label.Text.text = DemoCaptionText.PushShockwave;
                label.Text.alpha = 1f;
            }
            tr.SetPositionAndRotation(at, rotation);
            tr.localScale = _warmupScale * scale;
            if (!label.Text.gameObject.activeSelf) label.Text.gameObject.SetActive(true);
            return;
        }
        if (!_warmup) return;
        _warmup = false;
        tr.localScale = _warmupScale;
        label.Text.gameObject.SetActive(false);
        // The next caption sets its own text and alpha again.
        label.Shown = DemoCaption.None;
        label.Alpha = -1f;
    }
}
