using HandHero.Core;
using UnityEngine;

// Puppeteer with a clutch and RELATIVE (mouse-style) mapping.
// While the clutch (fist) is held, hand movement deltas drive the character's target:
//   target += handDelta * positionScale
// Releasing lets the character glide and the hand return to a comfortable
// position — exactly like lifting a mouse to reposition it.
//
// Relative mapping (instead of absolute hand->box mapping) was chosen because:
//  - no calibration of a neutral point is needed,
//  - a tracking glitch moves the character a little, not to a glitch position,
//  - the reachable arena is unlimited: clutch, drag, release, repeat.
//
// Input comes only as HandInputData (ADR 9): hands, debug keyboard/mouse, a bot
// or a test script drive this the same way. Fist thresholds live in XRHandsInputSource.
public class HandPuppeteerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlyingCharacter character;
    [Tooltip("Where this hero's input comes from (XR hands, debug keyboard/mouse, ...)")]
    [SerializeField] private HandInputSourceBehaviour inputSource;

    [Header("Mapping")]
    [Tooltip("World meters the character target moves per meter of hand movement")]
    [SerializeField] private float positionScale = 60f;

    [Header("Feedback (optional)")]
    [SerializeField] private Renderer clutchIndicator; // tinted while clutched
    [SerializeField] private Color clutchedColor = new Color(0.3f, 1f, 0.5f);
    [SerializeField] private Color releasedColor = new Color(1f, 1f, 1f, 0.4f);

    // Relative mapping (unit-tested in HandHero.Core).
    private readonly ClutchMapper _clutch = new ClutchMapper();
    private IHandInputSource _sourceOverride;

    public float PositionScale => positionScale;

    // Code-assigned source (bot, test). Takes priority over the inspector field.
    public void SetInputSource(IHandInputSource source)
    {
        _sourceOverride = source;
    }

    private IHandInputSource Source()
    {
        if (_sourceOverride != null) return _sourceOverride;
        return inputSource != null ? inputSource : null;
    }

    private void Update()
    {
        IHandInputSource source = Source();
        if (source == null || character == null) return;

        // Dead hero: drop the clutch so a fist still closed at respawn regrabs
        // from the spawn point instead of dragging toward the old target.
        if (!character.IsAlive)
        {
            if (_clutch.IsClutched) OnRelease();
            _clutch.Reset();
            return;
        }

        // Charging no longer roots the hero: the charge shot is a right-hand pinch
        // hold, so the left hand keeps flying it, slowed by PointingBeamController.
        HandInputData input = source.Current;

        // A lost hand arrives as ClutchHeld = false, so the character glides
        // instead of teleporting when the hand comes back somewhere else.
        // Grabbing starts from where the character currently is — no snap.
        ClutchResult clutch = _clutch.Step(input, character.transform.position, positionScale);

        if (clutch.JustGrabbed) OnGrab();
        if (clutch.JustReleased) OnRelease();
        if (clutch.Clutched) character.SetTarget(clutch.Target);
    }

    // A pooled bot reused while it was clutching (SP-9), or any disable: let go,
    // so the next grab starts from the hero instead of the old target.
    private void OnDisable()
    {
        if (_clutch.IsClutched && character != null) character.ClearTarget();
        _clutch.Reset();
    }

    private void OnGrab()
    {
        TintClutchIndicator(clutchedColor);
    }

    private void OnRelease()
    {
        character.ClearTarget();

        TintClutchIndicator(releasedColor);
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock _indicatorBlock;

    // Property block on the shared material, not a material clone (D4).
    private void TintClutchIndicator(Color color)
    {
        if (clutchIndicator == null) return;
        _indicatorBlock ??= new MaterialPropertyBlock();
        _indicatorBlock.SetColor(BaseColorId, color);
        _indicatorBlock.SetColor(ColorId, color);
        clutchIndicator.SetPropertyBlock(_indicatorBlock);
    }
}
