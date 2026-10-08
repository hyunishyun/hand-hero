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

    private void Awake()
    {
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
        // the last aim point and nothing fires.
        if (!input.HasAim) return;

        UpdateAim(input.AimRay);
        if (input.FireTriggered) TryFire();
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
                receiver.Receive(new BeamHit { Point = hit.point, Direction = dir, Shooter = character });

            if (hitEffectPrefab != null)
            {
                GameObject fx = Instantiate(hitEffectPrefab, end, Quaternion.LookRotation(-dir));
                Destroy(fx, 1f);
            }
        }

        if (beam != null)
        {
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
