using HandHero.Core;
using UnityEngine;

// Greybox flying hero for the arena prototype.
// Follows a target point with a speed-capped spring. The spring matters twice:
//  1. It filters hand-tracking noise (a jittery hand target becomes smooth flight).
//  2. It is exactly the model the Fusion server will run later — the network
//     input becomes "where the hand wants me", the simulation decides how the
//     character actually moves, so hand-position cheating is impossible.
public class FlyingCharacter : MonoBehaviour
{
    [Header("Flight Model")]
    [Tooltip("Pull toward the target point. Higher = snappier, lower = floatier")]
    [SerializeField] private float stiffness = 10f;
    [Tooltip("Velocity decay while clutched. Higher = less overshoot")]
    [SerializeField] private float damping = 5f;
    [SerializeField] private float maxSpeed = 25f;
    [Tooltip("Drag while released (gliding). Lower = longer glide")]
    [SerializeField] private float glideDrag = 0.8f;

    [Header("Run Items")]
    [Tooltip("Optional. Run items (SpeedMult); empty = one on this object, none = neutral")]
    [SerializeField] private RunHeroStats runStats;

    [Header("Team")]
    [Tooltip("Beams never hit a hero of the same team (run bots shoot through each other)")]
    [SerializeField] private HeroTeam team = HeroTeam.Player;

    [Header("Arena Bounds")]
    [SerializeField] private Transform arenaCenter;
    [SerializeField] private Vector3 arenaSize = new Vector3(35f, 20f, 35f);

    [Header("Visual (child mesh, rotated for style only)")]
    [SerializeField] private Transform visual;
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("Roll applied from lateral velocity, degrees at max speed")]
    [SerializeField] private float bankAmount = 40f;

    public Vector3 Velocity => _velocity;
    public bool IsClutched => _hasTarget;
    public ArenaBounds Bounds => GetArenaBounds();
    // False between death and respawn (HeroHealth): no flight, no control, no firing.
    public bool IsAlive => _alive;
    public HeroTeam Team => team;

    private Vector3 _velocity;
    private Vector3 _target;
    private bool _hasTarget;
    private bool _alive = true;
    private float _speedMultiplier = 1f;
    private float _chargeSpeedMultiplier = 1f;
    private float _paceSpeedMultiplier = 1f; // Round 5 deep review (DR-1)

    private void Awake()
    {
        runStats = RunHeroStats.Find(runStats, this);
    }

    // Run bots are spawned from a prefab, which can't reference the scene's arena.
    public void SetArenaCenter(Transform center)
    {
        arenaCenter = center;
    }

    // Called by HandPuppeteerController while the clutch (fist) is held.
    public void SetTarget(Vector3 worldPosition)
    {
        if (!_alive) return;
        _target = GetArenaBounds().Clamp(worldPosition);
        _hasTarget = true;
    }

    // Hit slow (ADR 3): scales maxSpeed only, the spring feel stays the same.
    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = Mathf.Clamp01(multiplier);
    }

    // Charge-shot slow (PointingBeamController). Separate channel from the hit
    // slow, which HeroHealth sets every frame; the two multiply.
    public void SetChargeSpeedMultiplier(float multiplier)
    {
        _chargeSpeedMultiplier = Mathf.Clamp01(multiplier);
    }

    // Round 5 deep review (DR-1): a bot flown slower than the run's (the demo's
    // Strikers, through BotInputSource.SetPace). A third channel beside the hit
    // slow and the charge slow; the three multiply. Kept across respawns; never
    // above 1. 1 = full speed (every other hero).
    public void SetPaceSpeedMultiplier(float multiplier)
    {
        _paceSpeedMultiplier = Mathf.Clamp01(multiplier);
    }

    public void Kill()
    {
        _alive = false;
        _hasTarget = false;
        _velocity = Vector3.zero;
        _chargeSpeedMultiplier = 1f;
    }

    // Teleports the hero (never the player's rig) and gives control back.
    public void Respawn(Vector3 worldPosition)
    {
        transform.position = GetArenaBounds().Clamp(worldPosition);
        _velocity = Vector3.zero;
        _hasTarget = false;
        _speedMultiplier = 1f;
        _chargeSpeedMultiplier = 1f;
        _alive = true;
    }

    // Called when the fist opens: the character keeps its momentum and glides.
    public void ClearTarget()
    {
        _hasTarget = false;
    }

    private void Update()
    {
        if (!_alive) return;

        float dt = Time.deltaTime;

        // Pure flight math lives in HandHero.Core (unit-tested, reusable by the
        // future Fusion FixedUpdateNetwork). This component only feeds it.
        var flightParams = new FlightParams
        {
            Stiffness = stiffness,
            Damping = damping,
            MaxSpeed = maxSpeed * _speedMultiplier * _chargeSpeedMultiplier * _paceSpeedMultiplier
                * CombatMath.SpeedMultiplier(RunHeroStats.StatsOf(runStats)),
            GlideDrag = glideDrag,
        };
        var state = new FlightState { Position = transform.position, Velocity = _velocity };
        state = SpringFlightModel.Step(state, _hasTarget, _target, flightParams, GetArenaBounds(), dt);

        _velocity = state.Velocity;
        transform.position = state.Position;

        UpdateVisual(dt);
    }

    private ArenaBounds GetArenaBounds()
    {
        return arenaCenter != null ? new ArenaBounds(arenaCenter.position, arenaSize) : default;
    }

    private void UpdateVisual(float dt)
    {
        if (visual == null) return;

        Vector3 flatVel = _velocity;
        if (flatVel.sqrMagnitude < 0.5f) return; // keep last facing when hovering

        // Face travel direction.
        Quaternion face = Quaternion.LookRotation(flatVel.normalized, Vector3.up);

        // Bank into lateral movement (style only, never applied to any camera).
        float lateral = Vector3.Dot(_velocity, visual.right) / Mathf.Max(maxSpeed, 0.01f);
        Quaternion bank = Quaternion.AngleAxis(-lateral * bankAmount, Vector3.forward);

        visual.rotation = Quaternion.Slerp(visual.rotation, face * bank, turnSpeed * dt);
    }

    private void OnDrawGizmosSelected()
    {
        if (arenaCenter == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(arenaCenter.position, arenaSize);
    }
}
