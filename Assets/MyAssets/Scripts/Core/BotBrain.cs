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
        Dash,     // round 5 (T2): flying to a new spot on its range sphere (Sniper, Lancer)
        Hold,     // round 5 (T2): still at that spot until its attack is fired (Lancer)
    }

    // Opponent AI that outputs HandInputData — the same input the player's hands
    // produce — so the bot flies through the same puppeteer clutch mapping and
    // flight model, with the same speed cap (fair, and it ports to the network
    // as just another input source). It never touches the hero directly.
    //
    // It holds the clutch the whole time and mirrors ClutchMapper's target, so it
    // can steer the hero toward any desired point by emitting hand deltas.
    // Shooting: lock aim (with error) -> telegraph for TelegraphTime -> fire.
    // Archetypes (round 4, S6) change the attack: telegraph and interval
    // multipliers, bursts (later shots re-aim) and the boss's alternating
    // patterns. The default attack (Striker) is the original bot exactly.
    // Round 5 (T2 / D4) adds a movement personality per pattern (BotMovement):
    // range, strafe width and rhythm, and dashes to a new spot on the range
    // sphere before / after an attack, holding still in between. The default
    // movement (Striker, or all zero) is the original bot exactly.
    public class BotBrain
    {
        private enum DashPhase
        {
            None,
            BeforeAttack, // flying to the spot it will attack from
            Hold,         // still at that spot through the telegraph and shots
            AfterAttack,  // flying to a new spot after the attack's last shot
        }

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

        // Movement personalities (round 5, T2): each pattern's movement, the
        // pattern of the attack under way, and the dash / hold around an attack.
        // The arena of the latest Step bounds the dash spots.
        private BotMovement _move;
        private BotMovement _altMove;
        private bool _activeIsAlt;
        private DashPhase _dash;
        private Vector3 _dashSpot;
        private float _dashTimer;
        private ArenaBounds _bounds;

        // Terrain pieces (round 5, T2-P4): world boxes a dash or hold spot keeps
        // out of, grown by the clearance (about the hero's radius). Hero flight
        // has no collision, so passing through a piece is harmless (round 4);
        // only a spot where the bot stops and fires from inside one is unfair.
        private Bounds[] _obstacles = new Bounds[16];
        private int _obstacleCount;
        private float _obstacleClearance;

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
        // Round 5 (T2): where the current dash goes, or where the bot holds.
        public Vector3 DashSpot => _dashSpot;

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

        // Movement personalities (round 5, T2 / D4): `move` goes with SetAttacks'
        // `attack`, `altMove` with its `altAttack`. Drops a dash under way.
        public void SetMovement(BotMovement move, BotMovement altMove)
        {
            _move = move;
            _altMove = altMove;
            _dash = DashPhase.None;
        }

        // Round 5 (T2-P4): the terrain pieces turned on (world boxes, copied) and
        // how far a dash or hold spot keeps from them. Kept across Reset and
        // Reseed; count 0 = none. Takes effect from the next dash.
        public void SetObstacles(Bounds[] boxes, int count, float clearance)
        {
            count = boxes == null ? 0 : Mathf.Clamp(count, 0, boxes.Length);
            if (count > _obstacles.Length) _obstacles = new Bounds[count];
            if (count > 0) Array.Copy(boxes, _obstacles, count);
            _obstacleCount = count;
            _obstacleClearance = Mathf.Max(0f, clearance);
        }

        // Round 5 (T2-P4): inside a terrain piece grown by the clearance.
        public bool InsideObstacle(Vector3 p)
        {
            for (int i = 0; i < _obstacleCount; i++)
            {
                Vector3 d = p - _obstacles[i].center;
                Vector3 e = _obstacles[i].extents;
                float c = _obstacleClearance;
                if (Mathf.Abs(d.x) <= e.x + c && Mathf.Abs(d.y) <= e.y + c && Mathf.Abs(d.z) <= e.z + c) return true;
            }
            return false;
        }

        private BotAttack Current => _useAlt && _switchEvery > 0 ? _alt : _primary;

        // Round 5 (T2): the next attack is the alt pattern's.
        private bool NextIsAlt => _useAlt && _switchEvery > 0;

        // The movement in charge now: the attack under way's (its telegraph,
        // shots and the dash after it), else the next attack's (its dash and hold).
        private BotMovement Move =>
            (_telegraphing || _shotsLeft > 0 || _dash == DashPhase.AfterAttack ? _activeIsAlt : NextIsAlt)
                ? _altMove
                : _move;

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
            _bounds = bounds;
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

            // Round 5 (T2): a dash or a hold owns the movement until it ends.
            if (_dash != DashPhase.None && UpdateDash(self, dt)) return _dashSpot;

            // Striker's movement scales are exactly 1, so its numbers are today's.
            BotMovement m = Move;
            float range = _p.PreferredRange * m.RangeScale;
            Vector3 ring = _perceived + radial * range;

            if (dist > range + _p.RangeTolerance)
            {
                State = BotState.Approach;
                return ring;
            }

            State = BotState.Strafe;

            _strafeTimer -= dt;
            if (_strafeTimer <= 0f) FlipStrafe();

            Vector3 desired = ring + Tangent(radial) * (_strafeSign * (_p.StrafeLead * m.StrafeScale));
            // Round 5 (T2): back onto the range sphere, so the distance stays the archetype's.
            if (m.KeepRange) desired = _perceived + (desired - _perceived).normalized * range;
            desired.y += Mathf.Sin(_time * 0.9f) * (_p.VerticalWeave * m.WeaveScale);

            // Pinned against a wall: go the other way round.
            if (bounds.Enabled && (bounds.Clamp(desired) - desired).sqrMagnitude > 1f
                && _strafeTimer < _p.StrafeSwitchInterval * m.SwitchScale * 0.5f)
                FlipStrafe();

            return desired;
        }

        // Round 5 (T2 / D4): advances a dash; true while the dash or the hold
        // owns the movement (the caller then flies to _dashSpot). A dash before
        // an attack turns into a hold where it ends; the dash after one hands
        // back to strafing. A wall that stops a dash short ends it at the timeout.
        private bool UpdateDash(Vector3 self, float dt)
        {
            if (_dash == DashPhase.Hold)
            {
                State = BotState.Hold;
                return true;
            }

            BotMovement m = Move;
            _dashTimer += dt;
            float arrive = m.ArriveDistance;
            bool arrived = (self - _dashSpot).sqrMagnitude <= arrive * arrive;
            if (!arrived && _dashTimer < m.DashTime)
            {
                State = BotState.Dash;
                return true;
            }

            if (_dash == DashPhase.BeforeAttack)
            {
                _dash = DashPhase.Hold;
                // Stopped short: hold right here, unless that is inside a terrain
                // piece (T2-P4); then keep making for the clear spot.
                if (!arrived && !InsideObstacle(self)) _dashSpot = self;
                State = BotState.Hold;
                return true;
            }

            _dash = DashPhase.None;
            return false;
        }

        // Round 5 (T2): sets off to a new spot `DashDistance` along the range
        // sphere, to a random side (the other one when a wall cuts it short),
        // at a random height inside the weave. A spot inside a terrain piece
        // (T2-P4) gives way to the other side, then to half the arc on either
        // side. No distance, or no clear spot: hold in place (before an attack)
        // or keep strafing (after one).
        private void StartDash(DashPhase phase, BotMovement m)
        {
            _dashTimer = 0f;
            if (m.DashDistance <= 0f)
            {
                NoDash(phase);
                return;
            }

            float range = _p.PreferredRange * m.RangeScale;
            float side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            float up = ((float)_rng.NextDouble() * 2f - 1f) * (_p.VerticalWeave * m.WeaveScale);

            Vector3 toSelf = _lastSelf - _perceived;
            Vector3 radial = toSelf.sqrMagnitude > 1e-6f ? toSelf.normalized : _lastRadial;
            float radians = m.DashDistance / Mathf.Max(range, 1f);

            Vector3 spot = SphereSpot(radial, side * radians, range, up);
            Vector3 clamped = _bounds.Clamp(spot);
            float cut = (clamped - spot).sqrMagnitude;
            if (cut > 1f)
            {
                Vector3 other = SphereSpot(radial, -side * radians, range, up);
                Vector3 otherClamped = _bounds.Clamp(other);
                if ((otherClamped - other).sqrMagnitude < cut)
                {
                    side = -side;
                    clamped = otherClamped;
                }
            }

            if (_obstacleCount > 0 && InsideObstacle(clamped)
                && !ClearSpot(radial, range, up, -side, radians, ref side, ref clamped)
                && !ClearSpot(radial, range, up, side, radians * 0.5f, ref side, ref clamped)
                && !ClearSpot(radial, range, up, -side, radians * 0.5f, ref side, ref clamped))
            {
                NoDash(phase);
                return;
            }

            _dash = phase;
            _dashSpot = clamped;
            _strafeSign = side; // strafe on the way it dashed
        }

        // Round 5 (T2-P4): the spot `radians` toward `trySide`, inside the arena;
        // when it is clear of the terrain pieces it becomes the dash's spot and side.
        private bool ClearSpot(Vector3 radial, float range, float up, float trySide, float radians,
            ref float side, ref Vector3 spot)
        {
            Vector3 candidate = _bounds.Clamp(SphereSpot(radial, trySide * radians, range, up));
            if (InsideObstacle(candidate)) return false;
            side = trySide;
            spot = candidate;
            return true;
        }

        // No dash: hold where it is until the attack (before one), or strafe on (after one).
        private void NoDash(DashPhase phase)
        {
            _dash = phase == DashPhase.BeforeAttack ? DashPhase.Hold : DashPhase.None;
            _dashSpot = _lastSelf;
        }

        // `radial` turned `radians` about the vertical, `range` from the
        // perceived enemy, `up` meters higher.
        private Vector3 SphereSpot(Vector3 radial, float radians, float range, float up)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            var dir = new Vector3(radial.x * c + radial.z * s, radial.y, radial.z * c - radial.x * s);
            Vector3 spot = _perceived + dir * range;
            spot.y += up;
            return spot;
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

            // Round 5 (T2 / D4): a dash-and-hold pattern (Lancer) first flies to a
            // new spot, setting off DashLeadTime before the attack is due so the
            // telegraph starts about on time, and telegraphs once it holds there.
            BotMovement next = NextIsAlt ? _altMove : _move;

            _fireTimer -= dt;
            if (_fireTimer > 0f)
            {
                if (next.DashBeforeAttack && _dash == DashPhase.None && _fireTimer <= next.DashLeadTime
                    && Vector3.Distance(self, _perceived) <= _p.MaxFireRange)
                    StartDash(DashPhase.BeforeAttack, next);
                return false;
            }
            if (Vector3.Distance(self, _perceived) > _p.MaxFireRange)
            {
                // Round 5 (T2): no attack to dash for or hold through.
                if (_dash == DashPhase.BeforeAttack || _dash == DashPhase.Hold) _dash = DashPhase.None;
                return false;
            }

            if (next.DashBeforeAttack && _dash != DashPhase.Hold)
            {
                if (_dash == DashPhase.None) StartDash(DashPhase.BeforeAttack, next);
                if (_dash != DashPhase.Hold) return false;
            }

            _lockedAim = _perceived + AimError(self, _perceived);
            _active = Current;
            _activeIsAlt = NextIsAlt;
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
            AfterAttack();
            return true;
        }

        // Round 5 (T2 / D4): the attack's last shot ends its hold; a pattern that
        // repositions (Sniper, Lancer) dashes to a new spot.
        private void AfterAttack()
        {
            BotMovement move = _activeIsAlt ? _altMove : _move;
            if (move.DashAfterAttack) StartDash(DashPhase.AfterAttack, move);
            else _dash = DashPhase.None;
        }

        // The telegraph and any burst shots left, and (round 5, T2) a dash or
        // hold; the interval timer is untouched.
        private void CancelAttack()
        {
            _telegraphing = false;
            _shotsLeft = 0;
            _dash = DashPhase.None;
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
            return _p.StrafeSwitchInterval * Move.SwitchScale * (0.5f + (float)_rng.NextDouble());
        }

        private static Vector3 Tangent(Vector3 radial)
        {
            Vector3 t = Vector3.Cross(Vector3.up, radial);
            return t.sqrMagnitude > 1e-6f ? t.normalized : Vector3.right;
        }
    }
}
