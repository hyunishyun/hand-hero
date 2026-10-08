using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject gameOverCanvas;

    [Header("Scene Names")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private bool isLoading = false;

    private void Start()
    {
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(false);
        }

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.OnDeath.AddListener(ShowGameOver);
        }
        else
        {
            Debug.LogError("PlayerHealth not found!");
        }
    }

    private void OnDestroy()
    {
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.OnDeath.RemoveListener(ShowGameOver);
        }
    }

    public void ShowGameOver()
    {
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
            Debug.Log("Game Over!");
        }
    }

    public void RestartGame()
    {
        if (isLoading) return;

        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"Restarting current scene: {currentScene}");
        StartCoroutine(LoadSceneWithDelay(currentScene));
    }

    public void LoadMainMenu()
    {
        if (isLoading) return;

        Debug.Log($"Loading lobby scene: {lobbySceneName}");
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