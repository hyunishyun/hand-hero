using System;
using UnityEngine;

namespace HandHero.Core
{
    // Difficulty knobs for BotBrain. BotDifficulty (ScriptableObject) wraps this for the inspector.
    [Serializable]
    public struct BotParams
    {
        [Tooltip("Seconds of lag in how the bot perceives the enemy position (0 = perfect)")]
        public float ReactionTime;
        [Tooltip("Max aim error per shot, degrees (random inside this cone)")]
        public float AimErrorDegrees;
        [Tooltip("Seconds between shots (after the previous shot)")]
        public float FireInterval;
        [Tooltip("Random extra seconds added to each interval, 0..this")]
        public float FireIntervalJitter;
        [Tooltip("Seconds the aim is locked and telegraphed before the beam fires")]
        public float TelegraphTime;
        [Tooltip("Shots are only started within this distance, meters")]
        public float MaxFireRange;

        [Tooltip("Distance the bot tries to keep from the enemy, meters")]
        public float PreferredRange;
        [Tooltip("Beyond PreferredRange + this the bot approaches instead of strafing")]
        public float RangeTolerance;
        [Tooltip("How far ahead sideways the bot pulls itself while strafing, meters")]
        public float StrafeLead;
        [Tooltip("Mean seconds between strafe direction flips")]
        public float StrafeSwitchInterval;
        [Tooltip("Vertical weave amplitude while strafing, meters")]
        public float VerticalWeave;

        [Tooltip("Seconds of dodging after being hit (no shots meanwhile)")]
        public float EvadeDuration;
        [Tooltip("How far the bot yanks itself sideways when hit, meters")]
        public float EvadeDistance;

        [Tooltip("Max virtual hand speed, m/s — keeps the bot within human reach (fairness)")]
        public float MaxHandSpeed;
        [Tooltip("Must match HandPuppeteerController.positionScale on the bot hero")]
        public float PositionScale;

        public static BotParams Default => new BotParams
        {
            ReactionTime = 0.35f,
            AimErrorDegrees = 4f,
            FireInterval = 2.2f,
            FireIntervalJitter = 1f,
            TelegraphTime = 0.6f,
            MaxFireRange = 40f,
            PreferredRange = 12f,
            RangeTolerance = 3f,
            StrafeLead = 5f,
            StrafeSwitchInterval = 3f,
            VerticalWeave = 2f,
            EvadeDuration = 0.8f,
            EvadeDistance = 6f,
            MaxHandSpeed = 1.5f,
            PositionScale = 60f,
        };
    }

    public enum BotState
    {
        Idle,     // no enemy: hover
        Approach, // too far: close in to PreferredRange
        Strafe,   // in range: circle the enemy, weave, shoot
        Evade,    // just got hit: yank sideways, hold fire
    }

    // Opponent AI that outputs HandInputData — the same input the player's hands
    // produce — so the bot flies through the same puppeteer clutch mapping and
    // flight model, with the same speed cap (fair, and it ports to the network
    // as just another input source). It never touches the hero directly.
    //
    // It holds the clutch the whole time and mirrors ClutchMapper's target, so it
    // can steer the hero toward any desired point by emitting hand deltas.
    // Shooting: lock aim (with error) -> telegraph for TelegraphTime -> fire.
    // Archetypes (round 4, S6) change only the attack: telegraph and interval
    // multipliers, bursts (later shots re-aim) and the boss's alternating
    // patterns. The default attack (Striker) is the original bot exactly.
    public class BotBrain
    {
        private readonly SeededRandom _rng;
        private BotParams _p;

        private bool _grabbed;
        private Vector3 _clutchTarget; // mirrors ClutchMapper's target
        private Vector3 _lastSelf;
        private Vector3 _lastRadial = Vector3.back;
        private float _time;

        private bool _hasPerceived;
        private Vector3 _perceived;

        private float _strafeSign = 1f;
        private float _strafeTimer;

        private float _evadeTimer;
        private Vector3 _evadeAnchor;
        private bool _hitPending;

