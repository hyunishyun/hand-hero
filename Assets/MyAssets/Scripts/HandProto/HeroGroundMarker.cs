using HandHero.Core;
using UnityEngine;

// Depth cue for third-person flight: a disc on the arena floor straight below
// the hero plus a faint drop line. Judging how far away a flying hero is in an
// empty 35 m box is hard without it. Presentation only, no collider.
public class HeroGroundMarker : MonoBehaviour
{
    [SerializeField] private FlyingCharacter character;
    [Tooltip("Flat disc (no collider) placed on the floor below the hero")]
    [SerializeField] private Transform disc;
    [Tooltip("Optional thin line from the hero down to the disc")]
    [SerializeField] private LineRenderer dropLine;

    [Tooltip("Disc diameter when the hero is on the floor")]
    [SerializeField] private float nearSize = 2.2f;
    [Tooltip("Disc diameter when the hero is at the arena ceiling")]
    [SerializeField] private float farSize = 0.8f;
    [SerializeField] private float floorLift = 0.03f;

    private void LateUpdate()
    {
        ArenaBounds bounds = character != null ? character.Bounds : default;
        bool visible = character != null && character.IsAlive && bounds.Enabled;
        if (disc != null) disc.gameObject.SetActive(visible);
        if (dropLine != null) dropLine.enabled = visible;
        if (!visible) return;

        Vector3 hero = character.transform.position;
        float floorY = bounds.Center.y - bounds.Size.y * 0.5f;
        Vector3 ground = new Vector3(hero.x, floorY + floorLift, hero.z);
        float height01 = bounds.Size.y > 0f ? Mathf.Clamp01((hero.y - floorY) / bounds.Size.y) : 0f;

        if (disc != null)
        {
            disc.position = ground;
            float size = Mathf.Lerp(nearSize, farSize, height01);
            disc.localScale = new Vector3(size, disc.localScale.y, size);
        }

        if (dropLine != null)
        {
            dropLine.positionCount = 2;
            dropLine.SetPosition(0, hero);
            dropLine.SetPosition(1, ground);
        }
    }
}
