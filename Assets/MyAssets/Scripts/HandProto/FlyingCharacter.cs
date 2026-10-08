using UnityEngine;

// Greybox flying hero for the arena prototype.
// Follows a target point with a speed-capped spring. The spring matters twice:
//  1. It filters hand-tracking noise (a jittery hand target becomes smooth flight).
//  2. It is exactly the model the Fusion server will run later — the network
//     input becomes "where the hand wants me", the simulation decides how the
//     character actually moves, so hand-position cheating is impossible.
public class FlyingCharacter : MonoBehaviour
{
    [Header("Flight Model")]
    [Tooltip("Pull toward the target point. Higher = snappier, lower = floatier")]
    [SerializeField] private float stiffness = 10f;
    [Tooltip("Velocity decay while clutched. Higher = less overshoot")]
    [SerializeField] private float damping = 5f;
    [SerializeField] private float maxSpeed = 25f;
    [Tooltip("Drag while released (gliding). Lower = longer glide")]
    [SerializeField] private float glideDrag = 0.8f;

    [Header("Arena Bounds")]
    [SerializeField] private Transform arenaCenter;
    [SerializeField] private Vector3 arenaSize = new Vector3(35f, 20f, 35f);

    [Header("Visual (child mesh, rotated for style only)")]
    [SerializeField] private Transform visual;
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("Roll applied from lateral velocity, degrees at max speed")]
    [SerializeField] private float bankAmount = 40f;

    public Vector3 Velocity => _velocity;
    public bool IsClutched => _hasTarget;

    private Vector3 _velocity;
    private Vector3 _target;
    private bool _hasTarget;

    // Called by HandPuppeteerController while the clutch (fist) is held.
    public void SetTarget(Vector3 worldPosition)
    {
        _target = ClampToArena(worldPosition);
        _hasTarget = true;
    }

    // Called when the fist opens: the character keeps its momentum and glides.
    public void ClearTarget()
    {
        _hasTarget = false;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (_hasTarget)
        {
            // Critically-damped-ish spring toward the target.
            Vector3 toTarget = _target - transform.position;
            _velocity += toTarget * (stiffness * dt);
            _velocity -= _velocity * (damping * dt);
        }
        else
        {
            // Glide: momentum with gentle drag, no gravity (hero flight).
            _velocity -= _velocity * (glideDrag * dt);
        }

        _velocity = Vector3.ClampMagnitude(_velocity, maxSpeed);
        transform.position = ClampToArena(transform.position + _velocity * dt);

        UpdateVisual(dt);
    }

    private Vector3 ClampToArena(Vector3 p)
    {
        if (arenaCenter == null) return p;
        Vector3 half = arenaSize * 0.5f;
        Vector3 local = p - arenaCenter.position;
        local.x = Mathf.Clamp(local.x, -half.x, half.x);
        local.y = Mathf.Clamp(local.y, -half.y, half.y);
        local.z = Mathf.Clamp(local.z, -half.z, half.z);
        return arenaCenter.position + local;
    }

    private void UpdateVisual(float dt)
    {
        if (visual == null) return;

        Vector3 flatVel = _velocity;
        if (flatVel.sqrMagnitude < 0.5f) return; // keep last facing when hovering

        // Face travel direction.
        Quaternion face = Quaternion.LookRotation(flatVel.normalized, Vector3.up);

        // Bank into lateral movement (style only, never applied to any camera).
        float lateral = Vector3.Dot(_velocity, visual.right) / Mathf.Max(maxSpeed, 0.01f);
        Quaternion bank = Quaternion.AngleAxis(-lateral * bankAmount, Vector3.forward);

        visual.rotation = Quaternion.Slerp(visual.rotation, face * bank, turnSpeed * dt);
    }

    private void OnDrawGizmosSelected()
    {
        if (arenaCenter == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(arenaCenter.position, arenaSize);
    }
}
