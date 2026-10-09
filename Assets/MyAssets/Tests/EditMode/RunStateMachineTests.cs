using System;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Run flow (autonomous plan R5): Intro -> Island -> IslandCleared -> OpenChest
    // -> (Shop before island 5) -> ChoosePortal -> Intro ... -> Boss (9) -> Victory,
    // or Defeat on a death without revives.
    public class RunStateMachineTests
    {
        private static RunStateMachine NewRun(int seed = 1)
        {
            var run = new RunStateMachine(RunParams.Default, new Random(seed));
            Assert.IsTrue(run.StartRun());
            return run;
        }

        private static void Advance(RunStateMachine run, float seconds)
        {
            const float step = 0.1f;
            for (float t = 0f; t < seconds; t += step) run.Tick(step);
        }

        // Kills every bot of the current island (Horde: waits out the timer).
        private static void ClearIsland(RunStateMachine run)
        {
            Assert.AreEqual(RunPhase.Island, run.Phase);
            if (run.Spec.Type == IslandType.Horde)
            {
                Advance(run, run.Params.HordeTime + 0.5f);
                return;
            }
            for (int i = 0; i < run.Spec.BotCount; i++) Assert.IsTrue(run.ReportBotKilled());
        }

        // From Intro of the current island to Intro of the next one (or Victory).
        private static void PlayIsland(RunStateMachine run)
        {
            Assert.AreEqual(RunPhase.Intro, run.Phase);
            Advance(run, run.Params.IntroTime + 0.2f);
            ClearIsland(run);
            Assert.AreEqual(RunPhase.IslandCleared, run.Phase);
            Advance(run, run.Params.ClearedTime + 0.2f);
            if (run.Phase == RunPhase.Victory) return;

            Assert.AreEqual(RunPhase.OpenChest, run.Phase);
            Assert.IsTrue(run.PickChestItem(0));
            if (run.Phase == RunPhase.Shop) Assert.IsTrue(run.LeaveShop());
            if (run.Phase == RunPhase.ChoosePortal) Assert.IsTrue(run.ChoosePortal(0));
        }

        [Test]
        public void StartRun_OnlyFromIdle_BeginsIntroOfIslandOne()
        {
            var run = new RunStateMachine(RunParams.Default, new Random(1));
            Assert.AreEqual(RunPhase.Idle, run.Phase);
            Assert.IsTrue(run.StartRun());
            Assert.AreEqual(RunPhase.Intro, run.Phase);
            Assert.AreEqual(1, run.Island);
            Assert.AreEqual(PortalRoller.FirstIsland, run.CurrentPortal);
            Assert.IsFalse(run.StartRun());
        }

        [Test]
        public void Intro_LastsIntroTime_ThenIsland()
        {
            var run = NewRun();
            Advance(run, run.Params.IntroTime - 0.3f);
            Assert.AreEqual(RunPhase.Intro, run.Phase);
            Advance(run, 0.5f);
            Assert.AreEqual(RunPhase.Island, run.Phase);
        }

        [Test]
        public void HappyPath_NineIslands_ShopBeforeFive_BossAtNine_Victory()
        {
            var run = NewRun(7);
            bool sawShop = false;
            for (int island = 1; island <= 9; island++)
            {
                Assert.AreEqual(island, run.Island);
                Assert.AreEqual(RunPhase.Intro, run.Phase);
                if (island == 9) Assert.AreEqual(IslandType.Boss, run.Spec.Type);
                else Assert.AreNotEqual(IslandType.Boss, run.Spec.Type);

                Advance(run, run.Params.IntroTime + 0.2f);
                ClearIsland(run);
                Advance(run, run.Params.ClearedTime + 0.2f);
                if (island == 9) break;

                Assert.AreEqual(RunPhase.OpenChest, run.Phase);
                Assert.Greater(run.ChestChoices.Count, 0);
                Assert.IsTrue(run.PickChestItem(0));

                if (island == 4)
                {
                    Assert.AreEqual(RunPhase.Shop, run.Phase);
                    sawShop = true;
                    Assert.IsTrue(run.LeaveShop());
                }
                else Assert.AreNotEqual(RunPhase.Shop, run.Phase);

                if (island == 8)
                {
                    // No portal choice before the boss.
                    Assert.AreEqual(RunPhase.Intro, run.Phase);
                    continue;
                }
                Assert.AreEqual(RunPhase.ChoosePortal, run.Phase);
                Assert.GreaterOrEqual(run.Portals.Count, 2);
                Assert.IsTrue(run.ChoosePortal(0));
            }

            Assert.AreEqual(RunPhase.Victory, run.Phase);
            Assert.AreEqual(9, run.IslandsCleared);
            Assert.AreEqual(8, run.Inventory.TotalLevels);
        }

        [Test]
        public void FirstChest_IsTheDamageChest()
        {
            var run = NewRun();
            Advance(run, run.Params.IntroTime + 0.2f);
            ClearIsland(run);
            Advance(run, run.Params.ClearedTime + 0.2f);
            Assert.AreEqual(RunPhase.OpenChest, run.Phase);
            Assert.AreEqual(ChestType.Damage, run.OpenedChest);
            foreach (ItemDefinition item in run.ChestChoices)
                Assert.That(item.Theme == ItemTheme.Damage || item.Theme == ItemTheme.Critical, item.Id);
        }

        [Test]
        public void PickChestItem_AddsToInventory_RejectsBadIndexAndWrongPhase()
        {
            var run = NewRun();
            Assert.IsFalse(run.PickChestItem(0)); // still Intro
            Advance(run, run.Params.IntroTime + 0.2f);
            ClearIsland(run);
            Advance(run, run.Params.ClearedTime + 0.2f);
            string id = run.ChestChoices[1].Id;
            Assert.IsFalse(run.PickChestItem(-1));
            Assert.IsFalse(run.PickChestItem(run.ChestChoices.Count));
            Assert.IsTrue(run.PickChestItem(1));
            Assert.AreEqual(1, run.Inventory.Level(id));
            Assert.AreEqual(RunPhase.ChoosePortal, run.Phase);
        }

        [Test]
        public void ChoosePortal_SetsNextIsland_AndRejectsBadIndex()
        {
            var run = NewRun(3);
            PlayIslandUntilPortal(run);
            Portal second = run.Portals[1];
            Assert.IsFalse(run.ChoosePortal(5));
            Assert.IsTrue(run.ChoosePortal(1));
            Assert.AreEqual(2, run.Island);
            Assert.AreEqual(second, run.CurrentPortal);
            Assert.AreEqual(second.Island, run.Spec.Type);
            Assert.AreEqual(RunPhase.Intro, run.Phase);
        }

        private static void PlayIslandUntilPortal(RunStateMachine run)
        {
            Advance(run, run.Params.IntroTime + 0.2f);
            ClearIsland(run);
            Advance(run, run.Params.ClearedTime + 0.2f);
            Assert.IsTrue(run.PickChestItem(0));
            Assert.AreEqual(RunPhase.ChoosePortal, run.Phase);
        }

        [Test]
        public void Arena_ClearsOnlyWhenEveryBotIsDown()
        {
            var run = NewRun();
            for (int i = 0; i < 2; i++) PlayIsland(run); // now at island 3
            ForceIsland(run, IslandType.Arena);
            Advance(run, run.Params.IntroTime + 0.2f);
            Assert.AreEqual(2, run.Spec.BotCount);
            Assert.IsTrue(run.ReportBotKilled());
            Assert.AreEqual(RunPhase.Island, run.Phase);
            Assert.AreEqual(1, run.BotsRemaining);
            Assert.IsTrue(run.ReportBotKilled());
            Assert.AreEqual(RunPhase.IslandCleared, run.Phase);
            Assert.AreEqual(0, run.BotsRemaining);
        }

        [Test]
        public void ReportBotKilled_IgnoredOutsideIsland_CountedWhilePaused()
        {
            var run = NewRun();
            Assert.IsFalse(run.ReportBotKilled()); // Intro
            Advance(run, run.Params.IntroTime + 0.2f);
            run.Pause();
            Assert.IsTrue(run.ReportBotKilled()); // D8 (BR-8): a paused-frame kill counts
            run.Resume();
            Assert.AreEqual(1, run.Kills);
        }

        [Test]
        public void Horde_ClearsAfterTimer_KillsDoNotClear()
        {
            var run = NewRun();
            ForceIsland(run, IslandType.Horde);
            Advance(run, run.Params.IntroTime + 0.2f);
            for (int i = 0; i < 10; i++) Assert.IsTrue(run.ReportBotKilled());
            Assert.AreEqual(RunPhase.Island, run.Phase);
            Assert.AreEqual(10, run.Kills);
            Advance(run, run.Params.HordeTime - 1f);
            Assert.AreEqual(RunPhase.Island, run.Phase);
            Assert.That(run.PhaseRemaining, Is.InRange(0.5f, 1.5f));
            Advance(run, 1.5f);
            Assert.AreEqual(RunPhase.IslandCleared, run.Phase);
        }

        [Test]
        public void WantsSpawn_RespectsMaxAliveAndArenaCount()
        {
            var run = NewRun();
            Assert.IsFalse(run.WantsSpawn(0)); // Intro
            Advance(run, run.Params.IntroTime + 0.2f);
            // island 1: Arena with 1 bot
            Assert.IsTrue(run.WantsSpawn(0));
            Assert.IsFalse(run.WantsSpawn(1));

            ForceIsland(run, IslandType.Horde);
            Advance(run, run.Params.IntroTime + 0.2f);
            Assert.IsTrue(run.WantsSpawn(1));
            Assert.IsFalse(run.WantsSpawn(2));
            for (int i = 0; i < 20; i++) run.ReportBotKilled();
            Assert.IsTrue(run.WantsSpawn(0)); // horde keeps respawning

            ForceIsland(run, IslandType.Arena, 6);
            Advance(run, run.Params.IntroTime + 0.2f);
            Assert.AreEqual(3, run.Spec.BotCount);
            Assert.IsTrue(run.WantsSpawn(1));
            Assert.IsFalse(run.WantsSpawn(2)); // at most 2 alive
            run.ReportBotKilled();
            run.ReportBotKilled();
            Assert.IsTrue(run.WantsSpawn(0));
            Assert.IsFalse(run.WantsSpawn(1)); // 2 down + 1 alive = all 3 spawned
        }

        // Test hook: replaces the current island and restarts its intro.
        private static void ForceIsland(RunStateMachine run, IslandType type, int island = -1)
        {
            run.DebugSetIsland(island > 0 ? island : run.Island, new Portal(type, ChestType.Random));
            Assert.AreEqual(RunPhase.Intro, run.Phase);
        }

        [Test]
        public void IslandSpec_CountsAndEnemyScaling()
        {
            RunParams p = RunParams.Default;
            Assert.AreEqual(1, RunRules.Island(1, IslandType.Arena, p).BotCount);
            Assert.AreEqual(1, RunRules.Island(2, IslandType.Arena, p).BotCount);
            Assert.AreEqual(2, RunRules.Island(3, IslandType.Arena, p).BotCount);
            Assert.AreEqual(2, RunRules.Island(5, IslandType.Arena, p).BotCount);
            Assert.AreEqual(3, RunRules.Island(6, IslandType.Arena, p).BotCount);
            Assert.AreEqual(3, RunRules.Island(8, IslandType.Arena, p).BotCount);

            // bot max health x (1 + 0.15 x islandIndex), islandIndex = island - 1
            Assert.AreEqual(1f, RunRules.Island(1, IslandType.Arena, p).HealthMult, 1e-5f);
            Assert.AreEqual(1.45f, RunRules.Island(4, IslandType.Arena, p).HealthMult, 1e-5f);
            Assert.AreEqual(1.45f, RunRules.Island(4, IslandType.Horde, p).HealthMult, 1e-5f);
            // Round 4 (D2): every run bot hits x1.15, on top of the Elite multiplier.
            Assert.AreEqual(1.15f, RunRules.Island(4, IslandType.Arena, p).DamageMult, 1e-5f);

            IslandSpec elite = RunRules.Island(4, IslandType.Elite, p);
            Assert.AreEqual(1, elite.BotCount);
            Assert.AreEqual(1.45f * 3f, elite.HealthMult, 1e-4f);
            Assert.AreEqual(1.5f * 1.15f, elite.DamageMult, 1e-5f);

            IslandSpec boss = RunRules.Island(9, IslandType.Boss, p);
            Assert.AreEqual(1, boss.BotCount);
            Assert.AreEqual(2.2f * 5f, boss.HealthMult, 1e-4f); // D2: boss health x6 -> x5
            Assert.Less(boss.FireIntervalMult, 1f);
            Assert.AreEqual(1f, RunRules.Island(9, IslandType.Arena, p).FireIntervalMult, 1e-5f);

            Assert.AreEqual(2, RunRules.Island(3, IslandType.Horde, p).MaxAlive);
            Assert.AreEqual(1, elite.MaxAlive);
        }

        [Test]
        public void Death_WithoutRevive_IsDefeat()
        {
            var run = NewRun();
            // D8 (BC-1): a death outside the island is resolved too (RunStateFixesTests).
            Advance(run, run.Params.IntroTime + 0.2f);
            Assert.IsFalse(run.ReportPlayerDeath());
            Assert.AreEqual(RunPhase.Defeat, run.Phase);
            Assert.AreEqual(0, run.IslandsCleared);

            // terminal until the menu
            Advance(run, 30f);
            Assert.AreEqual(RunPhase.Defeat, run.Phase);
            run.ReturnToMenu();
            Assert.AreEqual(RunPhase.Idle, run.Phase);
            Assert.IsTrue(run.StartRun());
            Assert.AreEqual(0, run.Inventory.TotalLevels);
        }

        [Test]
        public void Death_WithRevive_ContinuesOnce()
        {
            var run = NewRun();
            Assert.IsTrue(run.Inventory.Add(ItemCatalog.SecondWind));
            Assert.AreEqual(1, run.RevivesLeft);
            Advance(run, run.Params.IntroTime + 0.2f);

            Assert.IsTrue(run.ReportPlayerDeath());
            Assert.AreEqual(RunPhase.Island, run.Phase);
            Assert.AreEqual(0, run.RevivesLeft);
            Assert.AreEqual(50f, run.ReviveHealth(100f), 1e-5f);

            Assert.IsFalse(run.ReportPlayerDeath());
            Assert.AreEqual(RunPhase.Defeat, run.Phase);
        }

        [Test]
        public void HealAfterClear_AddsBasePlusItem_CappedAtMax()
        {
            var run = NewRun();
            Assert.AreEqual(65f, run.HealAfterClear(40f, 100f), 1e-5f);
            Assert.AreEqual(100f, run.HealAfterClear(90f, 100f), 1e-5f);
            Assert.IsTrue(run.Inventory.Add(ItemCatalog.NanoRepair));
            Assert.AreEqual(75f, run.HealAfterClear(40f, 100f), 1e-5f);
        }

        [Test]
        public void Pause_StopsTimers()
        {
            var run = NewRun();
            Assert.IsTrue(run.Pause());
            Advance(run, 20f);
            Assert.AreEqual(RunPhase.Intro, run.Phase);
            Assert.AreEqual(0f, run.RunTime, 1e-5f);
            Assert.IsTrue(run.Resume());
            Advance(run, run.Params.IntroTime + 0.2f);
            Assert.AreEqual(RunPhase.Island, run.Phase);
            Assert.Greater(run.RunTime, run.Params.IntroTime);
        }

        [Test]
        public void Pause_NotInIdle()
        {
            var run = new RunStateMachine(RunParams.Default, new Random(1));
            Assert.IsFalse(run.Pause());
        }

        [Test]
        public void PhaseChanged_FiresOnEveryTransition()
        {
            var run = new RunStateMachine(RunParams.Default, new Random(1));
            int changes = 0;
            run.PhaseChanged += _ => changes++;
            run.StartRun();
            Advance(run, run.Params.IntroTime + 0.2f);
            run.ReportBotKilled();
            Assert.AreEqual(3, changes); // Intro, Island, IslandCleared
        }

        [Test]
        public void FixedTime_FitsTheBudget()
        {
            // Countdowns, clear pauses and the worst case of horde islands must
            // leave most of the 10 minutes for fighting and choosing.
            RunParams p = RunParams.Default;
            float fixedSeconds = 9 * (p.IntroTime + p.ClearedTime) + 8 * p.HordeTime;
            Assert.Less(fixedSeconds, 7f * 60f);
        }
    }
}
