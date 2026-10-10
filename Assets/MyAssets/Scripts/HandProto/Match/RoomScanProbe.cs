using System.Collections.Generic;
using HandHero.Core;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

// Round 4 (S8, D9): MR room-scan spike, a throwaway-safe capability probe.
// Only in MR TABLE, only in the main menu: asks once for the Meta scene
// permission, then runs AR Foundation's plane and bounding box managers (Meta
// OpenXR "Planes" / "Bounding Boxes" features, fed by the Quest's Space Setup)
// and logs what they report: subsystem start, time to first result, counts,
// classifications, sizes and the largest up-facing surface near where the
// arena floor appears. Only counts and sizes are logged, never room geometry.
// Without the permission, without the features, or if a subsystem fails, the
// managers stay off and MR TABLE works exactly as before. A debug wireframe of
// the found planes and boxes shows in dev builds.
// Round 5 (T1): a dismissed dialog counts as a denial, a failure logs one
// UNAVAILABLE line and nothing else, the permission JNI check is throttled and
// stops once the answer is known, and the summary and shutdown wait a few
// frames after the menu closes (console copies only in dev builds).
public class RoomScanProbe : MonoBehaviour
{
    public const string ScenePermission = "com.oculus.permission.USE_SCENE";

    public enum WireframeMode { Off, DevBuildsOnly, Always }

    [Tooltip("Run the probe in MR TABLE (off: it never asks for the permission and never starts the managers)")]
    [SerializeField] private bool probeEnabled = true;

    [Header("References")]
    [SerializeField] private ArenaViewMode viewMode;
    [Tooltip("The probe only runs while the match is in the main menu")]
    [SerializeField] private MatchDirector match;
    [Tooltip("Its Trackables transform is the tracking space the scan is measured in")]
    [SerializeField] private XROrigin origin;
    [Tooltip("On the XR Origin, disabled in the scene; enabled only while the probe runs")]
    [SerializeField] private ARPlaneManager planeManager;
    [Tooltip("On the XR Origin, disabled in the scene; enabled only while the probe runs")]
    [SerializeField] private ARBoundingBoxManager boxManager;
    [Tooltip("Arena root (its position is the arena center)")]
    [SerializeField] private Transform arena;

    [Header("Measurement")]
    [Tooltip("Arena floor height in arena-local units (the arena is 20 high, centered on the root)")]
    [SerializeField] private float arenaFloorLocalY = -10f;
    [Tooltip("A surface counts as near the table when its center is within this horizontal distance of the arena floor center, real meters")]
    [SerializeField] private float nearTableRadius = 1f;
    [Tooltip("... and within this height of the arena floor, real meters")]
    [SerializeField] private float nearTableHeightGap = 0.5f;
    [Tooltip("The summary is logged this many seconds after the last change, and again on stop")]
    [SerializeField] private float summaryDelay = 3f;
    [Tooltip("Seconds between permission checks (a JNI call) while the answer is still open; none once it is known")]
    [SerializeField] private float permissionPollInterval = 0.5f;
    [Tooltip("Frames the managers keep running after the MR TABLE menu ends; the summary and the shutdown then miss the match-start frame (0 = same frame)")]
    [SerializeField] private int stopDelayFrames = 2;

    [Header("Wireframe")]
    [Tooltip("Debug outline of the found planes and boxes: on in dev builds and the editor by default, off in release")]
    [SerializeField] private WireframeMode wireframe = WireframeMode.DevBuildsOnly;
    [Tooltip("Line material (vertex colors), e.g. the beam material")]
    [SerializeField] private Material wireframeMaterial;
    [Tooltip("Line width, real meters")]
    [SerializeField] private float wireWidth = 0.004f;
    [Tooltip("Plane outline color")]
    [SerializeField] private Color planeColor = new Color(0.2f, 1f, 0.6f, 0.9f);
    [Tooltip("Bounding box outline color")]
    [SerializeField] private Color boxColor = new Color(1f, 0.85f, 0.2f, 0.9f);

