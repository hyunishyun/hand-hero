using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 1f;

    private bool isFading = false;
    private AudioClip nextTrack;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        if (isFading && musicSource != null)
        {
            musicSource.volume -= fadeSpeed * Time.unscaledDeltaTime;

            if (musicSource.volume <= 0)
            {
                musicSource.clip = nextTrack;
                musicSource.Play();
                isFading = false;

                StartCoroutine(FadeIn());
            }
        }
    }

    public void ChangeMusic(AudioClip newTrack)
    {
        if (musicSource == null || newTrack == null) return;

        if (musicSource.clip == newTrack) return;

        nextTrack = newTrack;
        isFading = true;
    }

    private System.Collections.IEnumerator FadeIn()
    {
        while (musicSource.volume < 1f)
        {
            musicSource.volume += fadeSpeed * Time.unscaledDeltaTime;
            yield return null;
        }

        musicSource.volume = 1f;
    }
}
