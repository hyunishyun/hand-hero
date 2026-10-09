using HandHero.Core;
using UnityEngine;

// Depth cue for third-person flight: a disc on the arena floor straight below
// the hero plus a faint drop line. Judging how far away a flying hero is in an
// empty 35 m box is hard without it. Presentation only, no collider.
public class HeroGroundMarker : MonoBehaviour
{
    [SerializeField] private FlyingCharacter character;
    [Tooltip("Optional. Mark this instead of the hero (CURSOR aim marker); the hero still gives bounds and visibility")]
    [SerializeField] private Transform follow;
    [Tooltip("Flat disc (no collider) placed on the floor below the hero")]
    [SerializeField] private Transform disc;
    [Tooltip("Optional thin line from the hero down to the disc")]
    [SerializeField] private LineRenderer dropLine;

    [Tooltip("Disc diameter when the hero is on the floor")]
    [SerializeField] private float nearSize = 2.2f;
    [Tooltip("Disc diameter when the hero is at the arena ceiling")]
    [SerializeField] private float farSize = 0.8f;
    [SerializeField] private float floorLift = 0.03f;

    [Header("Tracking-lost cue (D7)")]
    [Tooltip("Optional. The player's input: the marker turns grey while its clutch hand is untracked. Empty for bots")]
    [SerializeField] private HandInputSourceBehaviour clutchInput;
    [Tooltip("Disc and drop line color while the clutch hand is untracked")]
    [SerializeField] private Color clutchLostColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

    // Visibility is applied only when it changes (GC-8).
    private bool _visibleApplied;
    private bool _visible;
    private Renderer _discRenderer;
    private Color _lineStart, _lineEnd;
    private bool _lostShown;

    private void Awake()
    {
        if (dropLine != null)
        {
            dropLine.positionCount = 2;
            _lineStart = dropLine.startColor;
            _lineEnd = dropLine.endColor;
        }
        if (disc != null) _discRenderer = disc.GetComponent<Renderer>();
    }

    // Applied on change only.
    private void ShowClutchLost(bool lost)
    {
        if (lost == _lostShown) return;
        _lostShown = lost;
        RendererTint.Set(_discRenderer, lost, clutchLostColor);
        if (dropLine != null)
        {
            dropLine.startColor = lost ? clutchLostColor : _lineStart;
            dropLine.endColor = lost ? clutchLostColor : _lineEnd;
        }
    }

    private void LateUpdate()
    {
        ArenaBounds bounds = character != null ? character.Bounds : default;
        bool visible = character != null && character.IsAlive && bounds.Enabled &&
                       (follow == null || follow.gameObject.activeInHierarchy);
        if (!_visibleApplied || visible != _visible)
        {
            _visibleApplied = true;
            _visible = visible;
            if (disc != null) disc.gameObject.SetActive(visible);
            if (dropLine != null) dropLine.enabled = visible;
        }
        if (!visible) return;
        ShowClutchLost(clutchInput != null && clutchInput.isActiveAndEnabled && clutchInput.Current.ClutchHandLost);

        Vector3 hero = follow != null ? follow.position : character.transform.position;
        float floorY = bounds.Center.y - bounds.Size.y * 0.5f;
        Vector3 ground = new Vector3(hero.x, floorY + floorLift, hero.z);
        float height01 = bounds.Size.y > 0f ? Mathf.Clamp01((hero.y - floorY) / bounds.Size.y) : 0f;

        if (disc != null)
        {
            disc.position = ground;
            float size = Mathf.Lerp(nearSize, farSize, height01);
            disc.localScale = new Vector3(size, disc.localScale.y, size);
        }

        if (dropLine != null)
        {
            dropLine.SetPosition(0, hero);
            dropLine.SetPosition(1, ground);
        }
    }
}
