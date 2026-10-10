using System;
using System.Collections.Generic;
using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Meta progression A wiring (round 4, S5 / D4): a STARTING RELIC choice
    // before island 1's intro, offered only when a relic is unlocked. Picking adds
    // the relic to the run's inventory; NONE starts empty-handed.
    public class StartRelicTests
    {
        private static readonly string[] AllRelics =
            { ItemCatalog.SecondWind, ItemCatalog.BigChests, ItemCatalog.Dividends };

        private static RunStateMachine NewRun(int seed = 1) => new RunStateMachine(RunParams.Default, new Random(seed));

        private static void Advance(RunStateMachine run, float seconds)
        {
            const float step = 0.1f;
            for (float t = 0f; t < seconds; t += step) run.Tick(step);
        }

        [Test]
        public void NoUnlockedRelics_StartsIslandOneIntro_AsBefore()
        {
            RunStateMachine run = NewRun();
            Assert.IsTrue(run.StartRun(new string[0]));
            Assert.AreEqual(RunPhase.Intro, run.Phase);
            Assert.AreEqual(1, run.Island);

            RunStateMachine nullList = NewRun();
            Assert.IsTrue(nullList.StartRun(null));
            Assert.AreEqual(RunPhase.Intro, nullList.Phase);
        }

        [Test]
        public void UnlockedRelics_OpenTheStartRelicChoice_BeforeIslandOne()
        {
            RunStateMachine run = NewRun();
            var phases = new List<RunPhase>();
            run.PhaseChanged += phases.Add;

            Assert.IsTrue(run.StartRun(AllRelics));

            Assert.AreEqual(RunPhase.StartRelic, run.Phase);
            CollectionAssert.AreEqual(new[] { RunPhase.StartRelic }, phases);
            Assert.AreEqual(1, run.Island);
            Assert.AreEqual(PortalRoller.FirstIsland, run.CurrentPortal);
            Assert.AreEqual(3, run.StartRelicChoices.Count);
            for (int i = 0; i < AllRelics.Length; i++)
                Assert.AreEqual(AllRelics[i], run.StartRelicChoices[i].Id);
            Assert.AreEqual(0, run.Inventory.TotalLevels);
        }

        [Test]
        public void UnknownIds_AreSkipped_AndOnlyUnknownIdsMeanNoChoice()
        {
            RunStateMachine run = NewRun();
            Assert.IsTrue(run.StartRun(new[] { "no_such_item", ItemCatalog.BigChests, null }));
            Assert.AreEqual(RunPhase.StartRelic, run.Phase);
            Assert.AreEqual(1, run.StartRelicChoices.Count);
            Assert.AreEqual(ItemCatalog.BigChests, run.StartRelicChoices[0].Id);

            RunStateMachine none = NewRun();
            Assert.IsTrue(none.StartRun(new[] { "no_such_item" }));
            Assert.AreEqual(RunPhase.Intro, none.Phase);
        }

        [Test]
        public void PickStartRelic_AddsItToTheInventory_ThenIslandOneIntro()
        {
            RunStateMachine run = NewRun();
            run.StartRun(AllRelics);
            var phases = new List<RunPhase>();
            run.PhaseChanged += phases.Add;

            Assert.IsTrue(run.PickStartRelic(0));

            Assert.AreEqual(1, run.Inventory.Level(ItemCatalog.SecondWind));
            Assert.AreEqual(1, run.Inventory.TotalLevels);
            Assert.AreEqual(1, run.RevivesLeft); // Second Wind start: one revive from island 1
            Assert.AreEqual(RunPhase.Intro, run.Phase);
            CollectionAssert.AreEqual(new[] { RunPhase.Intro }, phases);
            Assert.AreEqual(1, run.Island);
            Assert.AreEqual(PortalRoller.FirstIsland, run.CurrentPortal);
            Assert.AreEqual(0, run.StartRelicChoices.Count);
        }

        [Test]
        public void SkipStartRelic_StartsWithNothing()
        {
            RunStateMachine run = NewRun();
            run.StartRun(AllRelics);

            Assert.IsTrue(run.SkipStartRelic());

            Assert.AreEqual(RunPhase.Intro, run.Phase);
            Assert.AreEqual(0, run.Inventory.TotalLevels);
            Assert.AreEqual(0, run.StartRelicChoices.Count);
        }

        [Test]
        public void StartRelicChoice_RejectsBadIndexAndOtherPhases()
        {
            RunStateMachine run = NewRun();
            Assert.IsFalse(run.PickStartRelic(0)); // Idle
            Assert.IsFalse(run.SkipStartRelic());

            run.StartRun(new[] { ItemCatalog.BigChests, ItemCatalog.Dividends });
            Assert.IsFalse(run.PickStartRelic(-1));
            Assert.IsFalse(run.PickStartRelic(2));
            Assert.AreEqual(RunPhase.StartRelic, run.Phase);

            Assert.IsTrue(run.PickStartRelic(1));
            Assert.IsFalse(run.PickStartRelic(0)); // Intro now
            Assert.IsFalse(run.SkipStartRelic());
            Assert.AreEqual(1, run.Inventory.TotalLevels);
        }

        [Test]
        public void StartRelic_WaitsForTheChoice_AndPausesLikeAChest()
        {
            RunStateMachine run = NewRun();
            run.StartRun(AllRelics);

            Advance(run, 60f);
            Assert.AreEqual(RunPhase.StartRelic, run.Phase);
            Assert.IsFalse(run.WantsSpawn(0));

            // Round 5 (F1-2): RunTime no longer runs in StartRelic at all, so the
            // pause check reads the phase clock, which still does.
            Assert.IsTrue(run.Pause());
            float before = run.PhaseTime;
            Advance(run, 5f);
            Assert.AreEqual(before, run.PhaseTime, 1e-5f, "paused: the phase clock stands");
            Assert.IsTrue(run.Resume());
            Assert.AreEqual(RunPhase.StartRelic, run.Phase);

            Advance(run, 1f);
            Assert.AreEqual(before + 1f, run.PhaseTime, 0.15f, "resumed: the phase clock runs again");
            Assert.AreEqual(0f, run.RunTime, 1e-5f, "the run clock waits for island 1");
        }

        // Round 5 (F1-2, D3): deliberately changed. Round 4 (S5-2) counted the
        // choice like every other one; the screen has no time limit, so it now
        // stays out of the run time and the best Victory time.
        [Test]
        public void RunTime_LeavesOutTheStartRelicChoice()
        {
            RunStateMachine run = NewRun();
            run.StartRun(AllRelics);
            Advance(run, 2f);
            Assert.AreEqual(0f, run.RunTime, 1e-5f);

            run.PickStartRelic(2);
            Advance(run, 1f);
            Assert.AreEqual(1f, run.RunTime, 0.15f, "the run clock starts with island 1's intro");
        }

        [Test]
        public void ChestChoices_StillCountInRunTime()
        {
            RunStateMachine run = NewRun();
            run.StartRun(null);
            Advance(run, run.Params.IntroTime + 0.1f);
            while (run.Phase == RunPhase.Island) run.ReportBotKilled();
            Advance(run, run.Params.ClearedTime + 0.1f);
            Assert.AreEqual(RunPhase.OpenChest, run.Phase);

            float before = run.RunTime;
            Advance(run, 2f);
            Assert.AreEqual(before + 2f, run.RunTime, 0.15f);
        }

        [Test]
        public void BestVictoryTime_LeavesOutTheStartRelicChoice()
        {
            RunStateMachine run = NewRun();
            run.StartRun(AllRelics);
            Advance(run, 30f); // a long look at the relic cards
            Assert.IsTrue(run.SkipStartRelic());
            run.DebugSetIsland(RunRules.BossIsland, RunStateMachine.BossPortal);
            Advance(run, run.Params.IntroTime + 0.1f);
            Assert.AreEqual(RunPhase.Island, run.Phase);
            while (run.Phase == RunPhase.Island) run.ReportBotKilled();
            Advance(run, run.Params.ClearedTime + 0.1f);
            Assert.AreEqual(RunPhase.Victory, run.Phase);

            var meta = new MetaProgress(new MemoryKeyValueStore());
            meta.OnRunEnded(RunResult.Victory, run.Island, run.RunTime, "Assist");
            Assert.Less(meta.BestWinSeconds("Assist"), 10f);
            Assert.Greater(meta.BestWinSeconds("Assist"), 0f);
        }

        [Test]
        public void PickedRelic_IsNeverOfferedAgainInThatRun()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                RunStateMachine run = NewRun(seed);
                run.StartRun(AllRelics);
                run.PickStartRelic(1); // Big Chests
                for (int roll = 0; roll < 5; roll++)
                    foreach (ItemDefinition item in ChestRoller.Roll(ChestType.Relic, run.Inventory, new Random(seed * 7 + roll)))
                        Assert.AreNotEqual(ItemCatalog.BigChests, item.Id, $"seed {seed}");
            }
        }

        [Test]
        public void ReturnToMenu_FromTheChoice_ClearsIt_AndANewRunStartsFresh()
        {
            RunStateMachine run = NewRun();
            run.StartRun(AllRelics);
            run.ReturnToMenu();
            Assert.AreEqual(RunPhase.Idle, run.Phase);
            Assert.AreEqual(0, run.StartRelicChoices.Count);

            Assert.IsTrue(run.StartRun(new[] { ItemCatalog.Dividends }));
            Assert.AreEqual(RunPhase.StartRelic, run.Phase);
            Assert.AreEqual(1, run.StartRelicChoices.Count);
            Assert.AreEqual(1, run.Island);
        }

        [Test]
        public void NewRunAfterAWin_StartsAtIslandOne_InTheChoice()
        {
            RunStateMachine run = NewRun();
            run.StartRun(null);
            run.DebugSetIsland(9, RunStateMachine.BossPortal);
            run.ReturnToMenu();

            run.StartRun(AllRelics);
            Assert.AreEqual(1, run.Island); // a quit here counts island 1 for the meta bests
            Assert.AreEqual(IslandType.Arena, run.Spec.Type);
        }
    }
}
