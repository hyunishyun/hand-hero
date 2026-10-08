using UnityEngine;
using TMPro;
using System.Collections;

public class WaveAnnouncement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject announcementPanel;
    [SerializeField] private TextMeshProUGUI waveTitleText;
    [SerializeField] private TextMeshProUGUI waveSubtitleText;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float fadeSpeed = 2f;

    [Header("Messages")]
    [SerializeField] private string waveStartMessage = "INCOMING!";
    [SerializeField] private string finalWaveMessage = "FINAL WAVE!";
    [SerializeField] private string nextWaveMessage = "Get Ready!";

    private CanvasGroup canvasGroup;
    private WaveManager waveManager;

    private void Start()
    {
        // CanvasGroup 추가 (Fade용)
        if (announcementPanel != null)
        {
            canvasGroup = announcementPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = announcementPanel.AddComponent<CanvasGroup>();
            }

            announcementPanel.SetActive(false);
        }

        // WaveManager 찾기
        waveManager = FindFirstObjectByType<WaveManager>();
        if (waveManager != null)
        {
            // WaveManager 이벤트 구독 (나중에 추가)
        }
    }

    public void ShowWaveStart(int waveNumber, int totalWaves)
    {
        if (announcementPanel == null) return;

        // 텍스트 설정
        if (waveTitleText != null)
        {
            waveTitleText.text = $"WAVE {waveNumber}";
        }

        if (waveSubtitleText != null)
        {
            if (waveNumber == totalWaves)
            {
                waveSubtitleText.text = finalWaveMessage;
            }
            else
            {
                waveSubtitleText.text = waveStartMessage;
            }
        }

        // 표시
        StopAllCoroutines();
        StartCoroutine(ShowAnnouncementCoroutine());
    }

    public void ShowNextWaveCountdown(float timeRemaining)
    {
        if (announcementPanel == null) return;

        if (waveTitleText != null)
        {
            waveTitleText.text = "NEXT WAVE";
        }

        if (waveSubtitleText != null)
        {
            waveSubtitleText.text = $"in {Mathf.CeilToInt(timeRemaining)}s";
        }

        StopAllCoroutines();
        StartCoroutine(ShowAnnouncementCoroutine());
    }

    private IEnumerator ShowAnnouncementCoroutine()
    {
        announcementPanel.SetActive(true);

        // Fade In
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            while (canvasGroup.alpha < 1f)
            {
                canvasGroup.alpha += Time.deltaTime * fadeSpeed;
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        // 표시 시간
        yield return new WaitForSeconds(displayDuration);

        // Fade Out
        if (canvasGroup != null)
        {
            while (canvasGroup.alpha > 0f)
            {
                canvasGroup.alpha -= Time.deltaTime * fadeSpeed;
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        announcementPanel.SetActive(false);
    }
}