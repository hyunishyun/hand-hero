using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("Health UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Settings")]
    [SerializeField] private bool useColorGradient = true;
    [SerializeField] private Color healthyColor = Color.green;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField] private Color criticalColor = Color.red;

    private PlayerHealth playerHealth;

    private void Start()
    {
        // PlayerHealth 찾기
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealth not found in scene!");
            return;
        }

        // 초기 업데이트
        UpdateHealthUI();
    }

    private void Update()
    {
        if (playerHealth != null)
        {
            UpdateHealthUI();
        }
    }

    private void UpdateHealthUI()
    {
        float currentHealth = playerHealth.CurrentHealth;
        float maxHealth = playerHealth.MaxHealth;
        float healthPercent = currentHealth / maxHealth;

        // Fill Amount 업데이트
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = healthPercent;

            // 색상 그라데이션
            if (useColorGradient)
            {
                if (healthPercent > 0.5f)
                {
                    // 100% → 50%: 초록 → 노랑
                    float t = (healthPercent - 0.5f) / 0.5f;
                    healthBarFill.color = Color.Lerp(warningColor, healthyColor, t);
                }
                else
                {
                    // 50% → 0%: 노랑 → 빨강
                    float t = healthPercent / 0.5f;
                    healthBarFill.color = Color.Lerp(criticalColor, warningColor, t);
                }
            }
        }

        // 텍스트 업데이트
        if (healthText != null)
        {
            healthText.text = $"HEALTH: {Mathf.CeilToInt(currentHealth)}";

            // 텍스트 색상도 변경 (선택사항)
            if (useColorGradient)
            {
                healthText.color = healthBarFill.color;
            }
        }
    }
}