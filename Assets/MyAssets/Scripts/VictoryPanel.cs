using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class VictoryPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject victoryCanvas;
    [SerializeField] private TextMeshProUGUI victoryMessage;

    [Header("Stats (Optional)")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("Scene Names")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private bool isLoading = false;

    private void Start()
    {
        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(false);
        }

        WaveManager waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.OnAllWavesComplete.AddListener(ShowVictory);
        }
    }

    private void OnDestroy()
    {
        WaveManager waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.OnAllWavesComplete.RemoveListener(ShowVictory);
        }
    }

    public void ShowVictory()
    {
        if (victoryCanvas != null)
        {
            victoryCanvas.SetActive(true);
            UpdateStats();
            Debug.Log("Victory!");
        }
    }

    private void UpdateStats()
    {
        if (statsText != null)
        {
            statsText.text = "Total Time: 05:32\nMonsters Killed: 45";
        }
    }

    public void RestartGame()
    {
        if (isLoading) return;

        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"Restarting: {currentScene}");
        StartCoroutine(LoadSceneWithDelay(currentScene));
    }

    public void LoadMainMenu()
    {
        if (isLoading) return;

        Debug.Log($"Loading Main Menu: {lobbySceneName}");
        StartCoroutine(LoadSceneWithDelay(lobbySceneName));
    }

    private IEnumerator LoadSceneWithDelay(string sceneName)
    {
        isLoading = true;

        // Time scale 리셋
        Time.timeScale = 1f;

        // Input 정리 대기 (중요!)
        yield return new WaitForSecondsRealtime(0.2f);

        // Scene 로드
        SceneManager.LoadScene(sceneName);
    }
}
