using System;
using HandHero.Core;
using NUnit.Framework;
using UnityEngine;
using Random = System.Random;

namespace HandHero.Tests
{
    // P8 (D8): run death and state fixes, and the Core zero-value guards.
    public class RunStateFixesTests
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

        private static RunStateMachine OnIsland()
        {
            RunStateMachine run = NewRun();
            Advance(run, run.Params.IntroTime + 0.2f);
            Assert.AreEqual(RunPhase.Island, run.Phase);
            return run;
        }

        // ---- Auto respawn off during a run (BR-5, BC-1) ----

        [Test]
        public void HealthModel_AutoRespawnOff_StaysDead()
        {
            var m = new HeroHealthModel(HealthParams.Default) { AutoRespawn = false };
            m.ApplyDamage(1000f);
            Assert.IsTrue(m.IsDead);

            for (int i = 0; i < 100; i++) Assert.IsFalse(m.Tick(0.1f));
            Assert.IsTrue(m.IsDead);

            m.Reset(); // Revive / ResetHealth still bring the hero back
            Assert.IsFalse(m.IsDead);
        }

        [Test]
        public void HealthModel_AutoRespawnOnByDefault()
        {
            var m = new HeroHealthModel(HealthParams.Default);
            Assert.IsTrue(m.AutoRespawn);
            m.ApplyDamage(1000f);
            bool respawned = false;
            for (int i = 0; i < 40 && !respawned; i++) respawned = m.Tick(0.1f);
            Assert.IsTrue(respawned);
        }

        // ---- A death is resolved in every non-Idle phase (BC-1) ----

        [Test]
        public void DeathWhilePaused_WithoutRevive_IsDefeat()
        {
            RunStateMachine run = OnIsland();
            Assert.IsTrue(run.Pause());
            Assert.IsFalse(run.ReportPlayerDeath());
            Assert.AreEqual(RunPhase.Defeat, run.Phase);
        }

        [Test]
        public void DeathWhilePaused_WithRevive_Revives()
        {
            RunStateMachine run = OnIsland();
            Assert.IsTrue(run.Inventory.Add(ItemCatalog.SecondWind));
            Assert.IsTrue(run.Pause());
            Assert.IsTrue(run.ReportPlayerDeath());
            Assert.AreEqual(0, run.RevivesLeft);
            Assert.AreEqual(RunPhase.Island, run.Phase);
        }

        [Test]
        public void DeathOutsideTheFight_IsStillResolved()
        {
            RunStateMachine run = NewRun();
            Assert.AreEqual(RunPhase.Intro, run.Phase);
            Assert.IsFalse(run.ReportPlayerDeath());
            Assert.AreEqual(RunPhase.Defeat, run.Phase);
        }

        [Test]
        public void DeathWhenIdleOrOver_IsIgnored()
        {
            var idle = new RunStateMachine(RunParams.Default, new Random(1));
            Assert.IsFalse(idle.ReportPlayerDeath());
            Assert.AreEqual(RunPhase.Idle, idle.Phase);

            RunStateMachine run = OnIsland();
            run.ReportPlayerDeath();
            Assert.AreEqual(RunPhase.Defeat, run.Phase);
            int phaseChanges = 0;
            run.PhaseChanged += _ => phaseChanges++;
            Assert.IsFalse(run.ReportPlayerDeath());
            Assert.AreEqual(RunPhase.Defeat, run.Phase);
            Assert.AreEqual(0, phaseChanges, "no second Defeat");
        }

        // ---- Kills in a paused frame count (BR-8) ----

        [Test]
        public void KillWhilePaused_IsCounted()
        {
            RunStateMachine run = OnIsland();
            Assert.IsTrue(run.Pause());
            Assert.IsTrue(run.ReportBotKilled());
            Assert.AreEqual(1, run.Kills);
        }

        // ---- Zero portals: fallback (BC-6) ----

