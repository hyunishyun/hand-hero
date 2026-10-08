using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Monster : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float chaseRange = 100f;  // 항상 추적하려면 큰 값

    [Header("Attack")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("References")]
    [SerializeField] private Transform player;

    // Public property for damage access
    public float Damage => damage;

    private NavMeshAgent agent;
    private bool isDead = false;
    private float lastAttackTime;

    private void Start()
    {
        currentHealth = maxHealth;
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;

        // Find player if not assigned
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                Debug.LogError("Player not found! Make sure XR Origin has 'Player' tag.");
            }
        }
    }

    private void Update()
    {
        if (isDead || player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            AttackPlayer();
        }
        else
        {
            ChasePlayer();
        }
    }

    private void ChasePlayer()
    {
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
    }

    private void AttackPlayer()
    {
        // Stop moving
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        // Look at player
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * 5f
            );
        }

        // Attack if cooldown is over
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            lastAttackTime = Time.time;
            DealDamage();
        }
    }

    private void DealDamage()
    {
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage((int)damage);
            Debug.Log($"Monster dealt {damage} damage to player!");
        }
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        currentHealth -= damageAmount;
        Debug.Log($"Monster took {damageAmount} damage. Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        // Disable NavMeshAgent
        if (agent != null)
        {
            agent.isStopped = true;
            agent.enabled = false;
        }

        Debug.Log("Monster died!");

        // WaveManager에 알림
        WaveManager waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.OnMonsterKilled();
        }

        // Destroy after delay
        Destroy(gameObject, 0.1f);
    }
}