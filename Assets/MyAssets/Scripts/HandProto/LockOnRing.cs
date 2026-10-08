using UnityEngine;

// Lock-on feedback for the aim assist: a camera-facing ring around the target
// the aim snapped to, readable at any distance, with a short "pop" when a new
// target is acquired. Presentation only (no collider).
[RequireComponent(typeof(LineRenderer))]
public class LockOnRing : MonoBehaviour
{
    private const int Segments = 48;

    [SerializeField] private LineRenderer line;
    [Tooltip("Smallest ring radius (meters), around close targets")]
    [SerializeField] private float minRadius = 1.2f;
    [Tooltip("Ring size as seen from the seat (degrees); keeps far targets readable")]
    [SerializeField] private float angularSize = 2.5f;
    [Tooltip("Line width at 10 m; grows with distance")]
    [SerializeField] private float width = 0.06f;
    [SerializeField] private Color color = new Color(1f, 0.15f, 0.15f);
    [Tooltip("Scale the ring starts at when a new target is acquired")]
    [SerializeField] private float popScale = 1.6f;
    [Tooltip("Seconds the acquire pop takes to settle (unscaled time)")]
    [SerializeField] private float popTime = 0.15f;

    private Transform _target;
    private float _popStart;

    public void Show(Transform target)
    {
        if (target == _target) return;
        _target = target;
        _popStart = Time.unscaledTime;
    }

    public void Hide()
    {
        _target = null;
        if (line != null) line.enabled = false;
    }

    private void Awake()
    {
        if (line == null) line = GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.startColor = line.endColor = color;
        line.enabled = false;
    }

    private void OnDisable()
    {
        Hide();
    }

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (_target == null || !_target.gameObject.activeInHierarchy || cam == null)
        {
            if (line.enabled) line.enabled = false;
            return;
        }

        Vector3 toTarget = _target.position - cam.transform.position;
        float dist = toTarget.magnitude;
        transform.position = _target.position;
        transform.rotation = Quaternion.LookRotation(toTarget, cam.transform.up);

        float pop = popTime > 0f
            ? Mathf.Lerp(popScale, 1f, Mathf.Clamp01((Time.unscaledTime - _popStart) / popTime))
            : 1f;
        float radius = Mathf.Max(minRadius, dist * Mathf.Tan(angularSize * Mathf.Deg2Rad)) * pop;
        for (int i = 0; i < Segments; i++)
        {
            float a = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
        }
        line.widthMultiplier = width * Mathf.Max(1f, dist / 10f);
        line.startColor = line.endColor = color;
        line.enabled = true;
    }
}
