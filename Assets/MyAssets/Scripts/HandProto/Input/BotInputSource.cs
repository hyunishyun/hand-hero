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

    private BotBrain _brain;
    private FlyingCharacter _enemyHero;
    private float _fireIntervalScale = 1f;

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

    // Run bots (R8): the boss fires more often (< 1). 1 = the difficulty asset's timing.
    public void SetFireIntervalScale(float scale)
    {
        _fireIntervalScale = Mathf.Max(0.05f, scale);
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
        bool hasEnemy = enemy != null && enemy.gameObject.activeInHierarchy && EnemyAlive();
        HandInputData input = _brain.Step(self.transform.position, hasEnemy, hasEnemy ? enemy.position : Vector3.zero,
            self.Bounds, Time.deltaTime);
        if (input.FireTriggered) ShotFired?.Invoke(_brain.LastShot);
        return input;
    }

    private bool EnemyAlive()
    {
        if (_enemyHero == null || _enemyHero.transform != enemy)
            _enemyHero = enemy.GetComponent<FlyingCharacter>();
        return _enemyHero == null || _enemyHero.IsAlive;
    }

    private BotParams CurrentParams()
    {
        BotParams p = difficulty != null ? difficulty.Params : BotParams.Default;
        if (puppeteer != null) p.PositionScale = puppeteer.PositionScale;
        p.FireInterval *= _fireIntervalScale;
        p.FireIntervalJitter *= _fireIntervalScale;
        return p;
    }
}
