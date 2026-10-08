using HandHero.Core;
using UnityEngine;

// Shows the hands-only menu panel that fits the match state (T7) and turns the
// point-and-pinch pointer on only while one is up:
//   Menu      -> main panel (START / TUTORIAL)
//   paused    -> pause panel (RESUME / MENU), in the tutorial (RESUME / SKIP / MENU)
//   MatchEnd  -> end panel (MENU; the match also returns by itself)
//   Run       -> RunChoiceMenu shows its own portal / chest / shop panels
// Panels are world-fixed in front of the seat, never head-locked.
public class HandMenu : MonoBehaviour
{
    [SerializeField] private MatchDirector director;
    [SerializeField] private HandMenuPointer pointer;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject tutorialPausePanel;
    [SerializeField] private GameObject matchEndPanel;
    [Tooltip("Optional RUN choice panels (portal / chest / shop); the pointer is on while they show")]
    [SerializeField] private RunChoiceMenu runChoices;

    private void LateUpdate()
    {
        MatchStateMachine m = director != null ? director.Match : null;
        bool paused = m != null && m.IsPaused;
        bool main = m != null && !paused && m.Phase == MatchPhase.Menu;
        bool end = m != null && !paused && m.Phase == MatchPhase.MatchEnd;
        bool tutorial = m != null && m.Phase == MatchPhase.Tutorial;

        SetActive(mainPanel, main);
        SetActive(pausePanel, paused && !tutorial);
        SetActive(tutorialPausePanel, paused && tutorial);
        SetActive(matchEndPanel, end);
        bool runChoice = runChoices != null && runChoices.IsShowing;
        if (pointer != null) pointer.enabled = main || paused || end || runChoice;
    }

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
