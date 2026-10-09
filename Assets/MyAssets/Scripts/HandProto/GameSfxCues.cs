using HandHero.Core;
using UnityEngine;

// Match and run flow sounds (P11, D14): countdown ticks, FIGHT, cleared, chest,
// VICTORY / DEFEAT and pause / resume. The rules live in Core SfxCues; this only
// listens to the two state machines and plays what they return.
public class GameSfxCues : MonoBehaviour
{
    [SerializeField] private MatchDirector match;
    [Tooltip("Optional. Empty = Quick Match / tutorial sounds only")]
    [SerializeField] private RunDirector run;

    private MatchStateMachine _match;
    private RunStateMachine _run;
    private readonly CountdownTicker _ticker = new CountdownTicker();

    // After the directors' Awake created their state machines.
    private void Start()
    {
        _match = match != null ? match.Match : null;
        _run = run != null ? run.Run : null;
        if (_match != null)
        {
            _match.PhaseChanged += OnMatchPhase;
            _match.PausedChanged += OnPaused;
        }
        if (_run != null) _run.PhaseChanged += OnRunPhase;
    }

    private void OnDestroy()
    {
        if (_match != null)
        {
            _match.PhaseChanged -= OnMatchPhase;
            _match.PausedChanged -= OnPaused;
        }
        if (_run != null) _run.PhaseChanged -= OnRunPhase;
    }

    private void Update()
    {
        if (_match == null) return;
        RunPhase runPhase = _run != null ? _run.Phase : RunPhase.Idle;
        if (!SfxCues.IsCountdown(_match.Phase, runPhase))
        {
            _ticker.Reset();
            return;
        }
        float remaining = _match.Phase == MatchPhase.Countdown ? _match.PhaseRemaining : _run.PhaseRemaining;
        if (_ticker.Step(remaining)) SfxPlayer.PlayUi(SfxId.CountdownTick);
    }

    private void OnMatchPhase(MatchPhase phase)
    {
        SfxPlayer.PlayUi(SfxCues.ForMatchPhase(phase, _match.RoundWinner, _match.MatchWinner));
    }

    private void OnRunPhase(RunPhase phase)
    {
        SfxPlayer.PlayUi(SfxCues.ForRunPhase(phase));
    }

    private void OnPaused(bool paused)
    {
        SfxPlayer.PlayUi(paused ? SfxId.Pause : SfxId.Resume);
    }
}
