using HandHero.Core;
using UnityEngine;

// Shows the hands-only menu panel that fits the match state (T7) and turns the
// point-and-pinch pointer on only while one is up:
//   Menu      -> main panel (START)
//   paused    -> pause panel (RESUME / MENU)
//   MatchEnd  -> end panel (MENU; the match also returns by itself)
// Panels are world-fixed in front of the seat, never head-locked.
public class HandMenu : MonoBehaviour
{
    [SerializeField] private MatchDirector director;
    [SerializeField] private HandMenuPointer pointer;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject matchEndPanel;

    private void LateUpdate()
    {
        MatchStateMachine m = director != null ? director.Match : null;
        bool paused = m != null && m.IsPaused;
        bool main = m != null && !paused && m.Phase == MatchPhase.Menu;
        bool end = m != null && !paused && m.Phase == MatchPhase.MatchEnd;

        SetActive(mainPanel, main);
        SetActive(pausePanel, paused);
        SetActive(matchEndPanel, end);
        if (pointer != null) pointer.enabled = main || paused || end;
    }

    private static void SetActive(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }
}
