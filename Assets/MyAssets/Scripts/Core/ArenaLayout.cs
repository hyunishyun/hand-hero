using System;
using UnityEngine;

namespace HandHero.Core
{
    // Fairness rules for a seeded arena layout (round 4, S7 / D8). Every position
    // is arena-local (the arena root's space), so the tabletop scale just works.
    [Serializable]
    public struct ArenaLayoutParams
    {
        [Tooltip("Pieces keep at least this far inside the arena walls (m, arena-local)")]
        public float Margin;
        [Tooltip("Floor-plan gap between two pieces, edge to edge (each piece counts as a circle of half its diagonal)")]
        public float MinPieceGap;
        [Tooltip("Floor-plan clear radius around the player start: no piece edge comes closer")]
        public float StartClearRadius;
        [Tooltip("Floor-plan clear radius around every bot spawn point: no piece edge comes closer")]
        public float SpawnClearRadius;
        [Tooltip("Smallest distance between two bot spawn points")]
        public float MinSpawnGap;
        [Tooltip("Where bot spawn points may go (arena-local box, the far side from the player)")]
        public Bounds SpawnArea;
        [Tooltip("Piece centers stay at least this far past the player start along +z, so no piece stands between the seat and the player hero")]
        public float MinPieceForward;
        [Tooltip("Gap kept between a piece and every keep-clear box (the greybox targets)")]
        public float KeepClearPadding;
        [Tooltip("Whole-layout tries before the fallback layout is used")]
        public int Attempts;

        public static ArenaLayoutParams Default => new ArenaLayoutParams
        {
            Margin = 1.5f,
            MinPieceGap = 3f,
            StartClearRadius = 5f,
            SpawnClearRadius = 3f,
            MinSpawnGap = 5f,
            // Today's spawn points are (6, 3, 12), (-7, 5, 12) and (1, 7, 15).
            SpawnArea = new Bounds(new Vector3(0f, 5f, 13f), new Vector3(20f, 6f, 6f)),
            MinPieceForward = 2f,
            KeepClearPadding = 0.5f,
            Attempts = 200,
        };
    }

    public struct ArenaLayoutResult
    {
        // Piece centers (on the floor), same order as the piece sizes.
        public Vector3[] Pieces;
        public Vector3[] SpawnPoints;
        // True when no layout met the rules and the fallback (or the last try) came back.
        public bool UsedFallback;
    }

    // Seeded random placement of the current terrain pieces (the pillars) and the
    // run bot spawn points. Same inputs and seed, same layout.
    public static class ArenaLayout
    {
        private const int SamplesPerItem = 30;

        // Floor-plan circle that holds the piece's footprint: half its xz diagonal.
        public static float Radius(Vector3 size) => 0.5f * Mathf.Sqrt(size.x * size.x + size.z * size.z);

        // One layout seed per run and island, independent of every other run stream.
        public static int IslandSeed(int runSeed, int island)
        {
            unchecked
            {
                ulong z = ((ulong)(uint)runSeed << 32) | (uint)island;
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (int)(uint)z;
            }
        }

        public static ArenaLayoutResult Generate(Vector3 arenaSize, Vector3 start, Vector3[] pieceSizes, int spawnCount,
            ArenaLayoutParams p, int seed, Vector3[] fallbackPieces = null, Vector3[] fallbackSpawns = null,
            Vector3[] keepClearCenters = null, Vector3[] keepClearSizes = null)
        {
            int pieceCount = pieceSizes != null ? pieceSizes.Length : 0;
            spawnCount = Math.Max(0, spawnCount);
            var pieces = new Vector3[pieceCount];
            var spawns = new Vector3[spawnCount];
            var rng = new System.Random(seed);
            int attempts = Math.Max(1, p.Attempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                if (!PlacePieces(rng, arenaSize, start, pieceSizes, p, pieces, keepClearCenters, keepClearSizes)) continue;
                if (!PlaceSpawns(rng, pieceSizes, p, pieces, spawns)) continue;
                if (!HasOpeningSightLine(start, spawns, pieces, pieceSizes)) continue;
                return new ArenaLayoutResult { Pieces = pieces, SpawnPoints = spawns, UsedFallback = false };
            }

            return new ArenaLayoutResult
            {
                Pieces = fallbackPieces != null ? (Vector3[])fallbackPieces.Clone() : pieces,
                SpawnPoints = fallbackSpawns != null ? (Vector3[])fallbackSpawns.Clone() : spawns,
                UsedFallback = true,
            };
        }

