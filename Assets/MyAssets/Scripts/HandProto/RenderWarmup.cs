using System.Collections.Generic;
using UnityEngine;

// GPU warmup at scene load (round 4, S1, D1). On device the first ~30 s of the
// first run dropped frames that later runs did not; part of that is the GPU
// building shader pipeline states the first time a run's objects are drawn.
// For a few frames after the scene loads, while the menu opens, this draws one
// pooled run bot, a beam in each beam color, a beam impact, a kill burst, the
// damage vignette at an invisible alpha and the charge orb, small and far in
// front of the camera, then hands everything back to its pool.
//
// Drawing the real objects in the real frame is the warmup that matches the
// device's render pass. Unity's GraphicsStateCollection needs a collection
// traced on the device first, so it is not used yet (QUESTIONS_FOR_HYUN).
// Runs after every other LateUpdate, so the vignette and charge orb owners
// cannot hide them again before the frame renders.
[DefaultExecutionOrder(1000)]
public class RenderWarmup : MonoBehaviour
{
    [Tooltip("Frames the warmup objects are drawn for (2-3 builds their pipeline states)")]
    [SerializeField] private int frames = 3;
    [Tooltip("Meters in front of the camera where the warmup objects are drawn")]
    [SerializeField] private float distance = 12f;
    [Tooltip("Scale of the warmup objects: small but non-zero so they still render")]
    [SerializeField] private float scale = 0.05f;

    [Header("References")]
    [Tooltip("Falls back to Camera.main")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("Lends one pooled run bot")]
    [SerializeField] private RunDirector run;
    [Tooltip("Effect prefabs served by a BeamImpactPool (beam impact, kill burst)")]
    [SerializeField] private GameObject[] effectPrefabs;
    [Tooltip("The beams' line material")]
    [SerializeField] private Material beamMaterial;
    [Tooltip("One warmup beam per color: player, bot, crit")]
    [SerializeField] private Color[] beamColors =
    {
        new Color(0.4f, 0.9f, 1f),
        new Color(1f, 0.25f, 0.2f),
        new Color(1f, 0.85f, 0.2f),
    };
    [SerializeField] private DamageVignette vignette;
    [Tooltip("The player's charge orb (inactive until a charge)")]
    [SerializeField] private Transform chargeOrb;

    // For PerfSpikeLogger's session header.
    public static bool IsRunning { get; private set; }
    public static float LastMs { get; private set; } = -1f;
    public static int LastFrames { get; private set; }

    private readonly List<PooledEffect> _effects = new List<PooledEffect>();
    private readonly List<Vector3> _effectScales = new List<Vector3>();
    private readonly List<GameObject> _beams = new List<GameObject>();
    private RunBot _bot;
    private Vector3 _spot;
    private Vector3 _up = Vector3.up;
    private Vector3 _orbScale = Vector3.one;
    private double _start;
    private int _frame;
    private bool _done;

    private void Awake()
    {
        // Before any Update: the logger's first menu flush waits for the warmup time.
        IsRunning = enabled && frames > 0;
        LastMs = -1f;
        LastFrames = 0;
    }

    private void LateUpdate()
    {
        if (_done) return;
        if (_frame == 0) Begin();
        if (_frame >= frames)
        {
            End();
            return;
        }
        Hold();
        _frame++;
    }

    private void OnDisable()
    {
        if (!_done && _frame > 0) End();
        else if (!_done) IsRunning = false;
    }

    private void Begin()
    {
        _start = Time.realtimeSinceStartupAsDouble;
        Camera cam = viewCamera != null ? viewCamera : Camera.main;
        Transform view = cam != null ? cam.transform : transform;
        _spot = view.position + view.forward * distance;
        _up = view.up;
        Vector3 right = view.right;

        if (run != null)
        {
            _bot = run.BorrowWarmupBot();
            if (_bot != null) _bot.ShowForWarmup(_spot, scale);
        }

        if (effectPrefabs != null)
        {
            for (int i = 0; i < effectPrefabs.Length; i++)
            {
                PooledEffect fx = BeamImpactPool.Borrow(effectPrefabs[i]);
                if (fx == null) continue;
                // Disabled first: the effect's OnEnable / Update would play and rescale it.
                fx.enabled = false;
                _effects.Add(fx);
                _effectScales.Add(fx.transform.localScale);
                fx.transform.SetPositionAndRotation(_spot + right * (0.2f * (i + 1)), Quaternion.identity);
                fx.transform.localScale = fx.transform.localScale * scale;
                fx.gameObject.SetActive(true);
            }
        }

        if (beamMaterial != null && beamColors != null)
        {
            for (int i = 0; i < beamColors.Length; i++)
            {
                var go = new GameObject("WarmupBeam");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = beamMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.useWorldSpace = true;
                line.positionCount = 2;
                Vector3 from = _spot - right * 0.3f + _up * (0.05f * i);
                line.SetPosition(0, from);
                line.SetPosition(1, from + right * 0.25f);
                line.widthMultiplier = 0.01f;
                line.startColor = line.endColor = beamColors[i];
                _beams.Add(go);
            }
        }

        if (chargeOrb != null) _orbScale = chargeOrb.localScale;
    }

    // Every warmup frame, after the owners' own Update / LateUpdate.
    private void Hold()
    {
        if (chargeOrb != null)
        {
            if (!chargeOrb.gameObject.activeSelf) chargeOrb.gameObject.SetActive(true);
            chargeOrb.position = _spot + _up * 0.2f;
            chargeOrb.localScale = Vector3.one * scale;
        }
        if (vignette != null) vignette.ShowWarmup(true);
    }

    private void End()
    {
        _done = true;

        if (_bot != null && run != null) run.ReturnWarmupBot(_bot);
        _bot = null;

        for (int i = 0; i < _effects.Count; i++)
        {
            PooledEffect fx = _effects[i];
            if (fx == null) continue;
            fx.gameObject.SetActive(false);
            fx.transform.localScale = _effectScales[i];
            fx.enabled = true; // inactive object: no OnEnable until its next Play
            fx.ReturnToPool();
        }
        _effects.Clear();
        _effectScales.Clear();

        for (int i = 0; i < _beams.Count; i++)
            if (_beams[i] != null) Destroy(_beams[i]);
        _beams.Clear();

        if (chargeOrb != null)
        {
            chargeOrb.localScale = _orbScale;
            if (chargeOrb.gameObject.activeSelf) chargeOrb.gameObject.SetActive(false);
        }
        if (vignette != null) vignette.ShowWarmup(false);

        LastMs = (float)((Time.realtimeSinceStartupAsDouble - _start) * 1000.0);
        LastFrames = _frame;
        IsRunning = false;
        Debug.Log($"[RenderWarmup] {LastFrames} frames, {LastMs:F1} ms");
        enabled = false;
    }
}
