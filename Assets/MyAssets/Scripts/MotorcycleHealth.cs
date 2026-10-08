using UnityEngine;

public class MotorcycleHealth : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Settings")]
    [SerializeField] private bool autoFindPlayer = true;

    private void Start()
    {
        // PlayerHealth 자동으로 찾기
        if (autoFindPlayer && playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();

            if (playerHealth == null)
            {
                Debug.LogError("PlayerHealth not found! Make sure PlayerHealth script is in the scene.");
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 몬스터와 충돌 체크
        Monster monster = collision.gameObject.GetComponent<Monster>();

        if (monster != null && playerHealth != null)
        {
            // 플레이어에게 데미지 전달
            playerHealth.TakeDamage(monster.Damage);

            Debug.Log($"Motorcycle hit by monster! Damage: {monster.Damage}");
        }
    }
}