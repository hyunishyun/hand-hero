using HandHero.Core;
using TMPro;
using UnityEngine;

// World-space match HUD (T6): a score line and a center banner placed in front
// of the fixed seat, inside the central field of view so the player never has
// to turn their head. Fixed in the world, not head-locked (comfort).
public class MatchHud : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchDirector director;
    [Tooltip("Score / round / timer line, above the arena center")]
    [SerializeField] private TMP_Text scoreLine;
    [Tooltip("Big center text: menu, countdown, round and match results")]
    [SerializeField] private TMP_Text banner;
    [Tooltip("Optional. RUN mode: island, objective, crystals and health on the score line (R10)")]
    [SerializeField] private RunDirector run;

    [Header("Text")]
    [SerializeField] private string playerName = "YOU";
    [SerializeField] private string opponentName = "BOT";
    [Tooltip("Seconds \"FIGHT!\" stays up when a round starts")]
    [SerializeField] private float fightBannerTime = 0.8f;

    // Last shown keys (P3 / GM-1…GM-5): strings are rebuilt and assigned only
    // when a shown integer changes, not every frame.
    private HudKey _scoreKey;
    private HudKey _bannerKey;
    private bool _keysValid;

    private void OnEnable() => _keysValid = false;

    private void LateUpdate()
    {
        if (director == null || director.Match == null) return;
        MatchStateMachine m = director.Match;
        RunStateMachine r = m.Phase == MatchPhase.Run && run != null && run.IsRunning ? run.Run : null;

        if (scoreLine != null)
        {
            // Round 5 T4: the demo has no score or timer line (its banner stays empty too).
            bool inMatch = m.Phase != MatchPhase.Menu && m.Phase != MatchPhase.Boot && m.Phase != MatchPhase.Tutorial
                && m.Phase != MatchPhase.Run && m.Phase != MatchPhase.Demo;
            bool inRun = r != null && r.Phase != RunPhase.Victory && r.Phase != RunPhase.Defeat;
            SetActive(scoreLine, inMatch || inRun);
            if (inMatch)
            {
                HudKey key = ScoreKey(m);
                if (!_keysValid || key != _scoreKey)
                {
                    _scoreKey = key;
                    scoreLine.text = ScoreText(m);
                }
            }
            else if (inRun)
            {
                HudKey key = RunStatusKey(r);
                if (!_keysValid || key != _scoreKey)
                {
                    _scoreKey = key;
                    scoreLine.text = RunStatusText(r);
                }
            }
        }

        if (banner != null)
        {
            bool menu = m.Phase == MatchPhase.Menu;
            HudKey key = m.IsPaused ? PausedKey : r != null ? RunBannerKey(r) : menu ? MenuKey() : BannerKey(m);
            if (!_keysValid || key != _bannerKey)
            {
                _bannerKey = key;
                string text = m.IsPaused ? "PAUSED" : r != null ? RunBannerText(r) : menu ? MenuText() : BannerText(m);
                SetActive(banner, !string.IsNullOrEmpty(text));
                banner.text = text;
            }
        }

        _keysValid = true;
    }

    private static void SetActive(Component c, bool active)
    {
        if (c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
    }

    private static readonly HudKey PausedKey = new HudKey(HudKeyKind.MatchBanner, -1);

    private static HudKey ScoreKey(MatchStateMachine m)
    {
        float seconds = m.Phase == MatchPhase.Fight ? m.PhaseRemaining : m.Params.RoundTime;
        return new HudKey(HudKeyKind.MatchScore, m.PlayerWins, m.OpponentWins, m.Round, Mathf.CeilToInt(seconds));
    }

    private HudKey RunStatusKey(RunStateMachine r)
    {
        IslandSpec spec = r.Spec;
        float hordeLeft = r.Phase == RunPhase.Island ? r.PhaseRemaining : r.Params.HordeTime;
        bool showObjective = r.Phase == RunPhase.Island || r.Phase == RunPhase.Intro;
        HeroHealth hp = run.PlayerHealth;
        return RunHudText.StatusKey(r.Island, spec.Type, showObjective, r.BotsRemaining, hordeLeft,
            r.Crystals.Balance, hp != null ? hp.CurrentHealth : 0f, hp != null ? hp.MaxHealth : 0f);
    }

    // Mirrors RunBannerText: every value that text shows is in the key.
    private HudKey RunBannerKey(RunStateMachine r)
    {
        switch (r.Phase)
        {
            case RunPhase.Intro:
                return RunHudText.IntroKey(r.Island, r.Spec.Type, r.PhaseRemaining);
            case RunPhase.Victory:
            case RunPhase.Defeat:
                return RunHudText.EndKey(r.Phase == RunPhase.Victory, r.IslandsCleared, r.Inventory.TotalLevels,
                    r.RunTime, MetaText.ChangesKey(run.LastMetaChanges));
            case RunPhase.Island:
                return new HudKey(HudKeyKind.MatchBanner, -2, r.PhaseTime < fightBannerTime ? 1 : 0);
            default:
                return new HudKey(HudKeyKind.MatchBanner, -3, (int)r.Phase);
        }
    }

    // Main menu banner (S5): the current aim mode's best line replaces the hint
    // once there is one. Mirrors MenuText.
    private HudKey MenuKey() =>
        run != null ? MetaText.MenuKey(run.BestIsland, run.BestWinSeconds) : MetaText.MenuKey(0, 0f);

    private string MenuText() =>
        MetaText.MenuBanner(run != null ? MetaText.BestLine(run.BestIsland, run.BestWinSeconds) : "");

    // Mirrors BannerText.
    private HudKey BannerKey(MatchStateMachine m)
    {
        int countdown = m.Phase == MatchPhase.Countdown ? Mathf.CeilToInt(m.PhaseRemaining) : 0;
        int fight = m.Phase == MatchPhase.Fight && m.PhaseTime < fightBannerTime ? 1 : 0;
        return new HudKey(HudKeyKind.MatchBanner, (int)m.Phase, m.Round, countdown, fight,
            (int)m.RoundWinner * 8 + (int)m.MatchWinner, m.PlayerWins * 1000 + m.OpponentWins);
    }

    private string ScoreText(MatchStateMachine m)
    {
        float seconds = m.Phase == MatchPhase.Fight ? m.PhaseRemaining : m.Params.RoundTime;
        int s = Mathf.CeilToInt(seconds);
        return $"{playerName} {m.PlayerWins} - {m.OpponentWins} {opponentName}    R{m.Round}    {s / 60}:{s % 60:00}";
    }

    private string RunStatusText(RunStateMachine r)
    {
        IslandSpec spec = r.Spec;
        float hordeLeft = r.Phase == RunPhase.Island ? r.PhaseRemaining : r.Params.HordeTime;
        string objective = r.Phase == RunPhase.Island || r.Phase == RunPhase.Intro
            ? RunHudText.Objective(spec.Type, r.BotsRemaining, hordeLeft)
            : "";
        HeroHealth hp = run.PlayerHealth;
        return RunHudText.Status(r.Island, spec.Type, objective, r.Crystals.Balance,
            hp != null ? hp.CurrentHealth : 0f, hp != null ? hp.MaxHealth : 0f);
    }

    // Choice phases leave the banner empty: their panels carry the headers.
    private string RunBannerText(RunStateMachine r)
    {
        switch (r.Phase)
        {
            case RunPhase.Intro:
                return RunHudText.IntroBanner(r.Island, r.Spec.Type, r.PhaseRemaining);
            case RunPhase.Island:
                return r.PhaseTime < fightBannerTime ? "FIGHT!" : "";
            case RunPhase.IslandCleared:
                return "CLEARED";
            case RunPhase.Victory:
            case RunPhase.Defeat:
                return RunHudText.EndBanner(r.Phase == RunPhase.Victory, r.IslandsCleared, r.Inventory.TotalLevels,
                    r.RunTime, MetaText.EndLines(run.LastMetaChanges));
            default:
                return "";
        }
    }

    private string BannerText(MatchStateMachine m)
    {
        switch (m.Phase)
        {
            case MatchPhase.Menu:
                return MenuText();
            case MatchPhase.Countdown:
                return $"ROUND {m.Round}\n{Mathf.CeilToInt(m.PhaseRemaining)}";
            case MatchPhase.Fight:
                return m.PhaseTime < fightBannerTime ? "FIGHT!" : "";
            case MatchPhase.RoundEnd:
                return m.RoundWinner == MatchSide.Player ? "ROUND WON"
                    : m.RoundWinner == MatchSide.Opponent ? "ROUND LOST"
                    : "DRAW";
            case MatchPhase.MatchEnd:
                string result = m.MatchWinner == MatchSide.Player ? "VICTORY"
                    : m.MatchWinner == MatchSide.Opponent ? "DEFEAT"
                    : "DRAW";
                return $"{result}\n<size=50%>{m.PlayerWins} - {m.OpponentWins}</size>";
            default:
                return "";
        }
    }
}
