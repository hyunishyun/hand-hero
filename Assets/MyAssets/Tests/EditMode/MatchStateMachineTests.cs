using HandHero.Core;
using NUnit.Framework;

namespace HandHero.Tests
{
    // T6: Boot -> Menu -> (Tutorial) -> Countdown -> Fight -> RoundEnd -> MatchEnd -> Menu, best of 3.
    public class MatchStateMachineTests
    {
        private static readonly MatchParams P = MatchParams.Default;

        private static MatchStateMachine InMenu()
        {
            var m = new MatchStateMachine(P);
            m.Tick(0.016f, 1f, 1f);
            Assert.AreEqual(MatchPhase.Menu, m.Phase);
            return m;
        }

        private static void Run(MatchStateMachine m, float seconds, float player = 1f, float opponent = 1f)
        {
            const float dt = 0.05f;
            int steps = (int)System.Math.Ceiling(seconds / dt);
            for (int i = 0; i < steps; i++) m.Tick(dt, player, opponent);
        }

        private static MatchStateMachine InFight()
        {
            var m = InMenu();
            Assert.IsTrue(m.StartMatch(false));
            Run(m, P.CountdownTime + 0.2f);
            Assert.AreEqual(MatchPhase.Fight, m.Phase);
            return m;
        }

        [Test]
        public void Defaults_BestOfThree_AndWorstCaseUnderTenMinutes()
        {
            Assert.AreEqual(2, P.RoundsToWin);
            Assert.AreEqual(5, P.MaxRounds);
            Assert.Less(P.WorstCaseMatchSeconds, 600f);
        }

        [Test]
        public void Boot_GoesToMenu_AndMenuWaits()
        {
            var m = new MatchStateMachine(P);
            Assert.AreEqual(MatchPhase.Boot, m.Phase);
            m.Tick(0.016f, 1f, 1f);
            Assert.AreEqual(MatchPhase.Menu, m.Phase);
            Run(m, 60f);
            Assert.AreEqual(MatchPhase.Menu, m.Phase);
        }

        [Test]
        public void StartMatch_CountsDownIntoRoundOne()
        {
            var m = InMenu();
            Assert.IsTrue(m.StartMatch(false));
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
            Assert.AreEqual(1, m.Round);
            Assert.AreEqual(0, m.PlayerWins);
            Assert.AreEqual(0, m.OpponentWins);

            Run(m, P.CountdownTime - 0.1f);
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
            Run(m, 0.15f);
            Assert.AreEqual(MatchPhase.Fight, m.Phase);
        }

        [Test]
        public void StartMatch_OutsideMenu_IsRejected()
        {
            var m = InFight();
            Assert.IsFalse(m.StartMatch(false));
            Assert.AreEqual(MatchPhase.Fight, m.Phase);
        }

        [Test]
        public void Tutorial_RunsBeforeTheCountdown()
        {
            var m = InMenu();
            m.StartMatch(true);
            Assert.AreEqual(MatchPhase.Tutorial, m.Phase);
            Run(m, 120f);
            Assert.AreEqual(MatchPhase.Tutorial, m.Phase, "tutorial has no timeout");
            Assert.IsTrue(m.CompleteTutorial());
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
            Assert.AreEqual(1, m.Round);
        }

        [Test]
        public void KO_EndsTheRound_ForTheOtherSide()
        {
            var m = InFight();
            Assert.IsTrue(m.ReportKO(MatchSide.Opponent));
            Assert.AreEqual(MatchPhase.RoundEnd, m.Phase);
            Assert.AreEqual(MatchSide.Player, m.RoundWinner);
            Assert.AreEqual(1, m.PlayerWins);
            Assert.IsFalse(m.ReportKO(MatchSide.Player), "only the first KO of a round counts");
            Assert.AreEqual(0, m.OpponentWins);
        }

        [Test]
        public void KO_OutsideFight_IsIgnored()
        {
            var m = InMenu();
            Assert.IsFalse(m.ReportKO(MatchSide.Opponent));
            Assert.AreEqual(0, m.PlayerWins);
        }