    private readonly RoomScanFlow _flow = new RoomScanFlow();
    private readonly RoomScanPermissionPoll _permissionPoll = new RoomScanPermissionPoll();
    private readonly RoomScanTimer _timer = new RoomScanTimer();
    private readonly List<RoomItem> _items = new List<RoomItem>();
    private readonly Dictionary<TrackableId, LineRenderer> _wires = new Dictionary<TrackableId, LineRenderer>();
    private readonly Vector3[] _corners = new Vector3[16];

    private bool _available;
    private bool _availabilityChecked;
    private bool _managersOn;
    private bool _dirty;
    private double _lastChange;
    private bool _summarySinceChange = true;

    // Written by the Android permission callbacks, read in Update.
    private const int AnswerNone = 0;
    private const int AnswerGranted = 1;
    private const int AnswerDenied = 2;
    private const int AnswerDismissed = 3; // round 5 (S8-1-2)
    private volatile int _permissionAnswer;

    private bool ShowWireframe =>
        wireframe == WireframeMode.Always
        || (wireframe == WireframeMode.DevBuildsOnly && (Debug.isDebugBuild || Application.isEditor));

    private void OnEnable()
    {
        if (planeManager != null) planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        if (boxManager != null) boxManager.trackablesChanged.AddListener(OnBoxesChanged);
    }

    private void OnDisable()
    {
        if (planeManager != null) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
        if (boxManager != null) boxManager.trackablesChanged.RemoveListener(OnBoxesChanged);
        SetManagers(false);
    }

    private void Update()
    {
        _flow.Enabled = probeEnabled;
        // Round 5 (F2-2): leaving the menu starts a match; the summary and the
        // manager shutdown come stopDelayFrames later, off the match-start frame.
        _flow.StopDelayFrames = stopDelayFrames;

        int answer = _permissionAnswer;
        if (answer != AnswerNone)
        {
            _permissionAnswer = AnswerNone;
            RoomScanEvent result = answer == AnswerDismissed
                ? _flow.OnPermissionDismissed()
                : _flow.OnPermissionResult(answer == AnswerGranted);
            Report(result, null);
        }

        bool tabletop = viewMode != null && viewMode.IsTabletop && InMenu();
        // Round 5 (S8-2-1 / F2-3 / F4-2): the JNI permission check runs only while the
        // answer is open, at most every permissionPollInterval seconds.
        _permissionPoll.Interval = permissionPollInterval;
        if (_permissionPoll.Due(tabletop && _flow.NeedsPermissionQuery, Time.realtimeSinceStartupAsDouble))
            _permissionPoll.Answer(Granted());
        RoomScanCommand c = _flow.Step(tabletop, tabletop && Available(), tabletop && _permissionPoll.LastAnswer);

        if (c.RequestPermission) RequestPermission();
        if (c.Event == RoomScanEvent.Stopped) LogSummary(force: false);
        // Round 5 (S8-1-3): a failed switch has logged UNAVAILABLE (Fail); nothing else follows.
        if (!SetManagers(c.RunManagers)) return;
        if (c.Event == RoomScanEvent.Started) Report(RoomScanEvent.Started, StartNote());
        else if (c.Event != RoomScanEvent.None) Report(c.Event, null);

        if (_managersOn) CheckManagersAlive();
        if (_managersOn && _dirty && !_summarySinceChange
            && Time.realtimeSinceStartupAsDouble - _lastChange >= summaryDelay)
            LogSummary(force: true);
    }

    private bool InMenu()
    {
        return match == null || match.Match == null || match.Match.Phase == MatchPhase.Menu;
    }

