using UnityEngine;

public class MusicBox : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private AudioClip musicTrack;
    [SerializeField] private float minimumSpeedToBreak = 10f;

    [Header("Effects")]
    [SerializeField] private GameObject destroyEffectPrefab;
    [SerializeField] private AudioClip breakSound;

    private bool isDestroyed = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (isDestroyed) return;

        float collisionSpeed = collision.relativeVelocity.magnitude;

        if (collisionSpeed >= minimumSpeedToBreak)
        {
            Break();
        }
    }

    private void Break()
    {
        isDestroyed = true;

        if (musicTrack != null)
        {
            AudioManager audioManager = FindObjectOfType<AudioManager>();
            if (audioManager != null)
            {
                audioManager.ChangeMusic(musicTrack);
            }
        }

        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }

        if (breakSound != null)
        {
            AudioSource.PlayClipAtPoint(breakSound, transform.position);
        }

        Destroy(gameObject);
    }
}