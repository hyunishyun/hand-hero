using HandHero.Core;
using UnityEngine;

// Island colour themes (round 5, T3 / D5): floor, back wall and terrain piece
// colours through MaterialPropertyBlocks (RendererTint, no material clones) and
// the directional light's colour and intensity. ArenaLayoutApplier picks one per
// island from the run seed (ArenaThemes.ForIsland) and calls RestoreDefault when
// the run ends, so Quick Match, the tutorial and the menu keep today's look.
// Placeholder colours until the art pass.
public class ArenaThemeApplier : MonoBehaviour
{
    [Tooltip("Floor renderers (the arena floor)")]
    [SerializeField] private Renderer[] floor;
    [Tooltip("Wall renderers (the back wall)")]
    [SerializeField] private Renderer[] walls;
    [Tooltip("Terrain piece renderers: today's pillars and every pooled low wall, platform and thin pillar")]
    [SerializeField] private Renderer[] pieces;
    [Tooltip("The scene's directional light; its colour and intensity at load are today's look")]
    [SerializeField] private Light sun;
    [Tooltip("Island themes, one per island from the run seed (consecutive islands never repeat); empty = today's look")]
    [SerializeField] private ArenaTheme[] themes = ArenaThemes.Defaults();

    private Color _defaultLightColor = Color.white;
    private float _defaultLightIntensity = 1f;
    private bool _themed;

    public int ThemeCount => themes != null ? themes.Length : 0;

    private void Awake()
    {
        if (sun == null) return;
        _defaultLightColor = sun.color;
        _defaultLightIntensity = sun.intensity;
    }

    // Applies the island's theme and returns its name for the run log ("" = none).
    public string ApplyForIsland(int runSeed, int island)
    {
        int index = ArenaThemes.ForIsland(runSeed, island, ThemeCount);
        if (index < 0) return "";
        ArenaTheme theme = themes[index];
        Tint(floor, true, theme.Floor);
        Tint(walls, true, theme.Wall);
        Tint(pieces, true, theme.Pieces);
        if (sun != null)
        {
            sun.color = theme.LightColor;
            sun.intensity = theme.LightIntensity;
        }
        _themed = true;
        return theme.Name ?? "";
    }

    // Today's look again: the materials' own colours and the light as loaded.
    public void RestoreDefault()
    {
        if (!_themed) return;
        Tint(floor, false, default);
        Tint(walls, false, default);
        Tint(pieces, false, default);
        if (sun != null)
        {
            sun.color = _defaultLightColor;
            sun.intensity = _defaultLightIntensity;
        }
        _themed = false;
    }

    private static void Tint(Renderer[] renderers, bool tinted, Color color)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++) RendererTint.Set(renderers[i], tinted, color);
    }
}
