using Fusion;
using UnityEngine;

// Networked version of MotorcycleController.
// Same hover/speed/slope model as the single-player script, but:
//  - input comes from BikeInputData (not InputActions),
//  - all simulation runs in FixedUpdateNetwork with Runner.DeltaTime,
//  - state that must survive prediction resimulation is [Networked],
//  - speed is scaled by NetworkedPlayerHealth.SpeedMultiplier (hit slow penalty).
//
// Public API (maxSpeed / GetCurrentSpeed / IsGrounded) intentionally matches the
// old controller so MotorcycleAudioController only needs its field type swapped.
//
// Prefab requirements: Rigidbody + NetworkObject + NetworkRigidbody3D (Physics Addon).
// Runner prefab requires RunnerSimulatePhysics3D.
[RequireComponent(typeof(Rigidbody))]
public class NetworkedMotorcycle : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float maxSpeed = 20f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;
    [SerializeField] private float brakeForce = 25f;

    [Header("Rotation Settings")]
    [SerializeField] private float turnSpeed = 80f;

    [Header("Ground / Hover")]
    [SerializeField] private float groundCheckDistance = 3f;
    [SerializeField] private float hoverHeight = 1f;
    [SerializeField] private float hoverForce = 500f;
    [SerializeField] private float hoverDamping = 15f;
    [SerializeField] private LayerMask groundLayer = -1;

    [Header("Slope Handling")]
    [SerializeField] private float maxSlopeAngle = 50f;
    [SerializeField] private float slopeAlignSpeed = 5f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpCooldown = 0.5f;

    [Header("Handlebar (visual only)")]
    [SerializeField] private Transform handlebar;
    [SerializeField] private float handlebarRotationAmount = 30f;

    // ---- Networked simulation state (survives resimulation) ----
    [Networked] private float CurrentSpeed { get; set; }
    [Networked] private NetworkButtons PreviousButtons { get; set; }
    [Networked] private TickTimer JumpCooldownTimer { get; set; }

    private Rigidbody rb;
    private NetworkedPlayerHealth health;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded;
    private float groundDistance;
    private float lastSteerInput; // presentation only (handlebar)

    public float GetCurrentSpeed() => CurrentSpeed;
    public bool IsGrounded() => isGrounded;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<NetworkedPlayerHealth>();

        rb.useGravity = true;
        rb.mass = 150f;
        rb.linearDamping = 1f;
        rb.angularDamping = 3f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void FixedUpdateNetwork()
    {
        // Runs on the server AND on the input-authority client (prediction).
        if (!GetInput(out BikeInputData input)) return;

        float dt = Runner.DeltaTime;

        // Dead bikes don't drive.
        if (health != null && health.IsDead)
        {
            CurrentSpeed = 0f;
            return;
        }

        // Hit slow penalty: scales target speed AND caps current speed,
        // so getting shot bites immediately even at full throttle.
        float speedMul = health != null ? health.SpeedMultiplier : 1f;

        // ---- Speed model (same shape as single-player version) ----
        if (input.Brake > 0.1f)
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, brakeForce * dt);
        else if (input.Throttle > 0.1f)
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, maxSpeed * input.Throttle * speedMul, acceleration * dt);
        else
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, deceleration * dt);

        CurrentSpeed = Mathf.Min(CurrentSpeed, maxSpeed * speedMul);

        GroundCheck();
        ApplyHoverForce();
        ApplyMovement();
        ApplyRotation(input.Steer, dt);
        AlignToSlope(dt);

        // ---- Jump (edge-detected button) ----
        var pressed = input.Buttons.GetPressed(PreviousButtons);
        PreviousButtons = input.Buttons;

        if (pressed.IsSet((int)BikeButtons.Jump) && isGrounded &&
            JumpCooldownTimer.ExpiredOrNotRunning(Runner))
        {
            JumpCooldownTimer = TickTimer.CreateFromSeconds(Runner, jumpCooldown);
            Vector3 v = rb.linearVelocity;
            v.y = jumpForce;
            rb.linearVelocity = v;
        }

        lastSteerInput = input.Steer;
    }

    private void GroundCheck()
    {
        if (Physics.Raycast(transform.position, Vector3.down,
            out RaycastHit hit, groundCheckDistance, groundLayer))
        {
            isGrounded = true;
            groundNormal = hit.normal;
            groundDistance = hit.distance;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            groundDistance = groundCheckDistance;
        }
    }

    private void ApplyHoverForce()
    {
        if (!isGrounded) return;

        float heightError = hoverHeight - groundDistance;
        float upVelocity = Vector3.Dot(rb.linearVelocity, Vector3.up);
        float force = (heightError * hoverForce) - (upVelocity * hoverDamping);
        rb.AddForce(Vector3.up * force, ForceMode.Force);
    }

    private void ApplyMovement()
    {
        Vector3 targetVelocity = transform.forward * CurrentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }

    private void ApplyRotation(float steer, float dt)
    {
        // NOTE: the old SmoothDampAngle used a hidden velocity field, which is
        // NOT resimulation-safe. A direct steer -> yaw mapping is deterministic.
        float deltaYaw = steer * turnSpeed * dt;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, deltaYaw, 0f));
    }

    private void AlignToSlope(float dt)
    {
        if (!isGrounded) return;

        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
        if (slopeAngle > maxSlopeAngle) return;

        float yaw = transform.eulerAngles.y;
        Vector3 desiredForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 projected = Vector3.ProjectOnPlane(desiredForward, groundNormal).normalized;
        if (projected.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(projected, groundNormal);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, target, slopeAlignSpeed * dt));
    }

    // Presentation only: handlebar visual, runs every rendered frame.
    public override void Render()
    {
        if (handlebar == null) return;
        float target = -lastSteerInput * handlebarRotationAmount;
        handlebar.localRotation = Quaternion.Lerp(
            handlebar.localRotation, Quaternion.Euler(0f, 0f, target), 10f * Time.deltaTime);
    }
}
