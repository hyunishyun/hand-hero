using HandHero.Core;
using UnityEngine;

// Enemy-beam warning: while the bot's aim is locked (BotBrain telegraph,
// ~0.6 s) a line from the bot hero through the locked aim point thickens and
// shifts from yellow to red. The player reads it and yanks their hero out of
// the line with the clutch hand — the core dodge loop.
public class BotTelegraphLine : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BotInputSource bot;
    [SerializeField] private FlyingCharacter botHero;
    [SerializeField] private LineRenderer line;

    [Header("Look")]
    [SerializeField] private float startWidth = 0.03f;
    [SerializeField] private float endWidth = 0.25f;
    [SerializeField] private Color startColor = new Color(1f, 0.9f, 0.2f, 0.4f);
    [SerializeField] private Color endColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Tooltip("Meters the line continues past the locked aim point")]
    [SerializeField] private float overshoot = 4f;

    private void Awake()
    {
        if (line != null)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (line == null) return;

        // A disabled bot (outside the fight, MatchDirector) keeps its last brain
        // state, so it must not leave a frozen warning line behind.
        bool show = bot != null && bot.isActiveAndEnabled && bot.Brain != null && bot.Brain.IsTelegraphing
                    && botHero != null && botHero.IsAlive;
        // Warning tone as the line appears (D14): the dodge cue also works by ear.
        if (show && !line.enabled) SfxPlayer.Play(SfxId.BotTelegraph, botHero.transform.position);
        line.enabled = show;
        if (!show) return;

        float t = bot.Brain.TelegraphProgress;
        Vector3 origin = botHero.transform.position;
        Vector3 aim = bot.Brain.LockedAimPoint;
        Vector3 dir = (aim - origin).normalized;

        line.SetPosition(0, origin);
        line.SetPosition(1, aim + dir * overshoot);

        // Archetypes (round 4, S6): the attack's width, and its beam color at the
        // start of the warning. It always ends in the same red: red = about to fire.
        BotAttack attack = bot.Brain.ActiveAttack;
        float width = Mathf.Lerp(startWidth, endWidth, t) * (attack.TelegraphWidthMult > 0f ? attack.TelegraphWidthMult : 1f);
        line.startWidth = width;
        line.endWidth = width;
        Color from = attack.BeamColor.a > 0f
            ? new Color(attack.BeamColor.r, attack.BeamColor.g, attack.BeamColor.b, startColor.a)
            : startColor;
        Color color = Color.Lerp(from, endColor, t);
        line.startColor = color;
        line.endColor = color;
    }
}
