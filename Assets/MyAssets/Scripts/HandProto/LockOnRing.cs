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
    private Transform _head;
    // Ring points are rewritten only when the radius or width changes (GC-6).
    private float _shownRadius = -1f;
    private float _shownWidth = -1f;
    private readonly Vector3[] _points = new Vector3[Segments];
    private static Vector3[] _unitCircle;

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
        if (_unitCircle == null)
        {
            _unitCircle = new Vector3[Segments];
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                _unitCircle[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
        }
    }

    private void OnDisable()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (_head == null)
        {
            Camera cam = Camera.main;
            if (cam != null) _head = cam.transform;
        }
        if (_target == null || !_target.gameObject.activeInHierarchy || _head == null)
        {
            if (line.enabled) line.enabled = false;
            return;
        }

        Vector3 toTarget = _target.position - _head.position;
        float dist = toTarget.magnitude;
        transform.position = _target.position;
        transform.rotation = Quaternion.LookRotation(toTarget, _head.up);

        float pop = popTime > 0f
            ? Mathf.Lerp(popScale, 1f, Mathf.Clamp01((Time.unscaledTime - _popStart) / popTime))
            : 1f;
        float radius = Mathf.Max(minRadius, dist * Mathf.Tan(angularSize * Mathf.Deg2Rad)) * pop;
        if (Mathf.Abs(radius - _shownRadius) > 0.001f)
        {
            _shownRadius = radius;
            for (int i = 0; i < Segments; i++) _points[i] = _unitCircle[i] * radius;
            line.SetPositions(_points);
        }
        float w = width * Mathf.Max(1f, dist / 10f);
        if (Mathf.Abs(w - _shownWidth) > 0.0001f)
        {
            _shownWidth = w;
            line.widthMultiplier = w;
        }
        if (!line.enabled) line.enabled = true;
    }
}
