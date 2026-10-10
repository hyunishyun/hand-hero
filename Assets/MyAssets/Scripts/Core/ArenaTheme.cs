using System;
using UnityEngine;

namespace HandHero.Core
{
    // One island colour theme (round 5, T3 / D5): greybox colours and the sun, no
    // art yet. Applied through MaterialPropertyBlocks, so no material is cloned.
    [Serializable]
    public struct ArenaTheme
    {
        [Tooltip("Name in the run log")]
        public string Name;
        [Tooltip("Arena floor colour")]
        public Color Floor;
        [Tooltip("Back wall colour")]
        public Color Wall;
        [Tooltip("Terrain piece colour: pillars, low walls, platforms, thin pillars")]
        public Color Pieces;
        [Tooltip("Directional light colour")]
        public Color LightColor;
        [Tooltip("Directional light intensity (today's look: 1.2)")]
        public float LightIntensity;
    }

    // The default themes and which one an island gets.
    public static class ArenaThemes
    {
        // Low-saturation surfaces and a near-white sun, so the saturated heroes stay
        // the brightest colours in view: the blue player, the red / violet / magenta
        // bots, the orange targets and the CURSOR marker. Placeholders until the art pass.
        public static ArenaTheme[] Defaults() => new[]
        {
            new ArenaTheme
            {
                Name = "Dusk",
                Floor = new Color(0.24f, 0.26f, 0.32f),
                Wall = new Color(0.36f, 0.38f, 0.46f),
                Pieces = new Color(0.47f, 0.48f, 0.56f),
                LightColor = new Color(1f, 0.87f, 0.76f),
                LightIntensity = 1.05f,
            },
            new ArenaTheme
            {
                Name = "Frost",
                Floor = new Color(0.62f, 0.68f, 0.72f),
                Wall = new Color(0.72f, 0.78f, 0.83f),
                Pieces = new Color(0.82f, 0.88f, 0.92f),
                LightColor = new Color(0.86f, 0.93f, 1f),
                LightIntensity = 1.15f,
            },
            new ArenaTheme
            {
                Name = "Ember",
                Floor = new Color(0.2f, 0.18f, 0.17f),
                Wall = new Color(0.3f, 0.26f, 0.24f),
                Pieces = new Color(0.39f, 0.33f, 0.3f),
                LightColor = new Color(1f, 0.84f, 0.68f),
                LightIntensity = 1.1f,
            },
        };

        // Theme index for this island: a seeded order of all themes, repeated, so
        // consecutive islands never share a theme and every theme shows once in
        // each run of `themeCount` islands. -1 when there are no themes.
        public static int ForIsland(int runSeed, int island, int themeCount)
        {
            if (themeCount <= 0) return -1;
            if (themeCount == 1) return 0;

            // Fisher-Yates over 0..count-1 from the run's own theme stream.
            var rng = new System.Random(ArenaLayout.IslandSeed(runSeed, ThemeStream));
            var order = new int[themeCount];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            int slot = ((island - 1) % themeCount + themeCount) % themeCount;
            return order[slot];
        }

        // Any value other than the island numbers (1-9) the layout seeds use.
        private const int ThemeStream = -1;
    }

    // What ArenaLayoutApplier put on one island (round 5, T3), for the run log.
    public struct ArenaIslandTerrain
    {
        public int LayoutSeed;
        // True when no layout met the rules: today's two pillars and spawn points, no extra pieces.
        public bool UsedFallback;
        // "" = today's look (no theme applied).
        public string Theme;
        // Extra pieces placed (all 0 on a fallback).
        public ArenaPieceCounts Pieces;
    }
}
