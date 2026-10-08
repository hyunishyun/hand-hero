using UnityEngine;

// Pure presentation. Subscribes to NetworkedGunController.Fired and replays
// the effects from the original GunController (muzzle flash, trail, audio,
// hit effect). Runs identically on every client, no networking code here.
[RequireComponent(typeof(NetworkedGunController))]
public class GunPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform gunBarrel; // muzzle on THIS bike's avatar/gun

    [Header("Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Bullet Trail")]
    [SerializeField] private LineRenderer bulletTrail;
    [SerializeField] private float trailDuration = 0.1f;
    [SerializeField] private Color trailColor = Color.yellow;
    [SerializeField] private float trailWidth = 0.02f;

    [Header("Audio")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioSource audioSource;

    private NetworkedGunController gun;
    private float trailTimer;

    private void Awake()
    {
        gun = GetComponent<NetworkedGunController>();

        if (bulletTrail != null)
        {
            bulletTrail.startColor = trailColor;
            bulletTrail.endColor = trailColor;
            bulletTrail.startWidth = trailWidth;
            bulletTrail.endWidth = trailWidth;
            bulletTrail.enabled = false;
            bulletTrail.useWorldSpace = true; // origin/end arrive in world space
        }
    }

    private void OnEnable()
    {
        gun.Fired += OnFired;
    }

    private void OnDisable()
    {
        gun.Fired -= OnFired;
    }

    private void Update()
    {
        if (trailTimer > 0f)
        {
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f && bulletTrail != null)
                bulletTrail.enabled = false;
        }
    }

    private void OnFired(Vector3 origin, Vector3 end, bool hitPlayer)
    {
        // Muzzle flash
        if (muzzleFlashPrefab != null && gunBarrel != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, gunBarrel);
            flash.transform.localPosition = Vector3.zero;
            flash.transform.localRotation = Quaternion.identity;
            Destroy(flash, 0.15f);
        }

        // Fire sound (random pitch, same as original)
        if (audioSource != null && fireSound != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.4f);
            audioSource.PlayOneShot(fireSound);
        }

        // Hit effect at impact point
        if (hitEffectPrefab != null)
        {
            Vector3 normal = (origin - end).normalized;
            GameObject effect = Instantiate(hitEffectPrefab, end, Quaternion.LookRotation(normal));
            Destroy(effect, 1f);
        }

        // Bullet trail
        if (bulletTrail != null)
        {
            bulletTrail.enabled = true;
            bulletTrail.SetPosition(0, origin);
            bulletTrail.SetPosition(1, end);
            trailTimer = trailDuration;
        }
    }
}
