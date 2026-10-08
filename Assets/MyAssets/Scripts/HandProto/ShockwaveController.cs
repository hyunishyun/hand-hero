using System.Collections.Generic;
using HandHero.Core;
using UnityEngine;

// Palm push -> shockwave from the hero (T5). Q7: no knockback (ADR 3) — every
// other hero inside the radius is stunned instead (heavy slow, no damage).
// Input comes only as HandInputData (ADR 9); push recognition lives in
// XRHandsInputSource. Nothing here touches the XR Origin.
public class ShockwaveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlyingCharacter character;
    [Tooltip("Where this hero's input comes from (XR hands, debug keyboard/mouse, ...)")]
    [SerializeField] private HandInputSourceBehaviour inputSource;
    [Tooltip("Optional ring that expands from the hero when the shockwave fires")]
    [SerializeField] private LineRenderer ring;
    [Tooltip("Optional. Run items (radius, stun length); empty = one on the hero, none = neutral")]
    [SerializeField] private RunHeroStats runStats;

    [Header("Shockwave")]
    [Tooltip("Heroes within this many meters of this hero are stunned")]
    [SerializeField] private float radius = 6f;
    [Tooltip("Seconds between shockwaves")]
    [SerializeField] private float cooldown = 3f;
    [Tooltip("Stun length in seconds")]
    [SerializeField] private float stunDuration = 1.2f;
    [Tooltip("Max-speed multiplier of stunned heroes")]
    [SerializeField, Range(0f, 1f)] private float stunMultiplier = 0.15f;

    [Header("Ring")]
    [SerializeField] private float ringDuration = 0.35f;
    [SerializeField] private Color ringColor = new Color(0.65f, 0.45f, 1f);
    [SerializeField] private int ringSegments = 48;

    private static readonly Collider[] OverlapBuffer = new Collider[32];

    private readonly HashSet<HeroHealth> _stunnedThisWave = new HashSet<HeroHealth>();
    private IHandInputSource _sourceOverride;
    private float _lastFireTime = -999f;
    private float _ringTimer;

    // With run items applied (the tuned radius when there are none).
    public float Radius => CombatMath.ShockwaveRadius(radius, RunHeroStats.StatsOf(runStats));
    public bool IsReady => Time.time - _lastFireTime >= cooldown;

    private void Awake()
    {
        runStats = RunHeroStats.Find(runStats, character);
        if (ring != null)
        {
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = ringSegments;
            ring.startColor = ringColor;
            ring.endColor = ringColor;
            ring.enabled = false;
        }
    }

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
        UpdateRing();

        IHandInputSource source = Source();
        if (source == null || character == null || !character.IsAlive) return;

        if (source.Current.Has(HandGestures.Shockwave) && IsReady) Fire();
    }

    private void Fire()
    {
        _lastFireTime = Time.time;
        Vector3 center = character.transform.position;
        HeroStats stats = RunHeroStats.StatsOf(runStats);
        float stun = CombatMath.StunDuration(stunDuration, stats);

        _stunnedThisWave.Clear();
        int count = Physics.OverlapSphereNonAlloc(center, Radius, OverlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider c = OverlapBuffer[i];
            if (c.transform.IsChildOf(character.transform)) continue;

            HeroHealth hero = c.GetComponentInParent<HeroHealth>();
            if (hero == null || !_stunnedThisWave.Add(hero)) continue;
            hero.ApplyStun(stun, stunMultiplier);
        }

        if (ring != null)
        {
            _ringTimer = ringDuration;
            ring.enabled = true;
        }
    }

    // Flat ring around the hero growing to the stun radius: shows the reach.
    private void UpdateRing()
    {
        if (ring == null || _ringTimer <= 0f) return;

        _ringTimer -= Time.deltaTime;
        if (_ringTimer <= 0f)
        {
            ring.enabled = false;
            return;
        }

        float t = 1f - _ringTimer / ringDuration;
        float r = Mathf.Lerp(0.5f, Radius, t);
        Vector3 center = character != null ? character.transform.position : transform.position;
        for (int i = 0; i < ringSegments; i++)
        {
            float a = i * Mathf.PI * 2f / ringSegments;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
        }
        float width = Mathf.Lerp(0.4f, 0.05f, t);
        ring.startWidth = width;
        ring.endWidth = width;
    }
}