        private float _fireTimer;
        private bool _telegraphing;
        private float _telegraphTimer;
        private float _telegraphDuration;
        private Vector3 _lockedAim;

        // Attack patterns (S6): the current attack's shots still to fire, the gap
        // timer between them, and the boss's pattern switching.
        private BotAttack _primary = BotArchetypes.StrikerAttack;
        private BotAttack _alt = BotArchetypes.StrikerAttack;
        private int _switchEvery;
        private bool _useAlt;
        private int _attacksDone;
        private BotAttack _active = BotArchetypes.StrikerAttack;
        private int _shotsLeft;
        private float _burstTimer;

        public BotBrain(BotParams p, int seed)
        {
            _p = p;
            _rng = new SeededRandom(seed);
            Reset();
        }

        public BotState State { get; private set; }
        public bool IsTelegraphing => _telegraphing;
        // 0 at telegraph start, 1 at the moment the beam fires.
        public float TelegraphProgress =>
            _telegraphing && _telegraphDuration > 0f ? 1f - _telegraphTimer / _telegraphDuration : 0f;
        // The attack being telegraphed or fired (its look for the telegraph line).
        public BotAttack ActiveAttack => _active;
        // The attack of the latest shot: read it on the FireTriggered frame (damage, beam look).
        public BotAttack LastShot { get; private set; } = BotArchetypes.StrikerAttack;
        // Between the shots of a burst.
        public bool IsBursting => _shotsLeft > 0 && !_telegraphing;
        public Vector3 LockedAimPoint => _lockedAim;
        // Seconds until the next shot may start its telegraph.
        public float TimeToNextShot => _fireTimer;
        public Vector3 PerceivedTarget => _perceived;

        public BotParams Params
        {
            get => _p;
            set => _p = value;
        }

        // Forget the clutch and plans (call when the input source is re-enabled
        // or the hero respawns): the next Step regrabs from the hero's position.
        public void Reset()
        {
            _grabbed = false;
            _hasPerceived = false;
            CancelAttack();
            _evadeTimer = 0f;
            _hitPending = false;
            // Jittered (D9) so bots spawned or resumed together don't fire in sync.
            _fireTimer = FirstShotDelay(_p.FireInterval * Current.IntervalMult, _rng.NextDouble());
            _strafeTimer = NextStrafeSwitch();
            State = BotState.Idle;
        }

        // A pooled bot reused for a new spawn (P6): its one random stream restarts
        // from the new seed (no allocation, round 4 S3), then Reset. The boss
        // pattern starts over with the primary attack.
        public void Reseed(int seed)
        {
            _rng.Reseed(seed);
            _useAlt = false;
            _attacksDone = 0;
            Reset();
        }

        // Archetype attacks (S6): `altAttack` takes over every `switchEvery`
        // begun attacks (0 = only `attack`). Takes effect from the next attack.
        public void SetAttacks(BotAttack attack, BotAttack altAttack, int switchEvery)
        {
            _primary = attack;
            _alt = altAttack;
            _switchEvery = Mathf.Max(0, switchEvery);
            _useAlt = false;
            _attacksDone = 0;
        }

        private BotAttack Current => _useAlt && _switchEvery > 0 ? _alt : _primary;

        // First shot after a reset: FireInterval x U(0.5, 1.0), u in [0, 1].
        public static float FirstShotDelay(float fireInterval, double u)
        {
            return fireInterval * (0.5f + 0.5f * (float)u);
        }

        // Per-spawn seed drawn from the run's own random stream (never 0, which
        // BotInputSource reads as "pick one from the clock").
        public static int SpawnSeed(System.Random runRng)
        {
            return runRng.Next(1, int.MaxValue);
        }

        // The bot's hero was hit: dodge on the next Step.
        public void NotifyHit()
        {
            _hitPending = true;
        }

