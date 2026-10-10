using System;
using UnityEngine;

namespace HandHero.Core
{
    public enum MatchPhase
    {
        Boot,
        Menu,
        Tutorial,  // optional, before the first countdown; ends via CompleteTutorial (T8)
        Countdown, // heroes reset at their spawn points, no control
        Fight,
        RoundEnd,  // shows the round result
        MatchEnd,  // shows the match result, then back to the menu
        Run,       // RUN mode (R8): RunStateMachine drives the game; stays until ReturnToMenu
        Demo,      // demo / practice mode (round 5, T4): no timer, no KOs; stays until ReturnToMenu
    }

    public enum MatchSide
    {
        None, // draw / undecided
        Player,
        Opponent,
    }

    [Serializable]
    public struct MatchParams
    {
        [Tooltip("Round wins needed to take the match (2 = best of 3)")]
        public int RoundsToWin;
        [Tooltip("Hard cap on rounds, so draws can't stretch a match past ~10 minutes")]
        public int MaxRounds;
        public float CountdownTime;
        [Tooltip("Round time limit in seconds; at time-up the hero with more health wins the round")]
        public float RoundTime;
        public float RoundEndTime;
        [Tooltip("Seconds the match result shows before returning to the menu")]
        public float MatchEndTime;

        public static MatchParams Default => new MatchParams
        {
            RoundsToWin = 2,
            MaxRounds = 5,
            CountdownTime = 3f,
            RoundTime = 90f,
            RoundEndTime = 3f,
            MatchEndTime = 10f,
        };

        // Longest possible match (every round a time-up draw) — must stay under 10 minutes.
        public float WorstCaseMatchSeconds => MaxRounds * (CountdownTime + RoundTime + RoundEndTime) + MatchEndTime;
    }

    // Single-player match flow (T6): Boot -> Menu -> Tutorial? -> Countdown ->
    // Fight -> RoundEnd -> (Countdown ... | MatchEnd) -> Menu. A round ends on the
    // first KO or at time-up (more health wins, equal = draw).
    // Fusion port: the phase, timer and score become [Networked] state advanced
    // by the host in FixedUpdateNetwork; clients only read them for the HUD.
    public class MatchStateMachine
    {
        private float _phaseTime;
        private bool _runAfterTutorial;

        public MatchStateMachine(MatchParams p)
        {
            Params = p;
            Phase = MatchPhase.Boot;
        }

        public event Action<MatchPhase> PhaseChanged;
        public event Action<bool> PausedChanged;

        public MatchParams Params { get; set; }
        public MatchPhase Phase { get; private set; }
        public int Round { get; private set; }
        public int PlayerWins { get; private set; }
        public int OpponentWins { get; private set; }
        public MatchSide RoundWinner { get; private set; }
        public MatchSide MatchWinner { get; private set; }

        // Paused: timers stop and KOs are ignored until Resume (T7).
        public bool IsPaused { get; private set; }

        // Seconds spent in the current phase.
        public float PhaseTime => _phaseTime;

        // Seconds left in a timed phase (Countdown, Fight, RoundEnd, MatchEnd); 0 otherwise.
        public float PhaseRemaining => Mathf.Max(0f, PhaseDuration(Phase) - _phaseTime);

        public bool StartMatch(bool withTutorial)
        {
            if (Phase != MatchPhase.Menu) return false;

            _runAfterTutorial = false;
            Round = 0;
            PlayerWins = 0;
            OpponentWins = 0;
            RoundWinner = MatchSide.None;
            MatchWinner = MatchSide.None;

            if (withTutorial) Enter(MatchPhase.Tutorial);
            else BeginRound();
            return true;
        }

        // RUN mode: the match only hosts pause and menu return; RunStateMachine
        // owns the islands, so Tick and ReportKO do nothing in this phase.
        // With the tutorial, the run starts when the tutorial completes (R10).
        public bool StartRun(bool withTutorial = false)
        {
            if (Phase != MatchPhase.Menu) return false;
            _runAfterTutorial = withTutorial;
            Enter(withTutorial ? MatchPhase.Tutorial : MatchPhase.Run);
            return true;
        }

