using System;
using UnityEngine;

namespace HandHero.Core
{
    // Extra terrain pieces on one island (round 5, T3 / D5), on top of today's two pillars.
    [Serializable]
    public struct ArenaPieceCounts
    {
        [Tooltip("Low walls (about 6 x 3 x 1 m) standing on the floor")]
        public int LowWalls;
        [Tooltip("Floating platforms (about 4 x 0.6 x 4 m) in the air")]
        public int Platforms;
        [Tooltip("Thin pillars (about 1 x 14 x 1 m) standing on the floor")]
        public int ThinPillars;

        public ArenaPieceCounts(int lowWalls, int platforms, int thinPillars)
        {
            LowWalls = lowWalls;
            Platforms = platforms;
            ThinPillars = thinPillars;
        }

        public int Total => LowWalls + Platforms + ThinPillars;
    }

    // One row of the island-depth table: on islands from FromIsland up to the next
    // row, each kind's count is drawn between Min and Max (inclusive).
    [Serializable]
    public struct ArenaPieceTier
    {
        [Tooltip("First island (1-based) of this row; the row with the highest From Island at or below the island applies")]
        public int FromIsland;
        [Tooltip("Fewest extra pieces of each kind on these islands")]
        public ArenaPieceCounts Min;
        [Tooltip("Most extra pieces of each kind on these islands (inclusive)")]
        public ArenaPieceCounts Max;

        public ArenaPieceTier(int fromIsland, ArenaPieceCounts min, ArenaPieceCounts max)
        {
            FromIsland = fromIsland;
            Min = min;
            Max = max;
        }
    }

    // Terrain stage 2 (round 5, T3 / D5): which extra pieces an island gets.
    // Deeper islands get more cover; the counts come from the island's layout
    // seed, so a run replays the same arenas. The scene builder makes a small pool
    // of each kind (all disabled); ArenaLayoutApplier turns on the first N of each
    // pool and places them with ArenaLayout. Quick Match and the tutorial use none.
    public static class ArenaPieceTable
    {
        // Pieces of each kind the scene builder makes; the default table never asks for more.
        public const int DefaultPoolSize = 2;

        public static readonly Vector3 LowWallSize = new Vector3(6f, 3f, 1f);
        public static readonly Vector3 PlatformSize = new Vector3(4f, 0.6f, 4f);
        public static readonly Vector3 ThinPillarSize = new Vector3(1f, 14f, 1f);

        // First guesses (round 5): island 1 at most one of each, the boss island calmer than 7-8.
        public static ArenaPieceTier[] Defaults() => new[]
        {
            new ArenaPieceTier(1, new ArenaPieceCounts(0, 0, 0), new ArenaPieceCounts(1, 1, 1)),
            new ArenaPieceTier(3, new ArenaPieceCounts(1, 0, 0), new ArenaPieceCounts(1, 1, 1)),
            new ArenaPieceTier(5, new ArenaPieceCounts(1, 1, 1), new ArenaPieceCounts(2, 1, 2)),
            new ArenaPieceTier(7, new ArenaPieceCounts(1, 1, 1), new ArenaPieceCounts(2, 2, 2)),
            new ArenaPieceTier(RunRules.BossIsland, new ArenaPieceCounts(1, 1, 1), new ArenaPieceCounts(1, 1, 1)),
        };

        // The row with the highest FromIsland at or below the island (table order does
        // not matter); an all-zero row when none applies.
        public static ArenaPieceTier Row(ArenaPieceTier[] table, int island)
        {
            var best = new ArenaPieceTier(int.MinValue, default, default);
            bool found = false;
            if (table != null)
            {
                for (int i = 0; i < table.Length; i++)
                {
                    if (table[i].FromIsland > island || table[i].FromIsland < best.FromIsland) continue;
                    best = table[i];
                    found = true;
                }
            }
            return found ? best : default;
        }

        // Seeded counts for this island, never more than the pool holds. `layoutSeed`
        // is the island's layout seed (ArenaLayout.IslandSeed); the counts use their
        // own stream from it, so they do not shift the piece positions' draws.
        public static ArenaPieceCounts Roll(ArenaPieceTier[] table, int island, int layoutSeed, ArenaPieceCounts pool)
        {
            ArenaPieceTier row = Row(table, island);
            var rng = new System.Random(ArenaLayout.IslandSeed(layoutSeed, CountStream));
            // One draw per kind, always in this order: the stream stays the same when a pool is empty.
            int walls = Pick(rng, row.Min.LowWalls, row.Max.LowWalls, pool.LowWalls);
            int platforms = Pick(rng, row.Min.Platforms, row.Max.Platforms, pool.Platforms);
            int thin = Pick(rng, row.Min.ThinPillars, row.Max.ThinPillars, pool.ThinPillars);
            return new ArenaPieceCounts(walls, platforms, thin);
        }

        // The pieces one island uses, in placement order: the fixed pieces (today's
        // pillars), then the first LowWalls / Platforms / ThinPillars of each pool
        // (counts above a pool's size are cut). Only the platforms float.
        public static void Compose(Vector3[] fixedSizes, Vector3[] wallSizes, Vector3[] platformSizes,
            Vector3[] thinSizes, ArenaPieceCounts counts, out Vector3[] sizes, out bool[] floating)
        {
            int fixedCount = fixedSizes != null ? fixedSizes.Length : 0;
            int walls = Clamp(counts.LowWalls, wallSizes);
            int platforms = Clamp(counts.Platforms, platformSizes);
            int thin = Clamp(counts.ThinPillars, thinSizes);
            sizes = new Vector3[fixedCount + walls + platforms + thin];
            floating = new bool[sizes.Length];

            int next = 0;
            for (int i = 0; i < fixedCount; i++) sizes[next++] = fixedSizes[i];
            for (int i = 0; i < walls; i++) sizes[next++] = wallSizes[i];
            for (int i = 0; i < platforms; i++)
            {
                floating[next] = true;
                sizes[next++] = platformSizes[i];
            }
            for (int i = 0; i < thin; i++) sizes[next++] = thinSizes[i];
        }

        // Any value other than the island numbers (1-9) the layout seeds use.
        private const int CountStream = -2;

        private static int Pick(System.Random rng, int min, int max, int cap)
        {
            min = Math.Max(0, min);
            max = Math.Max(min, max);
            int n = rng.Next(min, max + 1);
            return Math.Min(n, Math.Max(0, cap));
        }

        private static int Clamp(int count, Vector3[] pool) =>
            Math.Max(0, Math.Min(count, pool != null ? pool.Length : 0));
    }
}
