using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;

namespace HandHero.Tests
{
    // Terrain stage 2 (round 5, T3 / D5): low walls, floating platforms and thin
    // pillars per island depth, placed under the round-4 fairness rules, plus three
    // colour themes. Quick Match and the tutorial keep today's arena.
    public class ArenaTerrainTests
    {
        // The Arena_Main numbers (HandHeroSceneBuilder): 35 x 20 x 35 arena, the
        // player start at the arena origin, today's two pillars, three run spawns
        // and the four greybox targets as keep-clear boxes.
        private static readonly Vector3 Size = new Vector3(35f, 20f, 35f);
        private static readonly Vector3 Start = Vector3.zero;
        private static readonly Vector3[] Pillars = { new Vector3(2f, 10f, 2f), new Vector3(2f, 12f, 2f) };
        private const int Spawns = 3;
        private static readonly Vector3[] Targets =
        {
            new Vector3(-8f, 0f, 6f), new Vector3(8f, 3f, 0f), new Vector3(0f, -4f, 11f), new Vector3(-4f, 6f, -5f),
        };
        private static readonly Vector3[] TargetSizes = { Vector3.one * 2f, Vector3.one * 2f, Vector3.one * 2f, Vector3.one * 2f };

        private static ArenaLayoutParams P => ArenaLayoutParams.Default;
        private static float Floor => -Size.y * 0.5f;
        private static float Ceiling => Size.y * 0.5f;

        // The viewer's eye, arena-local (T3-P3): the VR seat (eye 1.2 m above the
        // origin, arena center at world (0, 2, 20)) and the MR TABLE eye (ViewLayout
        // with TabletopParams.Default). The hand aim ray starts next to the eye too.
        private static readonly Vector3 ArenaCenterWorld = new Vector3(0f, 2f, 20f);
        private static readonly Vector3 SeatEye = new Vector3(0f, 1.2f, 0f) - ArenaCenterWorld;
        private static Vector3 TableEye =>
            ViewLayout.Tabletop(ArenaCenterWorld, Size.x, TabletopParams.Default).EyeWorld - ArenaCenterWorld;

        // The builder's pools: DefaultPoolSize of each kind at the default sizes.
        private static Vector3[] Pool(Vector3 size)
        {
            var pool = new Vector3[ArenaPieceTable.DefaultPoolSize];
            for (int i = 0; i < pool.Length; i++) pool[i] = size;
            return pool;
        }

        private static void Compose(ArenaPieceCounts counts, out Vector3[] sizes, out bool[] floating) =>
            ArenaPieceTable.Compose(Pillars, Pool(ArenaPieceTable.LowWallSize), Pool(ArenaPieceTable.PlatformSize),
                Pool(ArenaPieceTable.ThinPillarSize), counts, out sizes, out floating);

        private static ArenaLayoutResult Make(int seed, Vector3[] sizes, bool[] floating, ArenaLayoutParams p) =>
            ArenaLayout.Generate(Size, Start, sizes, Spawns, p, seed, null, null, Targets, TargetSizes, floating);

        // Every default table row at its largest counts: the busiest arena each row can roll.
        private static IEnumerable<ArenaPieceCounts> BusiestRows()
        {
            foreach (ArenaPieceTier row in ArenaPieceTable.Defaults()) yield return row.Max;
        }

        // ---- ArenaLayout with floating pieces ----

        [Test]
        public void NoFloatingFlags_SameLayoutAsAllOnTheGround()
        {
            // Round 4 callers pass no flags: the draws must stay exactly the old ones.
            Compose(new ArenaPieceCounts(2, 0, 2), out Vector3[] sizes, out bool[] _);
            var allGround = new bool[sizes.Length];
            for (int seed = 0; seed < 50; seed++)
            {
                ArenaLayoutResult a = Make(seed, sizes, null, P);
                ArenaLayoutResult b = Make(seed, sizes, allGround, P);
                CollectionAssert.AreEqual(a.Pieces, b.Pieces, $"seed {seed}");
                CollectionAssert.AreEqual(a.SpawnPoints, b.SpawnPoints, $"seed {seed}");
            }
        }

