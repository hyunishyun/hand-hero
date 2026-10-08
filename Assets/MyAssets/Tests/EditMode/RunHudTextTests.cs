using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // RUN HUD text (R10): status line, countdown banner and the end screens.
    public class RunHudTextTests
    {
        [Test]
        public void Objective_CountsBotsLeft()
        {
            Assert.AreEqual("2 BOTS LEFT", RunHudText.Objective(IslandType.Arena, 2, 0f));
            Assert.AreEqual("1 BOT LEFT", RunHudText.Objective(IslandType.Elite, 1, 0f));
            Assert.AreEqual("BOSS", RunHudText.Objective(IslandType.Boss, 1, 0f));
        }

        [Test]
        public void Objective_HordeShowsSurviveSeconds()
        {
            Assert.AreEqual("SURVIVE 0:32", RunHudText.Objective(IslandType.Horde, 0, 31.2f));
        }

        [Test]
        public void Status_HasIslandTypeObjectiveCrystalsAndHealth()
        {
            string s = RunHudText.Status(3, IslandType.Arena, "2 BOTS LEFT", 150, 79.6f, 120f);
            StringAssert.Contains("ISLAND 3/9", s);
            StringAssert.Contains("ARENA", s);
            StringAssert.Contains("2 BOTS LEFT", s);
            StringAssert.Contains("150 CRYSTALS", s);
            StringAssert.Contains("HP 80/120", s);
        }

        [Test]
        public void Status_DeadShowsZeroHealth()
        {
            StringAssert.Contains("HP 0/100", RunHudText.Status(1, IslandType.Arena, "", 0, 0f, 100f));
        }

        [Test]
        public void IntroBanner_CountsDown()
        {
            string b = RunHudText.IntroBanner(1, IslandType.Arena, 2.4f);
            StringAssert.Contains("ISLAND 1/9", b);
            StringAssert.Contains("ARENA", b);
            StringAssert.EndsWith("3", b);
        }

        [Test]
        public void EndBanner_SummarizesTheRun()
        {
            string win = RunHudText.EndBanner(true, 9, 8, 522f);
            StringAssert.StartsWith("VICTORY", win);
            StringAssert.Contains("9 ISLANDS", win);
            StringAssert.Contains("8 ITEMS", win);
            StringAssert.Contains("8:42", win);

            string loss = RunHudText.EndBanner(false, 1, 1, 65f);
            StringAssert.StartsWith("DEFEAT", loss);
            StringAssert.Contains("1 ISLAND ", loss);
            StringAssert.Contains("1 ITEM ", loss);
            StringAssert.Contains("1:05", loss);
        }
    }
}