        [Test]
        public void PortalRoll_EmptyPools_GivesTheFallbackPortal()
        {
            var empty = new Inventory(_ => null); // every chest pool is empty
            var portals = PortalRoller.Roll(4, empty, new Random(3));
            Assert.AreEqual(1, portals.Count);
            Assert.AreEqual(PortalRoller.Fallback, portals[0]);
            Assert.AreEqual(IslandType.Arena, PortalRoller.Fallback.Island);
            Assert.AreEqual(ChestType.Random, PortalRoller.Fallback.Chest);
        }

        // ---- MaxAlive <= 0 (BC-7) ----

        [Test]
        public void MaxAliveZero_TreatedAsOne()
        {
            RunParams p = RunParams.Default;
            p.MaxAlive = 0;
            Assert.AreEqual(1, RunRules.Island(4, IslandType.Arena, p).MaxAlive);
            Assert.AreEqual(1, RunRules.Island(4, IslandType.Horde, p).MaxAlive);
            p.MaxAlive = -3;
            Assert.AreEqual(1, RunRules.Island(7, IslandType.Arena, p).MaxAlive);
        }

        // ---- TriggerGesture settle 0 (BC-8) ----

        [Test]
        public void Trigger_ZeroSettleTime_PullWithoutGripFires()
        {
            var t = new TriggerGesture();
            TriggerState s = t.Step(true, 1f, 0f, 0.7f, 0.45f, 0.7f, 0.45f, 0f, 0.02f);
            Assert.IsTrue(s.Fired);
            Assert.IsTrue(s.Held);
        }

        // ---- Tutorial telegraph time 0 (BC-9) ----

        [Test]
        public void Tutorial_ZeroTelegraphTime_ProgressIsNeverNaN()
        {
            TutorialParams p = TutorialParams.Default;
            var s = new TutorialSequencer(p);
            p.DodgeTelegraphTime = 0f;
            s.Params = p;
            Assert.AreEqual(0f, s.TelegraphProgress);

            // The guard itself, independent of the step reached.
            Assert.IsFalse(float.IsNaN(TutorialSequencer.Progress(0f, 0f)));
            Assert.AreEqual(1f, TutorialSequencer.Progress(0f, 0f));
            Assert.AreEqual(0.5f, TutorialSequencer.Progress(0.3f, 0.6f), 1e-5f);
        }

        // ---- Wrist press needs an open hand (BC-5) ----

        [Test]
        public void WristPress_WithAFist_DoesNotPause()
        {
            WristMenuParams p = WristMenuParams.Default;
            var g = new WristMenuGesture();
            const float dt = 1f / 72f;
            for (float t = 0f; t <= p.ShowDelay + 0.05f; t += dt) g.Step(true, 1f, 0f, 0f, dt, p);
            Assert.IsTrue(g.IsVisible);

            // Closing into a clutch fist reads as a pinch too.
            Assert.IsFalse(g.Step(true, 1f, 0.9f, 1f, dt, p));
        }

        [Test]
        public void WristPress_OpenHand_StillPauses()
        {
            WristMenuParams p = WristMenuParams.Default;
            var g = new WristMenuGesture();
            const float dt = 1f / 72f;
            for (float t = 0f; t <= p.ShowDelay + 0.05f; t += dt) g.Step(true, 1f, 0f, 0f, dt, p);
            Assert.IsTrue(g.Step(true, 1f, 0f, 1f, dt, p));
        }

        // ---- Teams (BR-6) ----

        [Test]
        public void Teams_SameTeamCannotHit()
        {
            Assert.IsFalse(HeroTeams.CanHit(HeroTeam.Bot, HeroTeam.Bot));
            Assert.IsTrue(HeroTeams.CanHit(HeroTeam.Bot, HeroTeam.Player));
            Assert.IsTrue(HeroTeams.CanHit(HeroTeam.Player, HeroTeam.Bot));
        }
    }
}