        // True when the segment a-b passes through any piece's box (center, size).
        public static bool SegmentBlocked(Vector3 a, Vector3 b, Vector3[] centers, Vector3[] sizes)
        {
            if (centers == null || sizes == null) return false;
            int n = Math.Min(centers.Length, sizes.Length);
            for (int i = 0; i < n; i++)
                if (SegmentHitsBox(a, b, centers[i] - sizes[i] * 0.5f, centers[i] + sizes[i] * 0.5f)) return true;
            return false;
        }

        private static bool PlacePieces(System.Random rng, Vector3 arenaSize, Vector3 start, Vector3[] sizes,
            ArenaLayoutParams p, Vector3[] pieces, Vector3[] keepCenters, Vector3[] keepSizes)
        {
            float floor = -arenaSize.y * 0.5f;
            for (int i = 0; i < pieces.Length; i++)
            {
                Vector3 size = sizes[i];
                float maxX = arenaSize.x * 0.5f - p.Margin - size.x * 0.5f;
                float maxZ = arenaSize.z * 0.5f - p.Margin - size.z * 0.5f;
                float minZ = Mathf.Max(-maxZ, start.z + p.MinPieceForward);
                if (maxX < 0f || minZ > maxZ) return false;
                float radius = Radius(size);

                bool placed = false;
                for (int s = 0; s < SamplesPerItem && !placed; s++)
                {
                    var c = new Vector3(Range(rng, -maxX, maxX), floor + size.y * 0.5f, Range(rng, minZ, maxZ));
                    if (Flat(c, start) - radius < p.StartClearRadius) continue;
                    bool ok = true;
                    for (int j = 0; j < i && ok; j++)
                        ok = Flat(c, pieces[j]) - radius - Radius(sizes[j]) >= p.MinPieceGap;
                    if (ok && keepCenters != null && keepSizes != null)
                    {
                        var box = new Bounds(c, size);
                        int n = Math.Min(keepCenters.Length, keepSizes.Length);
                        for (int k = 0; k < n && ok; k++)
                            ok = !box.Intersects(new Bounds(keepCenters[k],
                                keepSizes[k] + Vector3.one * (2f * p.KeepClearPadding)));
                    }
                    if (!ok) continue;
                    pieces[i] = c;
                    placed = true;
                }
                if (!placed) return false;
            }
            return true;
        }

        private static bool PlaceSpawns(System.Random rng, Vector3[] sizes, ArenaLayoutParams p, Vector3[] pieces,
            Vector3[] spawns)
        {
            Vector3 min = p.SpawnArea.min;
            Vector3 max = p.SpawnArea.max;
            for (int s = 0; s < spawns.Length; s++)
            {
                bool placed = false;
                for (int t = 0; t < SamplesPerItem && !placed; t++)
                {
                    var c = new Vector3(Range(rng, min.x, max.x), Range(rng, min.y, max.y), Range(rng, min.z, max.z));
                    bool ok = true;
                    for (int i = 0; i < pieces.Length && ok; i++)
                        ok = Flat(c, pieces[i]) - Radius(sizes[i]) >= p.SpawnClearRadius;
                    for (int j = 0; j < s && ok; j++)
                        ok = Vector3.Distance(c, spawns[j]) >= p.MinSpawnGap;
                    if (!ok) continue;
                    spawns[s] = c;
                    placed = true;
                }
                if (!placed) return false;
            }
            return true;
        }

        private static bool HasOpeningSightLine(Vector3 start, Vector3[] spawns, Vector3[] pieces, Vector3[] sizes)
        {
            if (spawns.Length == 0) return true;
            for (int s = 0; s < spawns.Length; s++)
                if (!SegmentBlocked(start, spawns[s], pieces, sizes)) return true;
            return false;
        }

        // Slab test: the segment a-b against the box [min, max].
        private static bool SegmentHitsBox(Vector3 a, Vector3 b, Vector3 min, Vector3 max)
        {
            float t0 = 0f, t1 = 1f;
            Vector3 d = b - a;
            for (int axis = 0; axis < 3; axis++)
            {
                float o = a[axis], dir = d[axis];
                if (Mathf.Abs(dir) < 1e-8f)
                {
                    if (o < min[axis] || o > max[axis]) return false;
                    continue;
                }
                float inv = 1f / dir;
                float tNear = (min[axis] - o) * inv;
                float tFar = (max[axis] - o) * inv;
                if (tNear > tFar) (tNear, tFar) = (tFar, tNear);
                if (tNear > t0) t0 = tNear;
                if (tFar < t1) t1 = tFar;
                if (t0 > t1) return false;
            }
            return true;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static float Range(System.Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);
    }
}
