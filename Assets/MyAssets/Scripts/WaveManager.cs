using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Wave
{
    public int monsterCount;
    public float spawnInterval = 1f;
}

public class WaveManager : MonoBehaviour
{
    [Header("Wave Settings")]
    [SerializeField] private Wave[] waves;
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float timeBetweenWaves = 5f;

    [Header("UI")]
    [SerializeField] private WaveAnnouncement waveAnnouncement;

    [Header("Events")]
    public UnityEvent OnAllWavesComplete;

    // Public properties for UI access
    public int CurrentWaveIndex => currentWaveIndex;
    public int TotalWaves => waves.Length;
    public int RemainingEnemies => monstersAlive;

    private int currentWaveIndex = 0;
    private int monstersAlive = 0;
    private int monstersToSpawn = 0;
    private float spawnTimer = 0f;
    private bool isSpawning = false;
    private bool waitingForNextWave = false;

    private void Start()
    {
        if (waves.Length == 0)
        {
            Debug.LogError("WaveManager: No waves configured!");
            return;
        }

        StartWave();
    }

    private void Update()
    {
        if (isSpawning)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && monstersToSpawn > 0)
            {
                SpawnMonster();
                monstersToSpawn--;
                spawnTimer = waves[currentWaveIndex].spawnInterval;

                if (monstersToSpawn == 0)
                {
                    isSpawning = false;
                }
            }
        }
        else if (waitingForNextWave)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                waitingForNextWave = false;
                StartWave();
            }
        }
        else if (monstersAlive == 0 && !isSpawning)
        {
            OnWaveComplete();
        }
    }

    private void StartWave()
    {
        if (currentWaveIndex >= waves.Length)
        {
            TriggerVictory();
            return;
        }

        Wave currentWave = waves[currentWaveIndex];
        monstersToSpawn = currentWave.monsterCount;
        monstersAlive = 0;
        isSpawning = true;
        spawnTimer = 0f;

        Debug.Log($"Wave {currentWaveIndex + 1} started!");
        // Wave 시작 알림 표시
        if (waveAnnouncement != null)
        {
            waveAnnouncement.ShowWaveStart(currentWaveIndex + 1, waves.Length);
        }
    }

    private void SpawnMonster()
    {
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("WaveManager: No spawn points assigned!");
            return;
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        GameObject monster = Instantiate(monsterPrefab, spawnPoint.position, spawnPoint.rotation);

        // Monster에 WaveManager 알림 설정
        Monster monsterScript = monster.GetComponent<Monster>();
        if (monsterScript != null)
        {
            // Monster가 죽을 때 알림 받기 위해 이벤트 구독 (나중에 구현)
        }

        monstersAlive++;
    }

    public void OnMonsterKilled()
    {
        monstersAlive--;

        if (monstersAlive < 0)
        {
            monstersAlive = 0;
        }
    }

    private void OnWaveComplete()
    {
        currentWaveIndex++;

        if (currentWaveIndex < waves.Length)
        {
            waitingForNextWave = true;
            spawnTimer = timeBetweenWaves;
            Debug.Log($"Wave {currentWaveIndex} complete! Next wave in {timeBetweenWaves}s");
            // 다음 Wave 카운트다운 표시
            if (waveAnnouncement != null)
            {
                waveAnnouncement.ShowNextWaveCountdown(timeBetweenWaves);
            }
        }
        else
        {
            TriggerVictory();
        }
    }

    private void TriggerVictory()
    {
        Debug.Log("All waves completed! Victory!");
        OnAllWavesComplete?.Invoke();

        // GameManager에도 알림 (있으면)
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.OnVictory();
        }
    }
}