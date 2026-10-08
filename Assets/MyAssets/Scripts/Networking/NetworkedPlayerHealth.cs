using Fusion;
using UnityEngine;

// Server-authoritative health. Encodes the agreed hit design:
//   hit  -> health down + temporary speed slow (gameplay penalty)
//   0 HP -> dead, respawn after a delay at a spawn point
// Presentation (damage flash, vignette, HUD) is driven from Render() via
// change detection, so every client sees effects without RPC spam.
public class NetworkedPlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Hit Slow Penalty")]
    [SerializeField] private float slowDuration = 2f;
    [SerializeField, Range(0.1f, 1f)] private float slowMultiplier = 0.5f;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 3f;

    // ---- Networked state (server writes, everyone reads) ----
    [Networked] public float CurrentHealth { get; private set; }
    [Networked] public NetworkBool IsDead { get; private set; }
    [Networked] private TickTimer SlowTimer { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }
    [Networked] private int DamageCount { get; set; } // bumped per hit, change-detected

    public float MaxHealth => maxHealth;

    // 1.0 normally, slowMultiplier while the hit penalty is active.
    public float SpeedMultiplier =>
        SlowTimer.ExpiredOrNotRunning(Runner) ? 1f : slowMultiplier;

    // Local presentation hooks. LocalPlayerBinder wires these to DamageFlash,
    // HUD, audio etc. for the local player only; remote bikes can hook avatar FX.
    public event System.Action Damaged;
    public event System.Action Died;
    public event System.Action Respawned;

    private ChangeDetector _changes;

    public override void Spawned()
    {
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (HasStateAuthority)
            CurrentHealth = maxHealth;
    }

    // Only callable meaningfully on the server (state authority).
    public void ServerApplyDamage(float damage)
    {
        if (!HasStateAuthority || IsDead) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        DamageCount++;
        SlowTimer = TickTimer.CreateFromSeconds(Runner, slowDuration);

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && IsDead && RespawnTimer.Expired(Runner))
            Respawn();
    }

    private void Respawn()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        SlowTimer = TickTimer.None;
        RespawnTimer = TickTimer.None;

        Transform sp = RespawnManager.GetSpawnPoint(Object.InputAuthority.PlayerId);
        if (sp == null) return;

        // Teleport through the NetworkRigidbody so interpolation doesn't smear
        // the bike across the map.
        var nrb = GetComponent<Fusion.Addons.Physics.NetworkRigidbody3D>();
        if (nrb != null)
        {
            nrb.Teleport(sp.position, sp.rotation);
        }
        else
        {
            transform.SetPositionAndRotation(sp.position, sp.rotation);
        }

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // Change detection -> presentation events, on every client.
    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(DamageCount):
                    if (DamageCount > 0) Damaged?.Invoke();
                    break;
                case nameof(IsDead):
                    if (IsDead) Died?.Invoke();
                    else Respawned?.Invoke();
                    break;
            }
        }
    }
}
