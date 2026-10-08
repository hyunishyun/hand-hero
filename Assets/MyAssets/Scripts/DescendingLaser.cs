using UnityEngine;
using UnityEngine.Events;

public class DescendingLaser : MonoBehaviour
{
    [Header("Laser Settings")]
    public GameObject origin;
    public float maxDistance = 50f;
    public LayerMask collisionMask;
    public bool isActive = false;

    [Header("Rotation")]
    public bool shouldRotate = true;
    public float rotationSpeed = 30f;

    [Header("Visual")]
    public Color laserColor = Color.red;
    public float laserWidth = 0.1f;

    [Header("Events")]
    public UnityEvent OnPlayerHit;

    [Header("Debug")]
    public bool showDebug = true;

    private LineRenderer lineRenderer;
    private Light laserLight;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        SetupLineRenderer();

        laserLight = GetComponent<Light>();
        if (laserLight != null)
        {
            laserLight.color = laserColor;
            laserLight.enabled = isActive;
        }

        SetLaserActive(isActive);
    }

    void SetupLineRenderer()
    {
        lineRenderer.startWidth = laserWidth;
        lineRenderer.endWidth = laserWidth;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = laserColor;
        lineRenderer.endColor = laserColor;
        lineRenderer.positionCount = 2;

        lineRenderer.material.EnableKeyword("_EMISSION");
        lineRenderer.material.SetColor("_EmissionColor", laserColor * 2f);
    }

    void Update()
    {
        if (isActive)
        {
            if (shouldRotate)
            {
                transform.Rotate(Vector3.up * rotationSpeed * Time.deltaTime);
            }

            CastLaser();
        }
    }

    void CastLaser()
    {
        if (origin == null)
        {
            origin = gameObject;
        }

        Vector3 startPos = origin.transform.position;
        Vector3 direction = origin.transform.forward;

        RaycastHit hit;

        if (Physics.Raycast(startPos, direction, out hit, maxDistance, collisionMask))
        {
            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, hit.point);

            if (hit.collider.CompareTag("Player"))
            {
                OnPlayerHit?.Invoke();
            }
        }
        else
        {
            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, startPos + direction * maxDistance);
        }
    }

    public void SetLaserActive(bool active)
    {
        isActive = active;
        lineRenderer.enabled = active;

        if (laserLight != null)
        {
            laserLight.enabled = active;
        }
    }

    void OnDrawGizmos()
    {
        if (showDebug && origin != null)
        {
            Gizmos.color = Color.red;
            Vector3 startPos = origin.transform.position;
            Vector3 direction = origin.transform.forward;

            Gizmos.DrawRay(startPos, direction * maxDistance);
        }
    }
}