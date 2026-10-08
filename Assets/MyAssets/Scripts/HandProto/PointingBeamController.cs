using UnityEngine;

// Right-hand commander aiming + pinch-to-fire.
// You point AT the world (shoulder-anchored ray from HandGestureTracker),
// a reticle shows the aim point, and a pinch makes the CHARACTER fire a beam
// from its own position to that point. Aiming is yours, firing is the hero's.
public class PointingBeamController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlyingCharacter character;
    [SerializeField] private Transform reticle;   // small sphere/quad in the arena
    [SerializeField] private LineRenderer beam;   // world-space line renderer

    [Header("Hand")]
    [SerializeField] private bool useRightHand = true;

    [Header("Aiming")]
    [SerializeField] private LayerMask aimMask = -1;
    [SerializeField] private float maxAimDistance = 80f;
    [SerializeField] private float reticleSmoothing = 20f;

    [Header("Firing (pinch) thresholds with hysteresis")]
    [SerializeField] private float pinchFireThreshold = 0.8f;
    [SerializeField] private float pinchResetThreshold = 0.5f;
    [SerializeField] private float fireCooldown = 0.35f;

    [Header("Beam")]
    [SerializeField] private float beamDuration = 0.12f;
    [SerializeField] private float beamWidth = 0.06f;
    [SerializeField] private Color beamColor = new Color(0.4f, 0.9f, 1f);

    [Header("Effects (optional)")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip fireSound;

    private Vector3 _aimPoint;
    private bool _pinchLatched;
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

    private void Update()
    {
        var tracker = HandGestureTracker.Instance;
        if (tracker == null || character == null) return;

        HandGestureTracker.HandState hand = useRightHand ? tracker.Right : tracker.Left;

        UpdateBeamTimer();

        if (!hand.IsTracked)
        {
            _pinchLatched = false;
            return; // reticle freezes at the last aim point
        }

        UpdateAim(hand);
        UpdateFire(hand);
    }

    private void UpdateAim(HandGestureTracker.HandState hand)
    {
        Ray ray = hand.AimRay;
        if (ray.direction == Vector3.zero) return;

        Vector3 point = Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask)
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

    private void UpdateFire(HandGestureTracker.HandState hand)
    {
        // Edge-detected pinch with hysteresis: fire once per pinch.
        if (!_pinchLatched && hand.PinchStrength >= pinchFireThreshold)
        {
            _pinchLatched = true;
            TryFire();
        }
        else if (_pinchLatched && hand.PinchStrength <= pinchResetThreshold)
        {
            _pinchLatched = false;
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

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist + 0.5f, aimMask))
        {
            end = hit.point;

            var target = hit.collider.GetComponentInParent<PrototypeTarget>();
            if (target != null) target.OnHit();

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
