using UnityEngine;

// Scene-side registry of spawn points. Used both for the initial spawn
// (NetworkGameLauncher) and for respawning after death (NetworkedPlayerHealth).
public class RespawnManager : MonoBehaviour
{
    private static RespawnManager _instance;

    [SerializeField] private Transform[] spawnPoints;

    private void Awake()
    {
        _instance = this;
    }

    public static Transform GetSpawnPoint(int index)
    {
        if (_instance == null || _instance.spawnPoints.Length == 0)
        {
            Debug.LogError("RespawnManager: no spawn points configured!");
            return null;
        }
        int safe = Mathf.Abs(index) % _instance.spawnPoints.Length;
        return _instance.spawnPoints[safe];
    }
}
