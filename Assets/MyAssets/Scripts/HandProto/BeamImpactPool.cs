using System.Collections.Generic;
using HandHero.Core;
using UnityEngine;

// Freeze hunt (P5, SP-3/GC-3): one scene-level pool of beam hit effects shared
// by every hero (player, Quick Match bot, run bots). Instances are prewarmed at
// scene load and live under this object, never under a bot, so pooled or
// despawned bots cannot take them along. An ImpactFlash returns itself here
// when its pop finishes. P12: one pool per effect prefab (a second one serves
// the kill burst); Play finds the pool by prefab.
public class BeamImpactPool : MonoBehaviour
{
    [Tooltip("The effect prefab (needs a PooledEffect such as ImpactFlash or KillBurst). Play calls with this prefab use this pool")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Effects created at scene load")]
    [SerializeField] private int prewarmCount = 8;
    [Tooltip("Most effects alive at once; past this a hit shows no effect")]
    [SerializeField] private int maxCount = 24;

    // The pools of the loaded scene.
    private static readonly List<BeamImpactPool> Active = new List<BeamImpactPool>();

    private ObjectPool<PooledEffect> _pool;

    private void Awake()
    {
        if (prefab == null || prefab.GetComponent<PooledEffect>() == null)
        {
            Debug.LogWarning("BeamImpactPool: prefab with a PooledEffect missing; effects fall back to Instantiate.", this);
            return;
        }
        _pool = new ObjectPool<PooledEffect>(Create, maxCount, OnGet, OnRelease);
        _pool.Prewarm(prewarmCount);
    }

    private void OnEnable()
    {
        if (_pool != null && !Active.Contains(this)) Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private static BeamImpactPool Find(GameObject effectPrefab)
    {
        for (int i = 0; i < Active.Count; i++)
            if (Active[i].prefab == effectPrefab) return Active[i];
        return null;
    }

    // Shows a hit effect. Uses the pool when it serves `effectPrefab`, else the
    // old Instantiate + Destroy path (a scene without a pool still works).
    public static void Play(GameObject effectPrefab, Vector3 position, Quaternion rotation)
    {
        if (effectPrefab == null) return;
        BeamImpactPool pool = Find(effectPrefab);
        if (pool != null)
        {
            PooledEffect fx = pool._pool.Get();
            if (fx == null) return;   // at the cap: skip rather than allocate mid-fight
            fx.transform.SetPositionAndRotation(position, rotation);
            fx.gameObject.SetActive(true);
            return;
        }

        GameObject go = Instantiate(effectPrefab, position, rotation);
        Destroy(go, 1f);
    }

    // GPU warmup (round 4, S1): one pooled effect handed out without playing, so
    // RenderWarmup can draw it at scene load; it goes back with ReturnToPool.
    // Null when no pool serves the prefab or every effect is busy.
    public static PooledEffect Borrow(GameObject effectPrefab)
    {
        BeamImpactPool pool = effectPrefab != null ? Find(effectPrefab) : null;
        return pool != null ? pool._pool.Get() : null;
    }

    private PooledEffect Create()
    {
        GameObject go = Instantiate(prefab, transform);
        go.name = prefab.name;
        var fx = go.GetComponent<PooledEffect>();
        fx.SetOwner(this);
        return fx;
    }

    // Activation happens in Play after the effect is placed, so OnEnable never
    // runs at the old spot.
    private static void OnGet(PooledEffect fx) { }

    private static void OnRelease(PooledEffect fx)
    {
        if (fx != null) fx.gameObject.SetActive(false);
    }

    internal void Return(PooledEffect fx)
    {
        if (_pool == null || !_pool.Release(fx)) fx.gameObject.SetActive(false);
    }
}