        [Test]
        public void PillarsOnly_IgnoresTheNewPlatformRules()
        {
            // Today's two pillars with impossible platform settings: nothing floats, nothing changes.
            ArenaLayoutParams odd = P;
            odd.PlatformMinY = 50f;
            odd.PlatformMaxY = 60f;
            odd.FlightLaneClearance = 30f;
            odd.StartLaneHalfHeight = 30f;
            odd.ViewerEyes = new[] { new Vector3(0f, 0f, 16f), new Vector3(0f, -9f, 5f) }; // T3-P3: platforms only
            for (int seed = 0; seed < 50; seed++)
            {
                ArenaLayoutResult a = ArenaLayout.Generate(Size, Start, Pillars, Spawns, P, seed);
                ArenaLayoutResult b = ArenaLayout.Generate(Size, Start, Pillars, Spawns, odd, seed);
                Assert.IsFalse(b.UsedFallback, $"seed {seed}");
                CollectionAssert.AreEqual(a.Pieces, b.Pieces, $"seed {seed}");
                CollectionAssert.AreEqual(a.SpawnPoints, b.SpawnPoints, $"seed {seed}");
            }
        }

        [Test]
        public void MixedLayout_SameSeed_SameLayout()
        {
            Compose(new ArenaPieceCounts(2, 2, 2), out Vector3[] sizes, out bool[] floating);
            ArenaLayoutResult a = Make(4321, sizes, floating, P);
            ArenaLayoutResult b = Make(4321, sizes, floating, P);
            CollectionAssert.AreEqual(a.Pieces, b.Pieces);
            CollectionAssert.AreEqual(a.SpawnPoints, b.SpawnPoints);
        }

        [Test]
        public void EveryDefaultRow_AtItsBusiest_FitsWithoutFallback()
        {
            foreach (ArenaPieceCounts counts in BusiestRows())
            {
                Compose(counts, out Vector3[] sizes, out bool[] floating);
                for (int seed = 0; seed < 200; seed++)
                {
                    ArenaLayoutResult r = Make(seed, sizes, floating, P);
                    Assert.IsFalse(r.UsedFallback, $"{Describe(counts)} seed {seed}");
                    Assert.AreEqual(sizes.Length, r.Pieces.Length);
                    Assert.AreEqual(Spawns, r.SpawnPoints.Length);
                }
            }
        }

