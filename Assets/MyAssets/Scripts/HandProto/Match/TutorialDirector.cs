using HandHero.Core;
using TMPro;
using UnityEngine;

// 30-second tutorial (T8, "first five minutes"): feeds what the scene sees into
// TutorialSequencer (HandHero.Core) and shows one short prompt per step, a goal
// ring, a practice target and a practice telegraph beam. Runs during
// MatchPhase.Tutorial only; the bot stays off (MatchDirector.opponentOnly).
// The ghost hand is a placeholder for a proper hand animation.
public class TutorialDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchDirector director;
    [SerializeField] private FlyingCharacter playerHero;
    [Tooltip("The input source driving the player's hero (same one the controllers use)")]
    [SerializeField] private HandInputSourceBehaviour playerInput;
    [Tooltip("Seat camera, for the ghost hand placement. Falls back to Camera.main")]
    [SerializeField] private Transform head;

    [Header("Scene objects (shown only during the tutorial)")]
    [SerializeField] private GameObject tutorialRoot;
    [SerializeField] private Transform ring;
    [SerializeField] private float ringRadius = 2f;
    [SerializeField] private BeamHitReceiver target;
    [Tooltip("Aim counts while the aim ray passes within this distance of the target center")]
    [SerializeField] private float targetAimRadius = 1.5f;
    [SerializeField] private Transform telegraphOrigin;
    [SerializeField] private LineRenderer telegraph;
    [Tooltip("May live outside the tutorial root (seat space, scaled with the tabletop view); shown with it")]
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private Transform ghostHand;

    [Header("Rules")]
    [SerializeField] private TutorialParams steps = TutorialParams.Default;
    [Tooltip("Hero speed (m/s) that counts as still gliding after letting go")]
    [SerializeField] private float glideMinSpeed = 1.5f;

    [Header("Telegraph look (same as the bot's warning line)")]
    [SerializeField] private float telegraphStartWidth = 0.03f;
    [SerializeField] private float telegraphEndWidth = 0.25f;
    [SerializeField] private Color telegraphStartColor = new Color(1f, 0.9f, 0.2f, 0.5f);
    [SerializeField] private Color telegraphEndColor = new Color(1f, 0.1f, 0.05f, 1f);
    [SerializeField] private float shotFlashTime = 0.15f;

    private TutorialSequencer _sequencer;
    private bool _running;
    private bool _targetHit;
    private float _shotFlash;
    private float _lastShotHitTime = -10f;

    private void OnEnable()
    {
        if (target != null) target.Hit += OnTargetHit;
    }

    private void OnDisable()
    {
        if (target != null) target.Hit -= OnTargetHit;
    }

    private void OnTargetHit(BeamHit hit)
    {
        if (hit.Shooter == playerHero) _targetHit = true;
    }

    private void Update()
    {
        MatchStateMachine m = director != null ? director.Match : null;
        bool active = m != null && m.Phase == MatchPhase.Tutorial;

        if (!active)
        {
            if (_running) Stop();
            return;
        }
        if (!_running) Begin();
        if (m.IsPaused) return;

        _sequencer.Params = steps;
        _sequencer.Tick(Time.deltaTime, Observe());
        _targetHit = false;

        if (_sequencer.IsDone)
        {
            director.CompleteTutorial();
            return;
        }

        UpdateVisuals();
    }

    private void Begin()
    {
        _running = true;
        _targetHit = false;
        _shotFlash = 0f;
        _sequencer = new TutorialSequencer(steps);
        _sequencer.DodgeShotFired += OnDodgeShot;
        if (tutorialRoot != null) tutorialRoot.SetActive(true);
        if (prompt != null) prompt.gameObject.SetActive(true);
    }

    private void Stop()
    {
        _running = false;
        if (tutorialRoot != null) tutorialRoot.SetActive(false);
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    private TutorialObservation Observe()
    {
        HandInputData input = playerInput != null ? playerInput.Current : default;
        var o = new TutorialObservation { ClutchHeld = input.ClutchHeld, TargetHit = _targetHit };

        if (playerHero != null)
        {
            o.HeroPosition = playerHero.transform.position;
            o.HeroMoving = playerHero.Velocity.magnitude >= glideMinSpeed;
            o.HeroInRing = ring != null && Vector3.Distance(o.HeroPosition, ring.position) <= ringRadius;
        }

        if (input.HasAim && target != null)
        {
            var aim = new Ray(input.AimOrigin, input.AimDirection);
            Vector3 toTarget = target.transform.position - aim.origin;
            float along = Vector3.Dot(toTarget, aim.direction);
            o.AimOnTarget = along > 0f && Vector3.Cross(aim.direction, toTarget).magnitude <= targetAimRadius;
        }

        return o;
    }

    private void OnDodgeShot(bool hit)
    {
        _shotFlash = shotFlashTime;
        if (hit) _lastShotHitTime = Time.time;
    }

    private void UpdateVisuals()
    {
        TutorialStep step = _sequencer.Step;

        if (ring != null) ring.gameObject.SetActive(step <= TutorialStep.DragToRing);
        if (target != null) target.gameObject.SetActive(step == TutorialStep.Aim || step == TutorialStep.Shoot);
        if (prompt != null) prompt.text = PromptText(step);

        UpdateTelegraph();
        UpdateGhostHand(step);
    }

    private string PromptText(TutorialStep step)
    {
        if (_sequencer.IsCelebrating) return "NICE!";

        switch (step)
        {
            case TutorialStep.Grab: return "Make a fist with your LEFT hand";
            case TutorialStep.DragToRing: return "Keep the fist and drag your hero into the ring";
            case TutorialStep.Glide: return "Open your hand - let it glide";
            case TutorialStep.Aim: return "Point your RIGHT hand at the target";
            case TutorialStep.Shoot: return "Pinch to shoot";
            case TutorialStep.Dodge:
                return Time.time - _lastShotHitTime < 1.5f
                    ? "Hit! Drag away when the line turns red"
                    : "Red line = incoming beam. Drag away!";
            default: return "";
        }
    }

    private void UpdateTelegraph()
    {
        if (telegraph == null) return;

        _shotFlash -= Time.deltaTime;
        bool show = _sequencer.IsTelegraphing || _shotFlash > 0f;
        telegraph.enabled = show && telegraphOrigin != null;
        if (!telegraph.enabled) return;

        float t = _sequencer.IsTelegraphing ? _sequencer.TelegraphProgress : 1f;
        telegraph.positionCount = 2;
        telegraph.SetPosition(0, telegraphOrigin.position);
        telegraph.SetPosition(1, _sequencer.LockedPoint);
        telegraph.widthMultiplier = Mathf.Lerp(telegraphStartWidth, telegraphEndWidth, t);
        Color c = Color.Lerp(telegraphStartColor, telegraphEndColor, t);
        telegraph.startColor = c;
        telegraph.endColor = c;
    }

    // Placeholder: a dot that shows where and how to move the hand.
    private void UpdateGhostHand(TutorialStep step)
    {
        if (ghostHand == null) return;
        Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
        bool show = h != null && !_sequencer.IsCelebrating && step != TutorialStep.Glide;
        ghostHand.gameObject.SetActive(show);
        if (!show) return;

        // Comfortable hand spots in front of the seat (left = clutch hand, right = aim hand).
        // Physical meters: lossyScale is the tabletop view scale (1 in the VR arena).
        float s = h.lossyScale.x;
        Vector3 leftHand = h.position + new Vector3(-0.2f, -0.35f, 0.4f) * s;
        Vector3 rightHand = h.position + new Vector3(0.2f, -0.3f, 0.4f) * s;
        float ping = Mathf.PingPong(Time.time * 0.8f, 1f);
        float pulse = 1f + 0.3f * Mathf.Sin(Time.time * 6f);

        Vector3 pos = leftHand;
        float scale = 1f;
        switch (step)
        {
            case TutorialStep.Grab:
                scale = pulse;
                break;
            case TutorialStep.DragToRing:
                if (playerHero != null && ring != null)
                {
                    // Small hand motion in the direction the hero has to travel.
                    Vector3 dir = (ring.position - playerHero.transform.position).normalized;
                    pos = leftHand + dir * (0.12f * ping * s);
                }
                break;
            case TutorialStep.Aim:
                pos = rightHand;
                break;
            case TutorialStep.Shoot:
                pos = rightHand;
                scale = pulse;
                break;
            case TutorialStep.Dodge:
                pos = leftHand + Vector3.right * (0.15f * (ping - 0.5f) * s);
                break;
        }

        ghostHand.position = pos;
        ghostHand.localScale = Vector3.one * (0.05f * scale * s);
    }
}
