using System.Linq;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Meta progression A (round 4, D4): starting relics unlocked by progress, and
    // personal bests per aim mode. Stored under hh.meta.* (design doc section 5).
    public class MetaProgressTests
    {
        private const string Assist = "Assist";
        private const string Cursor = "Cursor";
        // Keys other systems own (AimModeSetting, MatchDirector): never touched.
        private const string AimKey = "HandHero.AimMode";
        private const string TutorialKey = "HandHero.TutorialSeen";

        private MemoryKeyValueStore _store;
        private MetaProgress _meta;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryKeyValueStore();
            _meta = new MetaProgress(_store);
        }

        private MetaChanges Win(string aim, float seconds = 540f) =>
            _meta.OnRunEnded(RunResult.Victory, 9, seconds, aim);

        private MetaChanges Lose(string aim, int island) =>
            _meta.OnRunEnded(RunResult.Defeat, island, 200f, aim);

        // --- fresh install ---

        [Test]
        public void NewInstall_HasNothing()
        {
            Assert.AreEqual(0, _meta.Runs);
            Assert.AreEqual(0, _meta.Wins);
            Assert.IsFalse(_meta.HasPlayed);
            Assert.AreEqual(MetaUnlocks.None, _meta.Unlocked);
            Assert.AreEqual(0, _meta.BestIsland(Assist));
            Assert.AreEqual(0f, _meta.BestWinSeconds(Assist));
            Assert.AreEqual(0, _meta.StartingRelicChoices().Count);
        }

        // --- unlocks ---

        [Test]
        public void ReachingIsland5_UnlocksSecondWind()
        {
            MetaChanges c = Lose(Assist, 5);

            Assert.AreEqual(MetaUnlocks.SecondWind, c.NewUnlocks);
            Assert.AreEqual(MetaUnlocks.SecondWind, _meta.Unlocked);
            CollectionAssert.AreEqual(new[] { ItemCatalog.SecondWind }, _meta.StartingRelicChoices());
        }

        [Test]
        public void Island4_UnlocksNothing()
        {
            MetaChanges c = Lose(Assist, 4);

            Assert.AreEqual(MetaUnlocks.None, c.NewUnlocks);
            Assert.AreEqual(MetaUnlocks.None, _meta.Unlocked);
        }

        [Test]
        public void QuitAfterReachingIsland5_UnlocksSecondWind()
        {
            MetaChanges c = _meta.OnRunEnded(RunResult.Quit, 5, 300f, Cursor);

            Assert.AreEqual(MetaUnlocks.SecondWind, c.NewUnlocks);
        }

        [Test]
        public void FirstVictory_UnlocksBigChests_AndSecondWind()
        {
            MetaChanges c = Win(Assist);

            Assert.AreEqual(MetaUnlocks.SecondWind | MetaUnlocks.BigChests, c.NewUnlocks);
            Assert.AreEqual(1, _meta.Wins);
        }

        [Test]
        public void SecondVictoryInTheSameAimMode_NoDividends()
        {
            Win(Assist);
            MetaChanges c = Win(Assist);

            Assert.AreEqual(MetaUnlocks.None, c.NewUnlocks);
            Assert.IsFalse(_meta.Unlocked.HasFlag(MetaUnlocks.Dividends));
        }

        [Test]
        public void VictoryInTheOtherAimMode_UnlocksDividends()
        {
            Win(Assist);
            MetaChanges c = Win(Cursor);

            Assert.AreEqual(MetaUnlocks.Dividends, c.NewUnlocks);
        }

        [Test]
        public void VictoryInTheOtherAimMode_WorksFromCursorFirst()
        {
            Win(Cursor);
            Lose(Cursor, 3);
            MetaChanges c = Win(Assist);

            Assert.AreEqual(MetaUnlocks.Dividends, c.NewUnlocks);
        }

        [Test]
        public void DefeatInTheOtherAimMode_NoDividends()
        {
            Win(Assist);
            Lose(Cursor, 9);

            Assert.IsFalse(_meta.Unlocked.HasFlag(MetaUnlocks.Dividends));
        }

        [Test]
        public void StartingRelicChoices_InFixedOrder()
        {
            Win(Cursor);
            Win(Assist);

            CollectionAssert.AreEqual(new[] { ItemCatalog.SecondWind, ItemCatalog.BigChests, ItemCatalog.Dividends },
                _meta.StartingRelicChoices());
        }

        [Test]
        public void AnUnlock_IsReportedOnlyOnce()
        {
            Lose(Assist, 5);
            MetaChanges again = Lose(Assist, 6);

            Assert.AreEqual(MetaUnlocks.None, again.NewUnlocks);
            Assert.AreEqual(MetaUnlocks.SecondWind, _meta.Unlocked);
        }

        // --- personal bests ---

        [Test]
        public void BestIsland_IsPerAimMode()
        {
            Lose(Assist, 6);

            Assert.AreEqual(6, _meta.BestIsland(Assist));
            Assert.AreEqual(0, _meta.BestIsland(Cursor));
        }

        [Test]
        public void BestIsland_NeverGetsWorse()
        {
            Assert.IsTrue(Lose(Assist, 7).NewBestIsland);
            MetaChanges worse = Lose(Assist, 3);

            Assert.IsFalse(worse.NewBestIsland);
            Assert.AreEqual(7, _meta.BestIsland(Assist));
        }

        [Test]
        public void Victory_CountsAsIsland10_WithItsTime()
        {
            MetaChanges c = Win(Assist, 552.5f);

            Assert.AreEqual(MetaProgress.VictoryIsland, _meta.BestIsland(Assist));
            Assert.AreEqual(10, MetaProgress.VictoryIsland);
            Assert.AreEqual(552.5f, _meta.BestWinSeconds(Assist), 1e-4f);
            Assert.IsTrue(c.NewBestIsland);
            Assert.IsTrue(c.NewBestTime);
        }

        [Test]
        public void BestWinTime_OnlyFasterReplacesIt()
        {
            Win(Assist, 600f);

            MetaChanges slower = Win(Assist, 650f);
            Assert.IsFalse(slower.NewBestTime);
            Assert.IsFalse(slower.NewBestIsland, "a second Victory is no new best island");
            Assert.AreEqual(600f, _meta.BestWinSeconds(Assist), 1e-4f);

            MetaChanges faster = Win(Assist, 550f);
            Assert.IsTrue(faster.NewBestTime);
            Assert.AreEqual(550f, _meta.BestWinSeconds(Assist), 1e-4f);
        }

        [Test]
        public void BestWinTime_IsPerAimMode()
        {
            Win(Assist, 600f);

            Assert.AreEqual(0f, _meta.BestWinSeconds(Cursor));
            Assert.IsTrue(Win(Cursor, 700f).NewBestTime);
            Assert.AreEqual(600f, _meta.BestWinSeconds(Assist), 1e-4f);
        }

        [Test]
        public void QuitRun_CountsIslandReached_NeverTime()
        {
            MetaChanges c = _meta.OnRunEnded(RunResult.Quit, 6, 300f, Assist);

            Assert.IsTrue(c.NewBestIsland);
            Assert.IsFalse(c.NewBestTime);
            Assert.AreEqual(6, _meta.BestIsland(Assist));
            Assert.AreEqual(0f, _meta.BestWinSeconds(Assist));
        }

        [Test]
        public void DefeatOnIsland9_IsNotAVictory()
        {
            MetaChanges c = Lose(Assist, 9);

            Assert.AreEqual(9, _meta.BestIsland(Assist));
            Assert.AreEqual(0f, _meta.BestWinSeconds(Assist));
            Assert.IsFalse(c.NewBestTime);
            Assert.AreEqual(0, _meta.Wins);
        }

        [Test]
        public void RunsAndWins_AreCounted()
        {
            Lose(Assist, 2);
            _meta.OnRunEnded(RunResult.Quit, 1, 30f, Cursor);
            Win(Assist);

            Assert.AreEqual(3, _meta.Runs);
            Assert.AreEqual(1, _meta.Wins);
            Assert.IsTrue(_meta.HasPlayed);
        }

        // --- storage ---

        [Test]
        public void RunEnd_SavesOnce_WithTheVersion()
        {
            Lose(Assist, 3);

            Assert.AreEqual(1, _store.SaveCount);
            Assert.AreEqual(MetaProgress.Version, _store.GetInt("hh.meta.v"));
            Assert.AreEqual(1, MetaProgress.Version);
        }

        [Test]
        public void Progress_SurvivesAReload()
        {
            Win(Assist, 580f);
            Lose(Cursor, 4);

            var reloaded = new MetaProgress(_store);

            Assert.AreEqual(2, reloaded.Runs);
            Assert.AreEqual(1, reloaded.Wins);
            Assert.AreEqual(MetaUnlocks.SecondWind | MetaUnlocks.BigChests, reloaded.Unlocked);
            Assert.AreEqual(MetaProgress.VictoryIsland, reloaded.BestIsland(Assist));
            Assert.AreEqual(580f, reloaded.BestWinSeconds(Assist), 1e-4f);
            Assert.AreEqual(4, reloaded.BestIsland(Cursor));
            // The first Victory's aim mode is remembered: a Cursor win now unlocks Dividends.
            Assert.AreEqual(MetaUnlocks.Dividends, reloaded.OnRunEnded(RunResult.Victory, 9, 600f, Cursor).NewUnlocks);
        }

        [Test]
        public void EveryMetaKey_UsesThePrefix()
        {
            _store.SetInt(AimKey, 1);
            Win(Assist);
            Lose(Cursor, 5);

            foreach (string key in _store.Keys.Where(k => k != AimKey))
                StringAssert.StartsWith("hh.meta.", key);
        }

        [Test]
        public void VersionMismatch_ResetsMetaKeysOnly()
        {
            _store.SetInt("hh.meta.v", 2);
            _store.SetInt("hh.meta.runs", 5);
            _store.SetInt("hh.meta.unlocks", 7);
            _store.SetInt(AimKey, 1);

            var meta = new MetaProgress(_store);

            Assert.AreEqual(0, meta.Runs);
            Assert.AreEqual(MetaUnlocks.None, meta.Unlocked);
            Assert.IsFalse(_store.Keys.Any(k => k.StartsWith("hh.meta.") && k != "hh.meta.v"),
                "old meta keys are gone");
            Assert.AreEqual(1, _store.GetInt(AimKey));
        }

        [Test]
        public void Reset_ClearsOnlyMetaKeys()
        {
            _store.SetInt(AimKey, 1);
            _store.SetInt(TutorialKey, 1);
            Win(Assist);
            Win(Cursor);
            Lose(Assist, 4);
            int saves = _store.SaveCount;

            _meta.Reset();

            Assert.IsFalse(_store.Keys.Any(k => k.StartsWith("hh.meta.")), "no meta key left");
            Assert.AreEqual(1, _store.GetInt(AimKey));
            Assert.AreEqual(1, _store.GetInt(TutorialKey));
            Assert.AreEqual(saves + 1, _store.SaveCount);

            Assert.AreEqual(0, _meta.Runs);
            Assert.AreEqual(0, _meta.Wins);
            Assert.AreEqual(MetaUnlocks.None, _meta.Unlocked);
            Assert.AreEqual(0, _meta.BestIsland(Assist));
            Assert.AreEqual(0f, _meta.BestWinSeconds(Cursor));
            Assert.IsFalse(_meta.HasPlayed);
        }

        [Test]
        public void AfterReset_TheFirstVictoryCountsAgain()
        {
            Win(Assist);
            _meta.Reset();

            Assert.AreEqual(MetaUnlocks.SecondWind | MetaUnlocks.BigChests, Win(Cursor).NewUnlocks);
            Assert.AreEqual(MetaUnlocks.Dividends, Win(Assist).NewUnlocks);
        }
    }
}
