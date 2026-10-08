using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GunController : MonoBehaviour
{
    [Header("Gun Settings")]
    [SerializeField] private Transform gunBarrel;
    [SerializeField] private float fireRange = 100f;
    [SerializeField] private float fireRate = 0.2f;
    [SerializeField] private int damage = 25;
    [SerializeField] private LayerMask hitLayers = -1;

    [Header("Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Visual Effects")]
    [SerializeField] private LineRenderer bulletTrail;
    [SerializeField] private float trailDuration = 0.1f;
    [SerializeField] private Color trailColor = Color.yellow;
    [SerializeField] private float trailWidth = 0.02f;

    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Input")]
    [SerializeField] private InputActionProperty fireAction;

    [Header("Haptics (Optional)")]
    [SerializeField] private bool enableHaptics = false;
    [SerializeField] private float hapticIntensity = 0.5f;
    [SerializeField] private float hapticDuration = 0.1f;

    [Header("UI Interaction")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float uiRayDistance = 10f;
    [SerializeField] private bool enableUIInteraction = true;

    private float lastFireTime;
    private float trailTimer = 0f;
    private bool isQuitting = false;

    // UI Raycast용
    private EventSystem eventSystem;
    private PointerEventData pointerEventData;

    private void Start()
    {
        // LineRenderer 초기화
        if (bulletTrail != null)
        {
            bulletTrail.startColor = trailColor;
            bulletTrail.endColor = trailColor;
            bulletTrail.startWidth = trailWidth;
            bulletTrail.endWidth = trailWidth;
            bulletTrail.enabled = false;
            bulletTrail.useWorldSpace = false;
        }

        // UI 시스템 초기화
        InitializeUISystem();
    }

    private void InitializeUISystem()
    {
        // Main Camera 찾기
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("GunController: Main Camera not found for UI interaction!");
            }
        }

        // EventSystem 찾기
        eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("GunController: EventSystem not found! UI interaction may not work.");
        }

        Debug.Log("GunController: UI Interaction System initialized");
    }

    private void Update()
    {
        // Trail 타이머
        if (trailTimer > 0)
        {
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0 && bulletTrail != null)
            {
                bulletTrail.enabled = false;
            }
        }
    }

    private void OnEnable()
    {
        if (fireAction.action != null)
        {
            fireAction.action.Enable();
            fireAction.action.performed += OnFireInput;
        }

        // Scene 이벤트 구독
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        CleanupInputAction();
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnDestroy()
    {
        CleanupInputAction();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        CleanupInputAction();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        CleanupInputAction();
    }

    private void CleanupInputAction()
    {
        if (fireAction.action != null)
        {
            try
            {
                fireAction.action.performed -= OnFireInput;

                if (fireAction.action.enabled)
                {
                    fireAction.action.Disable();
                }
            }
            catch (System.Exception e)
            {
                if (!isQuitting)
                {
                    Debug.LogWarning($"Input cleanup: {e.Message}");
                }
            }
        }
    }

    private void OnFireInput(InputAction.CallbackContext context)
    {
        TryFire();
    }

    private void TryFire()
    {
        if (Time.time - lastFireTime < fireRate) return;

        lastFireTime = Time.time;
        Fire();
    }

    private void Fire()
    {
        // Muzzle flash
        if (muzzleFlashPrefab != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, gunBarrel);
            flash.transform.localPosition = Vector3.zero;
            flash.transform.localRotation = Quaternion.identity;
            Destroy(flash, 0.15f);
        }

        // Audio feedback
        if (audioSource != null && fireSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.4f);
            audioSource.PlayOneShot(fireSound);
        }

        // Haptic feedback
        if (enableHaptics)
        {
            TriggerHapticFeedback();
        }

        // ========== Ray 생성 ==========
        Ray ray = new Ray(gunBarrel.position, gunBarrel.forward);

        // ========== 1. Monster/3D Object Raycast (우선!) ==========
        RaycastHit hit;
        Vector3 endPoint;
        bool hitMonster = false;

        if (Physics.Raycast(ray, out hit, fireRange, hitLayers))
        {
            endPoint = hit.point;

            // Hit effect
            if (hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(effect, 1f);
            }

            // Damage target
            Monster monster = hit.collider.GetComponent<Monster>();
            if (monster != null)
            {
                monster.TakeDamage(damage);
                hitMonster = true;
                Debug.Log($"Hit Monster! Damage: {damage}");
            }
        }
        else
        {
            endPoint = gunBarrel.position + gunBarrel.forward * fireRange;
        }

        // Draw bullet trail
        ShowBulletTrail(gunBarrel.position, endPoint);

        // ========== 2. UI Button Raycast (Monster 안 맞았을 때만) ==========
        if (!hitMonster && enableUIInteraction)
        {
            CheckUIRaycast(ray);
        }
    }

    private void CheckUIRaycast(Ray ray)
    {
        if (eventSystem == null || mainCamera == null)
            return;

        // 가까운 거리부터 먼 거리까지 시도
        float[] testDistances = { 0.3f, 0.5f, 0.8f, 1f, 1.5f, 2f, 3f };

        foreach (float testDistance in testDistances)
        {
            Vector3 worldPoint = gunBarrel.position + gunBarrel.forward * testDistance;
            Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldPoint);

            // 화면 안에 있는지 확인
            if (screenPoint.z < 0 ||
                screenPoint.x < 0 || screenPoint.x > Screen.width ||
                screenPoint.y < 0 || screenPoint.y > Screen.height)
            {
                continue;
            }

            // PointerEventData 설정
            if (pointerEventData == null)
            {
                pointerEventData = new PointerEventData(eventSystem);
            }

            pointerEventData.position = screenPoint;

            // UI Raycast
            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerEventData, results);

            if (results.Count == 0)
                continue;

            // Button 찾기
            foreach (RaycastResult result in results)
            {
                Button button = result.gameObject.GetComponent<Button>();
                if (button == null)
                    button = result.gameObject.GetComponentInParent<Button>();

                if (button != null && button.interactable)
                {
                    button.onClick.Invoke();
                    Debug.Log($"✓ Button '{button.name}' clicked at {testDistance}m");
                    return;
                }
            }
        }
    }

    private void ShowBulletTrail(Vector3 start, Vector3 end)
    {
        if (bulletTrail != null)
        {
            bulletTrail.enabled = true;

            Vector3 localStart = bulletTrail.transform.InverseTransformPoint(start);
            Vector3 localEnd = bulletTrail.transform.InverseTransformPoint(end);

            bulletTrail.SetPosition(0, localStart);
            bulletTrail.SetPosition(1, localEnd);

            trailTimer = trailDuration;
        }
    }

    private void TriggerHapticFeedback()
    {
        // 나중에 구현
    }
}