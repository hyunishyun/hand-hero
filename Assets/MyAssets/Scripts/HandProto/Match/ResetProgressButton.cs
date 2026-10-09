using HandHero.Core;
using UnityEngine;

// Pause panel RESET PROGRESS (round 4, S5 / D5): the first press arms the
// button (CONFIRM RESET), a second press within confirmWindow seconds clears
// the meta progression (starting relics, personal bests). The aim mode and the
// tutorial flag stay. Unscaled time: the pause panel runs at Time.timeScale 0.
[RequireComponent(typeof(HandMenuButton))]
public class ResetProgressButton : MonoBehaviour
{
    [SerializeField] private RunDirector run;
    [Tooltip("Seconds the button waits for the confirming second press")]
    [SerializeField] private float confirmWindow = 3f;
    [Tooltip("Seconds the done label stays after a reset")]
    [SerializeField] private float doneTime = 1.5f;
    [SerializeField] private string idleText = "RESET PROGRESS";
    [SerializeField] private string confirmText = "CONFIRM RESET";
    [SerializeField] private string doneText = "PROGRESS RESET";

    private enum LabelState { None, Idle, Confirm, Done }

    private HandMenuButton _button;
    private ConfirmGate _gate;
    private float _doneUntil = float.NegativeInfinity;
    private LabelState _shown = LabelState.None;

    private void Awake()
    {
        _button = GetComponent<HandMenuButton>();
        _gate = new ConfirmGate(confirmWindow);
        _button.SetCustomAction(OnPress);
    }

    // Closing the pause panel drops a pending confirm: the next visit starts over.
    private void OnDisable()
    {
        _gate?.Cancel();
        _doneUntil = float.NegativeInfinity;
        _shown = LabelState.None;
    }

    private void OnPress()
    {
        float now = Time.unscaledTime;
        _gate.Window = confirmWindow;
        if (_gate.Press(now))
        {
            if (run != null) run.ResetProgress();
            _doneUntil = now + doneTime;
        }
        else _doneUntil = float.NegativeInfinity;
        SfxPlayer.PlayUi(SfxId.MenuPress);
        Refresh();
    }

    private void LateUpdate() => Refresh();

    // The label string is assigned only when its state changes.
    private void Refresh()
    {
        float now = Time.unscaledTime;
        LabelState state = _gate.IsArmed(now) ? LabelState.Confirm
            : now < _doneUntil ? LabelState.Done
            : LabelState.Idle;
        if (state == _shown) return;
        _shown = state;
        _button.SetLabel(state == LabelState.Confirm ? confirmText : state == LabelState.Done ? doneText : idleText);
    }
}
