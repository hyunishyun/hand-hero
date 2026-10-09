using HandHero.Core;
using UnityEngine;

// Freeze hunt (P5, SP-3/GC-3): one scene-level pool of beam hit effects shared
// by every hero (player, Quick Match bot, run bots). Instances are prewarmed at
// scene load and live under this object, never under a bot, so pooled or
// despawned bots cannot take them along. An ImpactFlash returns itself here
// when its pop finishes.
public class BeamImpactPool : MonoBehaviour
{
    [Tooltip("The hit effect prefab (needs an ImpactFlash). Shooters whose hitEffectPrefab matches use this pool")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Effects created at scene load")]
    [SerializeField] private int prewarmCount = 8;
    [Tooltip("Most effects alive at once; past this a hit shows no effect")]
    [SerializeField] private int maxCount = 24;

    // The pool of the loaded scene (null when there is none).
    public static BeamImpactPool Active { get; private set; }

    private ObjectPool<ImpactFlash> _pool;

    private void Awake()
    {
        if (prefab == null || prefab.GetComponent<ImpactFlash>() == null)
        {
            Debug.LogWarning("BeamImpactPool: prefab with an ImpactFlash missing; hits fall back to Instantiate.", this);
            return;
        }
        _pool = new ObjectPool<ImpactFlash>(Create, maxCount, OnGet, OnRelease);
        _pool.Prewarm(prewarmCount);
    }

    private void OnEnable()
    {
        if (_pool != null) Active = this;
    }

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }

    // Shows a hit effect. Uses the pool when it serves `effectPrefab`, else the
    // old Instantiate + Destroy path (a scene without a pool still works).
    public static void Play(GameObject effectPrefab, Vector3 position, Quaternion rotation)
    {
        if (effectPrefab == null) return;
        BeamImpactPool pool = Active;
        if (pool != null && pool.prefab == effectPrefab)
        {
            ImpactFlash fx = pool._pool.Get();
            if (fx == null) return;   // at the cap: skip rather than allocate mid-fight
            fx.transform.SetPositionAndRotation(position, rotation);
            fx.gameObject.SetActive(true);
            return;
        }

        GameObject go = Instantiate(effectPrefab, position, rotation);
        Destroy(go, 1f);
    }

    private ImpactFlash Create()
    {
        GameObject go = Instantiate(prefab, transform);
        go.name = prefab.name;
        var fx = go.GetComponent<ImpactFlash>();
        fx.SetOwner(this);
        return fx;
    }

    // Activation happens in Play after the effect is placed, so OnEnable never
    // runs at the old spot.
    private static void OnGet(ImpactFlash fx) { }

    private static void OnRelease(ImpactFlash fx)
    {
        if (fx != null) fx.gameObject.SetActive(false);
    }

    internal void Return(ImpactFlash fx)
    {
        if (_pool == null || !_pool.Release(fx)) fx.gameObject.SetActive(false);
    }
}
