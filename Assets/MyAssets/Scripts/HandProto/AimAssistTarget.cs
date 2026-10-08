using System.Collections.Generic;
using UnityEngine;

// Something the aim assist may snap to: heroes and tutorial practice targets.
// Registers itself while enabled; a hero counts only while alive.
public class AimAssistTarget : MonoBehaviour
{
    private static readonly List<AimAssistTarget> Registry = new List<AimAssistTarget>();

    [Tooltip("Optional. The hero this belongs to: not targetable while dead")]
    [SerializeField] private FlyingCharacter character;

    public static IReadOnlyList<AimAssistTarget> All => Registry;

    public FlyingCharacter Character => character;

    public bool IsTargetable => isActiveAndEnabled && (character == null || character.IsAlive);

    private void OnEnable()
    {
        if (!Registry.Contains(this)) Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }
}
