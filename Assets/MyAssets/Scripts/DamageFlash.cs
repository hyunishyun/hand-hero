using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DamageFlash : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image damageOverlay;

    [Header("Flash Settings")]
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.3f); // 빨강, 30% 투명도
    [SerializeField] private float flashDuration = 0.2f;
    [SerializeField] private AnimationCurve flashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Vignette Effect (Optional)")]
    [SerializeField] private bool useVignette = true;
    [SerializeField] private float vignetteIntensity = 0.5f;

    private PlayerHealth playerHealth;
    private Coroutine flashCoroutine;

    private void Start()
    {
        // PlayerHealth 찾기
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealth not found!");
            return;
        }

        // 이벤트 구독
        playerHealth.OnDamaged.AddListener(TriggerFlash);

        // 초기 상태: 완전 투명
        if (damageOverlay != null)
        {
            Color transparent = flashColor;
            transparent.a = 0f;
            damageOverlay.color = transparent;
        }
    }

    private void OnDestroy()
    {
        // 이벤트 구독 해제
        if (playerHealth != null)
        {
            playerHealth.OnDamaged.RemoveListener(TriggerFlash);
        }
    }

    public void TriggerFlash()
    {
        if (damageOverlay == null) return;

        // 이전 Flash 중단
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        // 새 Flash 시작
        flashCoroutine = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < flashDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / flashDuration;

            // Curve에 따라 Alpha 변경
            float curveValue = flashCurve.Evaluate(t);

            Color currentColor = flashColor;
            currentColor.a = flashColor.a * curveValue;

            damageOverlay.color = currentColor;

            yield return null;
        }

        // 완전 투명으로
        Color transparent = flashColor;
        transparent.a = 0f;
        damageOverlay.color = transparent;

        flashCoroutine = null;
    }
}