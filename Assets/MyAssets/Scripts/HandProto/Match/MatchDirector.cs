using HandHero.Core;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs the single-player match (T6) around MatchStateMachine (HandHero.Core):
// resets both heroes at each countdown, lets them act only while fighting, and
// turns hero deaths into round KOs. Nothing here moves the XR Origin.
public class MatchDirector : MonoBehaviour
{
    [Header("Heroes")]
    [SerializeField] private HeroHealth playerHealth;
    [SerializeField] private HeroHealth opponentHealth;

    [Header("Control gating")]
    [Tooltip("Enabled only while fighting (and in the tutorial): input sources, the bot, ability controllers")]
    [SerializeField] private Behaviour[] fightOnly;

    [Header("Rules")]
    [SerializeField] private MatchParams rules = MatchParams.Default;

    [Header("Debug keys (editor / desktop)")]
    [Tooltip("Enter = start from the menu or skip the result screen, Esc = back to the menu")]
    [SerializeField] private bool debugKeys = true;

    private MatchStateMachine _match;

    public MatchStateMachine Match => _match;

    private void Awake()
    {
        _match = new MatchStateMachine(rules);
        _match.PhaseChanged += OnPhaseChanged;
        SetFightControls(false);
    }

    private void OnEnable()
    {
        if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        if (opponentHealth != null) opponentHealth.Died += OnOpponentDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        if (opponentHealth != null) opponentHealth.Died -= OnOpponentDied;
    }

    // Menu entry points (debug keys now, the hand menu in T7).
    public void StartMatch(bool withTutorial = false) => _match.StartMatch(withTutorial);
    public void CompleteTutorial() => _match.CompleteTutorial();
    public void ReturnToMenu() => _match.ReturnToMenu();

    private void Update()
    {
        if (debugKeys) HandleDebugKeys();

        _match.Params = rules;
        _match.Tick(Time.deltaTime, Health01(playerHealth), Health01(opponentHealth));
    }

    private void HandleDebugKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            if (_match.Phase == MatchPhase.Menu) StartMatch();
            else if (_match.Phase == MatchPhase.MatchEnd) ReturnToMenu();
        }
        else if (keyboard.escapeKey.wasPressedThisFrame && _match.Phase != MatchPhase.Menu)
        {
            ReturnToMenu();
        }
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        if (phase == MatchPhase.Countdown || phase == MatchPhase.Menu)
        {
            // Fresh heroes at their spawn points for every round (and in the menu).
            if (playerHealth != null) playerHealth.ResetHealth();
            if (opponentHealth != null) opponentHealth.ResetHealth();
        }

        SetFightControls(phase == MatchPhase.Fight || phase == MatchPhase.Tutorial);
    }

    private void OnPlayerDied() => _match.ReportKO(MatchSide.Player);
    private void OnOpponentDied() => _match.ReportKO(MatchSide.Opponent);

    // Disabling an input source zeroes its HandInputData, so the puppeteer lets
    // go and the hero glides to a stop; the bot brain resets when re-enabled.
    private void SetFightControls(bool on)
    {
        if (fightOnly == null) return;
        foreach (Behaviour b in fightOnly)
            if (b != null) b.enabled = on;
    }

    private static float Health01(HeroHealth h)
    {
        return h != null && h.MaxHealth > 0f ? h.CurrentHealth / h.MaxHealth : 0f;
    }
}
