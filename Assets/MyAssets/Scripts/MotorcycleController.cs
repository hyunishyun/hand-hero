using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class MotorcycleController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] public float maxSpeed = 20f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float deceleration = 15f;
    [SerializeField] private float brakeForce = 25f;

    [Header("Rotation Settings")]
    [SerializeField] private float turnSpeed = 80f;
    [SerializeField] private float turnSmoothTime = 0.1f;

    [Header("Ground Detection")]
    [SerializeField] private float groundCheckDistance = 3f;
    [SerializeField] private float hoverHeight = 1f;
    [SerializeField] private float hoverForce = 500f;
    [SerializeField] private float hoverDamping = 15f;
    [SerializeField] private LayerMask groundLayer = -1;

    [Header("Slope Handling")]
    [SerializeField] private float maxSlopeAngle = 50f;
    [SerializeField] private float slopeAlignSpeed = 5f;

    [Header("Jump Settings")]
    [SerializeField] private InputActionProperty jumpAction;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpCooldown = 0.5f;

    [Header("Jump Audio")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Input Actions")]
    [SerializeField] private InputActionProperty accelerateAction;
    [SerializeField] private InputActionProperty brakeAction;
    [SerializeField] private InputActionProperty steerAction;

    [Header("Handlebar")]
    [SerializeField] private Transform handlebar;
    [SerializeField] private float handlebarRotationAmount = 30f;
    [SerializeField] private float handlebarSmoothTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = true;
    [SerializeField] private bool showDebugInfo = false;

    private Rigidbody rb;
    private float currentSpeed = 0f;
    private float targetTurnAngle = 0f;
    private float currentTurnVelocity = 0f;
    private float currentHandlebarRotation = 0f;
    private float handlebarVelocity = 0f;
    private Vector3 groundNormal = Vector3.up;
    private bool isGrounded = false;
    private float groundDistance = 0f;
    private float lastJumpTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 150f;
        rb.linearDamping = 1f;
        rb.angularDamping = 3f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void OnEnable()
    {
        // 기존 액션들
        if (accelerateAction.action != null)
        {
            accelerateAction.action.Enable();
        }

        if (brakeAction.action != null)
        {
            brakeAction.action.Enable();
        }

        if (steerAction.action != null)
        {
            steerAction.action.Enable();
        }

        // 점프 액션
        if (jumpAction.action != null)
        {
            jumpAction.action.Enable();
            jumpAction.action.performed += OnJump;
        }
    }

    private void OnDisable()
    {
        // 기존 액션들
        if (accelerateAction.action != null && accelerateAction.action.enabled)
        {
            accelerateAction.action.Disable();
        }

        if (brakeAction.action != null && brakeAction.action.enabled)
        {
            brakeAction.action.Disable();
        }

        if (steerAction.action != null && steerAction.action.enabled)
        {
            steerAction.action.Disable();
        }

        // 점프 액션
        if (jumpAction.action != null && jumpAction.action.enabled)
        {
            jumpAction.action.performed -= OnJump;
            jumpAction.action.Disable();
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateHandlebar();

        if (showDebugInfo)
        {
            Debug.Log($"Grounded: {isGrounded}, Distance: {groundDistance:F2}, Speed: {currentSpeed:F2}, Y Rotation: {transform.eulerAngles.y:F1}");
        }
    }

    private void FixedUpdate()
    {
        GroundCheck();
        ApplyHoverForce();
        HandleMovement();
        AlignToSlope();
        HandleRotation();
    }

    private void HandleInput()
    {
        float triggerValue = accelerateAction.action?.ReadValue<float>() ?? 0f;
        float gripValue = brakeAction.action?.ReadValue<float>() ?? 0f;
        Vector2 joystickInput = steerAction.action?.ReadValue<Vector2>() ?? Vector2.zero;

        if (gripValue > 0.1f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeForce * Time.deltaTime);
        }
        else if (triggerValue > 0.1f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed * triggerValue, acceleration * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, deceleration * Time.deltaTime);
        }

        targetTurnAngle = joystickInput.x * turnSpeed;
    }

    private void GroundCheck()
    {
        RaycastHit hit;
        Vector3 rayStart = transform.position;
        Vector3 rayDirection = Vector3.down;

        if (Physics.Raycast(rayStart, rayDirection, out hit, groundCheckDistance, groundLayer))
        {
            isGrounded = true;
            groundNormal = hit.normal;
            groundDistance = hit.distance;

            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, rayDirection * groundDistance, Color.green, 0.1f);
                Debug.DrawRay(hit.point, hit.normal * 2f, Color.blue, 0.1f);
            }
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            groundDistance = groundCheckDistance;

            if (showDebugRays)
            {
                Debug.DrawRay(rayStart, rayDirection * groundCheckDistance, Color.red, 0.1f);
            }
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

    private void HandleMovement()
    {
        Vector3 moveDirection = transform.forward;
        Vector3 targetVelocity = moveDirection * currentSpeed;
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
    }

    private void HandleRotation()
    {
        float smoothTurn = Mathf.SmoothDampAngle(0f, targetTurnAngle, ref currentTurnVelocity, turnSmoothTime);
        float deltaAngle = smoothTurn * Time.fixedDeltaTime;

        Vector3 currentEuler = transform.eulerAngles;
        currentEuler.y += deltaAngle;
        transform.rotation = Quaternion.Euler(currentEuler);
    }

    private void AlignToSlope()
    {
        if (!isGrounded) return;

        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);

        if (slopeAngle <= maxSlopeAngle)
        {
            float currentYRotation = transform.eulerAngles.y;
            Vector3 desiredForward = Quaternion.Euler(0f, currentYRotation, 0f) * Vector3.forward;
            Vector3 projectedForward = Vector3.ProjectOnPlane(desiredForward, groundNormal).normalized;

            if (projectedForward.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(projectedForward, groundNormal);
                Quaternion newRotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    slopeAlignSpeed * Time.fixedDeltaTime
                );
                transform.rotation = newRotation;
            }
        }
    }

    private void UpdateHandlebar()
    {
        if (handlebar == null) return;

        Vector2 joystickInput = steerAction.action?.ReadValue<Vector2>() ?? Vector2.zero;
        float targetRotation = -joystickInput.x * handlebarRotationAmount;

        currentHandlebarRotation = Mathf.SmoothDamp(
            currentHandlebarRotation,
            targetRotation,
            ref handlebarVelocity,
            handlebarSmoothTime
        );

        handlebar.localRotation = Quaternion.Euler(0f, 0f, currentHandlebarRotation);
    }

    // ========== 점프 시스템 ==========

    private void OnJump(InputAction.CallbackContext context)
    {
        TryJump();
    }

    private void TryJump()
    {
        if (Time.time - lastJumpTime < jumpCooldown) return;
        if (!isGrounded) return;

        PerformJump();
    }

    private void PerformJump()
    {
        lastJumpTime = Time.time;

        Vector3 currentVelocity = rb.linearVelocity;
        currentVelocity.y = jumpForce;
        rb.linearVelocity = currentVelocity;

        if (audioSource != null && jumpSound != null)
        {
            audioSource.PlayOneShot(jumpSound);
        }

        Debug.Log("Motorcycle jumped!");
    }

    public float GetCurrentSpeed() => currentSpeed;
    public bool IsGrounded() => isGrounded;

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * 3f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.right * 2f);

        if (isGrounded)
        {
            Vector3 targetPos = transform.position - Vector3.up * hoverHeight;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPos, 0.3f);
        }
    }
}