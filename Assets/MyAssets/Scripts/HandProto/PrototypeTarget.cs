using UnityEngine;

// Greybox shooting target: flashes on hit, dies after a few hits, respawns.
// Exists so the aiming loop has something to feel good against.
public class PrototypeTarget : MonoBehaviour
{
    [SerializeField] private int hitsToKill = 3;
    [SerializeField] private float respawnDelay = 2f;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private float flashDuration = 0.15f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Renderer _renderer;
    // The flash goes through a property block on the shared material (GC-10);
    // clearing the block restores the material's own color.
    private MaterialPropertyBlock _block;
    private int _hits;
    private float _flashTimer;

    private void Awake()
    {
        _renderer = GetComponentInChildren<Renderer>();
    }

    private void Update()
    {
        if (_flashTimer > 0f && _renderer != null)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
                _renderer.SetPropertyBlock(null);
        }
    }

    public void OnHit()
    {
        _hits++;

        if (_renderer != null)
        {
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, flashColor);
            _block.SetColor(ColorId, flashColor);
            _renderer.SetPropertyBlock(_block);
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
        if (_renderer != null) _renderer.SetPropertyBlock(null);
    }
}
