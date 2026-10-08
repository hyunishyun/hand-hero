using HandHero.Core;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;

// VR arena <-> passthrough tabletop (T10, optional, off by default).
// Tabletop shrinks the 35 m arena to about 1 m on the real table by scaling the
// viewer, not the arena: gameplay keeps running in full-size arena units with
// the same tuning. Only the XR Origin's uniform scale and the static Camera
// Offset change; the origin's position and rotation are never touched (ADR 4/5).
// Hands stay physical: HandGestureTracker.WorldScale reports the scale so hand
// thresholds use tracking-space meters. Switched from the main menu only.
public class ArenaViewMode : MonoBehaviour
{
    private const string TabletopKey = "HandHero.Tabletop";

    [Header("References")]
    [SerializeField] private XROrigin origin;
    [Tooltip("Arena root (its position is the arena center), same as FlyingCharacter.arenaCenter")]
    [SerializeField] private Transform arenaCenter;
    [Tooltip("Head camera: cleared to transparent in the tabletop mode so passthrough shows")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("Enabled only in the tabletop mode: AR Session + AR Camera Manager (Meta passthrough)")]
    [SerializeField] private Behaviour[] passthroughOnly;
    [Tooltip("Optional. Menu button label, names the mode the button switches to")]
    [SerializeField] private TMP_Text toggleLabel;

    [Header("Layout")]
    [Tooltip("Seated eye height above the XR Origin in the VR arena, meters")]
    [SerializeField] private float eyeHeight = 1.2f;
    [Tooltip("Arena width, meters (FlyingCharacter.arenaSize.x)")]
    [SerializeField] private float arenaWidth = 35f;
    [SerializeField] private TabletopParams tabletop = TabletopParams.Default;

    [Header("Behaviour")]
    [Tooltip("Start in the mode chosen last time. Off (default): every launch starts in the VR arena, " +
             "so an accidental MR TABLE press never sticks across runs")]
    [SerializeField] private bool rememberChoice = false;
    [SerializeField] private string toTabletopText = "MR TABLE";
    [SerializeField] private string toArenaText = "VR ARENA";

    private CameraClearFlags _arenaClearFlags;
    private Color _arenaBackground;

    public bool IsTabletop { get; private set; }
    public ViewLayout Layout { get; private set; }

    private void Awake()
    {
        if (viewCamera != null)
        {
            _arenaClearFlags = viewCamera.clearFlags;
            _arenaBackground = viewCamera.backgroundColor;
        }
        Apply(rememberChoice && PlayerPrefs.GetInt(TabletopKey, 0) == 1);
    }

    public void Toggle() => SetTabletop(!IsTabletop);

    public void SetTabletop(bool tabletopOn)
    {
        Apply(tabletopOn);
        if (!rememberChoice) return;
        PlayerPrefs.SetInt(TabletopKey, tabletopOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void Apply(bool tabletopOn)
    {
        IsTabletop = tabletopOn && arenaCenter != null;
        Layout = IsTabletop
            ? ViewLayout.Tabletop(arenaCenter.position, arenaWidth, tabletop)
            : ViewLayout.Arena(eyeHeight);

        if (origin != null)
        {
            // Scale only. Position and rotation stay at the world origin.
            origin.transform.localScale = Vector3.one * Layout.WorldScale;
            if (origin.CameraFloorOffsetObject != null)
                origin.CameraFloorOffsetObject.transform.localPosition = Layout.CameraOffsetLocal;
            // Device tracking origin: XROrigin re-applies this height on start.
            origin.CameraYOffset = Layout.CameraOffsetLocal.y;
        }

        if (passthroughOnly != null)
            foreach (Behaviour b in passthroughOnly)
                if (b != null) b.enabled = IsTabletop;

        if (viewCamera != null)
        {
            viewCamera.clearFlags = IsTabletop ? CameraClearFlags.SolidColor : _arenaClearFlags;
            viewCamera.backgroundColor = IsTabletop ? Color.clear : _arenaBackground;
        }

        if (toggleLabel != null) toggleLabel.text = IsTabletop ? toArenaText : toTabletopText;
    }
}
