using Fusion;
using UnityEngine;

// Networked version of GunController's combat half.
// Flow per the architecture we agreed on:
//   1. Fire button arrives inside BikeInputData (with the gun's world pose).
//   2. FixedUpdateNetwork runs the shot on the predicting client AND the server.
//   3. Hit detection uses Fusion lag compensation, so the server judges the shot
//      against where the shooter SAW the target — fair hits under latency.
//   4. Damage is applied only by the server (state authority).
//   5. Visuals/audio fire on every client via change detection (GunPresentation).
//
// The old UI-raycast hack is intentionally gone — menus move to
// XR Interaction Toolkit ray interactors in the lobby phase.
public class NetworkedGunController : NetworkBehaviour
{
    [Header("Gun Settings")]
    [SerializeField] private float fireRange = 100f;
    [SerializeField] private float fireCooldown = 0.2f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private LayerMask hitMask = -1;

    // ---- Networked state ----
    [Networked] private TickTimer FireTimer { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private int FireCount { get; set; }
    [Networked] private Vector3 LastFireOrigin { get; set; }
    [Networked] private Vector3 LastFireEnd { get; set; }
    [Networked] private NetworkBool LastShotHitPlayer { get; set; }

    // Presentation hook: (origin, end, hitPlayer). GunPresentation subscribes.
    public event System.Action<Vector3, Vector3, bool> Fired;

    private ChangeDetector _changes;

    public override void Spawned()
    {
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out BikeInputData input)) return;

        var pressed = input.Buttons.GetPressed(PreviousButtons);
        PreviousButtons = input.Buttons;

        if (!pressed.IsSet((int)BikeButtons.Fire)) return;
        if (!FireTimer.ExpiredOrNotRunning(Runner)) return;

        FireTimer = TickTimer.CreateFromSeconds(Runner, fireCooldown);

        Vector3 origin = input.GunPosition;
        Vector3 direction = input.GunRotation * Vector3.forward;
        Vector3 end = origin + direction * fireRange;
        bool hitPlayer = false;

        // Lag-compensated raycast: rewinds Hitboxes to the shooter's view tick.
        // IncludePhysX also tests static geometry (walls block shots).
        if (Runner.LagCompensation.Raycast(
                origin, direction, fireRange,
                player: Object.InputAuthority,
                hit: out var hit,
                layerMask: hitMask,
                options: HitOptions.IncludePhysX))
        {
            end = hit.Point;

            // Resolve a health component from either a Hitbox or a plain collider.
            NetworkedPlayerHealth targetHealth = null;
            if (hit.Hitbox != null)
                targetHealth = hit.Hitbox.Root.GetComponent<NetworkedPlayerHealth>();
            else if (hit.Collider != null)
                targetHealth = hit.Collider.GetComponentInParent<NetworkedPlayerHealth>();

            if (targetHealth != null &&
                targetHealth.Object.InputAuthority != Object.InputAuthority) // no self-damage
            {
                hitPlayer = true;
                if (HasStateAuthority) // damage is confirmed by the server only
                    targetHealth.ServerApplyDamage(damage);
            }
        }

        LastFireOrigin = origin;
        LastFireEnd = end;
        LastShotHitPlayer = hitPlayer;
        FireCount++;
    }

    // Every client (shooter, victim, bystanders) plays the shot visuals here.
    public override void Render()
    {
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(FireCount) && FireCount > 0)
                Fired?.Invoke(LastFireOrigin, LastFireEnd, LastShotHitPlayer);
        }
    }
}
