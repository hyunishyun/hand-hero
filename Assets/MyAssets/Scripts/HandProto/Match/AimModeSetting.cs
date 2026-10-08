using System;
using TMPro;
using UnityEngine;

public enum AimMode
{
    Assist, // reticle on the hand ray, snaps to a target inside a cone
    Cursor, // right-hand fist drags a 3D aim marker
}

// Player's aim mode (ASSIST / CURSOR), switched from the main menu and
// remembered across runs. The player's PointingBeamController reads it.
public class AimModeSetting : MonoBehaviour
{
    private const string AimModeKey = "HandHero.AimMode";

    [Tooltip("Optional. Menu button label, shows the current mode")]
    [SerializeField] private TMP_Text toggleLabel;
    [SerializeField] private string assistText = "AIM: ASSIST";
    [SerializeField] private string cursorText = "AIM: CURSOR";
    [Tooltip("Start in the mode chosen last time (the first launch is always ASSIST)")]
    [SerializeField] private bool rememberChoice = true;

    public AimMode Mode { get; private set; }

    public event Action<AimMode> Changed;

    private void Awake()
    {
        Apply(rememberChoice && PlayerPrefs.GetInt(AimModeKey, 0) == 1 ? AimMode.Cursor : AimMode.Assist);
    }

    public void Toggle()
    {
        Apply(Mode == AimMode.Assist ? AimMode.Cursor : AimMode.Assist);
        if (!rememberChoice) return;
        PlayerPrefs.SetInt(AimModeKey, Mode == AimMode.Cursor ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void Apply(AimMode mode)
    {
        Mode = mode;
        if (toggleLabel != null) toggleLabel.text = mode == AimMode.Cursor ? cursorText : assistText;
        Changed?.Invoke(mode);
    }
}