    // Is a plane or bounding box subsystem loaded? The Meta features create them at
    // XR start when enabled and supported; they never appear later, so check once.
    private bool Available()
    {
        if (_availabilityChecked) return _available;
        _availabilityChecked = true;
        XRLoader loader = XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null
            ? XRGeneralSettings.Instance.Manager.activeLoader
            : null;
        bool planes = loader != null && loader.GetLoadedSubsystem<XRPlaneSubsystem>() != null;
        bool boxes = loader != null && loader.GetLoadedSubsystem<XRBoundingBoxSubsystem>() != null;
        if (!planes && planeManager != null) planeManager.enabled = false;
        if (!boxes && boxManager != null) boxManager.enabled = false;
        _hasPlanes = planes && planeManager != null;
        _hasBoxes = boxes && boxManager != null;
        _available = _hasPlanes || _hasBoxes;
        HHLog.Info($"[RoomScanProbe] subsystems: planes={(planes ? 1 : 0)} boxes={(boxes ? 1 : 0)}");
        return _available;
    }

    private bool _hasPlanes;
    private bool _hasBoxes;

    private static bool Granted()
    {
#if UNITY_ANDROID
        if (Application.platform == RuntimePlatform.Android)
            return Permission.HasUserAuthorizedPermission(ScenePermission);
#endif
        return true; // editor / Link: no Android permission model
    }

    private void RequestPermission()
    {
#if UNITY_ANDROID
        if (Application.platform != RuntimePlatform.Android)
        {
            _permissionAnswer = AnswerGranted;
            return;
        }
        try
        {
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => _permissionAnswer = AnswerGranted;
            callbacks.PermissionDenied += _ => _permissionAnswer = AnswerDenied;
            // Round 5 (S8-1-2): closing the dialog without an answer used to leave the
            // probe waiting all session; it now counts as a denial for this session.
            callbacks.PermissionRequestDismissed += _ => _permissionAnswer = AnswerDismissed;
            Permission.RequestUserPermission(ScenePermission, callbacks);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[RoomScanProbe] permission request failed: {e.Message}");
            _permissionAnswer = AnswerDenied;
        }
#else
        _permissionAnswer = AnswerGranted;
#endif
    }

    // False when switching threw: Fail has turned the probe off and logged it.
    private bool SetManagers(bool on)
    {
        if (on == _managersOn) return true;
        _managersOn = on;
        try
        {
            if (planeManager != null) planeManager.enabled = on && _hasPlanes;
            if (boxManager != null) boxManager.enabled = on && _hasBoxes;
        }
        catch (System.Exception e)
        {
            Fail($"{(on ? "enabling" : "disabling")} the managers threw {e.GetType().Name}: {e.Message}");
            return false;
        }
        if (on && !_timer.Started) _timer.Start(Time.realtimeSinceStartupAsDouble);
        SetWiresVisible(on && ShowWireframe);
        return true;
    }

    // AR Foundation disables a manager whose subsystem fails to start.
    private void CheckManagersAlive()
    {
        bool planesDied = _hasPlanes && planeManager != null && !planeManager.enabled;
        bool boxesDied = _hasBoxes && boxManager != null && !boxManager.enabled;
        if (!planesDied && !boxesDied) return;
        if (planesDied) _hasPlanes = false;
        if (boxesDied) _hasBoxes = false;
        Debug.LogWarning($"[RoomScanProbe] subsystem stopped by AR Foundation: planes={(planesDied ? 1 : 0)} boxes={(boxesDied ? 1 : 0)}");
        if (!_hasPlanes && !_hasBoxes) Fail("no subsystem left running");
    }

    // Round 5 (S8-1-3): off for the rest of the session. UNAVAILABLE is logged once,
    // with the reason, and the flow reports nothing after it (no STARTED, STOPPED
    // or SUMMARY). The managers are switched off directly, never through
    // SetManagers, so a second throw cannot come back here.
    private void Fail(string why)
    {
        _hasPlanes = _hasBoxes = false;
        _managersOn = false;
        try
        {
            if (planeManager != null) planeManager.enabled = false;
            if (boxManager != null) boxManager.enabled = false;
        }
        catch (System.Exception) { /* already broken; reported below */ }
        SetWiresVisible(false);
        Debug.LogWarning($"[RoomScanProbe] off for this session: {why}");
        RoomScanEvent e = _flow.OnFailed();
        if (e != RoomScanEvent.None) PerfSpikeLogger.Mark(PerfRecordKind.RoomScan, (int)e, "failed: " + why);
    }

