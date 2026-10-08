using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuManager : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "MainLevel";

    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject helpPanel;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip backgroundMusic;
    private AudioSource audioSource;

    private bool isLoading = false;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null && backgroundMusic != null)
        {
            audioSource.clip = backgroundMusic;
            audioSource.loop = true;
            audioSource.volume = 0.3f;
            audioSource.Play();
        }

        // 초기: Title만
        ShowTitle();
    }

    void ShowTitle()
    {
        if (titlePanel) titlePanel.SetActive(true);
        if (settingsPanel) settingsPanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(false);
        if (helpPanel) helpPanel.SetActive(false);
    }

    public void ShowSettings()
    {
        PlaySound();
        if (titlePanel) titlePanel.SetActive(false);
        if (settingsPanel) settingsPanel.SetActive(true);
    }

    public void ShowCredits()
    {
        PlaySound();
        if (titlePanel) titlePanel.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(true);
    }

    public void ShowHelp()
    {
        PlaySound();
        if (titlePanel) titlePanel.SetActive(false);
        if (helpPanel) helpPanel.SetActive(true);
    }

    public void BackToTitle()
    {
        PlaySound();
        ShowTitle();
    }

    public void StartGame()
    {
        if (isLoading) return;
        PlaySound();
        StartCoroutine(LoadGame());
    }

    IEnumerator LoadGame()
    {
        isLoading = true;
        Time.timeScale = 1f;
        yield return new WaitForSecondsRealtime(0.2f);
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        PlaySound();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void PlaySound()
    {
        if (audioSource && buttonClickSound)
            audioSource.PlayOneShot(buttonClickSound);
    }
}