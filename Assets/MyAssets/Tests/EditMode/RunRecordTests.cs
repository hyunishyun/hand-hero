using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Run telemetry (round 3, P13 / D16): one record per run, one JSON line in run_log.jsonl.
    public class RunRecordTests
    {
        private static RunRecorder Begun()
        {
            var rec = new RunRecorder();
            rec.Begin(42, "Assist", "Arena", "2026-10-09T10:00:00");
            return rec;
        }

        private static RunRecord Finish(RunRecorder rec, RunResult result, float runTime) =>
            rec.Finish(result, runTime, new RunTotals());

        [Test]
        public void FinishWithoutBegin_ReturnsNull()
        {
            Assert.IsNull(new RunRecorder().Finish(RunResult.Quit, 1f, new RunTotals()));
        }

        [Test]
        public void SecondFinish_ReturnsNull_SoARunIsWrittenOnce()
        {
            RunRecorder rec = Begun();
            Assert.IsNotNull(Finish(rec, RunResult.Victory, 10f));
            Assert.IsNull(Finish(rec, RunResult.Quit, 10f));
            Assert.IsFalse(rec.IsRecording);
        }

        [Test]
        public void IslandFightTime_RunsFromFightStartToClear()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(1, IslandType.Arena);
            rec.FightStarted(3f);
            rec.FightEnded(33f);
            rec.IslandStarted(2, IslandType.Horde);
            rec.FightStarted(40f);
            RunRecord r = Finish(rec, RunResult.Defeat, 50f); // still fighting: closed at the finish time

            Assert.AreEqual(2, r.Islands.Count);
            Assert.AreEqual(1, r.Islands[0].Number);
            Assert.AreEqual(IslandType.Arena, r.Islands[0].Type);
            Assert.AreEqual(30f, r.Islands[0].FightSeconds, 1e-4f);
            Assert.AreEqual(IslandType.Horde, r.Islands[1].Type);
            Assert.AreEqual(10f, r.Islands[1].FightSeconds, 1e-4f);
            Assert.AreEqual(50f, r.TotalSeconds, 1e-4f);
        }

        [Test]
        public void DamageAndDeaths_GoToTheCurrentIsland()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(1, IslandType.Arena);
            rec.DamageTaken(20f);
            rec.IslandStarted(2, IslandType.Elite);
            rec.DamageTaken(15f);
            rec.DamageTaken(5f);
            rec.PlayerDied(revived: true);
            rec.PlayerDied(revived: false);
            RunRecord r = Finish(rec, RunResult.Defeat, 99f);

            Assert.AreEqual(20f, r.Islands[0].DamageTaken, 1e-4f);
            Assert.AreEqual(20f, r.Islands[1].DamageTaken, 1e-4f);
            Assert.AreEqual(0, r.Islands[0].Deaths);
            Assert.AreEqual(2, r.Islands[1].Deaths);
            Assert.AreEqual(1, r.RevivesUsed);
            Assert.AreEqual(2, r.DeathIsland);
        }

        [Test]
        public void NoDefeat_DeathIslandIsZero()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(1, IslandType.Arena);
            rec.PlayerDied(revived: true);
            Assert.AreEqual(0, Finish(rec, RunResult.Victory, 1f).DeathIsland);
        }

        [Test]
        public void EventsBeforeAnIsland_AreIgnored()
        {
            RunRecorder rec = Begun();
            rec.DamageTaken(10f);
            rec.FightEnded(5f);
            Assert.AreEqual(0, Finish(rec, RunResult.Quit, 1f).Islands.Count);
        }

        [Test]
        public void ItemsAndShop_AreListed()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(4, IslandType.Arena);
            rec.ItemPicked("focus_lens", 1, ItemSource.Chest);
            rec.ItemPicked("focus_lens", 2, ItemSource.Shop);
            rec.ShopRerolled();
            RunRecord r = Finish(rec, RunResult.Quit, 1f);

            Assert.AreEqual(2, r.Items.Count);
            Assert.AreEqual("focus_lens", r.Items[1].Id);
            Assert.AreEqual(2, r.Items[1].Level);
            Assert.AreEqual(4, r.Items[1].Island);
            Assert.AreEqual(ItemSource.Shop, r.Items[1].Source);
            Assert.AreEqual(1, r.ShopBuys);
            Assert.AreEqual(1, r.ShopRerolls);
        }

        [Test]
        public void Totals_AndPinchHolds_AreCopied()
        {
            RunRecorder rec = Begun();
            var holds = new PinchHoldStats();
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.1f });
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.9f, ChargeStarted = true, Released = true });
            RunRecord r = rec.Finish(RunResult.Victory, 300f, new RunTotals
            {
                Kills = 12, CrystalsEarned = 400, CrystalsSpent = 250, ShotsFired = 80, ShotsHit = 60, ChargeShots = 3,
                Holds = holds,
            });

            Assert.AreEqual(42, r.Seed);
            Assert.AreEqual("Assist", r.AimMode);
            Assert.AreEqual(RunResult.Victory, r.Result);
            Assert.AreEqual(12, r.Kills);
            Assert.AreEqual(400, r.CrystalsEarned);
            Assert.AreEqual(250, r.CrystalsSpent);
            Assert.AreEqual(0.75f, r.HitRate, 1e-4f);
            Assert.AreEqual(3, r.ChargeShots);
            CollectionAssert.AreEqual(new[] { 0.1f, 0.9f }, r.HoldSeconds);
            CollectionAssert.AreEqual(new[] { false, true }, r.HoldCharged);
        }

        [Test]
        public void HitRate_NoShots_IsZero()
        {
            Assert.AreEqual(0f, Finish(Begun(), RunResult.Quit, 1f).HitRate);
        }

        [Test]
        public void NewBegin_StartsAFreshRecord()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(1, IslandType.Arena);
            rec.ItemPicked("x", 1, ItemSource.Chest);
            Finish(rec, RunResult.Quit, 1f);
            rec.Begin(7, "Cursor", "Tabletop", "t");
            RunRecord r = Finish(rec, RunResult.Quit, 1f);
            Assert.AreEqual(0, r.Islands.Count);
            Assert.AreEqual(0, r.Items.Count);
            Assert.AreEqual(7, r.Seed);
        }

        [Test]
        public void Json_IsOneLine_WithTheSummaryFields()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(1, IslandType.Arena);
            rec.FightStarted(3f);
            rec.DamageTaken(12.5f);
            rec.FightEnded(33.25f);
            rec.ItemPicked("focus_lens", 1, ItemSource.Chest);
            var holds = new PinchHoldStats();
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.4f, ChargeStarted = true, Released = true });
            RunRecord r = rec.Finish(RunResult.Defeat, 40f, new RunTotals { ShotsFired = 3, ShotsHit = 1, Holds = holds });

            string json = RunRecordJson.ToJson(r);
            StringAssert.DoesNotContain("\n", json);
            StringAssert.StartsWith("{\"v\":1,", json);
            StringAssert.Contains("\"seed\":42", json);
            StringAssert.Contains("\"aim\":\"Assist\"", json);
            StringAssert.Contains("\"result\":\"Defeat\"", json);
            StringAssert.Contains("\"total_s\":40", json);
            StringAssert.Contains("\"hit_rate\":0.333", json);
            StringAssert.Contains("\"islands\":[{\"n\":1,\"type\":\"Arena\",\"fight_s\":30.25,\"damage\":12.5,\"deaths\":0}]", json);
            StringAssert.Contains("\"items\":[{\"id\":\"focus_lens\",\"level\":1,\"island\":1,\"from\":\"chest\"}]", json);
            StringAssert.Contains("\"hold_s\":[0.4]", json);
            StringAssert.Contains("\"hold_charged\":[1]", json);
            StringAssert.EndsWith("}", json);
        }

        [Test]
        public void Json_EscapesStrings()
        {
            var rec = new RunRecorder();
            rec.Begin(1, "a\"b\\c", "v", "t");
            string json = RunRecordJson.ToJson(rec.Finish(RunResult.Quit, 0f, new RunTotals()));
            StringAssert.Contains("\"aim\":\"a\\\"b\\\\c\"", json);
        }

        [Test]
        public void Json_UsesInvariantDecimalPoint()
        {
            System.Globalization.CultureInfo saved = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                string json = RunRecordJson.ToJson(Begun().Finish(RunResult.Quit, 1.5f, new RunTotals()));
                StringAssert.Contains("\"total_s\":1.5", json);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = saved;
            }
        }

        [Test]
        public void Wallet_CountsEarnedAndSpent()
        {
            var wallet = new CrystalWallet();
            wallet.Add(100);
            wallet.Add(-5);
            Assert.IsTrue(wallet.TrySpend(30));
            Assert.IsFalse(wallet.TrySpend(500));
            Assert.AreEqual(100, wallet.TotalEarned);
            Assert.AreEqual(30, wallet.TotalSpent);
        }

        [Test]
        public void PinchHoldStats_ExposesWhichHoldsFiredACharge()
        {
            var holds = new PinchHoldStats();
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.2f });
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.7f, Released = true });
            CollectionAssert.AreEqual(new[] { false, true }, holds.ChargeShotFlags);
        }

        // A hold that started a charge (slowdown + orb) but was released before the
        // charge was ready is the misfire D13 targets; it fires no charge shot.
        [Test]
        public void PinchHoldStats_ExposesWhichHoldsStartedACharge()
        {
            var holds = new PinchHoldStats();
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.2f });
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.4f, ChargeStarted = true });
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.9f, ChargeStarted = true, Released = true });
            CollectionAssert.AreEqual(new[] { false, true, true }, holds.ChargeStartedFlags);
            holds.Clear();
            Assert.AreEqual(0, holds.ChargeStartedFlags.Count);
        }

        [Test]
        public void Json_HasPerHoldChargeStartedFlags()
        {
            var holds = new PinchHoldStats();
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.4f, ChargeStarted = true });
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.1f });
            RunRecord r = Begun().Finish(RunResult.Quit, 1f, new RunTotals { Holds = holds });
            CollectionAssert.AreEqual(new[] { true, false }, r.HoldStarted);
            StringAssert.Contains("\"hold_started\":[1,0]", RunRecordJson.ToJson(r));
        }

        // Round 4 (S5): the starting relic is picked before island 1, so it is a
        // run field, not an item pick (picks belong to an island).
        [Test]
        public void StartRelic_IsKeptFromBeforeTheFirstIsland()
        {
            RunRecorder rec = Begun();
            rec.StartRelicPicked(ItemCatalog.SecondWind);
            rec.IslandStarted(1, IslandType.Arena);
            RunRecord r = Finish(rec, RunResult.Quit, 5f);

            Assert.AreEqual(ItemCatalog.SecondWind, r.StartRelic);
            Assert.AreEqual(0, r.Items.Count);
            StringAssert.Contains("\"start_relic\":\"second_wind\"", RunRecordJson.ToJson(r));
        }

        [Test]
        public void StartRelic_EmptyWithoutAPick_AndResetByBegin()
        {
            RunRecorder rec = Begun();
            rec.StartRelicPicked(ItemCatalog.Dividends);
            Finish(rec, RunResult.Quit, 1f);

            RunRecord next = Finish(Begun(), RunResult.Quit, 1f);
            Assert.AreEqual("", next.StartRelic);
            StringAssert.Contains("\"start_relic\":\"\"", RunRecordJson.ToJson(next));
        }

        // Round 4 (S6): kills and damage taken per bot archetype, in first-seen order.
        [Test]
        public void PerArchetype_KillsAndDamageTaken()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(3, IslandType.Arena);
            rec.BotKilled("Gunner");
            rec.BotKilled("Striker");
            rec.BotKilled("Gunner");
            rec.DamageTaken(10f, "Gunner");
            rec.DamageTaken(5.5f, "Sniper");
            rec.DamageTaken(2f, "Gunner");
            rec.DamageTaken(3f); // shooter unknown: the island total only
            RunRecord r = Finish(rec, RunResult.Quit, 30f);

            Assert.AreEqual(20.5f, r.Islands[0].DamageTaken, 1e-4f);
            Assert.AreEqual(2f, r.KillsBy.Get("Gunner"));
            Assert.AreEqual(12f, r.DamageBy.Get("Gunner"), 1e-4f);
            Assert.AreEqual(0f, r.DamageBy.Get("Lancer"));
            string json = RunRecordJson.ToJson(r);
            StringAssert.Contains("\"kills_by\":{\"Gunner\":2,\"Striker\":1}", json);
            StringAssert.Contains("\"damage_by\":{\"Gunner\":12,\"Sniper\":5.5}", json);
        }

        [Test]
        public void PerArchetype_EmptyWithoutBots_AndResetByBegin()
        {
            RunRecorder rec = Begun();
            rec.BotKilled("Lancer");
            Finish(rec, RunResult.Quit, 1f);

            string json = RunRecordJson.ToJson(Finish(Begun(), RunResult.Quit, 1f));
            StringAssert.Contains("\"kills_by\":{}", json);
            StringAssert.Contains("\"damage_by\":{}", json);
        }

        // Round 4 (S7): the seeded arena layout of every island, written only when one was applied.
        [Test]
        public void LayoutSeed_PerIsland_InTheJson()
        {
            RunRecorder rec = Begun();
            rec.IslandStarted(1, IslandType.Arena);
            rec.IslandLayout(-12345);
            rec.IslandStarted(2, IslandType.Horde);
            rec.IslandLayout(777);
            rec.IslandStarted(3, IslandType.Arena); // no layout applied
            RunRecord r = Finish(rec, RunResult.Quit, 9f);

            Assert.IsTrue(r.Islands[0].HasLayoutSeed);
            Assert.AreEqual(-12345, r.Islands[0].LayoutSeed);
            Assert.AreEqual(777, r.Islands[1].LayoutSeed);
            Assert.IsFalse(r.Islands[2].HasLayoutSeed);
            string json = RunRecordJson.ToJson(r);
            StringAssert.Contains("\"deaths\":0,\"layout_seed\":-12345}", json);
            StringAssert.Contains("\"n\":2,\"type\":\"Horde\",\"fight_s\":0,\"damage\":0,\"deaths\":0,\"layout_seed\":777}", json);
            StringAssert.Contains("{\"n\":3,\"type\":\"Arena\",\"fight_s\":0,\"damage\":0,\"deaths\":0}", json);
        }

        [Test]
        public void LayoutSeed_IgnoredBeforeTheFirstIsland()
        {
            RunRecorder rec = Begun();
            rec.IslandLayout(5);
            rec.IslandStarted(1, IslandType.Arena);
            Assert.IsFalse(Finish(rec, RunResult.Quit, 1f).Islands[0].HasLayoutSeed);
        }

        // Round 5 (D2): per hold, the pinch that ended it, for retuning the release on device.
        private static PinchHoldStats HoldsWithDiagnostics()
        {
            var holds = new PinchHoldStats();
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.15f }, new PinchRelease
            {
                By = PinchReleaseBy.Relative, PeakStrength = 1f, MinStrength = 0.85f, ReleaseStrength = 0.711f,
                MetaSeen = true,
            });
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 1.2f, ChargeStarted = true, Released = true },
                new PinchRelease { By = PinchReleaseBy.Meta, PeakStrength = 0.98f, MinStrength = 0.9f, ReleaseStrength = 0.93f });
            holds.Add(new ChargeStep { HoldEnded = true, HoldSeconds = 0.1f }); // CURSOR trigger hold
            return holds;
        }

        [Test]
        public void PinchHoldStats_RecordsThePinchReleasePerHold()
        {
            PinchHoldStats holds = HoldsWithDiagnostics();
            CollectionAssert.AreEqual(new[] { 1f, 0.98f, 0f }, holds.PeakStrengths);
            CollectionAssert.AreEqual(new[] { 0.85f, 0.9f, 0f }, holds.MinStrengths);
            CollectionAssert.AreEqual(new[] { 0.711f, 0.93f, 0f }, holds.ReleaseStrengths);
            CollectionAssert.AreEqual(new[] { PinchReleaseBy.Relative, PinchReleaseBy.Meta, PinchReleaseBy.None },
                holds.ReleasedBy);
            CollectionAssert.AreEqual(new[] { true, false, false }, holds.MetaSeenFlags);

            holds.Clear();
            Assert.AreEqual(0, holds.ReleasedBy.Count);
            Assert.AreEqual(0, holds.PeakStrengths.Count);
        }

        [Test]
        public void PinchHoldStats_IgnoresReleasesWithoutAnEndedHold()
        {
            var holds = new PinchHoldStats();
            holds.Add(new ChargeStep { Charging = true }, new PinchRelease { By = PinchReleaseBy.Relative });
            Assert.AreEqual(0, holds.ReleasedBy.Count);
            holds.Recording = false;
            holds.Add(new ChargeStep { HoldEnded = true }, new PinchRelease { By = PinchReleaseBy.Relative });
            Assert.AreEqual(0, holds.ReleasedBy.Count);
        }

        [Test]
        public void Json_HasPerHoldPinchDiagnostics()
        {
            RunRecord r = Begun().Finish(RunResult.Quit, 1f, new RunTotals { Holds = HoldsWithDiagnostics() });
            CollectionAssert.AreEqual(new[] { PinchReleaseBy.Relative, PinchReleaseBy.Meta, PinchReleaseBy.None },
                r.HoldReleaseBy);
            CollectionAssert.AreEqual(new[] { 0.85f, 0.9f, 0f }, r.HoldMinStrength);

            string json = RunRecordJson.ToJson(r);
            StringAssert.Contains("\"hold_s\":[0.15,1.2,0.1]", json);
            StringAssert.Contains("\"min_strength\":[0.85,0.9,0]", json);
            StringAssert.Contains("\"release_strength\":[0.711,0.93,0]", json);
            StringAssert.Contains("\"release_by\":[\"relative\",\"meta\",\"none\"]", json);
            StringAssert.Contains("\"peak_strength\":[1,0.98,0]", json);
            StringAssert.Contains("\"meta_seen\":[1,0,0]", json);
            StringAssert.EndsWith("}", json);
        }

        [Test]
        public void Json_ReleaseByNames()
        {
            var holds = new PinchHoldStats();
            foreach (PinchReleaseBy by in new[]
                     { PinchReleaseBy.Absolute, PinchReleaseBy.Lost, PinchReleaseBy.None, PinchReleaseBy.Meta })
                holds.Add(new ChargeStep { HoldEnded = true }, new PinchRelease { By = by });
            string json = RunRecordJson.ToJson(Begun().Finish(RunResult.Quit, 1f, new RunTotals { Holds = holds }));
            StringAssert.Contains("\"release_by\":[\"absolute\",\"lost\",\"none\",\"meta\"]", json);
        }

        // Deep review DR-7: run_log.jsonl keeps runs across `adb install -r`, so each
        // run names the APK that wrote it (run_summary.py groups by it).
        [Test]
        public void Json_NamesTheBuildThatWroteTheRun()
        {
            var rec = new RunRecorder();
            rec.Begin(42, "Assist", "Arena", "2026-10-10T14:00:00", "9.2.0+20261010_1500_release");
            string json = RunRecordJson.ToJson(Finish(rec, RunResult.Quit, 1f));
            StringAssert.StartsWith("{\"v\":1,\"build\":\"9.2.0+20261010_1500_release\",\"seed\":42,", json);
        }

        [Test]
        public void Json_BuildIsEmpty_WhenNotGiven_AndResetByBegin()
        {
            var rec = new RunRecorder();
            rec.Begin(1, "Assist", "Arena", "t", "editor");
            Finish(rec, RunResult.Quit, 1f);
            rec.Begin(2, "Assist", "Arena", "t");
            string json = RunRecordJson.ToJson(Finish(rec, RunResult.Quit, 1f));
            StringAssert.Contains("\"build\":\"\"", json);
            StringAssert.DoesNotContain("editor", json);
        }

        [Test]
        public void Json_NoHolds_EmptyDiagnosticArrays()
        {
            string json = RunRecordJson.ToJson(Finish(Begun(), RunResult.Quit, 1f));
            StringAssert.Contains("\"release_by\":[]", json);
            StringAssert.Contains("\"min_strength\":[]", json);
            StringAssert.Contains("\"pinch_strength_s\":{}", json);
        }

        // Deep review DR-9, second pass: the aim hand's time per unsmoothed strength, so
        // run_summary.py can read where the thumb rests (report A5). Only bins with time,
        // lowest first, keyed by the strength rounded to 0.01 ("<0.50" for the rest).
        [Test]
        public void Json_PinchStrengthTime_NonEmptyBinsInOrder()
        {
            var holds = new PinchHoldStats();
            holds.AddStrength(1f, 0.5f);
            holds.AddStrength(0.711f, 2f);
            holds.AddStrength(0.3f, 1.5f);
            holds.AddStrength(0.709f, 0.25f);

            RunRecord r = Begun().Finish(RunResult.Quit, 1f, new RunTotals { Holds = holds });
            Assert.AreEqual(PinchStrengthTime.Bins, r.PinchStrengthSeconds.Length);
            Assert.AreEqual(2.25f, r.PinchStrengthSeconds[PinchStrengthTime.BinOf(0.71f)], 1e-6f);

            string json = RunRecordJson.ToJson(r);
            StringAssert.Contains("\"pinch_strength_s\":{\"<0.50\":1.5,\"0.71\":2.25,\"1.00\":0.5}", json);
            StringAssert.EndsWith("}", json);
        }

        [Test]
        public void PinchStrengthTime_IsACopy_NotClearedWithTheNextRun()
        {
            var holds = new PinchHoldStats();
            holds.AddStrength(0.71f, 1f);
            RunRecord r = Begun().Finish(RunResult.Quit, 1f, new RunTotals { Holds = holds });
            holds.Clear();
            Assert.AreEqual(1f, r.PinchStrengthSeconds[PinchStrengthTime.BinOf(0.71f)], 1e-6f);
        }
    }
}