        [Test]
        public void TimeUp_HigherHealthWins_EqualIsDraw()
        {
            var m = InFight();
            Run(m, P.RoundTime + 0.3f, player: 0.4f, opponent: 0.7f);
            Assert.AreEqual(MatchPhase.RoundEnd, m.Phase);
            Assert.AreEqual(MatchSide.Opponent, m.RoundWinner);
            Assert.AreEqual(1, m.OpponentWins);

            Run(m, P.RoundEndTime + P.CountdownTime + 0.3f);
            Assert.AreEqual(MatchPhase.Fight, m.Phase);
            Run(m, P.RoundTime + 0.3f, player: 0.5f, opponent: 0.5f);
            Assert.AreEqual(MatchSide.None, m.RoundWinner);
            Assert.AreEqual(1, m.OpponentWins);
            Assert.AreEqual(0, m.PlayerWins);
        }

        [Test]
        public void TwoRoundWins_EndTheMatch_ThenBackToMenu()
        {
            var m = InFight();
            m.ReportKO(MatchSide.Opponent);
            Run(m, P.RoundEndTime + 0.2f);
            Assert.AreEqual(MatchPhase.Countdown, m.Phase);
            Assert.AreEqual(2, m.Round);

            Run(m, P.CountdownTime + 0.2f);
            m.ReportKO(MatchSide.Opponent);
            Assert.AreEqual(MatchPhase.RoundEnd, m.Phase, "the deciding round's result shows first");
            Run(m, P.RoundEndTime + 0.2f);
            Assert.AreEqual(MatchPhase.MatchEnd, m.Phase);
            Assert.AreEqual(MatchSide.Player, m.MatchWinner);

            Run(m, P.MatchEndTime + 0.2f);
            Assert.AreEqual(MatchPhase.Menu, m.Phase);
        }

        [Test]
        public void SplitRounds_GoToADecider()
        {
            var m = InFight();
            m.ReportKO(MatchSide.Opponent);
            Run(m, P.RoundEndTime + P.CountdownTime + 0.3f);
            m.ReportKO(MatchSide.Player);
            Run(m, P.RoundEndTime + P.CountdownTime + 0.3f);
            Assert.AreEqual(MatchPhase.Fight, m.Phase);
            Assert.AreEqual(3, m.Round);

            m.ReportKO(MatchSide.Player);
            Run(m, P.RoundEndTime + 0.2f);
            Assert.AreEqual(MatchPhase.MatchEnd, m.Phase);
            Assert.AreEqual(MatchSide.Opponent, m.MatchWinner);
        }

        [Test]
        public void DrawsStopAtMaxRounds()
        {
            var m = InFight();
            for (int round = 1; round <= P.MaxRounds; round++)
            {
                Assert.AreEqual(MatchPhase.Fight, m.Phase, $"round {round}");
                Run(m, P.RoundTime + 0.3f);
                Run(m, P.RoundEndTime + P.CountdownTime + 0.3f);
            }
            Assert.AreEqual(MatchPhase.MatchEnd, m.Phase);
            Assert.AreEqual(MatchSide.None, m.MatchWinner);
        }

        [Test]
        public void ReturnToMenu_FromAnyPhase_ClearsTheScore()
        {
            var m = InFight();
            m.ReportKO(MatchSide.Opponent);
            m.ReturnToMenu();
            Assert.AreEqual(MatchPhase.Menu, m.Phase);

            m.StartMatch(false);
            Assert.AreEqual(1, m.Round);
            Assert.AreEqual(0, m.PlayerWins);
        }

        [Test]
        public void PhaseChanged_FiresOncePerTransition()
        {
            var m = new MatchStateMachine(P);
            var seen = new System.Collections.Generic.List<MatchPhase>();
            m.PhaseChanged += seen.Add;

            m.Tick(0.016f, 1f, 1f);
            m.StartMatch(false);
            Run(m, P.CountdownTime + 0.2f);
            m.ReportKO(MatchSide.Player);

            CollectionAssert.AreEqual(
                new[] { MatchPhase.Menu, MatchPhase.Countdown, MatchPhase.Fight, MatchPhase.RoundEnd }, seen);
        }

        [Test]
        public void PhaseRemaining_CountsDownTimedPhases()
        {
            var m = InMenu();
            m.StartMatch(false);
            Assert.AreEqual(P.CountdownTime, m.PhaseRemaining, 1e-4f);
            m.Tick(1f, 1f, 1f);
            Assert.AreEqual(P.CountdownTime - 1f, m.PhaseRemaining, 1e-4f);
        }
    }
}
