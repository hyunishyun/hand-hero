using HandHero.Core;
using UnityEngine;

// Bot opponent as an input source: BotBrain turns "where is the enemy" into
// HandInputData, which drives the bot hero's own HandPuppeteerController and
// PointingBeamController exactly like a player's hands would (ADR 9).
[DefaultExecutionOrder(HandInputSourceBehaviour.ExecutionOrder)]
public class BotInputSource : HandInputSourceBehaviour
{
    [Header("References")]
    [Tooltip("The hero this bot flies")]
    [SerializeField] private FlyingCharacter self;
    [Tooltip("The bot hero's puppeteer; its positionScale is used so bot and player map hands the same way")]
    [SerializeField] private HandPuppeteerController puppeteer;
    [Tooltip("What the bot hunts (the player hero)")]
    [SerializeField] private Transform enemy;
    [Tooltip("Optional: hits on the bot hero make it dodge")]
    [SerializeField] private BeamHitReceiver hitReceiver;

    [Header("Behaviour")]
    [Tooltip("Difficulty knobs; built-in defaults when empty")]
    [SerializeField] private BotDifficulty difficulty;
    [Tooltip("Random seed; 0 = different every play")]
    [SerializeField] private int seed;
    [Tooltip("Run bots (round 5): how far a dash or hold spot keeps from a terrain piece, meters (the hero collider's radius is 0.9)")]
    [SerializeField] private float obstacleClearance = 1f;

    [Header("Seat view (round 5 deep review)")]
    [Tooltip("Bots that keep their range (Sniper, Gunner, Lancer, boss) stay where the VR seat looks: off = they may circle beside the player or fly right in front of their face. Strikers ignore it")]
    [SerializeField] private bool keepInSeatView = true;
    [Tooltip("The VR seat's eye, arena-local meters (the arena center is at world (0, 2, 20), the eye at world (0, 1.2, 0)); the seat faces the arena center")]
    [SerializeField] private Vector3 seatEye = new Vector3(0f, -0.8f, -20f);
    [Tooltip("Degrees off the seat's forward, seen from above, that a range keeper may go (the Quest 3 view reaches about 55)")]
    [SerializeField] private float seatMaxYaw = 45f;
    [Tooltip("Level meters a range keeper keeps from the seat's eye")]
    [SerializeField] private float seatMinDistance = 8f;

    private BotBrain _brain;
    private FlyingCharacter _enemyHero;
    private float _fireIntervalScale = 1f;
    private float _pace = 1f; // Round 5 T4, deep review DR-1

    public BotBrain Brain => _brain;

    // A shot fires this frame (round 4, S6), with the attack it belongs to. Raised
    // while sampling, so RunBot sets the beam's shot profile before it fires.
    public event System.Action<BotAttack> ShotFired;

    // Run bot archetypes (S6): attack patterns from the next attack on, and
    // (round 5, T2) the movement that goes with each pattern. Call before
    // Reseed so the first shot's delay uses the archetype's interval.
    public void SetArchetype(BotArchetype archetype)
    {
        _brain.SetAttacks(archetype.Attack, archetype.AltAttack, archetype.SwitchEvery);
        _brain.SetMovement(archetype.Movement, archetype.AltMovement);
    }

    public void SetEnemy(Transform target)
    {
        enemy = target;
    }

    // Run bots (round 5, T2-P4): the terrain pieces turned on (world boxes),
    // which dash and hold spots keep `obstacleClearance` away from.
    public void SetObstacles(Bounds[] boxes, int count)
    {
        _brain.SetObstacles(boxes, count, obstacleClearance);
    }

    // Run bots (R8): the boss fires more often (< 1). 1 = the difficulty asset's timing.
    public void SetFireIntervalScale(float scale)
    {
        _fireIntervalScale = Mathf.Max(0.05f, scale);
    }

    // Round 5 T4: demo bots fly slower (< 1). Deep review DR-1: the hand speed
    // cap alone never slowed them, so this scales what sets the flight speed
    // (BotParams.Paced: strafe lead, evade jump, weave, hand speed cap) and the
    // bot hero's max speed. Never above 1 (fairness). 1 = the difficulty asset's bot.
    public void SetPace(float scale)
    {
        _pace = Mathf.Clamp(scale, BotParams.MinPace, 1f);
        if (self != null) self.SetPaceSpeedMultiplier(_pace);
    }

    // Pooled run bots (P6, D9): a distinct seed per spawn, so bots spawned in the
    // same frame don't strafe and fire in lock-step. Also resets the brain.
    public void Reseed(int newSeed)
    {
        seed = newSeed;
        _brain.Params = CurrentParams();
        _brain.Reseed(newSeed);
    }

    private void Awake()
    {
        _brain = new BotBrain(CurrentParams(), seed != 0 ? seed : System.Environment.TickCount);
    }

    private void OnEnable()
    {
        _brain.Reset();
        if (hitReceiver != null) hitReceiver.Hit += OnHit;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (hitReceiver != null) hitReceiver.Hit -= OnHit;
    }

    private void OnHit(BeamHit hit)
    {
        _brain.NotifyHit();
    }

    protected override HandInputData Sample()
    {
        if (self == null) return default;

        // Dead: let go (the puppeteer drops its clutch too) and regrab at respawn.
        if (!self.IsAlive)
        {
            _brain.Reset();
            return default;
        }

        _brain.Params = CurrentParams();
        ArenaBounds bounds = self.Bounds;
        _brain.SetSeat(Seat(bounds));
        bool hasEnemy = enemy != null && enemy.gameObject.activeInHierarchy && EnemyAlive();
        HandInputData input = _brain.Step(self.transform.position, hasEnemy, hasEnemy ? enemy.position : Vector3.zero,
            bounds, Time.deltaTime);
        if (input.FireTriggered) ShotFired?.Invoke(_brain.LastShot);
        return input;
    }

    private bool EnemyAlive()
    {
        if (_enemyHero == null || _enemyHero.transform != enemy)
            _enemyHero = enemy.GetComponent<FlyingCharacter>();
        return _enemyHero == null || _enemyHero.IsAlive;
    }

    // Deep review DR-2: the VR seat in world space (the arena box is axis-aligned
    // around the arena center), facing the arena center. No arena: no seat.
    private BotSeat Seat(ArenaBounds bounds)
    {
        if (!keepInSeatView || !bounds.Enabled) return default;
        return BotSeat.At(bounds.Center + seatEye, -seatEye, seatMaxYaw, seatMinDistance);
    }

    private BotParams CurrentParams()
    {
        BotParams p = difficulty != null ? difficulty.Params : BotParams.Default;
        if (puppeteer != null) p.PositionScale = puppeteer.PositionScale;
        p.FireInterval *= _fireIntervalScale;
        p.FireIntervalJitter *= _fireIntervalScale;
        return p.Paced(_pace); // Round 5 T4, deep review DR-1
    }
}
