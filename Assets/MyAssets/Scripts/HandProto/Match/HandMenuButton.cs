using TMPro;
using UnityEngine;

// A big world-space menu button (T7) selected by pointing + pinching
// (HandMenuPointer). Needs a trigger collider on the same object; menus are
// only shown outside the fight, so it never catches beams.
[RequireComponent(typeof(Collider))]
public class HandMenuButton : MonoBehaviour
{
    [SerializeField] private MatchDirector director;
    [SerializeField] private MatchDirector.MenuAction action;

    [Header("Visuals")]
    [Tooltip("Tinted while pointed at")]
    [SerializeField] private Renderer background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color idleColor = new Color(0.15f, 0.2f, 0.3f);
    [SerializeField] private Color hoverColor = new Color(0.25f, 0.6f, 1f);
    [Tooltip("Scale multiplier while pointed at")]
    [SerializeField] private float hoverScale = 1.08f;

    private MaterialPropertyBlock _block;
    private Vector3 _baseScale;
    private bool _hovered;

    public MatchDirector.MenuAction Action => action;

    private void Awake()
    {
        _baseScale = transform.localScale;
        ApplyVisuals();
    }

    private void OnDisable()
    {
        SetHovered(false);
    }

    public void SetHovered(bool hovered)
    {
        if (_hovered == hovered) return;
        _hovered = hovered;
        ApplyVisuals();
    }

    public void Press()
    {
        if (director != null) director.HandleMenuAction(action);
    }

    private void ApplyVisuals()
    {
        if (_baseScale == Vector3.zero) _baseScale = transform.localScale;
        transform.localScale = _hovered ? _baseScale * hoverScale : _baseScale;

        if (background == null) return;
        _block ??= new MaterialPropertyBlock();
        Color c = _hovered ? hoverColor : idleColor;
        _block.SetColor("_BaseColor", c);
        _block.SetColor("_Color", c);
        background.SetPropertyBlock(_block);
    }
}
