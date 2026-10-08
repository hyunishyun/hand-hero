using UnityEngine;

public class JumpPad : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private float cooldown = 0.5f;

    [Header("Effects")]
    [SerializeField] private ParticleSystem jumpEffect;
    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Animation")]
    [SerializeField] private Transform padVisual;
    [SerializeField] private float pushDepth = 0.1f;
    [SerializeField] private float animationSpeed = 10f;

    private float lastJumpTime;
    private Vector3 originalPosition;
    private bool isPushed = false;

    private void Start()
    {
        if (padVisual != null)
        {
            originalPosition = padVisual.localPosition;
        }
    }

    private void Update()
    {
        if (padVisual != null && isPushed)
        {
            padVisual.localPosition = Vector3.Lerp(
                padVisual.localPosition,
                originalPosition,
                animationSpeed * Time.deltaTime
            );

            if (Vector3.Distance(padVisual.localPosition, originalPosition) < 0.01f)
            {
                isPushed = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastJumpTime < cooldown) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            PerformJump(rb);
        }
    }

    private void PerformJump(Rigidbody rb)
    {
        lastJumpTime = Time.time;

        Vector3 currentVelocity = rb.linearVelocity;
        currentVelocity.y = jumpForce;
        rb.linearVelocity = currentVelocity;

        if (jumpEffect != null)
        {
            jumpEffect.Play();
        }

        if (padVisual != null)
        {
            padVisual.localPosition = originalPosition - Vector3.up * pushDepth;
            isPushed = true;
        }

        if (audioSource != null && jumpSound != null)
        {
            audioSource.PlayOneShot(jumpSound);
        }
    }
}