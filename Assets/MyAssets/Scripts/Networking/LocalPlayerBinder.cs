using Fusion;
using UnityEngine;
using UnityEngine.Events;

// Runs once when a bike spawns. If this bike belongs to the LOCAL player,
// it connects the scene-side presentation systems (XR rig follower, HUD,
// damage flash) to this specific bike — replacing all the old
// FindFirstObjectByType<PlayerHealth>() lookups that assumed one player.
public class LocalPlayerBinder : NetworkBehaviour
{
    public static NetworkedMotorcycle LocalBike { get; private set; }
    public static NetworkedPlayerHealth LocalHealth { get; private set; }

    [Header("Local Presentation Hooks (wired in Inspector or via code)")]
    public UnityEvent OnLocalDamaged;   // -> DamageFlash.TriggerFlash etc.
    public UnityEvent OnLocalDied;      // -> death overlay / fade
    public UnityEvent OnLocalRespawned; // -> clear overlay

    private NetworkedPlayerHealth health;

    public override void Spawned()
    {
        if (!HasInputAuthority) return; // only bind MY bike

        LocalBike = GetComponent<NetworkedMotorcycle>();
        LocalHealth = GetComponent<NetworkedPlayerHealth>();
        health = LocalHealth;

        // 1) XR Origin follows this bike from now on.
        var rigSync = FindFirstObjectByType<XROriginSync>();
        if (rigSync != null)
            rigSync.SetMotorcycle(transform);
        else
            Debug.LogError("LocalPlayerBinder: XROriginSync not found in scene!");

        // 2) Health presentation events.
        if (health != null)
        {
            health.Damaged += HandleDamaged;
            health.Died += HandleDied;
            health.Respawned += HandleRespawned;
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (health != null)
        {
            health.Damaged -= HandleDamaged;
            health.Died -= HandleDied;
            health.Respawned -= HandleRespawned;
        }
        if (LocalBike == GetComponent<NetworkedMotorcycle>())
        {
            LocalBike = null;
            LocalHealth = null;
        }
    }

    private void HandleDamaged() => OnLocalDamaged?.Invoke();
    private void HandleDied() => OnLocalDied?.Invoke();
    private void HandleRespawned() => OnLocalRespawned?.Invoke();
}
