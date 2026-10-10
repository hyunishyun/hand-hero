using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // Round 5, T4 (D6): demo mode rules - the Demo match phase, the invulnerable
    // player, the demo bots and perf flushing while recording.
    public class DemoModeTests
    {
        private static MatchStateMachine InMenu()
        {
            var m = new MatchStateMachine(MatchParams.Default);
            m.Tick(0.016f, 1f, 1f);
            Assert.AreEqual(MatchPhase.Menu, m.Phase);
            return m;
        }

        [Test]
        public void StartDemo_OnlyFromMenu()
        {
            var m = InMenu();
            Assert.IsTrue(m.StartDemo());
            Assert.AreEqual(MatchPhase.Demo, m.Phase);
            Assert.IsFalse(m.StartDemo());
            Assert.IsFalse(m.StartMatch(false), "no Quick Match while the demo is on");
            Assert.IsFalse(m.StartRun(), "no run while the demo is on");
            Assert.AreEqual(MatchPhase.Demo, m.Phase);

            var running = InMenu();
            running.StartRun();
            Assert.IsFalse(running.StartDemo(), "no demo while a run is on");
            Assert.AreEqual(MatchPhase.Run, running.Phase);

            var fighting = InMenu();
            fighting.StartMatch(false);
            Assert.IsFalse(fighting.StartDemo(), "no demo while a Quick Match is on");
            Assert.AreEqual(MatchPhase.Countdown, fighting.Phase);
        }

        [Test]
        public void Demo_HasNoTimer_AndIgnoresKOs()
        {
            var m = InMenu();
            m.StartDemo();
            // Ten minutes with the player at zero health: nothing ends the demo.
            for (int i = 0; i < 12000; i++) m.Tick(0.05f, 0f, 1f);
            Assert.AreEqual(MatchPhase.Demo, m.Phase);
            Assert.AreEqual(0f, m.PhaseRemaining);

            Assert.IsFalse(m.ReportKO(MatchSide.Player));
            Assert.IsFalse(m.ReportKO(MatchSide.Opponent));
            Assert.AreEqual(MatchPhase.Demo, m.Phase);
            Assert.AreEqual(0, m.Round);
            Assert.AreEqual(0, m.PlayerWins);
            Assert.AreEqual(0, m.OpponentWins);
        }

        [Test]
        public void Demo_PausesResumes_AndReturnsToMenu()
        {
            var m = InMenu();
            m.StartDemo();
            Assert.IsTrue(m.Pause(), "the wrist button pauses the demo");
            Assert.IsTrue(m.IsPaused);
            Assert.AreEqual(MatchPhase.Demo, m.Phase);
            Assert.IsTrue(m.Resume());
            Assert.IsFalse(m.IsPaused);

            m.Pause();
            m.ReturnToMenu();
            Assert.AreEqual(MatchPhase.Menu, m.Phase);
            Assert.IsFalse(m.IsPaused, "MENU from the pause panel unpauses");
            Assert.IsTrue(m.StartDemo(), "the demo starts again from the menu");
        }

        [Test]
        public void Demo_CannotCompleteATutorial()
        {
            var m = InMenu();
            m.StartRun(withTutorial: true);
            m.ReturnToMenu(); // run tutorial abandoned
            m.StartDemo();
            Assert.IsFalse(m.CompleteTutorial());
            Assert.AreEqual(MatchPhase.Demo, m.Phase);

            m.ReturnToMenu();
            m.StartMatch(withTutorial: true);
            m.CompleteTutorial();
            Assert.AreEqual(MatchPhase.Countdown, m.Phase, "a later Quick Match tutorial still leads to its countdown");
        }

        [Test]
        public void PhaseChanged_FiresOnceIntoTheDemo_AndOnceBack()
        {
            var m = InMenu();
            int count = 0;
            MatchPhase last = MatchPhase.Boot;
            m.PhaseChanged += p =>
            {
                count++;
                last = p;
            };

            m.StartDemo();
            Assert.AreEqual(1, count);
            Assert.AreEqual(MatchPhase.Demo, last);
            for (int i = 0; i < 100; i++) m.Tick(0.05f, 1f, 1f);
            Assert.AreEqual(1, count);

            m.ReturnToMenu();
            Assert.AreEqual(2, count);
            Assert.AreEqual(MatchPhase.Menu, last);
        }

        [Test]
        public void Invulnerable_HealthNeverDrops_AndTheHeroNeverDies()
        {
            var h = new HeroHealthModel(HealthParams.Default) { Invulnerable = true };
            for (int i = 0; i < 50; i++) Assert.AreEqual(HitOutcome.Damaged, h.ApplyDamage(70f));

            Assert.AreEqual(HealthParams.Default.MaxHealth, h.CurrentHealth);
            Assert.IsFalse(h.IsDead);
            Assert.AreEqual(1f, h.Normalized);
            Assert.AreEqual(50, h.DamageCount, "every hit still counts, so the hit effects play");
        }

        [Test]
        public void Invulnerable_HitsStillSlow_LikeTheGame()
        {
            var h = new HeroHealthModel(HealthParams.Default) { Invulnerable = true };
            h.ApplyDamage(10f);
            Assert.AreEqual(HealthParams.Default.SlowMultiplier, h.SpeedMultiplier);

            h.Tick(HealthParams.Default.SlowDuration + 0.1f);
            Assert.AreEqual(1f, h.SpeedMultiplier);
        }

        [Test]
        public void Invulnerable_IgnoresZeroDamage_AndStillStuns()
        {
            var h = new HeroHealthModel(HealthParams.Default) { Invulnerable = true };
            Assert.AreEqual(HitOutcome.Ignored, h.ApplyDamage(0f));
            Assert.AreEqual(0, h.DamageCount);

            h.ApplyStun(1f, 0.15f);
            Assert.IsTrue(h.IsStunned);
        }

        [Test]
        public void Invulnerable_SurvivesReset_AndOffRestoresNormalHits()
        {
            var h = new HeroHealthModel(HealthParams.Default) { Invulnerable = true };
            h.Reset();
            Assert.IsTrue(h.Invulnerable, "a mode flag like AutoRespawn");

            h.Invulnerable = false;
            Assert.AreEqual(HitOutcome.Damaged, h.ApplyDamage(40f));
            Assert.AreEqual(60f, h.CurrentHealth, 1e-4f);
            Assert.AreEqual(HitOutcome.Killed, h.ApplyDamage(100f));
            Assert.IsTrue(h.IsDead);
        }

        [Test]
        public void Perf_DemoCountsAsCombat_UnlessPaused()
        {
            Assert.IsTrue(PerfFlushPolicy.InCombat(MatchPhase.Demo, RunPhase.Idle, false));
            Assert.IsFalse(PerfFlushPolicy.InCombat(MatchPhase.Demo, RunPhase.Idle, true));
        }

        [Test]
        public void Defaults_TwoSlowGentleStrikers()
        {
            DemoParams p = DemoParams.Default;
            Assert.AreEqual(2, DemoRules.BotCount(p, 2));

            BotArchetype a = DemoRules.Archetype(p);
            Assert.AreEqual(BotArchetypeId.Striker, a.Id);
            Assert.AreEqual(BotShape.None, a.Shapes);
            Assert.Greater(a.Attack.TelegraphMult, BotArchetypes.StrikerAttack.TelegraphMult, "longer warning");
            Assert.AreEqual(1, a.Attack.BurstCount);
            Assert.AreEqual(0, a.SwitchEvery);
            Assert.AreEqual(a.Attack.TelegraphMult, a.AltAttack.TelegraphMult);

            IslandSpec spec = DemoRules.Spec(p);
            Assert.Greater(spec.DamageMult, 0f, "hits still land (effects play)");
            Assert.Less(spec.DamageMult, 1f);
            Assert.Greater(spec.FireIntervalMult, 1f, "fewer shots than a run's Striker");
            Assert.AreEqual(1f, spec.HealthMult);
            Assert.Less(DemoRules.SpeedScale(p), 1f, "slower than a run's Striker");
        }

        [Test]
        public void BotCount_NeverAboveMaxOrTheBotsThatExist()
        {
            DemoParams p = DemoParams.Default;
            p.BotCount = 5;
            Assert.AreEqual(DemoRules.MaxBots, DemoRules.BotCount(p, 10));
            Assert.AreEqual(1, DemoRules.BotCount(p, 1));

            p.BotCount = -1;
            Assert.AreEqual(0, DemoRules.BotCount(p, 2));
            p.BotCount = 1;
            Assert.AreEqual(0, DemoRules.BotCount(p, 0));
            Assert.AreEqual(1, DemoRules.BotCount(p, 2));
        }

        [Test]
        public void SpeedScale_NeverFasterThanTheRunsBots()
        {
            DemoParams p = DemoParams.Default;
            p.BotSpeedScale = 3f;
            Assert.AreEqual(1f, DemoRules.SpeedScale(p));
            p.BotSpeedScale = 0.01f;
            Assert.AreEqual(0.1f, DemoRules.SpeedScale(p), 1e-6f);
            p.BotSpeedScale = 0f;
            Assert.AreEqual(1f, DemoRules.SpeedScale(p), "unset = the run's speed");
        }

        [Test]
        public void ZeroMultipliers_FallBackToOne()
        {
            var p = new DemoParams(); // every field 0, e.g. serialized before a field existed
            IslandSpec spec = DemoRules.Spec(p);
            Assert.AreEqual(1f, spec.DamageMult);
            Assert.AreEqual(1f, spec.FireIntervalMult);
            Assert.AreEqual(BotArchetypes.StrikerAttack.TelegraphMult, DemoRules.Archetype(p).Attack.TelegraphMult);
        }

        [Test]
        public void Archetype_LeavesTheRunsStrikerUntouched()
        {
            float before = BotArchetypes.Get(BotArchetypeId.Striker).Attack.TelegraphMult;
            DemoRules.Archetype(DemoParams.Default);
            Assert.AreEqual(before, BotArchetypes.Get(BotArchetypeId.Striker).Attack.TelegraphMult);
            Assert.AreEqual(before, BotArchetypes.StrikerAttack.TelegraphMult);
        }
    }
}
