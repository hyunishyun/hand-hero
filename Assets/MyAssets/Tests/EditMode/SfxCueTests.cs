using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // P11 (D14): which game moments make which sound.
    public class SfxCueTests
    {
        [Test]
        public void Countdown_TicksOnEveryWholeSecond_ThenStops()
        {
            var ticker = new CountdownTicker();
            int ticks = 0;
            float[] remaining = { 3f, 2.9f, 2.5f, 2f, 1.5f, 1f, 0.5f, 0.01f, 0f, 0f };
            foreach (float r in remaining)
                if (ticker.Step(r)) ticks++;
            Assert.AreEqual(3, ticks, "3, 2, 1 — FIGHT is the phase cue, not a tick");
        }

        [Test]
        public void Countdown_JoinedMidway_TicksOnceForTheCurrentSecond()
        {
            var ticker = new CountdownTicker();
            Assert.IsTrue(ticker.Step(1.7f));
            Assert.IsFalse(ticker.Step(1.2f));
            Assert.IsTrue(ticker.Step(0.9f));
        }

        [Test]
        public void Countdown_Reset_StartsOver()
        {
            var ticker = new CountdownTicker();
            ticker.Step(1f);
            ticker.Reset();
            Assert.IsTrue(ticker.Step(1f));
        }

        [Test]
        public void Countdown_HitchSkippingASecond_TicksOnce()
        {
            var ticker = new CountdownTicker();
            Assert.IsTrue(ticker.Step(3f));
            Assert.IsTrue(ticker.Step(1.5f), "one tick, not a burst for the skipped second");
            Assert.IsFalse(ticker.Step(1.1f));
        }

        [Test]
        public void MatchPhases()
        {
            Assert.AreEqual(SfxId.Fight, SfxCues.ForMatchPhase(MatchPhase.Fight, MatchSide.None, MatchSide.None));
            Assert.AreEqual(SfxId.IslandCleared,
                SfxCues.ForMatchPhase(MatchPhase.RoundEnd, MatchSide.Player, MatchSide.None));
            Assert.AreEqual(SfxId.None, SfxCues.ForMatchPhase(MatchPhase.RoundEnd, MatchSide.Opponent, MatchSide.None));
            Assert.AreEqual(SfxId.Victory, SfxCues.ForMatchPhase(MatchPhase.MatchEnd, MatchSide.Player, MatchSide.Player));
            Assert.AreEqual(SfxId.Defeat,
                SfxCues.ForMatchPhase(MatchPhase.MatchEnd, MatchSide.Opponent, MatchSide.Opponent));
            Assert.AreEqual(SfxId.Defeat, SfxCues.ForMatchPhase(MatchPhase.MatchEnd, MatchSide.None, MatchSide.None));
            Assert.AreEqual(SfxId.None, SfxCues.ForMatchPhase(MatchPhase.Menu, MatchSide.None, MatchSide.None));
            Assert.AreEqual(SfxId.None, SfxCues.ForMatchPhase(MatchPhase.Run, MatchSide.None, MatchSide.None));
        }

        [Test]
        public void RunPhases()
        {
            Assert.AreEqual(SfxId.Fight, SfxCues.ForRunPhase(RunPhase.Island));
            Assert.AreEqual(SfxId.IslandCleared, SfxCues.ForRunPhase(RunPhase.IslandCleared));
            Assert.AreEqual(SfxId.ChestOpen, SfxCues.ForRunPhase(RunPhase.OpenChest));
            Assert.AreEqual(SfxId.Victory, SfxCues.ForRunPhase(RunPhase.Victory));
            Assert.AreEqual(SfxId.Defeat, SfxCues.ForRunPhase(RunPhase.Defeat));
            Assert.AreEqual(SfxId.None, SfxCues.ForRunPhase(RunPhase.Idle));
            Assert.AreEqual(SfxId.None, SfxCues.ForRunPhase(RunPhase.Intro));
        }

        // Round 4 (S5): the starting relic chest opens like every other chest.
        [Test]
        public void StartRelic_PlaysTheChestOpenSound()
        {
            Assert.AreEqual(SfxId.ChestOpen, SfxCues.ForRunPhase(RunPhase.StartRelic));
        }

        [Test]
        public void CountdownPhases()
        {
            Assert.IsTrue(SfxCues.IsCountdown(MatchPhase.Countdown, RunPhase.Idle));
            Assert.IsTrue(SfxCues.IsCountdown(MatchPhase.Run, RunPhase.Intro));
            Assert.IsFalse(SfxCues.IsCountdown(MatchPhase.Run, RunPhase.Island));
            Assert.IsFalse(SfxCues.IsCountdown(MatchPhase.Menu, RunPhase.Intro), "a stale run phase outside RUN");
        }

        [Test]
        public void HeroEvents_DependOnTheTeam()
        {
            Assert.AreEqual(SfxId.HitTaken, SfxCues.ForHeroDamaged(HeroTeam.Player));
            Assert.AreEqual(SfxId.None, SfxCues.ForHeroDamaged(HeroTeam.Bot), "the shooter already heard HitDealt");
            Assert.AreEqual(SfxId.BotDown, SfxCues.ForHeroDied(HeroTeam.Bot));
            Assert.AreEqual(SfxId.None, SfxCues.ForHeroDied(HeroTeam.Player), "DEFEAT or a revive follows");
        }

        [Test]
        public void Shots_PlayerAndBotSoundDifferent()
        {
            Assert.AreEqual(SfxId.BeamFire, SfxCues.ForShot(HeroTeam.Player, false));
            Assert.AreEqual(SfxId.ChargeRelease, SfxCues.ForShot(HeroTeam.Player, true));
            Assert.AreEqual(SfxId.EnemyFire, SfxCues.ForShot(HeroTeam.Bot, false));
            Assert.AreEqual(SfxId.EnemyFire, SfxCues.ForShot(HeroTeam.Bot, true));
        }

        [Test]
        public void RateLimiter_DropsRepeatsOfTheSameEvent()
        {
            var limiter = new SfxRateLimiter(0.05f);
            Assert.IsTrue(limiter.Allow(SfxId.HitDealt, 1f));
            Assert.IsFalse(limiter.Allow(SfxId.HitDealt, 1.02f));
            Assert.IsTrue(limiter.Allow(SfxId.BotDown, 1.02f), "other events are independent");
            Assert.IsTrue(limiter.Allow(SfxId.HitDealt, 1.06f));
        }
    }
}
