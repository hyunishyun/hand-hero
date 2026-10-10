using System.Collections.Generic;
using HandHero.Core;
using UnityEngine;

// Right-hand commander aiming + pinch-to-fire.
// You point AT the world (shoulder-anchored ray from HandGestureTracker),
// a reticle shows the aim point, and a pinch makes the CHARACTER fire a beam
// from its own position to that point. Aiming is yours, firing is the hero's.
// Input comes only as HandInputData (ADR 9); pinch thresholds live in XRHandsInputSource.
public class PointingBeamController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlyingCharacter character;
    [SerializeField] private Transform reticle;   // small sphere/quad in the arena
    [SerializeField] private LineRenderer beam;   // world-space line renderer
    [Tooltip("Where this hero's input comes from (XR hands, debug keyboard/mouse, ...)")]
    [SerializeField] private HandInputSourceBehaviour inputSource;
    [Tooltip("Optional. Run items (damage, crit, cooldown, charge); empty = one on the hero, none = neutral")]
    [SerializeField] private RunHeroStats runStats;

    [Header("Aiming")]
    [SerializeField] private LayerMask aimMask = -1;
    [SerializeField] private float maxAimDistance = 80f;
    [SerializeField] private float reticleSmoothing = 20f;

    [Header("Aim Assist (ASSIST mode)")]
    [Tooltip("The aim snaps to a target within this cone around the aim ray (degrees); 0 = off")]
    [SerializeField] private float assistAngle = 4f;
    [Tooltip("The snap holds until the target leaves this cone (degrees, wider than assistAngle = no flicker)")]
    [SerializeField] private float assistReleaseAngle = 6f;
    [Tooltip("Acquire cone in the MR tabletop view (degrees): heroes are tiny there, so it is wider. Ignored when assistAngle is 0")]
    [SerializeField] private float tabletopAssistAngle = 6f;
    [Tooltip("Release cone in the MR tabletop view (degrees, wider than tabletopAssistAngle = no flicker)")]
    [SerializeField] private float tabletopAssistReleaseAngle = 8f;
    [Tooltip("The view counts as the tabletop when HandGestureTracker.WorldScale is above this (VR arena = 1)")]
    [SerializeField] private float tabletopWorldScaleThreshold = 1.5f;
    [Tooltip("Optional. Ring shown around the target the aim snapped to (both modes)")]
    [SerializeField] private LockOnRing lockOnRing;

    [Header("Aim Mode")]
    [Tooltip("Player's ASSIST / CURSOR setting. Empty = always ASSIST (the bot)")]
    [SerializeField] private AimModeSetting aimModeSetting;
    [Tooltip("Optional. This hero's health: the cursor returns to the arena center on (re)spawn and round start")]
    [SerializeField] private HeroHealth health;
    [Tooltip("Shown only in ASSIST mode (the reticle)")]
    [SerializeField] private GameObject[] assistOnlyVisuals;
    [Tooltip("Shown only in CURSOR mode (the marker and its floor disc)")]
    [SerializeField] private GameObject[] cursorOnlyVisuals;

    [Header("Cursor (CURSOR mode: right gun grip drags a 3D aim marker, index fires)")]
    [Tooltip("The 3D aim marker (no collider)")]
    [SerializeField] private Transform cursorMarker;
    [Tooltip("World meters the marker moves per meter of right-hand movement (same idea as the puppeteer's positionScale)")]
    [SerializeField] private float cursorPositionScale = 60f;
    [Tooltip("The aim snaps to a target this close to the marker (meters); 0 = off")]
    [SerializeField] private float cursorAssistRadius = 1.2f;
    [Tooltip("The snap holds until the target is this far from the marker (meters, larger = no flicker)")]
    [SerializeField] private float cursorAssistReleaseRadius = 1.8f;

    [Header("Tracking-lost cue (D7)")]
    [Tooltip("Reticle (ASSIST) / cursor marker (CURSOR) color while the aim hand is not tracked; the lock-on ring hides meanwhile")]
    [SerializeField] private Color aimLostColor = new Color(0.45f, 0.45f, 0.45f);

    [Header("Firing")]
    [SerializeField] private float fireCooldown = 0.35f;
    [Tooltip("Health removed from a hero (HeroHealth) per beam hit")]
    [SerializeField] private float damage = 20f;

    [Header("Charge Shot (held pinch: press fires a normal shot, holding charges, release fires)")]
    [Tooltip("Max speed multiplier while charging (the left hand can still fly the hero)")]
    [SerializeField] private float chargeMoveSpeedMultiplier = 0.6f;
    [Tooltip("Min/max charge seconds; shorter holds fire nothing")]
    [SerializeField] private ChargeParams charge = ChargeParams.Default;
    [Tooltip("Damage of a charge shot released right at the min charge time")]
    [SerializeField] private float chargeMinDamage = 30f;
    [Tooltip("Damage of a fully charged shot")]
    [SerializeField] private float chargeMaxDamage = 70f;
    [Tooltip("Beam width multiplier of a fully charged shot (min charge = 1.5x)")]
    [SerializeField] private float chargeMaxWidthMultiplier = 4f;
    [Tooltip("Optional orb that follows the hero and grows while charging (no collider)")]
    [SerializeField] private Transform chargeIndicator;
    [SerializeField] private float chargeIndicatorMaxSize = 1.4f;

    [Header("Beam")]
    [SerializeField] private float beamDuration = 0.12f;
    [SerializeField] private float beamWidth = 0.06f;
    [SerializeField] private Color beamColor = new Color(0.4f, 0.9f, 1f);
    [Tooltip("Beam color of a critical hit (run item Focus Lens)")]
    [SerializeField] private Color critBeamColor = new Color(1f, 0.85f, 0.2f);

    [Header("Effects (optional)")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Optional recording played on this hero's AudioSource; empty = SfxPlayer's synthesized shot")]
    [SerializeField] private AudioClip fireSound;
    [Tooltip("Pitch of the hit-confirm sound on a critical hit (normal hits = 1)")]
    [SerializeField] private float critHitPitch = 1.35f;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];
    private static readonly Collider[] OverlapBuffer = new Collider[8];
    // How far inside a collider's bounds the shot origin must be to count as inside
    // it: a hero clamped onto the arena floor or back wall sits exactly on their
    // surface and still fires.
    private const float CoverInset = 0.001f;

    private Vector3 _aimPoint;
    private IHandInputSource _sourceOverride;
    private readonly ShotCooldown _shots = new ShotCooldown();
    private float _beamTimer;
    private float _damageScale = 1f;
    // Run bot archetypes (round 4, S6): the next shot's damage, beam look and fire
    // pitch. Neutral (the player, the Quick Match bot, Strikers) = x1, own color.
    private float _shotDamageMult = 1f;
    private float _shotWidthMult = 1f;
    private float _shotDurationMult = 1f;
    private Color _shotColor = Color.clear;
    private float _shotPitch = 1f;
    private readonly ChargeShotModel _charge = new ChargeShotModel();
    private readonly PinchHoldStats _pinchHolds = new PinchHoldStats();

    // Assist candidates, rebuilt each frame. The current pick is remembered as the
    // target object, not an index, so list changes never jump the lock elsewhere.
    private readonly List<Vector3> _candidatePositions = new List<Vector3>();
    private readonly List<AimAssistTarget> _candidateTargets = new List<AimAssistTarget>();
    private AimAssistTarget _assistTarget;
    private Vector3 _rawAimPoint;
    private Transform _head;
    private readonly AimCursorModel _cursor = new AimCursorModel();
    private bool _visualsApplied;
    private AimMode _visualsMode;
    private Renderer _reticleRenderer;
    private Renderer _cursorRenderer;
    private bool _aimLostShown;
    private bool _chargeSounded;
    private bool _readySounded;

    // Where the hero fires: the reticle point (ASSIST) or the cursor marker (CURSOR).
    public Vector3 AimPoint => _aimPoint;
    public AimAssistTarget AssistTarget => _assistTarget;
    public AimMode Mode => aimModeSetting != null ? aimModeSetting.Mode : AimMode.Assist;
    // Every finished pinch / trigger hold: duration and whether it charged (D13, for run telemetry).
    public PinchHoldStats PinchHolds => _pinchHolds;
    // Run telemetry (D16): shots since ResetShotStats; a hit = the ray reached a hero.
    public int ShotsFired { get; private set; }
    public int ShotsHit { get; private set; }
    public int ChargeShotsFired { get; private set; }
    // Round 5 T4: the charge is building (past HoldDelay), for the demo's HOLD = CHARGE caption.
    public bool IsCharging { get; private set; }

    public void ResetShotStats()
    {
        ShotsFired = 0;
        ShotsHit = 0;
        ChargeShotsFired = 0;
        _pinchHolds.Clear();
    }

    // Pinch holds are recorded during a run only (round 4, S3): RunDirector turns
    // this on at run start and off once the run log is written.
    public void SetHoldRecording(bool on)
    {
        _pinchHolds.Recording = on;
    }

    // Cursor back to the arena center, assist lock dropped.
    public void ResetAim()
    {
        SetAssistTarget(null);
        if (character == null) return;
        _cursor.Reset(character.Bounds.Center);
        if (Mode == AimMode.Cursor) _aimPoint = _cursor.Position;
    }

    private void Start()
    {
        ResetAim();
    }

    private void OnEnable()
    {
        if (health != null) health.Respawned += OnRespawned;
    }

    // Revive or respawn (BR-1): no charge or buffered shot carries over to the new life.
    private void OnRespawned()
    {
        ResetAim();
        _charge.Cancel();
        UpdateChargeIndicator(default);
        if (character != null) character.SetChargeSpeedMultiplier(1f);
        _shots.ClearPending();
    }

    private void Awake()
    {
        runStats = RunHeroStats.Find(runStats, character);
        _pinchHolds.Recording = false; // until a run starts (SetHoldRecording)
        if (chargeIndicator != null) chargeIndicator.gameObject.SetActive(false);
        if (reticle != null) _reticleRenderer = reticle.GetComponent<Renderer>();
        if (cursorMarker != null) _cursorRenderer = cursorMarker.GetComponent<Renderer>();

        if (beam != null)
        {
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = beamWidth;
            beam.endWidth = beamWidth * 0.5f;
            beam.startColor = beamColor;
            beam.endColor = beamColor;
            beam.enabled = false;
        }
    }

    // Run bots (R8): enemy damage scaling (Elite). 1 = the tuned damage.
    public void SetDamageScale(float scale)
    {
        _damageScale = Mathf.Max(0f, scale);
    }

    // Run bots (S6): set by RunBot right before the bot's shot fires (the bot's
    // input samples earlier in the frame). color alpha 0 = beamColor.
    public void SetShotProfile(float damageMult, float widthMult, float durationMult, Color color, float pitch)
    {
        _shotDamageMult = Mathf.Max(0f, damageMult);
        _shotWidthMult = widthMult > 0f ? widthMult : 1f;
        _shotDurationMult = durationMult > 0f ? durationMult : 1f;
        _shotColor = color;
        _shotPitch = pitch > 0f ? pitch : 1f;
    }

    // Code-assigned source (bot, test). Takes priority over the inspector field.
    public void SetInputSource(IHandInputSource source)
    {
        _sourceOverride = source;
    }

    private IHandInputSource Source()
    {
        if (_sourceOverride != null) return _sourceOverride;
        return inputSource != null ? inputSource : null;
    }

    private void Update()
    {
        IHandInputSource source = Source();
        if (source == null || character == null) return;

        HandInputData input = source.Current;

        UpdateBeamTimer();

        AimMode mode = Mode;
        ApplyModeVisuals(mode);
        ShowAimLost(input.AimHandLost);

        // The lock is re-checked only while aiming; a target that died meanwhile
        // (aim hand out of view) must not keep the ring on its hidden body.
        if (_assistTarget != null && !_assistTarget.IsTargetable) SetAssistTarget(null);

        // CURSOR: the gun grip drags the marker. ASSIST: a lost aiming hand
        // arrives as HasAim = false and the reticle freezes at the last aim point.
        if (mode == AimMode.Cursor) UpdateCursor(input);
        else if (input.HasAim) UpdateAim(input.AimRay);

        // CURSOR fires with the index trigger (the pinch can't happen while gripping);
        // the same pinch rules then apply to it.
        if (mode == AimMode.Cursor)
        {
            input.FireTriggered = input.TriggerFired;
            input.PinchHeld = input.TriggerHeld;
        }

        // Charge shot: a held pinch charges, release fires. Losing the aim hand or
        // a switched-off source (pause) cancels without firing (ChargeInputRule).
        // Run items scale the numbers; no items (Quick Match, the bot) = the tuned values.
        HeroStats stats = RunHeroStats.StatsOf(runStats);

        ChargeInputAction action = ChargeInputRule.Decide(input, character.IsAlive);
        ChargeStep step = default;
        if (action == ChargeInputAction.Cancel) _charge.Cancel();
        else step = _charge.Step(action == ChargeInputAction.Hold, Time.deltaTime, CombatMath.Charge(charge, stats));
        // The pinch that ended this hold (round 5, D2); CURSOR holds are trigger holds.
        _pinchHolds.Add(step, mode == AimMode.Cursor ? default : input.PinchRelease);

        UpdateChargeIndicator(step);
        UpdateChargeSounds(step);
        character.SetChargeSpeedMultiplier(step.Charging ? chargeMoveSpeedMultiplier : 1f);
        IsCharging = step.Charging; // Round 5 T4

        if (step.Released && _aimPoint != Vector3.zero)
        {
            Fire(CombatMath.Shot(Mathf.Lerp(chargeMinDamage, chargeMaxDamage, step.Power) * _damageScale, true, stats,
                    CritRoll(stats)),
                Mathf.Lerp(1.5f, chargeMaxWidthMultiplier, step.Power), true);
            return;
        }

        // The press edge of a pinch fires a normal shot; holding on only charges.
        // A pull during the cooldown is buffered and fires when it ends; losing the
        // aim hand or the hero drops the buffered shot.
        if (!input.HasAim || !character.IsAlive) _shots.ClearPending();
        else if (_shots.Step(input.FireTriggered, Time.time, CombatMath.FireCooldown(fireCooldown, stats)))
            Fire(CombatMath.Shot(damage * _damageScale * _shotDamageMult, false, stats, CritRoll(stats)), 1f, false);
    }

    // Player only (D14): a hum when the charge starts (after HoldDelay) and a ping
    // when releasing would fire. Bots charge silently; their telegraph warns instead.
    private void UpdateChargeSounds(ChargeStep step)
    {
        if (!step.Charging)
        {
            _chargeSounded = false;
            _readySounded = false;
            return;
        }
        if (character.Team != HeroTeam.Player) return;
        Vector3 at = character.transform.position;
        if (!_chargeSounded)
        {
            _chargeSounded = true;
            SfxPlayer.Play(SfxId.ChargeStart, at);
        }
        if (step.Ready && !_readySounded)
        {
            _readySounded = true;
            SfxPlayer.Play(SfxId.ChargeReady, at);
        }
    }

    private void OnDisable()
    {
        if (health != null) health.Respawned -= OnRespawned;
        SetAssistTarget(null);
        _charge.Cancel();
        IsCharging = false; // Round 5 T4
        UpdateChargeIndicator(default);
        if (character != null) character.SetChargeSpeedMultiplier(1f);
        // Pooled bots (SP-6): no beam or buffered shot left over for the next spawn.
        _shots.ClearPending();
        _beamTimer = 0f;
        if (beam != null) beam.enabled = false;
        ShowAimLost(false);
    }

    // Grey reticle / cursor and no lock-on ring while the aim hand is untracked,
    // so a tracking dropout never reads as a frozen game (D7). Applied on change only.
    private void ShowAimLost(bool lost)
    {
        if (lost == _aimLostShown) return;
        _aimLostShown = lost;
        RendererTint.Set(_reticleRenderer, lost, aimLostColor);
        RendererTint.Set(_cursorRenderer, lost, aimLostColor);
        if (lockOnRing != null) lockOnRing.SetSuppressed(lost);
    }

    private void UpdateChargeIndicator(ChargeStep step)
    {
        if (chargeIndicator == null) return;
        if (chargeIndicator.gameObject.activeSelf != step.Charging) chargeIndicator.gameObject.SetActive(step.Charging);
        if (!step.Charging) return;

        chargeIndicator.position = character.transform.position;
        // Small until the min charge is reached, then grows with the charge.
        float size = step.Ready ? Mathf.Lerp(0.5f, 1f, step.Fraction) : 0.3f;
        chargeIndicator.localScale = Vector3.one * (size * chargeIndicatorMaxSize);
    }

    private void UpdateAim(Ray ray)
    {
        if (ray.direction == Vector3.zero) return;

        HandGestureTracker tracker = HandGestureTracker.Instance;
        bool tabletop = tracker != null && tracker.WorldScale > tabletopWorldScaleThreshold;
        AimAssist.Cone(tabletop, assistAngle, assistReleaseAngle, tabletopAssistAngle, tabletopAssistReleaseAngle,
            out float acquire, out float release);
        // No cone and nothing held (the bots: 0 deg assist): nothing can be picked, skip the scan (GC-1).
        int pick = -1;
        if (acquire > 0f || _assistTarget != null)
        {
            CollectCandidates();
            int current = _assistTarget != null ? _candidateTargets.IndexOf(_assistTarget) : -1;
            pick = AimAssist.SelectByAngle(ray, _candidatePositions, current, acquire, release);
        }
        SetAssistTarget(pick >= 0 ? _candidateTargets[pick] : null);

        // The reticle always shows where the hand points (smoothed against jitter):
        // snapped onto a target it would sit inside the hero mesh and vanish. The
        // lock-on ring shows the snap instead.
        // Nothing hit: where the ray leaves the arena, never far behind it.
        Vector3 raw = RaycastIgnoringSelf(ray, maxAimDistance, out RaycastHit hit)
            ? hit.point
            : character.Bounds.AimFallback(ray, maxAimDistance);
        float t = 1f - Mathf.Exp(-reticleSmoothing * Time.deltaTime);
        _rawAimPoint = _rawAimPoint == Vector3.zero ? raw : Vector3.Lerp(_rawAimPoint, raw, t);

        // Snapped: fire exactly at the target (a smoothed point would trail a fast hero).
        _aimPoint = pick >= 0 ? _candidatePositions[pick] : _rawAimPoint;

        if (reticle != null)
        {
            reticle.position = _rawAimPoint;
            // Reticle keeps a readable size at any distance.
            Transform head = HeadTransform();
            float dist = Vector3.Distance(head != null ? head.position : ray.origin, _rawAimPoint);
            reticle.localScale = Vector3.one * Mathf.Max(0.1f, dist * 0.02f);
        }
    }

    private void UpdateCursor(HandInputData input)
    {
        Vector3 cursor = _cursor.Step(input.AimClutchHeld, input.AimClutchDelta, cursorPositionScale, character.Bounds);
        if (cursorMarker != null) cursorMarker.position = cursor;

        CollectCandidates();
        int current = _assistTarget != null ? _candidateTargets.IndexOf(_assistTarget) : -1;
        int pick = AimAssist.SelectByRadius(cursor, _candidatePositions, current, cursorAssistRadius,
            cursorAssistReleaseRadius);
        SetAssistTarget(pick >= 0 ? _candidateTargets[pick] : null);

        // The clutch already smooths the marker; a snapped aim sits on the target.
        _aimPoint = pick >= 0 ? _candidatePositions[pick] : cursor;
    }

    private void ApplyModeVisuals(AimMode mode)
    {
        if (_visualsApplied && _visualsMode == mode) return;
        _visualsApplied = true;
        _visualsMode = mode;
        SetAssistTarget(null); // a lock never carries over between modes

        SetActive(assistOnlyVisuals, mode == AimMode.Assist);
        SetActive(cursorOnlyVisuals, mode == AimMode.Cursor);
        if (mode == AimMode.Cursor) _aimPoint = _cursor.Position;
    }

    private static void SetActive(GameObject[] objects, bool on)
    {
        if (objects == null) return;
        foreach (GameObject go in objects)
            if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    // Targetable heroes and practice targets, excluding this controller's own hero.
    private void CollectCandidates()
    {
        _candidatePositions.Clear();
        _candidateTargets.Clear();
        Transform self = character.transform;
        // Index loop: foreach over IReadOnlyList boxes its enumerator every frame (GC-1).
        var all = AimAssistTarget.All;
        for (int i = 0; i < all.Count; i++)
        {
            AimAssistTarget t = all[i];
            if (t == null || !t.IsTargetable || t.transform.IsChildOf(self)) continue;
            _candidateTargets.Add(t);
            _candidatePositions.Add(t.transform.position);
        }
    }

    // Camera.main looked up once, again only if the camera goes away (GC-9).
    private Transform HeadTransform()
    {
        if (_head == null)
        {
            Camera cam = Camera.main;
            if (cam != null) _head = cam.transform;
        }
        return _head;
    }

    // The lock-on ring follows the current snap target in both modes.
    private void SetAssistTarget(AimAssistTarget target)
    {
        _assistTarget = target;
        if (lockOnRing == null) return;
        if (target != null) lockOnRing.Show(target.transform);
        else lockOnRing.Hide();
    }


    // Draws from Unity's RNG only when a crit is possible, so heroes without
    // crit items (Quick Match, the bot) leave the random sequence untouched.
    private static double CritRoll(HeroStats stats) => stats.CritChance > 0f ? Random.value : 1.0;

    private void Fire(ShotDamage shot, float widthMultiplier, bool charged)
    {
        float shotDamage = shot.Damage;
        _shots.MarkFired(Time.time);
        ShotsFired++;
        if (charged) ChargeShotsFired++;

        Vector3 origin = character.transform.position;

        // The actual damage ray goes from the CHARACTER to the aim point, so
        // cover matters from the hero's position, not from the player's seat.
        Vector3 dir = (_aimPoint - origin).normalized;
        float dist = Vector3.Distance(origin, _aimPoint);
        Vector3 end = _aimPoint;

        if (OriginInsideCover(origin))
        {
            // Blocked at the hero (review T3-R2-1): the cover it sits in takes the
            // shot, no damage. Shots at it stop on the cover's face the same way.
            end = origin;
            BeamImpactPool.Play(hitEffectPrefab, end,
                dir != Vector3.zero ? Quaternion.LookRotation(-dir) : Quaternion.identity);
        }
        else if (RaycastIgnoringSelf(new Ray(origin, dir), dist + 0.5f, out RaycastHit hit))
        {
            end = hit.point;

            var target = hit.collider.GetComponentInParent<PrototypeTarget>();
            if (target != null) target.OnHit();

            var receiver = hit.collider.GetComponentInParent<BeamHitReceiver>();
            if (receiver != null)
            {
                ShotsHit++;
                receiver.Receive(new BeamHit { Point = hit.point, Direction = dir, Damage = shotDamage, Shooter = character });
            }

            // Hit confirm for the player only; a crit rings higher (D15).
            if ((target != null || receiver != null) && character.Team == HeroTeam.Player)
                SfxPlayer.PlayUi(SfxId.HitDealt, shot.Crit ? critHitPitch : 1f);

            BeamImpactPool.Play(hitEffectPrefab, end, Quaternion.LookRotation(-dir));
        }

        if (beam != null)
        {
            float width = beamWidth * widthMultiplier * _shotWidthMult;
            beam.startWidth = width;
            beam.endWidth = width * 0.5f;
            Color color = shot.Crit ? critBeamColor : _shotColor.a > 0f ? _shotColor : beamColor;
            beam.startColor = color;
            beam.endColor = color;
            beam.SetPosition(0, origin);
            beam.SetPosition(1, end);
            beam.enabled = true;
            _beamTimer = beamDuration * _shotDurationMult;
        }

        if (audioSource != null && fireSound != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.1f);
            audioSource.PlayOneShot(fireSound);
        }
        else
        {
            SfxPlayer.Play(SfxCues.ForShot(character.Team, charged), origin, _shotPitch);
        }
    }

    // Round 5 review (T3-R2-1): the shot starts inside solid cover - any non-trigger
    // collider on the aim mask that is not a hero (terrain pieces, pillars, targets).
    // Heroes don't collide with terrain, and the damage raycast never reports a
    // collider that contains its origin, so a hero inside a floating platform fired
    // out freely while shots at it stopped on the slab. One overlap query per shot,
    // the same rule for the player and the bots (ADR fairness).
    private bool OriginInsideCover(Vector3 origin)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, CoverInset, OverlapBuffer, aimMask,
            QueryTriggerInteraction.Ignore);
        Transform self = character.transform;
        for (int i = 0; i < count; i++)
        {
            Collider c = OverlapBuffer[i];
            if (c == null || c.transform.IsChildOf(self)) continue;
            if (c.GetComponentInParent<FlyingCharacter>() != null) continue;
            Bounds inside = c.bounds;
            inside.Expand(-2f * CoverInset);
            if (inside.Contains(origin)) return true;
        }
        return false;
    }

    // Nearest hit that is not this controller's own hero, so a hero never
    // blocks its own aim or beam (works for player and bot without extra layers).
    private bool RaycastIgnoringSelf(Ray ray, float distance, out RaycastHit nearest)
    {
        nearest = default;
        int count = Physics.RaycastNonAlloc(ray, HitBuffer, distance, aimMask);
        bool found = false;
        Transform self = character.transform;

        for (int i = 0; i < count; i++)
        {
            RaycastHit h = HitBuffer[i];
            if (h.collider.transform.IsChildOf(self)) continue;
            if (found && h.distance >= nearest.distance) continue;
            // Same team (BR-6): the beam passes through, no hit, no dodge, no kill reward.
            FlyingCharacter other = h.collider.GetComponentInParent<FlyingCharacter>();
            if (other != null && !HeroTeams.CanHit(character.Team, other.Team)) continue;
            nearest = h;
            found = true;
        }
        return found;
    }

    private void UpdateBeamTimer()
    {
        if (_beamTimer > 0f)
        {
            _beamTimer -= Time.deltaTime;
            if (_beamTimer <= 0f && beam != null)
                beam.enabled = false;
        }
    }
}
