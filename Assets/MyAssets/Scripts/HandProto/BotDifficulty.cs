using HandHero.Core;
using UnityEngine;

// Tunable bot difficulty (reaction time, aim error, fire interval, ...).
// Read every frame by BotInputSource, so edits in Play mode apply live.
[CreateAssetMenu(menuName = "HandHero/Bot Difficulty", fileName = "BotDifficulty")]
public class BotDifficulty : ScriptableObject
{
    [SerializeField] private BotParams parameters = BotParams.Default;

    public BotParams Params => parameters;
}
