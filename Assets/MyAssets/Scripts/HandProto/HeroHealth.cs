using System;
using System.Collections.Generic;
using HandHero.Core;
using UnityEngine;

// Single-player hero health, ADR 3: a beam hit lowers health and slows the hero
// (2 s at 50 % max speed); at 0 HP the hero disappears and respawns after 3 s.
// No knockback, and nothing here ever touches the XR Origin — hit feedback is
// hero-side only (flash, health bar).
//
// Fusion port: same semantics as NetworkedPlayerHealth (Networking~).
//   OnBeamHit -> ServerApplyDamage, Update/Tick -> FixedUpdateNetwork,
//   Damaged/Died/Respawned -> the Render() change-detection events.
// The rules themselves live in HeroHealthModel (HandHero.Core, unit-tested).
public class HeroHealth : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlyingCharacter character;
    [Tooltip("Beam hits arrive here; defaults to the one on this object")]
    [SerializeField] private BeamHitReceiver hitReceiver;
    [Tooltip("Respawn point; empty = where the hero started")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Optional bar scaled on X by remaining health")]
    [SerializeField] private Transform healthBarFill;
    [Tooltip("Optional. Run items (max health, damage taken); empty = one on this object, none = neutral")]
    [SerializeField] private RunHeroStats runStats;

    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Hit Slow Penalty")]
    [SerializeField] private float slowDuration = 2f;
    [SerializeField, Range(0.1f, 1f)] private float slowMultiplier = 0.5f;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 3f;

    [Header("Hit Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.12f;
    [Tooltip("Hero tint while stunned by a shockwave")]
    [SerializeField] private Color stunColor = new Color(0.55f, 0.35f, 1f);

    public event Action Damaged;
    public event Action Stunned;
    public event Action Died;
    public event Action Respawned;

    public float CurrentHealth => _model.CurrentHealth;
    // With run items applied (base maxHealth when there are none).
    public float MaxHealth => _model != null ? _model.Params.MaxHealth : maxHealth;
    public bool IsDead => _model.IsDead;
    public float SpeedMultiplier => _model.SpeedMultiplier;
    public bool IsStunned => _model.IsStunned;

    private HeroHealthModel _model;
    private float _healthScale = 1f;
    private Vector3 _spawnPosition;
    private float _flashTimer;
    private bool _stunTinted;
    private Vector3 _barScale;
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<Color> _baseColors = new List<Color>();
    private Collider[] _colliders;

    private void Awake()
    {
        if (character == null) character = GetComponent<FlyingCharacter>();
        if (hitReceiver == null) hitReceiver = GetComponent<BeamHitReceiver>();
        runStats = RunHeroStats.Find(runStats, this);

        _model = new HeroHealthModel(CurrentParams());
        _spawnPosition = transform.position;
        _colliders = GetComponentsInChildren<Collider>();
        if (healthBarFill != null) _barScale = healthBarFill.localScale;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (healthBarFill != null && r.transform.IsChildOf(healthBarFill)) continue;
            _renderers.Add(r);
            _baseColors.Add(r.material.color);
        }
    }

    private void OnEnable()
    {
        if (hitReceiver != null) hitReceiver.Hit += OnBeamHit;
    }

    private void OnDisable()
    {
        if (hitReceiver != null) hitReceiver.Hit -= OnBeamHit;
    }

    public void ApplyDamage(float damage)
    {
        HitOutcome outcome = _model.ApplyDamage(CombatMath.DamageTaken(damage, RunHeroStats.StatsOf(runStats)));
        if (outcome == HitOutcome.Ignored) return;

        _flashTimer = flashDuration;
        SetColor(flashColor);
        Damaged?.Invoke();

        if (outcome == HitOutcome.Killed) Die();
        UpdateBar();
    }

    // Shockwave (T5, Q7): slow/stagger, no damage, no knockback.
    public void ApplyStun(float duration, float multiplier)
    {
        if (_model.IsDead) return;
        _model.ApplyStun(duration, multiplier);
        Stunned?.Invoke();
    }

    // Full health, alive, at the spawn point (round start).
    public void ResetHealth()
    {
        SyncMaxHealth();
        _model.Reset();
        Respawn();
    }

    // Run bots (R8): enemy scaling of the base max health, then full health.
    public void SetHealthScale(float scale)
    {
        _healthScale = Mathf.Max(0.01f, scale);
        ResetHealth();
    }

    // Run (R8): a revive item saved the run. Alive again at the spawn point with this health.
    public void Revive(float health)
    {
        _model.Reset();
        _model.SetHealth(health);
        Respawn();
    }

    // Run (R8): back to the spawn point between islands, keeping the health.
    public void ReturnToSpawn()
    {
        if (_model.IsDead) return;
        Vector3 at = spawnPoint != null ? spawnPoint.position : _spawnPosition;
        if (character != null) character.Respawn(at);
        else transform.position = at;
    }

    // Run (R8): heal on island clear. Capped at max, ignored while dead.
    public void Heal(float amount)
    {
        _model.Heal(amount);
        UpdateBar();
    }

    // Run (R8): health carry-over and the spiked chest. Never kills (min 1).
    public void SetHealth(float health)
    {
        SyncMaxHealth();
        _model.SetHealth(health);
        UpdateBar();
    }

    private void OnBeamHit(BeamHit hit)
    {
        ApplyDamage(hit.Damage);
    }

    private void Update()
    {
        SyncMaxHealth();
        _model.Params = CurrentParams();
        if (_model.Tick(Time.deltaTime)) Respawn();

        if (character != null && !_model.IsDead) character.SetSpeedMultiplier(_model.SpeedMultiplier);

        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f) RestoreColors();
        }
        else if (_model.IsStunned != _stunTinted)
        {
            // The hit flash takes priority; the stun tint shows outside it.
            if (_model.IsStunned)
            {
                SetColor(stunColor);
                _stunTinted = true;
            }
            else
            {
                RestoreColors();
            }
        }
    }

    private void Die()
    {
        if (character != null) character.Kill();
        SetVisible(false);
        Died?.Invoke();
    }

    private void Respawn()
    {
        Vector3 at = spawnPoint != null ? spawnPoint.position : _spawnPosition;
        if (character != null) character.Respawn(at);
        else transform.position = at;

        SetVisible(true);
        RestoreColors();
        UpdateBar();
        Respawned?.Invoke();
    }

    private float EffectiveMaxHealth() => CombatMath.MaxHealth(maxHealth * _healthScale, RunHeroStats.StatsOf(runStats));

    // A max-health item mid-run: a gain heals by the gain, a loss clamps (HeroHealthModel).
    private void SyncMaxHealth()
    {
        float max = EffectiveMaxHealth();
        if (Mathf.Approximately(max, _model.Params.MaxHealth)) return;
        _model.ChangeMaxHealth(max);
        UpdateBar();
    }

    private HealthParams CurrentParams()
    {
        return new HealthParams
        {
            MaxHealth = EffectiveMaxHealth(),
            SlowDuration = slowDuration,
            SlowMultiplier = slowMultiplier,
            RespawnDelay = respawnDelay,
        };
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        foreach (Collider c in _colliders) c.enabled = visible;
    }

    private void SetColor(Color color)
    {
        foreach (Renderer r in _renderers) r.material.color = color;
    }

    private void RestoreColors()
    {
        _flashTimer = 0f;
        _stunTinted = false;
        for (int i = 0; i < _renderers.Count; i++) _renderers[i].material.color = _baseColors[i];
    }

    private void UpdateBar()
    {
        if (healthBarFill == null) return;
        Vector3 s = _barScale;
        s.x *= Mathf.Max(0.0001f, _model.Normalized);
        healthBarFill.localScale = s;
    }
}