        [Test]
        public void GroundPieces_StandOnTheFloor_PlatformsFloatInTheirBand()
        {
            Compose(new ArenaPieceCounts(2, 2, 2), out Vector3[] sizes, out bool[] floating);
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed, sizes, floating, P);
                Assert.IsFalse(r.UsedFallback, $"seed {seed}");
                for (int i = 0; i < sizes.Length; i++)
                {
                    Vector3 c = r.Pieces[i];
                    float half = sizes[i].y * 0.5f;
                    if (!floating[i])
                    {
                        Assert.AreEqual(Floor + half, c.y, 1e-4f, $"ground piece {i} seed {seed}");
                        continue;
                    }
                    Assert.GreaterOrEqual(c.y, P.PlatformMinY - 1e-4f, $"platform {i} seed {seed}");
                    Assert.LessOrEqual(c.y, P.PlatformMaxY + 1e-4f, $"platform {i} seed {seed}");
                    Assert.GreaterOrEqual(c.y - half - Floor, P.FlightLaneClearance - 1e-4f, "lane under it");
                    Assert.GreaterOrEqual(Ceiling - (c.y + half), P.FlightLaneClearance - 1e-4f, "lane over it");
                }
            }
        }

        [Test]
        public void Platforms_UseTheWholeBand_NotOneHeight()
        {
            Compose(new ArenaPieceCounts(0, 1, 0), out Vector3[] sizes, out bool[] floating);
            float low = float.MaxValue, high = float.MinValue;
            for (int seed = 0; seed < 100; seed++)
            {
                ArenaLayoutResult r = Make(seed, sizes, floating, P);
                Assert.IsFalse(r.UsedFallback, $"seed {seed}");
                float y = r.Pieces[2].y;
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            Assert.Greater(high - low, 2f);
        }

        [Test]
        public void Platforms_StayOutOfTheStartLane()
        {
            // A band that straddles the start height: platforms go above or below the lane, never into it.
            ArenaLayoutParams p = P;
            p.PlatformMinY = -6f;
            p.PlatformMaxY = 6f;
            Compose(new ArenaPieceCounts(0, 2, 0), out Vector3[] sizes, out bool[] floating);
            bool above = false, below = false;
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed, sizes, floating, p);
                Assert.IsFalse(r.UsedFallback, $"seed {seed}");
                for (int i = 0; i < sizes.Length; i++)
                {
                    if (!floating[i]) continue;
                    float half = sizes[i].y * 0.5f;
                    float y = r.Pieces[i].y;
                    bool clear = y - half >= Start.y + p.StartLaneHalfHeight - 1e-4f
                                 || y + half <= Start.y - p.StartLaneHalfHeight + 1e-4f;
                    Assert.IsTrue(clear, $"platform at y {y} seed {seed}");
                    above |= y > Start.y;
                    below |= y < Start.y;
                }
            }
            Assert.IsTrue(above && below, "both sides of the lane are used");
        }

        [Test]
        public void Platforms_KeepAFlightLaneOverTheFloor_WhenTheBandReachesIt()
        {
            // The default band starts at 2 m, far above the floor lane; a band down to
            // the floor must still leave FlightLaneClearance under every platform.
            ArenaLayoutParams p = P;
            p.PlatformMinY = Floor;
            p.PlatformMaxY = Floor + 4f;
            p.StartLaneHalfHeight = 0f;
            Compose(new ArenaPieceCounts(0, 2, 0), out Vector3[] sizes, out bool[] floating);
            float lowest = float.MaxValue;
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed, sizes, floating, p);
                Assert.IsFalse(r.UsedFallback, $"seed {seed}");
                for (int i = 0; i < sizes.Length; i++)
                {
                    if (!floating[i]) continue;
                    float under = r.Pieces[i].y - sizes[i].y * 0.5f - Floor;
                    Assert.GreaterOrEqual(under, P.FlightLaneClearance - 1e-4f, $"platform {i} seed {seed}");
                    lowest = Mathf.Min(lowest, under);
                }
            }
            Assert.Less(lowest, P.FlightLaneClearance + 0.5f, "the band really reaches down to the lane");
        }

        [Test]
        public void NoRoomForAPlatform_FallsBack()
        {
            // The ceiling lane leaves no height for a platform at all.
            ArenaLayoutParams p = P;
            p.PlatformMinY = 9f;
            p.PlatformMaxY = 9.5f;
            Compose(new ArenaPieceCounts(0, 1, 0), out Vector3[] sizes, out bool[] floating);
            Assert.IsTrue(Make(3, sizes, floating, p).UsedFallback);
        }

        [Test]
        public void PiecePairs_KeepTheFloorGap_OrAFullFlightLaneBetween()
        {
            Compose(new ArenaPieceCounts(2, 2, 2), out Vector3[] sizes, out bool[] floating);
            for (int seed = 0; seed < 200; seed++)
            {
                ArenaLayoutResult r = Make(seed, sizes, floating, P);
                Assert.IsFalse(r.UsedFallback, $"seed {seed}");
                for (int i = 0; i < sizes.Length; i++)
                {
                    for (int j = i + 1; j < sizes.Length; j++)
                    {
                        float gap = HorizontalDistance(r.Pieces[i], r.Pieces[j])
                                    - ArenaLayout.Radius(sizes[i]) - ArenaLayout.Radius(sizes[j]);
                        if (gap >= P.MinPieceGap - 1e-4f) continue;
                        Assert.IsTrue(floating[i] || floating[j], $"ground pieces {i} and {j} too close, seed {seed}");
                        Assert.GreaterOrEqual(VerticalGap(r.Pieces[i], sizes[i], r.Pieces[j], sizes[j]),
                            P.FlightLaneClearance - 1e-4f, $"pieces {i} and {j} seed {seed}");
                    }
                }
            }
        }

        [Test]
        public void MixedLayouts_KeepEveryRoundFourRule()
        {
            foreach (ArenaPieceCounts counts in BusiestRows())
            {
                Compose(counts, out Vector3[] sizes, out bool[] floating);
                for (int seed = 0; seed < 200; seed++)
                {
                    ArenaLayoutResult r = Make(seed, sizes, floating, P);
                    string at = $"{Describe(counts)} seed {seed}";
                    Assert.IsFalse(r.UsedFallback, at);
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        Vector3 c = r.Pieces[i];
                        Vector3 s = sizes[i];
                        Assert.LessOrEqual(Mathf.Abs(c.x) + s.x * 0.5f, Size.x * 0.5f - P.Margin + 1e-4f, "x " + at);
                        Assert.LessOrEqual(Mathf.Abs(c.z) + s.z * 0.5f, Size.z * 0.5f - P.Margin + 1e-4f, "z " + at);
                        Assert.GreaterOrEqual(c.z, Start.z + P.MinPieceForward - 1e-4f, "forward " + at);
                        Assert.GreaterOrEqual(HorizontalDistance(c, Start) - ArenaLayout.Radius(s),
                            P.StartClearRadius - 1e-4f, "start " + at);
                        for (int b = 0; b < Targets.Length; b++)
                            Assert.IsFalse(new Bounds(c, s).Intersects(new Bounds(Targets[b], TargetSizes[b])),
                                $"target {b} " + at);
                    }

                    bool open = false;
                    for (int k = 0; k < r.SpawnPoints.Length; k++)
                    {
                        Vector3 sp = r.SpawnPoints[k];
                        Assert.IsTrue(Inside(P.SpawnArea, sp), "spawn area " + at);
                        for (int i = 0; i < sizes.Length; i++)
                            Assert.GreaterOrEqual(HorizontalDistance(r.Pieces[i], sp) - ArenaLayout.Radius(sizes[i]),
                                P.SpawnClearRadius - 1e-4f, "spawn clear " + at);
                        for (int t = k + 1; t < r.SpawnPoints.Length; t++)
                            Assert.GreaterOrEqual(Vector3.Distance(sp, r.SpawnPoints[t]), P.MinSpawnGap - 1e-4f,
                                "spawn gap " + at);
                        // T3-P3: the same spawn is open from the start and, past the
                        // platforms, from the seat eye and the MR TABLE eye.
                        open |= !ArenaLayout.SegmentBlocked(Start, sp, r.Pieces, sizes)
                                && !FloatingBlocks(SeatEye, sp, r.Pieces, sizes, floating)
                                && !FloatingBlocks(TableEye, sp, r.Pieces, sizes, floating);
                    }
                    Assert.IsTrue(open, "sight line " + at);
                }
            }
        }

        // T3-P3: a platform above the hero's line can still sit between the viewer's
        // eye (and the hand aim ray) and a spawn. One spawn in a small box at
        // (0, 5, 16) and one platform at a fixed height: the seat eye's line crosses
        // 3.7 m around z 4-10, the MR TABLE eye's line crosses 5.5 m around z 0-12.
        // A layout that comes back must keep that spawn in view from both eyes.
        [Test]
        public void Platforms_NeverHideTheOpeningSpawn_FromTheSeatEye() => PlatformKeepsSpawnInView(3.7f);

        [Test]
        public void Platforms_NeverHideTheOpeningSpawn_FromTheTableEye() => PlatformKeepsSpawnInView(5.5f);

        private static void PlatformKeepsSpawnInView(float platformY)
        {
            ArenaLayoutParams p = P;
            p.PlatformMinY = platformY;
            p.PlatformMaxY = platformY;
            p.SpawnArea = new Bounds(new Vector3(0f, 5f, 16f), new Vector3(0.5f, 0.2f, 0.5f));
            Vector3[] sizes = { ArenaPieceTable.PlatformSize };
            bool[] floating = { true };
            int placed = 0;
            for (int seed = 0; seed < 300; seed++)
            {
                ArenaLayoutResult r = ArenaLayout.Generate(Size, Start, sizes, 1, p, seed, null, null, null, null,
                    floating);
                if (r.UsedFallback) continue;
                placed++;
                Vector3 spawn = r.SpawnPoints[0];
                Assert.IsFalse(FloatingBlocks(SeatEye, spawn, r.Pieces, sizes, floating),
                    $"seat eye, platform at {r.Pieces[0]} seed {seed}");
                Assert.IsFalse(FloatingBlocks(TableEye, spawn, r.Pieces, sizes, floating),
                    $"table eye, platform at {r.Pieces[0]} seed {seed}");
            }
            Assert.GreaterOrEqual(placed, 290, "the rule moves the platform, it does not fall back");
        }

        [Test]
        public void DefaultViewerEyes_AreTheSeatAndTheTableEye()
        {
            Vector3[] eyes = ArenaLayoutParams.Default.ViewerEyes;
            Assert.AreEqual(2, eyes.Length);
            Assert.That(Vector3.Distance(SeatEye, eyes[0]), Is.LessThan(1e-3f), "seat " + eyes[0]);
            Assert.That(Vector3.Distance(TableEye, eyes[1]), Is.LessThan(1e-3f), "table " + eyes[1] + " vs " + TableEye);
        }

        [Test]
        public void FloatingFlags_ShorterThanThePieces_TheRestStandOnTheFloor()
        {
            Vector3[] sizes = { Pillars[0], Pillars[1], ArenaPieceTable.ThinPillarSize };
            ArenaLayoutResult r = Make(11, sizes, new[] { false }, P);
            Assert.IsFalse(r.UsedFallback);
            for (int i = 0; i < sizes.Length; i++)
                Assert.AreEqual(Floor + sizes[i].y * 0.5f, r.Pieces[i].y, 1e-4f, $"piece {i}");
        }

        // ---- ArenaPieceTable ----

        [Test]
        public void Compose_FixedThenWallsPlatformsThinPillars_OnlyPlatformsFloat()
        {
            Compose(new ArenaPieceCounts(1, 2, 1), out Vector3[] sizes, out bool[] floating);
            Vector3[] expected =
            {
                Pillars[0], Pillars[1], ArenaPieceTable.LowWallSize, ArenaPieceTable.PlatformSize,
                ArenaPieceTable.PlatformSize, ArenaPieceTable.ThinPillarSize,
            };
            CollectionAssert.AreEqual(expected, sizes);
            CollectionAssert.AreEqual(new[] { false, false, false, true, true, false }, floating);
        }

        [Test]
        public void Compose_CutsCountsToThePool()
        {
            ArenaPieceTable.Compose(Pillars, new[] { ArenaPieceTable.LowWallSize }, null,
                new[] { ArenaPieceTable.ThinPillarSize }, new ArenaPieceCounts(3, 2, 5), out Vector3[] sizes,
                out bool[] floating);
            Assert.AreEqual(4, sizes.Length); // 2 pillars + 1 wall + 0 platforms + 1 thin pillar
            Assert.AreEqual(4, floating.Length);
        }

        [Test]
        public void Row_IsTheHighestFromIslandAtOrBelowTheIsland_InAnyOrder()
        {
            ArenaPieceTier[] table =
            {
                new ArenaPieceTier(8, new ArenaPieceCounts(3, 3, 3), new ArenaPieceCounts(3, 3, 3)),
                new ArenaPieceTier(1, new ArenaPieceCounts(1, 1, 1), new ArenaPieceCounts(1, 1, 1)),
                new ArenaPieceTier(4, new ArenaPieceCounts(2, 2, 2), new ArenaPieceCounts(2, 2, 2)),
            };
            Assert.AreEqual(1, ArenaPieceTable.Row(table, 1).FromIsland);
            Assert.AreEqual(1, ArenaPieceTable.Row(table, 3).FromIsland);
            Assert.AreEqual(4, ArenaPieceTable.Row(table, 4).FromIsland);
            Assert.AreEqual(4, ArenaPieceTable.Row(table, 7).FromIsland);
            Assert.AreEqual(8, ArenaPieceTable.Row(table, 9).FromIsland);
            // Below the first row, or no table: no extra pieces.
            Assert.AreEqual(0, ArenaPieceTable.Row(table, 0).Max.Total);
            Assert.AreEqual(0, ArenaPieceTable.Roll(null, 5, 123, Full).Total);
        }

        private static ArenaPieceCounts Full => new ArenaPieceCounts(ArenaPieceTable.DefaultPoolSize,
            ArenaPieceTable.DefaultPoolSize, ArenaPieceTable.DefaultPoolSize);

        [Test]
        public void Roll_SameSeed_SameCounts()
        {
            ArenaPieceTier[] table = ArenaPieceTable.Defaults();
            for (int seed = 0; seed < 50; seed++)
                Assert.AreEqual(ArenaPieceTable.Roll(table, 6, seed, Full), ArenaPieceTable.Roll(table, 6, seed, Full));
        }

        [Test]
        public void Roll_StaysInsideTheIslandsRow()
        {
            ArenaPieceTier[] table = ArenaPieceTable.Defaults();
            for (int island = 1; island <= RunRules.IslandCount; island++)
            {
                ArenaPieceTier row = ArenaPieceTable.Row(table, island);
                for (int seed = 0; seed < 300; seed++)
                {
                    ArenaPieceCounts c = ArenaPieceTable.Roll(table, island, seed, Full);
                    string at = $"island {island} seed {seed}";
                    Assert.That(c.LowWalls, Is.InRange(row.Min.LowWalls, row.Max.LowWalls), at);
                    Assert.That(c.Platforms, Is.InRange(row.Min.Platforms, row.Max.Platforms), at);
                    Assert.That(c.ThinPillars, Is.InRange(row.Min.ThinPillars, row.Max.ThinPillars), at);
                }
            }
        }

        [Test]
        public void Roll_UsesTheWholeRange_AcrossSeeds()
        {
            ArenaPieceTier[] table = ArenaPieceTable.Defaults();
            ArenaPieceTier row = ArenaPieceTable.Row(table, 7);
            var seen = new HashSet<int>();
            for (int seed = 0; seed < 200; seed++) seen.Add(ArenaPieceTable.Roll(table, 7, seed, Full).LowWalls);
            Assert.IsTrue(seen.Contains(row.Min.LowWalls) && seen.Contains(row.Max.LowWalls));
        }

        [Test]
        public void Roll_NeverAsksForMoreThanThePoolHolds()
        {
            ArenaPieceTier[] table = { new ArenaPieceTier(1, new ArenaPieceCounts(2, 2, 2), new ArenaPieceCounts(4, 4, 4)) };
            var pool = new ArenaPieceCounts(1, 0, 3);
            for (int seed = 0; seed < 100; seed++)
            {
                ArenaPieceCounts c = ArenaPieceTable.Roll(table, 1, seed, pool);
                Assert.LessOrEqual(c.LowWalls, 1);
                Assert.AreEqual(0, c.Platforms);
                Assert.LessOrEqual(c.ThinPillars, 3);
            }
        }

        [Test]
        public void Defaults_DeeperIslandsGetMoreCover()
        {
            ArenaPieceTier[] table = ArenaPieceTable.Defaults();
            Assert.Less(MeanTotal(table, 1), MeanTotal(table, 5));
            Assert.Less(MeanTotal(table, 5), MeanTotal(table, 7));
        }

        [Test]
        public void Defaults_FitThePoolTheBuilderMakes_AndStartAtIslandOne()
        {
            ArenaPieceTier[] table = ArenaPieceTable.Defaults();
            Assert.AreEqual(1, ArenaPieceTable.Row(table, 1).FromIsland);
            foreach (ArenaPieceTier row in table)
            {
                Assert.LessOrEqual(row.Max.LowWalls, ArenaPieceTable.DefaultPoolSize);
                Assert.LessOrEqual(row.Max.Platforms, ArenaPieceTable.DefaultPoolSize);
                Assert.LessOrEqual(row.Max.ThinPillars, ArenaPieceTable.DefaultPoolSize);
                Assert.LessOrEqual(row.Min.Total, row.Max.Total);
            }
        }

        [Test]
        public void Roll_DependsOnTheSeedAndTheRowOnly()
        {
            // The same layout seed on two islands of the same row gives the same
            // counts, and the counts vary with the seed.
            ArenaPieceTier[] table = ArenaPieceTable.Defaults();
            Assert.AreEqual(ArenaPieceTable.Roll(table, 7, 99, Full), ArenaPieceTable.Roll(table, 8, 99, Full));
            var combos = new HashSet<ArenaPieceCounts>();
            for (int seed = 0; seed < 100; seed++) combos.Add(ArenaPieceTable.Roll(table, 7, seed, Full));
            Assert.Greater(combos.Count, 3);
        }

        // ---- Themes ----

        [Test]
        public void ThemeForIsland_SameRun_SameTheme()
        {
            for (int island = 1; island <= RunRules.IslandCount; island++)
                Assert.AreEqual(ArenaThemes.ForIsland(777, island, 3), ArenaThemes.ForIsland(777, island, 3));
        }

        [Test]
        public void ThemeForIsland_NeverRepeatsOnTheNextIsland()
        {
            for (int run = 0; run < 100; run++)
                for (int island = 1; island < RunRules.IslandCount; island++)
                    Assert.AreNotEqual(ArenaThemes.ForIsland(run, island, 3), ArenaThemes.ForIsland(run, island + 1, 3),
                        $"run {run} island {island}");
        }

        [Test]
        public void ThemeForIsland_EveryThemeOnceInThreeIslands()
        {
            for (int run = 0; run < 100; run++)
            {
                var seen = new HashSet<int>();
                for (int island = 4; island <= 6; island++) seen.Add(ArenaThemes.ForIsland(run, island, 3));
                Assert.AreEqual(3, seen.Count, $"run {run}");
            }
        }

        [Test]
        public void ThemeForIsland_DiffersBetweenRuns()
        {
            var firstThemes = new HashSet<int>();
            for (int run = 0; run < 100; run++) firstThemes.Add(ArenaThemes.ForIsland(run * 7919, 1, 3));
            Assert.AreEqual(3, firstThemes.Count);
        }

        [Test]
        public void ThemeForIsland_NoThemesOrOne()
        {
            Assert.AreEqual(-1, ArenaThemes.ForIsland(5, 1, 0));
            Assert.AreEqual(0, ArenaThemes.ForIsland(5, 1, 1));
            Assert.AreEqual(0, ArenaThemes.ForIsland(5, 2, 1));
        }

        [Test]
        public void DefaultThemes_ThreeNamedThemesWithALitSun()
        {
            ArenaTheme[] themes = ArenaThemes.Defaults();
            Assert.AreEqual(3, themes.Length);
            var names = new HashSet<string>();
            foreach (ArenaTheme theme in themes)
            {
                Assert.IsFalse(string.IsNullOrEmpty(theme.Name));
                names.Add(theme.Name);
                Assert.That(theme.LightIntensity, Is.InRange(0.8f, 1.5f), theme.Name);
                Assert.AreEqual(1f, theme.Floor.a, 1e-4f, theme.Name);
                Assert.AreEqual(1f, theme.Wall.a, 1e-4f, theme.Name);
                Assert.AreEqual(1f, theme.Pieces.a, 1e-4f, theme.Name);
            }
            Assert.AreEqual(3, names.Count);
        }

        [Test]
        public void DefaultThemes_StayDesaturated_SoHeroesStandOut()
        {
            // Heroes, bots, targets and the CURSOR marker are saturated (S >= 0.75);
            // the arena surfaces and the sun stay well below that.
            foreach (ArenaTheme theme in ArenaThemes.Defaults())
            {
                Assert.LessOrEqual(Saturation(theme.Floor), 0.3f, theme.Name + " floor");
                Assert.LessOrEqual(Saturation(theme.Wall), 0.3f, theme.Name + " wall");
                Assert.LessOrEqual(Saturation(theme.Pieces), 0.3f, theme.Name + " pieces");
                Assert.LessOrEqual(Saturation(theme.LightColor), 0.35f, theme.Name + " sun");
            }
        }

        // ---- Run log ----

        [Test]
        public void Terrain_PerIsland_InTheJson_AfterTheLayoutSeed()
        {
            var rec = new RunRecorder();
            rec.Begin(42, "Assist", "Vr", "2026-10-09T10:00:00");
            rec.IslandStarted(1, IslandType.Arena);
            rec.IslandLayout(55);
            rec.IslandTerrain("Frost", new ArenaPieceCounts(1, 0, 2));
            rec.IslandStarted(2, IslandType.Horde);
            rec.IslandTerrain("Dusk", new ArenaPieceCounts(0, 0, 0)); // a fallback: no layout seed
            rec.IslandStarted(3, IslandType.Arena); // no applier
            RunRecord r = rec.Finish(RunResult.Quit, 9f, new RunTotals());

            Assert.IsTrue(r.Islands[0].HasTerrain);
            Assert.AreEqual("Frost", r.Islands[0].Theme);
            Assert.AreEqual(new ArenaPieceCounts(1, 0, 2), r.Islands[0].Pieces);
            Assert.IsFalse(r.Islands[2].HasTerrain);
            string json = RunRecordJson.ToJson(r);
            StringAssert.Contains(
                "\"layout_seed\":55,\"theme\":\"Frost\",\"low_walls\":1,\"platforms\":0,\"thin_pillars\":2}", json);
            StringAssert.Contains(
                "\"deaths\":0,\"theme\":\"Dusk\",\"low_walls\":0,\"platforms\":0,\"thin_pillars\":0}", json);
            StringAssert.Contains("{\"n\":3,\"type\":\"Arena\",\"fight_s\":0,\"damage\":0,\"deaths\":0}", json);
        }

        [Test]
        public void Terrain_IgnoredBeforeTheFirstIsland_NullThemeIsEmpty()
        {
            var rec = new RunRecorder();
            rec.Begin(1, "Cursor", "Table", "2026-10-09T10:00:00");
            rec.IslandTerrain("Ember", new ArenaPieceCounts(1, 1, 1));
            rec.IslandStarted(1, IslandType.Arena);
            rec.IslandStarted(2, IslandType.Arena);
            rec.IslandTerrain(null, default);
            RunRecord r = rec.Finish(RunResult.Quit, 1f, new RunTotals());
            Assert.IsFalse(r.Islands[0].HasTerrain);
            Assert.AreEqual("", r.Islands[1].Theme);
            StringAssert.Contains("\"theme\":\"\"", RunRecordJson.ToJson(r));
        }

        // ---- helpers ----

        private static float MeanTotal(ArenaPieceTier[] table, int island)
        {
            float sum = 0f;
            const int seeds = 400;
            for (int seed = 0; seed < seeds; seed++) sum += ArenaPieceTable.Roll(table, island, seed, Full).Total;
            return sum / seeds;
        }

        private static float Saturation(Color c)
        {
            Color.RGBToHSV(c, out float _, out float s, out float _);
            return s;
        }

        // Bounds.Contains with the edges included (a plain min / max check).
        private static bool Inside(Bounds b, Vector3 p) =>
            p.x >= b.min.x - 1e-4f && p.x <= b.max.x + 1e-4f && p.y >= b.min.y - 1e-4f && p.y <= b.max.y + 1e-4f
            && p.z >= b.min.z - 1e-4f && p.z <= b.max.z + 1e-4f;

        // True when the segment eye-target passes through a floating piece's box.
        private static bool FloatingBlocks(Vector3 eye, Vector3 target, Vector3[] pieces, Vector3[] sizes,
            bool[] floating)
        {
            for (int i = 0; i < pieces.Length; i++)
                if (floating[i] && ArenaLayout.SegmentBlocked(eye, target, new[] { pieces[i] }, new[] { sizes[i] }))
                    return true;
            return false;
        }

        private static float VerticalGap(Vector3 a, Vector3 sizeA, Vector3 b, Vector3 sizeB) =>
            Mathf.Max((a.y - sizeA.y * 0.5f) - (b.y + sizeB.y * 0.5f), (b.y - sizeB.y * 0.5f) - (a.y + sizeA.y * 0.5f));

        private static float HorizontalDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static string Describe(ArenaPieceCounts c) =>
            $"walls {c.LowWalls} platforms {c.Platforms} thin {c.ThinPillars}";
    }
}
