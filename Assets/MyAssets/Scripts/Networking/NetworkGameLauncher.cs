using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

// Starts (or joins) a Fusion Host Mode session and spawns one bike per player.
// Put this on an empty "NetworkLauncher" object in the game scene.
// NOTE: empty callback signatures below may need tiny adjustments depending on
// your exact Fusion 2 SDK minor version — the compiler will tell you.
public class NetworkGameLauncher : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Prefabs")]
    [SerializeField] private NetworkRunner runnerPrefab;
    [SerializeField] private NetworkObject playerBikePrefab;

    [Header("Session")]
    [SerializeField] private string sessionName = "DesertRace";
    [SerializeField] private bool autoStartOnPlay = true;

    private NetworkRunner _runner;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedBikes = new();

    private async void Start()
    {
        if (autoStartOnPlay) await StartSession();
    }

    // Can also be hooked to a lobby UI button.
    public async System.Threading.Tasks.Task StartSession()
    {
        if (_runner != null) return;

        _runner = Instantiate(runnerPrefab);
        _runner.ProvideInput = true; // this peer sends VR input
        _runner.AddCallbacks(this);

        var sceneInfo = new NetworkSceneInfo();
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

        var result = await _runner.StartGame(new StartGameArgs
        {
            // First peer becomes the Host (server authority), others join as clients.
            GameMode = GameMode.AutoHostOrClient,
            SessionName = sessionName,
            Scene = sceneInfo,
            SceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
        });

        if (!result.Ok)
            Debug.LogError($"Fusion StartGame failed: {result.ShutdownReason}");
    }

    // ---- Player lifecycle (server only) ----

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        Transform sp = RespawnManager.GetSpawnPoint(player.PlayerId);
        NetworkObject bike = runner.Spawn(
            playerBikePrefab, sp.position, sp.rotation, inputAuthority: player);
        _spawnedBikes[player] = bike;
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer) return;

        if (_spawnedBikes.TryGetValue(player, out NetworkObject bike))
        {
            runner.Despawn(bike);
            _spawnedBikes.Remove(player);
        }
    }

    // ---- Input pump: hardware -> network ----

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        if (HardwareInputCollector.Instance != null)
            input.Set(HardwareInputCollector.Instance.Collect());
    }

    // ---- Unused callbacks (required by the interface) ----

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}
