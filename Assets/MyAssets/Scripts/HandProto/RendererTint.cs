using UnityEngine;

// Recolors a renderer through a MaterialPropertyBlock (no material clone, SP-2)
// and restores the material's own color by clearing the block. Sets both
// _BaseColor (URP) and _Color (built-in) like HeroHealth's hit flash.
public static class RendererTint
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static MaterialPropertyBlock s_block;

    public static void Set(Renderer renderer, bool tinted, Color color)
    {
        if (renderer == null) return;
        if (!tinted)
        {
            renderer.SetPropertyBlock(null);
            return;
        }
        s_block ??= new MaterialPropertyBlock();
        s_block.Clear();
        s_block.SetColor(BaseColorId, color);
        s_block.SetColor(ColorId, color);
        renderer.SetPropertyBlock(s_block);
    }
}
