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

    [Header("Text")]
    [SerializeField] private string playerName = "YOU";
    [SerializeField] private string opponentName = "BOT";
    [Tooltip("Seconds \"FIGHT!\" stays up when a round starts")]
    [SerializeField] private float fightBannerTime = 0.8f;

    private void LateUpdate()
    {
        if (director == null || director.Match == null) return;
        MatchStateMachine m = director.Match;

        if (scoreLine != null)
        {
            bool inMatch = m.Phase != MatchPhase.Menu && m.Phase != MatchPhase.Boot && m.Phase != MatchPhase.Tutorial;
            scoreLine.gameObject.SetActive(inMatch);
            if (inMatch) scoreLine.text = ScoreText(m);
        }

        if (banner != null)
        {
            string text = BannerText(m);
            banner.gameObject.SetActive(!string.IsNullOrEmpty(text));
            banner.text = text;
        }
    }

    private string ScoreText(MatchStateMachine m)
    {
        float seconds = m.Phase == MatchPhase.Fight ? m.PhaseRemaining : m.Params.RoundTime;
        int s = Mathf.CeilToInt(seconds);
        return $"{playerName} {m.PlayerWins} - {m.OpponentWins} {opponentName}    R{m.Round}    {s / 60}:{s % 60:00}";
    }

    private string BannerText(MatchStateMachine m)
    {
        switch (m.Phase)
        {
            case MatchPhase.Menu:
                return "HAND HERO\n<size=50%>Enter: start</size>";
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
