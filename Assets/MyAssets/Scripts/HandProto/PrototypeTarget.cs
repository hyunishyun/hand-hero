using UnityEngine;

// Greybox shooting target: flashes on hit, dies after a few hits, respawns.
// Exists so the aiming loop has something to feel good against.
public class PrototypeTarget : MonoBehaviour
{
    [SerializeField] private int hitsToKill = 3;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.15f;

    private Renderer _renderer;
    private Color _baseColor;
    private int _hits;
    private float _flashTimer;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null) _baseColor = _renderer.material.color;
    }

    private void Update()
    {
        if (_flashTimer > 0f && _renderer != null)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
                _renderer.material.color = _baseColor;
        }
    }

    public void OnHit()
    {
        _hits++;

        if (_renderer != null)
        {
            _renderer.material.color = flashColor;
            _flashTimer = flashDuration;
        }

        if (_hits >= hitsToKill)
        {
            _hits = 0;
            gameObject.SetActive(false);
            Invoke(nameof(Respawn), respawnDelay);
        }
    }

    private void Respawn()
    {
        gameObject.SetActive(true);
        if (_renderer != null) _renderer.material.color = _baseColor;
    }
}
