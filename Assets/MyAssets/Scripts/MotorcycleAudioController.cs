using UnityEngine;
using UnityEngine.InputSystem;

public class MotorcycleAudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MotorcycleController motorcycleController;

    [Header("Engine Audio Sources")]
    [SerializeField] private AudioSource engineIdle;
    [SerializeField] private AudioSource engineLow;
    [SerializeField] private AudioSource engineMid;
    [SerializeField] private AudioSource engineHigh;

    [Header("Engine Settings")]
    [SerializeField] private float engineVolumeFadeSpeed = 15f;
    [SerializeField] private float enginePitchVariation = 0.15f;
    [SerializeField] private float idlePeakSpeed = 0.12f;
    [SerializeField] private float lowPeakSpeed = 0.3f;
    [SerializeField] private float midPeakSpeed = 0.6f;
    [SerializeField] private float highPeakSpeed = 0.88f;

    [Header("Effect Audio Sources")]
    [SerializeField] private AudioSource brakeAudio;
    [SerializeField] private AudioSource throttleAudio;
    [SerializeField] private AudioSource tireAudio;

    [Header("Effect Audio Clips")]
    [SerializeField] private AudioClip throttleOpenClip;
    [SerializeField] private AudioClip throttleCloseClip;

    [Header("Brake Settings")]
    [SerializeField] private float brakeMinSpeed = 2f;
    [SerializeField] private float brakeMaxVolume = 0.8f;

    [Header("Tire Settings")]
    [SerializeField] private float tireMaxVolume = 0.5f;
    [SerializeField] private float tireFadeSpeed = 3f;

    [Header("Input Actions")]
    [SerializeField] private InputActionProperty accelerateAction;
    [SerializeField] private InputActionProperty brakeAction;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Private state
    private float currentIdleVolume = 0f;
    private float currentLowVolume = 0f;
    private float currentMidVolume = 0f;
    private float currentHighVolume = 0f;
    private float currentTireVolume = 0f;

    private bool wasAccelerating = false;
    private bool isBraking = false;
    private float lastBrakeVolume = 0f;

    private void Start()
    {
        // Start all engine layers playing
        if (engineIdle != null)
        {
            engineIdle.Play();
            engineIdle.volume = 0f;
            Debug.Log("Engine Idle started");
        }
        else
        {
            Debug.LogWarning("Engine Idle AudioSource is not assigned!");
        }

        if (engineLow != null)
        {
            engineLow.Play();
            engineLow.volume = 0f;
            Debug.Log("Engine Low started");
        }
        else
        {
            Debug.LogWarning("Engine Low AudioSource is not assigned!");
        }

        if (engineMid != null)
        {
            engineMid.Play();
            engineMid.volume = 0f;
            Debug.Log("Engine Mid started");
        }
        else
        {
            Debug.LogWarning("Engine Mid AudioSource is not assigned!");
        }

        if (engineHigh != null)
        {
            engineHigh.Play();
            engineHigh.volume = 0f;
            Debug.Log("Engine High started");
        }
        else
        {
            Debug.LogError("Engine High AudioSource is not assigned!");
        }

        // Start tire audio
        if (tireAudio != null && tireAudio.clip != null)
        {
            tireAudio.Play();
            tireAudio.volume = 0f;
            Debug.Log("Tire audio started");
        }
    }

    private void Update()
    {
        UpdateEngineAudio();
        UpdateBrakeAudio();
        UpdateThrottleAudio();
        UpdateTireAudio();

        if (showDebugInfo)
        {
            DebugAudioLevels();
        }
    }

    private void UpdateEngineAudio()
    {
        if (motorcycleController == null)
        {
            Debug.LogError("MotorcycleController reference is missing!");
            return;
        }

        // Get current speed ratio (0 to 1)
        float currentSpeed = motorcycleController.GetCurrentSpeed();
        float maxSpeed = motorcycleController.maxSpeed;
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);

        // Calculate target volume for each engine layer
        float targetIdleVolume = CalculateLayerVolume(speedRatio, idlePeakSpeed, 0.3f);
        float targetLowVolume = CalculateLayerVolume(speedRatio, lowPeakSpeed, 0.3f);
        float targetMidVolume = CalculateLayerVolume(speedRatio, midPeakSpeed, 0.4f);
        float targetHighVolume = CalculateLayerVolume(speedRatio, highPeakSpeed, 0.5f);

        // Special handling for high speed (80%+)
        if (speedRatio > 0.8f)
        {
            // Map 0.8~1.0 to 0~1
            float highSpeedBoost = (speedRatio - 0.8f) / 0.2f;

            // Guarantee minimum High volume: starts at 50%, goes to 100%
            float guaranteedHighVolume = 0.5f + (highSpeedBoost * 0.5f);
            targetHighVolume = Mathf.Max(targetHighVolume, guaranteedHighVolume);

            // Gradually fade out lower layers
            targetIdleVolume *= (1f - highSpeedBoost * 0.9f);
            targetLowVolume *= (1f - highSpeedBoost * 0.6f);
            targetMidVolume *= (1f - highSpeedBoost * 0.4f);
        }

        // Smooth volume transitions with adaptive fade speed
        float idleFadeSpeed = engineVolumeFadeSpeed;
        float lowFadeSpeed = engineVolumeFadeSpeed;
        float midFadeSpeed = engineVolumeFadeSpeed;
        float highFadeSpeed = speedRatio > 0.75f ? engineVolumeFadeSpeed * 3f : engineVolumeFadeSpeed;

        currentIdleVolume = Mathf.Lerp(currentIdleVolume, targetIdleVolume, idleFadeSpeed * Time.deltaTime);
        currentLowVolume = Mathf.Lerp(currentLowVolume, targetLowVolume, lowFadeSpeed * Time.deltaTime);
        currentMidVolume = Mathf.Lerp(currentMidVolume, targetMidVolume, midFadeSpeed * Time.deltaTime);
        currentHighVolume = Mathf.Lerp(currentHighVolume, targetHighVolume, highFadeSpeed * Time.deltaTime);

        // Force minimum High volume at very high speeds to eliminate lag
        if (speedRatio > 0.85f)
        {
            float minHighVolume = 0.55f + ((speedRatio - 0.85f) / 0.15f) * 0.45f;
            currentHighVolume = Mathf.Max(currentHighVolume, minHighVolume);
        }

        // Apply volumes to audio sources
        if (engineIdle != null)
        {
            engineIdle.volume = currentIdleVolume;
        }
        if (engineLow != null)
        {
            engineLow.volume = currentLowVolume;
        }
        if (engineMid != null)
        {
            engineMid.volume = currentMidVolume;
        }
        if (engineHigh != null)
        {
            engineHigh.volume = currentHighVolume;
        }

        // Apply subtle pitch variation based on speed
        float pitchVariation = 1f + (speedRatio * enginePitchVariation);
        if (engineIdle != null) engineIdle.pitch = pitchVariation * 0.9f;
        if (engineLow != null) engineLow.pitch = pitchVariation * 0.95f;
        if (engineMid != null) engineMid.pitch = pitchVariation * 1.0f;
        if (engineHigh != null) engineHigh.pitch = pitchVariation * 1.05f;
    }

    private float CalculateLayerVolume(float speedRatio, float peakSpeed, float range)
    {
        // Calculate distance from peak
        float distance = Mathf.Abs(speedRatio - peakSpeed);

        // Convert distance to volume (inverse relationship)
        float volume = Mathf.Clamp01(1f - (distance / range));

        // Apply curve for smoother falloff
        volume = Mathf.Pow(volume, 2f);

        return volume;
    }

    private void UpdateBrakeAudio()
    {
        if (brakeAudio == null || motorcycleController == null) return;

        // Get brake input
        float brakeValue = brakeAction.action.ReadValue<float>();
        float currentSpeed = motorcycleController.GetCurrentSpeed();
        bool isGrounded = motorcycleController.IsGrounded();

        // Determine if we should play brake sound
        bool shouldBrake = brakeValue > 0.1f && currentSpeed > brakeMinSpeed && isGrounded;

        if (shouldBrake && !isBraking)
        {
            // Start braking
            brakeAudio.Play();
            isBraking = true;
        }
        else if (!shouldBrake && isBraking)
        {
            // Stop braking
            brakeAudio.Stop();
            isBraking = false;
        }

        // Adjust brake volume based on speed and brake pressure
        if (isBraking)
        {
            float speedRatio = Mathf.Clamp01(currentSpeed / motorcycleController.maxSpeed);
            float targetVolume = brakeValue * speedRatio * brakeMaxVolume;
            lastBrakeVolume = Mathf.Lerp(lastBrakeVolume, targetVolume, 10f * Time.deltaTime);
            brakeAudio.volume = lastBrakeVolume;

            // Adjust pitch slightly based on speed
            brakeAudio.pitch = 0.9f + (speedRatio * 0.2f);
        }
    }

    private void UpdateThrottleAudio()
    {
        if (throttleAudio == null) return;

        // Get throttle input
        float throttleValue = accelerateAction.action.ReadValue<float>();
        bool isAccelerating = throttleValue > 0.1f;

        // Detect throttle changes
        if (isAccelerating && !wasAccelerating)
        {
            // Throttle opened
            if (throttleOpenClip != null)
            {
                throttleAudio.PlayOneShot(throttleOpenClip, 0.6f);
            }
        }
        else if (!isAccelerating && wasAccelerating)
        {
            // Throttle closed
            if (throttleCloseClip != null)
            {
                throttleAudio.PlayOneShot(throttleCloseClip, 0.4f);
            }
        }

        wasAccelerating = isAccelerating;
    }

    private void UpdateTireAudio()
    {
        if (tireAudio == null || motorcycleController == null) return;

        // Calculate tire volume based on speed
        float currentSpeed = motorcycleController.GetCurrentSpeed();
        float speedRatio = Mathf.Clamp01(currentSpeed / motorcycleController.maxSpeed);
        bool isGrounded = motorcycleController.IsGrounded();

        // Target volume
        float targetVolume = isGrounded ? speedRatio * tireMaxVolume : 0f;

        // Smooth fade
        currentTireVolume = Mathf.Lerp(currentTireVolume, targetVolume, tireFadeSpeed * Time.deltaTime);
        tireAudio.volume = currentTireVolume;

        // Pitch variation
        tireAudio.pitch = 0.95f + (speedRatio * 0.15f);
    }

    private void DebugAudioLevels()
    {
        if (motorcycleController == null) return;

        float speedRatio = Mathf.Clamp01(motorcycleController.GetCurrentSpeed() / motorcycleController.maxSpeed);

        Debug.Log($"Speed: {motorcycleController.GetCurrentSpeed():F1}/{motorcycleController.maxSpeed:F1} ({speedRatio:P0}) | " +
                  $"Engine - I:{currentIdleVolume:F2} L:{currentLowVolume:F2} M:{currentMidVolume:F2} H:{currentHighVolume:F2} | " +
                  $"Tire:{currentTireVolume:F2}");
    }
}