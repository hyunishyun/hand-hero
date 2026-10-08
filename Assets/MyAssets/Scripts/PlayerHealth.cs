using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;  // ← float!

    [Header("Events")]
    public UnityEvent OnDamaged;
    public UnityEvent OnDeath;
    public UnityEvent<float> OnHealthChanged;

    // Public properties for UI access
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void TakeDamage(float damage)  // float 파라미터
    {
        if (isDead) return;

        currentHealth -= damage;  // ← 이제 에러 없음!
        currentHealth = Mathf.Max(0, currentHealth);

        OnDamaged?.Invoke();
        OnHealthChanged?.Invoke(currentHealth);

        Debug.Log($"Player took {damage} damage. Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        OnHealthChanged?.Invoke(currentHealth);

        Debug.Log($"Player healed {amount}. Health: {currentHealth}/{maxHealth}");
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        OnDeath?.Invoke();

        Debug.Log("Player died!");

        // Game Over logic here
    }

    public bool IsDead() => isDead;
}