        public HandInputData Step(Vector3 selfPosition, bool hasEnemy, Vector3 enemyPosition, ArenaBounds bounds, float dt)
        {
            _time += dt;
            _lastSelf = selfPosition;
            Perceive(hasEnemy, enemyPosition, dt);

            var input = new HandInputData { ClutchHeld = true };

            // Closing the fist: ClutchMapper starts its target at the hero, so the
            // grab frame carries no motion.
            bool justGrabbed = !_grabbed;
            if (justGrabbed)
            {
                _grabbed = true;
                _clutchTarget = selfPosition;
            }

            Vector3 desired = bounds.Clamp(DesiredPosition(selfPosition, bounds, dt));
            if (!justGrabbed)
            {
                float scale = Mathf.Max(_p.PositionScale, 1e-3f);
                Vector3 step = Vector3.ClampMagnitude(desired - _clutchTarget, _p.MaxHandSpeed * scale * dt);
                _clutchTarget += step;
                input.ClutchDelta = step / scale;
            }

            if (_hasPerceived)
            {
                bool fire = UpdateShooting(selfPosition, dt);
                Vector3 aimPoint = _telegraphing || fire ? _lockedAim : _perceived;
                Vector3 dir = aimPoint - selfPosition;
                if (dir.sqrMagnitude > 1e-8f)
                {
                    input.HasAim = true;
                    input.AimOrigin = selfPosition;
                    input.AimDirection = dir.normalized;
                    input.FireTriggered = fire;
                }
            }
            else
            {
                CancelAttack();
            }

            return input;
        }

        private void Perceive(bool hasEnemy, Vector3 enemyPosition, float dt)
        {
            if (!hasEnemy)
            {
                _hasPerceived = false;
                return;
            }

            if (!_hasPerceived || _p.ReactionTime <= 0f)
            {
                _perceived = enemyPosition;
            }
            else
            {
                float t = 1f - Mathf.Exp(-dt / _p.ReactionTime);
                _perceived = Vector3.Lerp(_perceived, enemyPosition, t);
            }
            _hasPerceived = true;
        }

        private Vector3 DesiredPosition(Vector3 self, ArenaBounds bounds, float dt)
        {
            if (_hitPending)
            {
                _hitPending = false;
                StartEvade();
            }

            if (_evadeTimer > 0f)
            {
                _evadeTimer -= dt;
                State = BotState.Evade;
                return _evadeAnchor;
            }

            if (!_hasPerceived)
            {
                State = BotState.Idle;
                return _clutchTarget; // hold still
            }

            Vector3 toSelf = self - _perceived;
            float dist = toSelf.magnitude;
            Vector3 radial = dist > 1e-3f ? toSelf / dist : _lastRadial;
            _lastRadial = radial;
            Vector3 ring = _perceived + radial * _p.PreferredRange;

            if (dist > _p.PreferredRange + _p.RangeTolerance)
            {
                State = BotState.Approach;
                return ring;
            }

            State = BotState.Strafe;

            _strafeTimer -= dt;
            if (_strafeTimer <= 0f) FlipStrafe();

            Vector3 desired = ring + Tangent(radial) * (_strafeSign * _p.StrafeLead);
            desired.y += Mathf.Sin(_time * 0.9f) * _p.VerticalWeave;

            // Pinned against a wall: go the other way round.
            if (bounds.Enabled && (bounds.Clamp(desired) - desired).sqrMagnitude > 1f && _strafeTimer < _p.StrafeSwitchInterval * 0.5f)
                FlipStrafe();

            return desired;
        }

