using HandHero.Core;
using UnityEngine;

// Bot kill burst (P12, D15): the child shards fly outward from the bot, ease to a
// stop and shrink away, then the burst returns to its BeamImpactPool. The shard
// directions come from their starting offsets in the prefab. Runs on game time,
// so it freezes while paused like the beam hit effect.
public class KillBurst : PooledEffect
{
    [Tooltip("Seconds until the shards are gone")]
    [SerializeField] private float lifetime = 0.45f;
    [Tooltip("Shard launch speed (meters per second, eases to a stop)")]
    [SerializeField] private float speed = 7f;
    [Tooltip("Shard spin (degrees per second)")]
    [SerializeField] private float spin = 540f;

    private Transform[] _shards;
    private Vector3[] _directions;
    private Vector3[] _starts;
    private Vector3[] _scales;
    private float _age;

    private void Awake()
    {
        int n = transform.childCount;
        _shards = new Transform[n];
        _directions = new Vector3[n];
        _starts = new Vector3[n];
        _scales = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Transform shard = transform.GetChild(i);
            _shards[i] = shard;
            _starts[i] = shard.localPosition;
            _directions[i] = shard.localPosition.sqrMagnitude > 0f ? shard.localPosition.normalized : Vector3.up;
            _scales[i] = shard.localScale;
        }
    }

    private void OnEnable()
    {
        _age = 0f;
        Apply();
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= lifetime)
        {
            Finish();
            return;
        }
        Apply();
    }

    private void Apply()
    {
        if (_shards == null) return;
        float distance = KillBurstMotion.Distance(_age, lifetime, speed);
        float scale = KillBurstMotion.Scale(_age, lifetime);
        Quaternion rotation = Quaternion.Euler(spin * _age, spin * 0.7f * _age, 0f);
        for (int i = 0; i < _shards.Length; i++)
        {
            Transform shard = _shards[i];
            shard.localPosition = _starts[i] + _directions[i] * distance;
            shard.localScale = _scales[i] * scale;
            shard.localRotation = rotation;
        }
    }
}
