using System.Collections.Generic;
using HandHero.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using InputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;

// Runs the single-player match (T6) around MatchStateMachine (HandHero.Core):
// resets both heroes at each countdown, lets them act only while fighting, and
// turns hero deaths into round KOs. Pausing (T7) freezes game time
// (Time.timeScale = 0); hand tracking and the menus run on unscaled time.
// Nothing here moves the XR Origin.
public class MatchDirector : MonoBehaviour
{
    public enum MenuAction
    {
        StartMatch,        // runs the tutorial first until it has been completed or skipped once
        StartWithTutorial,
        Resume,
        TogglePause,
        ReturnToMenu,
        SkipTutorial,
        ToggleViewMode,    // VR arena <-> passthrough tabletop, main menu only (T10)
    }

    private const string TutorialSeenKey = "HandHero.TutorialSeen";

    [Header("Heroes")]
    [SerializeField] private HeroHealth playerHealth;
    [SerializeField] private HeroHealth opponentHealth;

    [Header("Control gating")]
    [Tooltip("Enabled only while fighting (and in the tutorial): the player's input sources")]
    [SerializeField] private Behaviour[] fightOnly;
    [Tooltip("Enabled only while fighting, not in the tutorial: the bot")]
    [SerializeField] private Behaviour[] opponentOnly;

    [Header("Rules")]
    [SerializeField] private MatchParams rules = MatchParams.Default;

    [Header("View")]
    [Tooltip("Optional. VR arena / passthrough tabletop switch (Arena_Main only)")]
    [SerializeField] private ArenaViewMode viewMode;

    [Header("Auto pause")]
    [Tooltip("Pause when the app loses focus or is suspended (system menu, headset off, Meta button)")]
    [SerializeField] private bool pauseOnFocusLoss = true;
    [Tooltip("Pause when the headset's proximity sensor reports the user took it off")]
    [SerializeField] private bool pauseOnHeadsetRemoved = true;

    [Header("Debug keys (editor / desktop)")]
    [Tooltip("Enter = start from the menu / skip the tutorial or result screen, T = tutorial, Esc = back to the menu, P = pause/resume")]
    [SerializeField] private bool debugKeys = true;

    private MatchStateMachine _match;
    private bool _userWasPresent = true;
    private readonly List<XRInputDevice> _heads = new List<XRInputDevice>();

    public MatchStateMachine Match => _match;
    public bool IsPaused => _match != null && _match.IsPaused;

    private void Awake()
    {
        _match = new MatchStateMachine(rules);
        _match.PhaseChanged += OnPhaseChanged;
        _match.PausedChanged += OnPausedChanged;
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
        Time.timeScale = 1f; // never leave the app frozen
    }

    // Menu entry points: hand menu buttons, the wrist button and debug keys.
    public void StartMatch(bool withTutorial = false) => _match.StartMatch(withTutorial);

    // Completing or skipping both count as having seen the tutorial.
    public void CompleteTutorial()
    {
        if (_match.Phase != MatchPhase.Tutorial) return;
        _match.Resume(); // skipped from the pause panel
        _match.CompleteTutorial();
        PlayerPrefs.SetInt(TutorialSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static bool TutorialSeen => PlayerPrefs.GetInt(TutorialSeenKey, 0) == 1;
    public void ReturnToMenu() => _match.ReturnToMenu();
    public void Pause() => _match.Pause();
    public void Resume() => _match.Resume();
    public void TogglePause() => _match.TogglePause();

    public void HandleMenuAction(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.StartMatch: StartMatch(withTutorial: !TutorialSeen); break;
            case MenuAction.StartWithTutorial: StartMatch(withTutorial: true); break;
            case MenuAction.Resume: Resume(); break;
            case MenuAction.TogglePause: TogglePause(); break;
            case MenuAction.ReturnToMenu: ReturnToMenu(); break;
            case MenuAction.SkipTutorial: CompleteTutorial(); break;
            case MenuAction.ToggleViewMode: ToggleViewMode(); break;
        }
    }

    // The view only changes in the main menu, never mid-match (no view jumps while playing).
    public void ToggleViewMode()
    {
        if (viewMode != null && _match.Phase == MatchPhase.Menu) viewMode.Toggle();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && pauseOnFocusLoss) Pause();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && pauseOnFocusLoss) Pause();
    }

    private void Update()
    {
        if (debugKeys) HandleDebugKeys();
        if (pauseOnHeadsetRemoved) CheckUserPresence();

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
            else if (_match.Phase == MatchPhase.Tutorial) CompleteTutorial();
            else if (_match.Phase == MatchPhase.MatchEnd) ReturnToMenu();
        }
        else if (keyboard.tKey.wasPressedThisFrame && _match.Phase == MatchPhase.Menu)
        {
            StartMatch(withTutorial: true);
        }
        else if (keyboard.escapeKey.wasPressedThisFrame && _match.Phase != MatchPhase.Menu)
        {
            ReturnToMenu();
        }
        else if (keyboard.pKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    // Headset proximity sensor (OpenXR user presence). Pauses on the
    // present -> absent edge only; resuming is always the player's choice.
    private void CheckUserPresence()
    {
        _heads.Clear();
        XRInputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, _heads);
        if (_heads.Count == 0 || !_heads[0].TryGetFeatureValue(XRCommonUsages.userPresence, out bool present))
            return;

        if (_userWasPresent && !present) Pause();
        _userWasPresent = present;
    }

    private void OnPhaseChanged(MatchPhase phase)
    {
        if (phase == MatchPhase.Countdown || phase == MatchPhase.Menu)
        {
            // Fresh heroes at their spawn points for every round (and in the menu).
            if (playerHealth != null) playerHealth.ResetHealth();
            if (opponentHealth != null) opponentHealth.ResetHealth();
        }

        ApplyControlGating();
    }

    private void OnPausedChanged(bool paused)
    {
        Time.timeScale = paused ? 0f : 1f;
        ApplyControlGating();
    }

    private void ApplyControlGating()
    {
        bool fighting = _match.Phase == MatchPhase.Fight && !_match.IsPaused;
        bool practicing = _match.Phase == MatchPhase.Tutorial && !_match.IsPaused;
        SetEnabled(fightOnly, fighting || practicing);
        SetEnabled(opponentOnly, fighting);
    }

    private void OnPlayerDied() => _match.ReportKO(MatchSide.Player);
    private void OnOpponentDied() => _match.ReportKO(MatchSide.Opponent);

    private void SetFightControls(bool on)
    {
        SetEnabled(fightOnly, on);
        SetEnabled(opponentOnly, on);
    }

    // Disabling an input source zeroes its HandInputData, so the puppeteer lets
    // go and the hero glides to a stop; the bot brain resets when re-enabled.
    private static void SetEnabled(Behaviour[] behaviours, bool on)
    {
        if (behaviours == null) return;
        foreach (Behaviour b in behaviours)
            if (b != null) b.enabled = on;
    }

    private static float Health01(HeroHealth h)
    {
        return h != null && h.MaxHealth > 0f ? h.CurrentHealth / h.MaxHealth : 0f;
    }
}
