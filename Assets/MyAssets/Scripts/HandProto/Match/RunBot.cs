using HandHero.Core;
using UnityEngine;

// Root of one RUN-mode bot, spawned by RunDirector from the prefab the scene
// builder generates (R8). Holds the bot's own hero and controllers; the prefab
// can't reference the scene, so Setup wires the arena and the enemy at spawn.
// Run bots never get items: only the island's enemy scaling applies.
public class RunBot : MonoBehaviour
{
    [SerializeField] private FlyingCharacter hero;
    [SerializeField] private HeroHealth health;
    [SerializeField] private BotInputSource input;
    [SerializeField] private PointingBeamController pointing;

    public HeroHealth Health => health;
    public FlyingCharacter Hero => hero;

    public void Setup(Transform arenaCenter, Transform enemy, IslandSpec spec)
    {
        if (hero != null) hero.SetArenaCenter(arenaCenter);
        if (input != null)
        {
            input.SetEnemy(enemy);
            input.SetFireIntervalScale(spec.FireIntervalMult);
        }
        if (pointing != null) pointing.SetDamageScale(spec.DamageMult);
        if (health != null) health.SetHealthScale(spec.HealthMult);
    }

    // Off = the bot lets go and glides (pause, island over), like the match gating.
    public void SetControlled(bool on)
    {
        if (input != null && input.enabled != on) input.enabled = on;
    }
}