    private string StartNote()
    {
        return $"planes_subsystem={(_hasPlanes ? 1 : 0)} boxes_subsystem={(_hasBoxes ? 1 : 0)}";
    }

    // Round 5 (F2-2): the perf log line is the record. The console copy exists only
    // in the editor and dev builds (HHLog), so a release build never builds a
    // Debug.Log stack trace, e.g. for the summary when the menu closes.
    private void Report(RoomScanEvent e, string note)
    {
        PerfSpikeLogger.Mark(PerfRecordKind.RoomScan, (int)e, note);
        HHLog.Info($"[RoomScanProbe] {RoomScanSummary.EventName(e)}{(note != null ? " " + note : "")}");
    }

    private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        Changed();
        int count = planeManager.trackables.count;
        if (_timer.Observe(RoomItemKind.Plane, count, Time.realtimeSinceStartupAsDouble))
            Report(RoomScanEvent.FirstPlanes, $"count={count} after={_timer.FirstSeconds(RoomItemKind.Plane):F2}s");
        if (!ShowWireframe) return;
        foreach (ARPlane p in args.added) DrawPlane(p);
        foreach (ARPlane p in args.updated) DrawPlane(p);
        foreach (KeyValuePair<TrackableId, ARPlane> r in args.removed) RemoveWire(r.Key);
    }

    private void OnBoxesChanged(ARTrackablesChangedEventArgs<ARBoundingBox> args)
    {
        Changed();
        int count = boxManager.trackables.count;
        if (_timer.Observe(RoomItemKind.Box, count, Time.realtimeSinceStartupAsDouble))
            Report(RoomScanEvent.FirstBoxes, $"count={count} after={_timer.FirstSeconds(RoomItemKind.Box):F2}s");
        if (!ShowWireframe) return;
        foreach (ARBoundingBox b in args.added) DrawBox(b);
        foreach (ARBoundingBox b in args.updated) DrawBox(b);
        foreach (KeyValuePair<TrackableId, ARBoundingBox> r in args.removed) RemoveWire(r.Key);
    }

    private void Changed()
    {
        _dirty = true;
        _summarySinceChange = false;
        _lastChange = Time.realtimeSinceStartupAsDouble;
    }

    private void LogSummary(bool force)
    {
        if (!_dirty && !force) return;
        if (_summarySinceChange) return;
        _summarySinceChange = true;
        Transform space = TrackingSpace();
        if (space == null) return;

        CollectItems(space);
        Vector3 tablePoint = arena != null
            ? space.InverseTransformPoint(arena.TransformPoint(new Vector3(0f, arenaFloorLocalY, 0f)))
            : Vector3.zero;
        string line = RoomScanSummary.Format(_items, _timer, tablePoint, nearTableRadius, nearTableHeightGap);
        Report(RoomScanEvent.Summary, line);
    }

    private Transform TrackingSpace()
    {
        if (origin == null) return null;
        return origin.TrackablesParent != null ? origin.TrackablesParent : origin.transform;
    }

    // Tracking-space (real meter) items: only what the summary needs.
    private void CollectItems(Transform space)
    {
        _items.Clear();
        if (planeManager != null)
        {
            foreach (ARPlane p in planeManager.trackables)
            {
                _items.Add(new RoomItem
                {
                    Kind = RoomItemKind.Plane,
                    Label = p.classifications.ToString(),
                    HorizontalUp = p.alignment == PlaneAlignment.HorizontalUp,
                    Center = space.InverseTransformPoint(p.center),
                    Size = new Vector3(p.size.x, 0f, p.size.y),
                });
            }
        }
        if (boxManager != null)
        {
            foreach (ARBoundingBox b in boxManager.trackables)
            {
                _items.Add(new RoomItem
                {
                    Kind = RoomItemKind.Box,
                    Label = b.classifications.ToString(),
                    Center = space.InverseTransformPoint(b.transform.position),
                    Size = UprightSize(space, b.transform, b.size),
                });
            }
        }
    }

    // The box's local axis closest to tracking-space up becomes y; the other two
    // are the footprint, so a yawed box keeps its true top-face area.
    private static Vector3 UprightSize(Transform space, Transform box, Vector3 size)
    {
        Vector3 up = space.up;
        float dx = Mathf.Abs(Vector3.Dot(box.right, up));
        float dy = Mathf.Abs(Vector3.Dot(box.up, up));
        float dz = Mathf.Abs(Vector3.Dot(box.forward, up));
        if (dx >= dy && dx >= dz) return new Vector3(size.y, size.x, size.z);
        if (dz >= dy && dz >= dx) return new Vector3(size.x, size.z, size.y);
        return size;
    }

    // ---- debug wireframe (world-space lines; the trackables never move here) ----

    private float WorldWidth => wireWidth * (origin != null ? origin.transform.lossyScale.x : 1f);

    private LineRenderer Wire(ARTrackable trackable, Color color)
    {
        if (_wires.TryGetValue(trackable.trackableId, out LineRenderer lr) && lr != null) return lr;
        var go = new GameObject("ScanWire");
        go.transform.SetParent(trackable.transform, false);
        lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.sharedMaterial = wireframeMaterial;
        lr.startColor = lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = _managersOn;
        _wires[trackable.trackableId] = lr;
        return lr;
    }

    private void DrawPlane(ARPlane p)
    {
        LineRenderer lr = Wire(p, planeColor);
        Vector2 c = p.centerInPlaneSpace;
        Vector2 h = p.size * 0.5f;
        Transform t = p.transform;
        _corners[0] = t.TransformPoint(new Vector3(c.x - h.x, 0f, c.y - h.y));
        _corners[1] = t.TransformPoint(new Vector3(c.x + h.x, 0f, c.y - h.y));
        _corners[2] = t.TransformPoint(new Vector3(c.x + h.x, 0f, c.y + h.y));
        _corners[3] = t.TransformPoint(new Vector3(c.x - h.x, 0f, c.y + h.y));
        _corners[4] = _corners[0];
        lr.widthMultiplier = WorldWidth;
        lr.positionCount = 5;
        lr.SetPositions(_corners);
    }

    // One strip through all 12 edges: b0 b1 b2 b3 b0 t0 t1 b1 t1 t2 b2 t2 t3 b3 t3 t0.
    private static readonly int[] BoxPath = { 0, 1, 2, 3, 0, 4, 5, 1, 5, 6, 2, 6, 7, 3, 7, 4 };
    private readonly Vector3[] _boxCorners = new Vector3[8];

    private void DrawBox(ARBoundingBox b)
    {
        LineRenderer lr = Wire(b, boxColor);
        Vector3 h = b.size * 0.5f;
        Transform t = b.transform;
        for (int i = 0; i < 8; i++)
        {
            float x = (i == 1 || i == 2 || i == 5 || i == 6) ? h.x : -h.x;
            float y = i >= 4 ? h.y : -h.y;
            float z = (i == 2 || i == 3 || i == 6 || i == 7) ? h.z : -h.z;
            _boxCorners[i] = t.TransformPoint(new Vector3(x, y, z));
        }
        for (int i = 0; i < BoxPath.Length; i++) _corners[i] = _boxCorners[BoxPath[i]];
        lr.widthMultiplier = WorldWidth;
        lr.positionCount = BoxPath.Length;
        lr.SetPositions(_corners);
    }

    private void RemoveWire(TrackableId id)
    {
        if (!_wires.TryGetValue(id, out LineRenderer lr)) return;
        _wires.Remove(id);
        if (lr != null) Destroy(lr.gameObject);
    }

    private void SetWiresVisible(bool visible)
    {
        foreach (LineRenderer lr in _wires.Values)
            if (lr != null) lr.enabled = visible;
    }
}
