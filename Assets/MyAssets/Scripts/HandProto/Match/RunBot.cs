using System;
using HandHero.Core;
using UnityEngine;

// Root of one RUN-mode bot, made by RunDirector from the prefab the scene
// builder generates (R8). Holds the bot's own hero and controllers; the prefab
// can't reference the scene, so Activate wires the arena and the enemy at spawn.
// Run bots never get items: only the island's enemy scaling applies.
//
// Pooled (P6, SP-1/SP-4/RF-1): RunDirector reuses bots through Activate /
// Deactivate instead of Instantiate / Destroy. Reset checklist on every
// Activate: spawn position, health scale + full health + respawn (resets the
// flight state and aim via HeroHealth.Respawned), arena and enemy, fire and
// damage scaling, the archetype (shapes, body color, attacks), a fresh brain
// seed. Deactivate turns the input off first, so OnDisable clears the
// puppeteer clutch and the beam / charge / telegraph, and the hero leaves the
// aim assist registry at once.
public class RunBot : MonoBehaviour
{
    [SerializeField] private FlyingCharacter hero;
    [SerializeField] private HeroHealth health;
    [SerializeField] private BotInputSource input;
    [SerializeField] private PointingBeamController pointing;
    [Tooltip("Archetype silhouette parts in BotShape bit order: Needle, Block, Lance (round 4, S6)")]
    [SerializeField] private GameObject[] shapes;

    private AimAssistTarget _assistTarget;
    private Vector3 _baseScale = Vector3.one;

    public HeroHealth Health => health;
    public FlyingCharacter Hero => hero;
    // The archetype of the current spawn (looks, attacks, telemetry).
    public BotArchetype Archetype { get; private set; } = BotArchetypes.Get(BotArchetypeId.Striker);

    // This bot went down. Subscribed once per pooled bot, not per spawn.
    public event Action<RunBot> Died;

    private void Awake()
    {
        if (health != null) health.Died += OnHealthDied;
        if (input != null) input.ShotFired += OnShotFired;
        if (hero != null) _assistTarget = hero.GetComponent<AimAssistTarget>();
        _baseScale = transform.localScale;
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= OnHealthDied;
        if (input != null) input.ShotFired -= OnShotFired;
    }

    private void OnHealthDied() => Died?.Invoke(this);

    // Same frame, before the beam controller fires it: this shot's damage and look.
    private void OnShotFired(BotAttack attack)
    {
        if (pointing != null)
            pointing.SetShotProfile(attack.DamageMult, attack.BeamWidthMult, attack.BeamDurationMult, attack.BeamColor,
                attack.FirePitch);
    }

    public void Activate(Vector3 at, Transform arenaCenter, Transform enemy, IslandSpec spec, int seed, bool controlled,
        BotArchetype archetype)
    {
        transform.SetPositionAndRotation(at, Quaternion.identity);
        if (health != null) health.SetSpawnPosition(at);
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        Setup(arenaCenter, enemy, spec);
        ApplyArchetype(archetype);
        if (input != null) input.Reseed(seed);
        SetControlled(controlled);
    }

    // Looks and attacks only (D7): flight, health and the hit receiver stay the same.
    private void ApplyArchetype(BotArchetype archetype)
    {
        Archetype = archetype;
        ShowShapes(archetype.Shapes);
        if (health != null) health.SetBaseColor(archetype.BodyColor.a > 0f, archetype.BodyColor);
        if (input != null) input.SetArchetype(archetype);
        OnShotFired(archetype.Attack); // neutral until the first shot sets it again
    }

    private void ShowShapes(BotShape on)
    {
        if (shapes == null) return;
        for (int i = 0; i < shapes.Length; i++)
        {
            bool show = ((int)on & (1 << i)) != 0;
            if (shapes[i] != null && shapes[i].activeSelf != show) shapes[i].SetActive(show);
        }
    }

    public void Deactivate()
    {
        SetControlled(false);
        if (gameObject.activeSelf) gameObject.SetActive(false);
        // Undo ShowForWarmup while inactive: the assist target registers on the next Activate.
        transform.localScale = _baseScale;
        if (_assistTarget != null && !_assistTarget.enabled) _assistTarget.enabled = true;
    }

    // GPU warmup (round 4, S1): drawn for a few frames at scene load so its
    // shaders and pipeline states exist before the first run. No controls, and
    // the aim assist target is switched off before activation so it never
    // registers. Every archetype part shows (S6). Deactivate undoes the rest;
    // the next Activate sets the shapes.
    public void ShowForWarmup(Vector3 at, float scale)
    {
        SetControlled(false);
        if (_assistTarget != null) _assistTarget.enabled = false;
        ShowShapes(BotShape.Needle | BotShape.Block | BotShape.Lance);
        transform.SetPositionAndRotation(at, Quaternion.identity);
        transform.localScale = _baseScale * scale;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    public void Setup(Transform arenaCenter, Transform enemy, IslandSpec spec)
    {
        if (hero != null) hero.SetArenaCenter(arenaCenter);
        if (input != null)
        {
            input.SetEnemy(enemy);
            input.SetFireIntervalScale(spec.FireIntervalMult);
        }
        if (pointing != null) pointing.SetDamageScale(spec.DamageMult);
        if (health != null) health.SetHealthScale(spec.HealthMult);
    }

    // Off = the bot lets go and glides (pause, island over), like the match gating.
    public void SetControlled(bool on)
    {
        if (input != null && input.enabled != on) input.enabled = on;
    }
}
