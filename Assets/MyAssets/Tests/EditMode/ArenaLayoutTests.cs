using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Terrain variety, first slice (round 4, S7 / D8): the current pillars and the
    // run bot spawn points get seeded random positions per island, under fairness rules.
    public class ArenaLayoutTests
    {
        // The Arena_Main numbers (HandHeroSceneBuilder): 35 x 20 x 35 arena, player
        // start at the arena origin, two pillars, three run spawn points on the far side.
        private static readonly Vector3 Size = new Vector3(35f, 20f, 35f);
        private static readonly Vector3 Start = Vector3.zero;
        private static readonly Vector3[] Pillars = { new Vector3(2f, 10f, 2f), new Vector3(2f, 12f, 2f) };
        private const int Spawns = 3;

        private static ArenaLayoutParams P => ArenaLayoutParams.Default;

        private static ArenaLayoutResult Make(int seed) =>
            ArenaLayout.Generate(Size, Start, Pillars, Spawns, P, seed);

        [Test]
        public void SameSeed_SameLayout()
        {
            ArenaLayoutResult a = Make(1234);
            ArenaLayoutResult b = Make(1234);
            CollectionAssert.AreEqual(a.Pieces, b.Pieces);
            CollectionAssert.AreEqual(a.SpawnPoints, b.SpawnPoints);
        }

        [Test]
        public void DifferentSeeds_GiveDifferentLayouts()
        {
            int differing = 0;
            ArenaLayoutResult first = Make(1);
            for (int seed = 2; seed < 22; seed++)
                if (Make(seed).Pieces[0] != first.Pieces[0]) differing++;
            Assert.Greater(differing, 15);
        }

        [Test]
        public void ReturnsOnePositionPerPieceAndSpawn()
        {
            ArenaLayoutResult r = Make(7);
            Assert.AreEqual(Pillars.Length, r.Pieces.Length);
            Assert.AreEqual(Spawns, r.SpawnPoints.Length);
            Assert.IsFalse(r.UsedFallback);
        }

        [Test]
        public void PiecesStandOnTheFloor_InsideTheBoundsWithMargin()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed);
                for (int i = 0; i < r.Pieces.Length; i++)
                {
                    Vector3 c = r.Pieces[i];
                    Vector3 s = Pillars[i];
                    Assert.AreEqual(-Size.y * 0.5f + s.y * 0.5f, c.y, 1e-4f, "on the floor");
                    Assert.LessOrEqual(Mathf.Abs(c.x) + s.x * 0.5f, Size.x * 0.5f - P.Margin + 1e-4f, $"x seed {seed}");
                    Assert.LessOrEqual(Mathf.Abs(c.z) + s.z * 0.5f, Size.z * 0.5f - P.Margin + 1e-4f, $"z seed {seed}");
                }
            }
        }

        [Test]
        public void PiecesKeepTheirGap()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed);
                float gap = HorizontalDistance(r.Pieces[0], r.Pieces[1])
                            - ArenaLayout.Radius(Pillars[0]) - ArenaLayout.Radius(Pillars[1]);
                Assert.GreaterOrEqual(gap, P.MinPieceGap - 1e-4f, $"seed {seed}");
            }
        }

        [Test]
        public void StartArea_StaysClear()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed);
                for (int i = 0; i < r.Pieces.Length; i++)
                    Assert.GreaterOrEqual(HorizontalDistance(r.Pieces[i], Start) - ArenaLayout.Radius(Pillars[i]),
                        P.StartClearRadius - 1e-4f, $"seed {seed}");
            }
        }

        [Test]
        public void SpawnPoints_InsideTheSpawnArea_ClearOfPiecesAndApart()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed);
                for (int s = 0; s < r.SpawnPoints.Length; s++)
                {
                    Vector3 sp = r.SpawnPoints[s];
                    Assert.IsTrue(P.SpawnArea.Contains(sp), $"spawn {sp} seed {seed}");
                    for (int i = 0; i < r.Pieces.Length; i++)
                        Assert.GreaterOrEqual(HorizontalDistance(r.Pieces[i], sp) - ArenaLayout.Radius(Pillars[i]),
                            P.SpawnClearRadius - 1e-4f, $"seed {seed}");
                    for (int t = s + 1; t < r.SpawnPoints.Length; t++)
                        Assert.GreaterOrEqual(Vector3.Distance(sp, r.SpawnPoints[t]), P.MinSpawnGap - 1e-4f);
                }
            }
        }

        [Test]
        public void OpeningSightLine_FromStartToAtLeastOneSpawn()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed);
                bool open = false;
                for (int s = 0; s < r.SpawnPoints.Length && !open; s++)
                    open = !ArenaLayout.SegmentBlocked(Start, r.SpawnPoints[s], r.Pieces, Pillars);
                Assert.IsTrue(open, $"seed {seed}");
            }
        }

        [Test]
        public void SegmentBlocked_DetectsAPillarInTheWay()
        {
            Vector3[] centers = { new Vector3(0f, -5f, 6f) };
            Vector3[] sizes = { new Vector3(2f, 10f, 2f) };
            Assert.IsTrue(ArenaLayout.SegmentBlocked(new Vector3(0f, -3f, 0f), new Vector3(0f, -3f, 12f), centers, sizes));
            Assert.IsFalse(ArenaLayout.SegmentBlocked(new Vector3(5f, -3f, 0f), new Vector3(5f, -3f, 12f), centers, sizes));
            // Over the top of the pillar (top at y = 0).
            Assert.IsFalse(ArenaLayout.SegmentBlocked(new Vector3(0f, 2f, 0f), new Vector3(0f, 2f, 12f), centers, sizes));
        }

        [Test]
        public void ImpossibleRules_FallBack_AndSayIt()
        {
            ArenaLayoutParams p = P;
            p.StartClearRadius = 100f; // nothing fits
            Vector3[] fallbackPieces = { new Vector3(-7f, -5f, 4f), new Vector3(9f, -4f, 9f) };
            Vector3[] fallbackSpawns = { new Vector3(6f, 3f, 12f), new Vector3(-7f, 5f, 12f), new Vector3(1f, 7f, 15f) };
            ArenaLayoutResult r = ArenaLayout.Generate(Size, Start, Pillars, Spawns, p, 5, fallbackPieces, fallbackSpawns);
            Assert.IsTrue(r.UsedFallback);
            CollectionAssert.AreEqual(fallbackPieces, r.Pieces);
            CollectionAssert.AreEqual(fallbackSpawns, r.SpawnPoints);
        }

        [Test]
        public void PiecesKeepOffTheKeepClearBoxes()
        {
            // The greybox targets (HandHeroSceneBuilder targetSpots, 2 m cubes) stay where they are.
            Vector3[] boxes = { new Vector3(-8f, 0f, 6f), new Vector3(8f, 3f, 0f), new Vector3(0f, -4f, 11f), new Vector3(-4f, 6f, -5f) };
            Vector3[] boxSizes = { Vector3.one * 2f, Vector3.one * 2f, Vector3.one * 2f, Vector3.one * 2f };
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = ArenaLayout.Generate(Size, Start, Pillars, Spawns, P, seed, null, null, boxes, boxSizes);
                Assert.IsFalse(r.UsedFallback, $"seed {seed}");
                for (int i = 0; i < r.Pieces.Length; i++)
                    for (int b = 0; b < boxes.Length; b++)
                        Assert.IsFalse(new Bounds(r.Pieces[i], Pillars[i]).Intersects(new Bounds(boxes[b], boxSizes[b])),
                            $"piece {i} box {b} seed {seed}");
            }
        }

        [Test]
        public void PiecesStayOnTheFarSideOfThePlayerStart()
        {
            // Never between the seat (arena -z) and the player hero.
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed);
                for (int i = 0; i < r.Pieces.Length; i++)
                    Assert.GreaterOrEqual(r.Pieces[i].z, Start.z + P.MinPieceForward - 1e-4f, $"seed {seed}");
            }
        }

        [Test]
        public void IslandSeed_IsStablePerRunAndIsland_AndDiffersBetweenIslands()
        {
            Assert.AreEqual(ArenaLayout.IslandSeed(99, 3), ArenaLayout.IslandSeed(99, 3));
            Assert.AreNotEqual(ArenaLayout.IslandSeed(99, 3), ArenaLayout.IslandSeed(99, 4));
            Assert.AreNotEqual(ArenaLayout.IslandSeed(99, 3), ArenaLayout.IslandSeed(100, 3));
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