        // Returns true on the frame the beam fires.
        private bool UpdateShooting(Vector3 self, float dt)
        {
            if (State == BotState.Evade)
            {
                CancelAttack(); // a dodge drops the rest of a burst too
                return false;
            }

            if (_telegraphing)
            {
                _telegraphTimer -= dt;
                if (_telegraphTimer > 0f) return false;
                _telegraphing = false;
                return FireShot();
            }

            // The rest of a burst: no new telegraph, each shot re-aims (with error).
            if (_shotsLeft > 0)
            {
                _burstTimer -= dt;
                if (_burstTimer > 0f) return false;
                _lockedAim = _perceived + AimError(self, _perceived);
                return FireShot();
            }

            _fireTimer -= dt;
            if (_fireTimer > 0f) return false;
            if (Vector3.Distance(self, _perceived) > _p.MaxFireRange) return false;

            _lockedAim = _perceived + AimError(self, _perceived);
            _active = Current;
            _shotsLeft = Mathf.Max(1, _active.BurstCount);
            CountAttackForSwitch();
            float telegraph = _p.TelegraphTime * _active.TelegraphMult;
            if (telegraph <= 0f) return FireShot();

            _telegraphing = true;
            _telegraphTimer = telegraph;
            _telegraphDuration = telegraph;
            return false;
        }

        // The boss's pattern switch counts an attack once it begins (final review
        // F1-1): a hit cancels the telegraph or the rest of a burst, and counting
        // only finished attacks left the boss stuck in whichever pattern the
        // player interrupted most. The next attack reads Current, so an
        // uninterrupted sequence is the same as before.
        private void CountAttackForSwitch()
        {
            if (_switchEvery > 0 && ++_attacksDone >= _switchEvery)
            {
                _attacksDone = 0;
                _useAlt = !_useAlt;
            }
        }

        // One shot of the active attack. The last one starts the interval (scaled
        // by the attack).
        private bool FireShot()
        {
            LastShot = _active;
            _shotsLeft--;
            if (_shotsLeft > 0)
            {
                _burstTimer = _active.BurstGap;
                return true;
            }

            float m = _active.IntervalMult;
            _fireTimer = _p.FireInterval * m + (float)_rng.NextDouble() * (_p.FireIntervalJitter * m);
            return true;
        }

        // The telegraph and any burst shots left; the interval timer is untouched.
        private void CancelAttack()
        {
            _telegraphing = false;
            _shotsLeft = 0;
        }

        private void StartEvade()
        {
            CancelAttack();
            // Re-fire floor scaled by the latest attack, so a hit never makes a
            // slow attack (Sniper, Lancer) come back sooner (F1-1). Striker: x1.
            _fireTimer = Mathf.Max(_fireTimer, _p.FireInterval * 0.5f * _active.IntervalMult);
            _evadeTimer = _p.EvadeDuration;

            float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            float up = (float)_rng.NextDouble() - 0.5f;
            Vector3 dir = (Tangent(_lastRadial) * side + Vector3.up * up).normalized;
            _evadeAnchor = _lastSelf + dir * _p.EvadeDistance;
            _strafeSign = side;
        }

        // Random offset perpendicular to the line of fire, inside the error cone.
        private Vector3 AimError(Vector3 self, Vector3 aimPoint)
        {
            if (_p.AimErrorDegrees <= 0f) return Vector3.zero;

            Vector3 line = aimPoint - self;
            float dist = line.magnitude;
            if (dist < 1e-3f) return Vector3.zero;

            Vector3 random = new Vector3(
                (float)_rng.NextDouble() * 2f - 1f,
                (float)_rng.NextDouble() * 2f - 1f,
                (float)_rng.NextDouble() * 2f - 1f);
            Vector3 perp = Vector3.ProjectOnPlane(random, line / dist);
            if (perp.sqrMagnitude < 1e-6f) perp = Tangent(line / dist);

            float angle = (float)_rng.NextDouble() * _p.AimErrorDegrees * Mathf.Deg2Rad;
            return perp.normalized * (dist * Mathf.Tan(angle));
        }

        private void FlipStrafe()
        {
            _strafeSign = -_strafeSign;
            _strafeTimer = NextStrafeSwitch();
        }

        private float NextStrafeSwitch()
        {
            return _p.StrafeSwitchInterval * (0.5f + (float)_rng.NextDouble());
        }

        private static Vector3 Tangent(Vector3 radial)
        {
            Vector3 t = Vector3.Cross(Vector3.up, radial);
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.right;
        }
    }
}
