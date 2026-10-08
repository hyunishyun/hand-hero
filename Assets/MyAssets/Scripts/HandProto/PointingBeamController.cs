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

    [Header("Aiming")]
    [SerializeField] private LayerMask aimMask = -1;
    [SerializeField] private float maxAimDistance = 80f;
    [SerializeField] private float reticleSmoothing = 20f;

    [Header("Firing")]
    [SerializeField] private float fireCooldown = 0.35f;
    [Tooltip("Health removed from a hero (HeroHealth) per beam hit")]
    [SerializeField] private float damage = 20f;

    [Header("Charge Shot (both palms together, fires on release)")]
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

    [Header("Effects (optional)")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

    private Vector3 _aimPoint;
    private IHandInputSource _sourceOverride;
    private float _lastFireTime = -999f;
    private float _beamTimer;
    private readonly ChargeShotModel _charge = new ChargeShotModel();

    private void Awake()
    {
        if (chargeIndicator != null) chargeIndicator.gameObject.SetActive(false);

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

        // A lost aiming hand arrives as HasAim = false: the reticle freezes at
        // the last aim point and normal shots don't fire.
        if (input.HasAim) UpdateAim(input.AimRay);

        // Charge shot: releases at the last aim point even if the aiming hand
        // dropped out while the hands were together.
        bool charging = input.Has(HandGestures.ChargeHeld);
        if (!character.IsAlive) _charge.Cancel();
        ChargeStep step = _charge.Step(charging && character.IsAlive, Time.deltaTime, charge);
        UpdateChargeIndicator(step);
        if (step.Released && _aimPoint != Vector3.zero)
        {
            Fire(Mathf.Lerp(chargeMinDamage, chargeMaxDamage, step.Power),
                Mathf.Lerp(1.5f, chargeMaxWidthMultiplier, step.Power));
            return;
        }

        if (input.HasAim && input.FireTriggered && !charging && character.IsAlive) TryFire();
    }

    private void UpdateChargeIndicator(ChargeStep step)
    {
        if (chargeIndicator == null) return;
        chargeIndicator.gameObject.SetActive(step.Charging);
        if (!step.Charging) return;

        chargeIndicator.position = character.transform.position;
        // Small until the min charge is reached, then grows with the charge.
        float size = step.Ready ? Mathf.Lerp(0.5f, 1f, step.Fraction) : 0.3f;
        chargeIndicator.localScale = Vector3.one * (size * chargeIndicatorMaxSize);
    }

    private void UpdateAim(Ray ray)
    {
        if (ray.direction == Vector3.zero) return;

        Vector3 point = RaycastIgnoringSelf(ray, maxAimDistance, out RaycastHit hit)
            ? hit.point
            : ray.GetPoint(maxAimDistance);

        float t = 1f - Mathf.Exp(-reticleSmoothing * Time.deltaTime);
        _aimPoint = _aimPoint == Vector3.zero ? point : Vector3.Lerp(_aimPoint, point, t);

        if (reticle != null)
        {
            reticle.position = _aimPoint;
            // Reticle keeps a readable size at any distance.
            float dist = Vector3.Distance(Camera.main != null ? Camera.main.transform.position : ray.origin, _aimPoint);
            reticle.localScale = Vector3.one * Mathf.Max(0.1f, dist * 0.02f);
        }
    }

    private void TryFire()
    {
        if (Time.time - _lastFireTime < fireCooldown) return;
        Fire(damage, 1f);
    }

    private void Fire(float shotDamage, float widthMultiplier)
    {
        _lastFireTime = Time.time;

        Vector3 origin = character.transform.position;

        // The actual damage ray goes from the CHARACTER to the aim point, so
        // cover matters from the hero's position, not from the player's seat.
        Vector3 dir = (_aimPoint - origin).normalized;
        float dist = Vector3.Distance(origin, _aimPoint);
        Vector3 end = _aimPoint;

        if (RaycastIgnoringSelf(new Ray(origin, dir), dist + 0.5f, out RaycastHit hit))
        {
            end = hit.point;

            var target = hit.collider.GetComponentInParent<PrototypeTarget>();
            if (target != null) target.OnHit();

            var receiver = hit.collider.GetComponentInParent<BeamHitReceiver>();
            if (receiver != null)
                receiver.Receive(new BeamHit { Point = hit.point, Direction = dir, Damage = shotDamage, Shooter = character });

            if (hitEffectPrefab != null)
            {
                GameObject fx = Instantiate(hitEffectPrefab, end, Quaternion.LookRotation(-dir));
                Destroy(fx, 1f);
            }
        }

        if (beam != null)
        {
            beam.startWidth = beamWidth * widthMultiplier;
            beam.endWidth = beamWidth * 0.5f * widthMultiplier;
            beam.SetPosition(0, origin);
            beam.SetPosition(1, end);
            beam.enabled = true;
            _beamTimer = beamDuration;
        }

        if (audioSource != null && fireSound != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.1f);
            audioSource.PlayOneShot(fireSound);
        }
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
