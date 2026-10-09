using HandHero.Core;
using UnityEngine;

// Damage-taken flash (P12, D15): a red edge vignette on a quad parented to the
// camera, so the XR Origin never moves. Alpha <= 0.35 and <= 0.25 s are enforced
// in Core FlashEnvelope; the comfort toggle turns it off entirely. The edge
// texture is generated once at startup (no asset).
public class DamageVignette : MonoBehaviour
{
    [Tooltip("The player's health; every hit taken flashes the edges")]
    [SerializeField] private HeroHealth playerHealth;
    [Tooltip("The quad in front of the camera (transparent unlit material)")]
    [SerializeField] private Renderer quad;

    [Header("Look")]
    [Tooltip("Comfort: off = no red flash at all")]
    [SerializeField] private bool flashOnDamage = true;
    [Tooltip("Edge color (alpha comes from Peak Alpha)")]
    [SerializeField] private Color color = new Color(1f, 0.08f, 0.05f);
    [Tooltip("Alpha at the start of the flash (capped at 0.35)")]
    [Range(0f, 0.35f)] [SerializeField] private float peakAlpha = 0.3f;
    [Tooltip("Flash length in seconds (capped at 0.25)")]
    [Range(0.05f, 0.25f)] [SerializeField] private float duration = 0.2f;
    [Tooltip("Share of the view from the center that stays clear (0..1)")]
    [Range(0f, 0.9f)] [SerializeField] private float clearCenter = 0.5f;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private const int TextureSize = 64;

    private readonly FlashEnvelope _flash = new FlashEnvelope();
    private MaterialPropertyBlock _block;
    private Texture2D _texture;
    private float _shownAlpha = -1f;

    public bool FlashOnDamage
    {
        get => flashOnDamage;
        set
        {
            flashOnDamage = value;
            if (!value) _flash.Cancel();
        }
    }

    private void Awake()
    {
        _texture = EdgeTexture(clearCenter);
        _block = new MaterialPropertyBlock();
        if (quad != null) quad.enabled = false;
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Damaged -= OnDamaged;
        _flash.Cancel();
        Show(0f);
    }

    private void OnDestroy()
    {
        if (_texture != null) Destroy(_texture);
    }

    private void OnDamaged()
    {
        if (flashOnDamage) _flash.Trigger();
    }

    private void LateUpdate()
    {
        if (!_flash.IsActive && _shownAlpha == 0f) return;
        Show(_flash.Step(Time.unscaledDeltaTime, peakAlpha, duration));
    }

    // GPU warmup (round 4, S1): draws the quad at an invisible alpha. Called after
    // this LateUpdate (RenderWarmup runs late), which hides it again next frame
    // unless the warmup calls once more; ShowWarmup(false) hides it for good.
    public void ShowWarmup(bool on)
    {
        if (on && _flash.IsActive) return; // a real flash is showing
        Show(on ? WarmupAlpha : 0f);
    }

    private const float WarmupAlpha = 1f / 255f;

    // Writes the renderer only when the alpha changes; hidden at 0 (no overdraw).
    private void Show(float alpha)
    {
        if (alpha == _shownAlpha || quad == null) return;
        _shownAlpha = alpha;
        bool on = alpha > 0f;
        if (quad.enabled != on) quad.enabled = on;
        if (!on) return;
        Color c = color;
        c.a = alpha;
        _block.SetTexture(BaseMapId, _texture);
        _block.SetColor(BaseColorId, c);
        quad.SetPropertyBlock(_block);
    }

    // White, transparent in the middle, opaque toward the edges (smoothstep).
    private static Texture2D EdgeTexture(float clear)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            name = "DamageVignette",
        };
        var pixels = new Color32[TextureSize * TextureSize];
        float half = (TextureSize - 1) * 0.5f;
        for (int y = 0; y < TextureSize; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            float dx = (x - half) / half;
            float dy = (y - half) / half;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f) * 1.25f;
            float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(clear, 1f, r));
            pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
