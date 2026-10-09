using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Meta progression A text (round 4, S5): the best line in the main menu, the
    // NEW BEST / UNLOCKED lines under the run summary, the starting relic chest.
    public class MetaTextTests
    {
        [Test]
        public void BestLine_HiddenBeforeTheFirstRunInThisAimMode()
        {
            Assert.AreEqual("", MetaText.BestLine(0, 0f));
        }

        [Test]
        public void BestLine_IslandOnly_UntilAWin()
        {
            Assert.AreEqual("BEST  ISLAND 7", MetaText.BestLine(7, 0f));
        }

        [Test]
        public void BestLine_AfterAWin_ShowsTheLastIslandAndTheFastestWin()
        {
            Assert.AreEqual("BEST  ISLAND 9  -  WIN 9:12", MetaText.BestLine(MetaProgress.VictoryIsland, 551.4f));
        }

        [Test]
        public void MenuBanner_HintBeforeTheFirstRun_BestLineAfter()
        {
            Assert.AreEqual("HAND HERO\n<size=50%>point and pinch to choose</size>", MetaText.MenuBanner(""));
            Assert.AreEqual("HAND HERO\n<size=50%>BEST  ISLAND 4</size>", MetaText.MenuBanner("BEST  ISLAND 4"));
        }

        [Test]
        public void MenuKey_ChangesWithEveryShownValue()
        {
            Assert.AreEqual(MetaText.MenuKey(7, 551.4f), MetaText.MenuKey(7, 551.9f)); // both show 9:12
            Assert.AreNotEqual(MetaText.MenuKey(7, 0f), MetaText.MenuKey(8, 0f));
            Assert.AreNotEqual(MetaText.MenuKey(10, 551f), MetaText.MenuKey(10, 549f));
            Assert.AreNotEqual(MetaText.MenuKey(0, 0f), new HudKey(HudKeyKind.MatchBanner, (int)MatchPhase.Menu));
        }

        [Test]
        public void EndLines_EmptyWhenNothingChanged()
        {
            Assert.AreEqual("", MetaText.EndLines(new MetaChanges()));
        }

        [Test]
        public void EndLines_NewBest_ForAnIslandOrATime()
        {
            Assert.AreEqual("NEW BEST", MetaText.EndLines(new MetaChanges { NewBestIsland = true }));
            Assert.AreEqual("NEW BEST", MetaText.EndLines(new MetaChanges { NewBestTime = true }));
            Assert.AreEqual("NEW BEST", MetaText.EndLines(new MetaChanges { NewBestIsland = true, NewBestTime = true }));
        }

        [Test]
        public void EndLines_OneUnlockLinePerRelic_InCardOrder()
        {
            var changes = new MetaChanges
            {
                NewBestIsland = true,
                NewUnlocks = MetaUnlocks.BigChests | MetaUnlocks.SecondWind,
            };
            Assert.AreEqual("NEW BEST\nUNLOCKED: SECOND WIND START\nUNLOCKED: BIG CHESTS START",
                MetaText.EndLines(changes));
            Assert.AreEqual("UNLOCKED: DIVIDENDS START",
                MetaText.EndLines(new MetaChanges { NewUnlocks = MetaUnlocks.Dividends }));
        }

        [Test]
        public void ChangesKey_DiffersForEveryShownChange()
        {
            Assert.AreEqual(0, MetaText.ChangesKey(new MetaChanges()));
            int island = MetaText.ChangesKey(new MetaChanges { NewBestIsland = true });
            int unlock = MetaText.ChangesKey(new MetaChanges { NewUnlocks = MetaUnlocks.SecondWind });
            int both = MetaText.ChangesKey(new MetaChanges { NewBestIsland = true, NewUnlocks = MetaUnlocks.SecondWind });
            Assert.AreNotEqual(0, island);
            Assert.AreNotEqual(island, unlock);
            Assert.AreNotEqual(unlock, both);
        }

        [Test]
        public void EndBanner_PutsTheMetaLinesUnderTheSummary_AtTheSummarySize()
        {
            string banner = RunHudText.EndBanner(true, 9, 8, 522f, "NEW BEST\nUNLOCKED: BIG CHESTS START");
            Assert.AreEqual("VICTORY\n<size=50%>9 ISLANDS    8 ITEMS    8:42</size>"
                + "\n<size=50%>NEW BEST\nUNLOCKED: BIG CHESTS START</size>", banner);
            Assert.AreEqual(RunHudText.EndBanner(false, 2, 1, 65f), RunHudText.EndBanner(false, 2, 1, 65f, ""));
        }

        [Test]
        public void EndKey_IncludesTheMetaChanges()
        {
            Assert.AreNotEqual(RunHudText.EndKey(true, 9, 8, 522f, 0), RunHudText.EndKey(true, 9, 8, 522f, 3));
            Assert.AreEqual(RunHudText.EndKey(true, 9, 8, 522f), RunHudText.EndKey(true, 9, 8, 522f, 0));
        }

        [Test]
        public void StartRelicChest_HeaderAndNoneCard()
        {
            Assert.AreEqual("STARTING RELIC", RunChoiceText.StartRelicHeader);
            StringAssert.StartsWith("<b>NONE</b>", RunChoiceText.NoneCard);
        }
    }
}
