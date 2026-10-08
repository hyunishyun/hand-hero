using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject hudCanvas;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("UI Text")]
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private TextMeshProUGUI victoryText;

    [Header("Settings")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    private bool isGameOver = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeUI();
    }

    private void InitializeUI()
    {
        if (hudCanvas != null) hudCanvas.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    public void OnPlayerDeath()
    {
        if (isGameOver) return;

        isGameOver = true;
        ShowGameOver();
    }

    public void OnVictory()
    {
        if (isGameOver) return;

        isGameOver = true;
        ShowVictory();
    }

    private void ShowGameOver()
    {
        if (hudCanvas != null)
        {
            hudCanvas.SetActive(false);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }

    private void ShowVictory()
    {
        if (hudCanvas != null)
        {
            hudCanvas.SetActive(false);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ReturnToLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogError("Lobby scene name not set!");
            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