        // Demo mode (round 5, T4 / D6): a practice arena for recordings. Like Run,
        // the match only hosts pause and menu return; Tick and ReportKO do nothing
        // in this phase. Never goes through the tutorial.
        public bool StartDemo()
        {
            if (Phase != MatchPhase.Menu) return false;
            Enter(MatchPhase.Demo);
            return true;
        }

        public bool CompleteTutorial()
        {
            if (Phase != MatchPhase.Tutorial) return false;
            if (_runAfterTutorial)
            {
                _runAfterTutorial = false;
                Enter(MatchPhase.Run);
            }
            else BeginRound();
            return true;
        }

        // A hero was knocked out. Only the first KO of a fight counts.
        public bool ReportKO(MatchSide loser)
        {
            if (Phase != MatchPhase.Fight || IsPaused || loser == MatchSide.None) return false;
            EndRound(loser == MatchSide.Player ? MatchSide.Opponent : MatchSide.Player);
            return true;
        }

        public void ReturnToMenu()
        {
            SetPaused(false);
            Enter(MatchPhase.Menu);
        }

        // Pausing only makes sense once a match is under way. Returns true if the state changed.
        public bool Pause()
        {
            if (IsPaused || Phase == MatchPhase.Boot || Phase == MatchPhase.Menu) return false;
            SetPaused(true);
            return true;
        }

        public bool Resume()
        {
            if (!IsPaused) return false;
            SetPaused(false);
            return true;
        }

        public bool TogglePause() => IsPaused ? Resume() : Pause();

        // Health values are 0..1 and only matter at time-up.
        public void Tick(float dt, float playerHealth01, float opponentHealth01)
        {
            if (IsPaused) return;
            _phaseTime += dt;

            switch (Phase)
            {
                case MatchPhase.Boot:
                    Enter(MatchPhase.Menu);
                    break;

                case MatchPhase.Countdown:
                    if (Expired()) Enter(MatchPhase.Fight);
                    break;

                case MatchPhase.Fight:
                    if (Expired())
                    {
                        MatchSide winner = playerHealth01 > opponentHealth01 ? MatchSide.Player
                            : opponentHealth01 > playerHealth01 ? MatchSide.Opponent
                            : MatchSide.None;
                        EndRound(winner);
                    }
                    break;

                case MatchPhase.RoundEnd:
                    if (!Expired()) break;
                    if (IsMatchDecided()) EndMatch();
                    else BeginRound();
                    break;

                case MatchPhase.MatchEnd:
                    if (Expired()) Enter(MatchPhase.Menu);
                    break;
            }
        }

        private void BeginRound()
        {
            Round++;
            RoundWinner = MatchSide.None;
            Enter(MatchPhase.Countdown);
        }

        private void EndRound(MatchSide winner)
        {
            RoundWinner = winner;
            if (winner == MatchSide.Player) PlayerWins++;
            else if (winner == MatchSide.Opponent) OpponentWins++;
            Enter(MatchPhase.RoundEnd);
        }

        private bool IsMatchDecided()
        {
            return PlayerWins >= Params.RoundsToWin || OpponentWins >= Params.RoundsToWin || Round >= Params.MaxRounds;
        }

        private void EndMatch()
        {
            MatchWinner = PlayerWins > OpponentWins ? MatchSide.Player
                : OpponentWins > PlayerWins ? MatchSide.Opponent
                : MatchSide.None;
            Enter(MatchPhase.MatchEnd);
        }

        private bool Expired() => _phaseTime >= PhaseDuration(Phase);

        private float PhaseDuration(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Countdown: return Params.CountdownTime;
                case MatchPhase.Fight: return Params.RoundTime;
                case MatchPhase.RoundEnd: return Params.RoundEndTime;
                case MatchPhase.MatchEnd: return Params.MatchEndTime;
                default: return 0f;
            }
        }

        private void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            PausedChanged?.Invoke(paused);
        }

        private void Enter(MatchPhase phase)
        {
            Phase = phase;
            _phaseTime = 0f;
            PhaseChanged?.Invoke(phase);
        }
    }
}
