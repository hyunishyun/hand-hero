using System;
using UnityEngine;

namespace HandHero.Core
{
    // Demo mode tuning (round 5, T4 / D6): a practice arena for recordings and
    // warm-ups. The demo bots are Strikers made slower and gentler than the run's;
    // the player is invulnerable (HeroHealthModel.Invulnerable).
    [Serializable]
    public struct DemoParams
    {
        [Tooltip("Strikers flying in the demo (0-2); each comes back after a KO like any hero")]
        public int BotCount;
        [Tooltip("x the bots' max hand speed (BotParams.MaxHandSpeed), 0.1-1: below 1 = slower than the run's Strikers")]
        public float BotSpeedScale;
        [Tooltip("x the Striker telegraph time: above 1 = a longer warning")]
        public float TelegraphMult;
        [Tooltip("x the seconds between the bots' shots")]
        public float FireIntervalMult;
        [Tooltip("x the bots' shot damage (the player loses no health in the demo; the hit effects still play)")]
        public float DamageMult;

        public static DemoParams Default => new DemoParams
        {
            BotCount = 2,
            BotSpeedScale = 0.6f,
            TelegraphMult = 1.8f,
            FireIntervalMult = 1.6f,
            DamageMult = 0.4f,
        };
    }

    // What the demo spawns (round 5, T4). DemoDirector keeps MaxBots bots ready
    // from scene load and activates BotCount of them with Archetype and Spec.
    public static class DemoRules
    {
        public const int MaxBots = 2;

        // Never more than MaxBots or the bots that exist.
        public static int BotCount(DemoParams p, int available)
        {
            return Mathf.Clamp(p.BotCount, 0, Mathf.Min(MaxBots, Mathf.Max(0, available)));
        }

        // A Striker (its looks and attack) with a longer warning.
        public static BotArchetype Archetype(DemoParams p)
        {
            BotArchetype a = BotArchetypes.Get(BotArchetypeId.Striker);
            a.Attack.TelegraphMult = BotArchetypes.StrikerAttack.TelegraphMult * Positive(p.TelegraphMult);
            a.AltAttack = a.Attack;
            a.SwitchEvery = 0;
            return a;
        }

        // The enemy scaling RunBot.Activate applies: full health, the demo's damage
        // and shot pacing. A zero or negative multiplier falls back to 1.
        public static IslandSpec Spec(DemoParams p)
        {
            int count = Mathf.Clamp(p.BotCount, 0, MaxBots);
            return new IslandSpec
            {
                Type = IslandType.Arena,
                BotCount = count,
                MaxAlive = count,
                HealthMult = 1f,
                DamageMult = Positive(p.DamageMult),
                FireIntervalMult = Positive(p.FireIntervalMult),
            };
        }

        // Never faster than the run's bots (the hand speed cap is the fairness rule).
        public static float SpeedScale(DemoParams p)
        {
            return p.BotSpeedScale > 0f ? Mathf.Clamp(p.BotSpeedScale, 0.1f, 1f) : 1f;
        }

        private static float Positive(float value) => value > 0f ? value : 1f;
    }

    public enum DemoCaption
    {
        None,
        Grab,      // the clutch hand closed
        Fire,      // a normal shot
        Charge,    // a charge started, or a charged shot fired
        Shockwave, // a palm push fired the shockwave
    }

    // In-game text stays English (rule 5b.10).
    public static class DemoCaptionText
    {
        public const string Grab = "GRAB";
        public const string PinchFire = "PINCH = FIRE";
        public const string TriggerFire = "TRIGGER = FIRE";
        public const string HoldCharge = "HOLD = CHARGE";
        public const string PushShockwave = "PUSH = SHOCKWAVE";

        // CURSOR aim fires with the index trigger, not a pinch.
        public static string Text(DemoCaption caption, bool cursorAim)
        {
            switch (caption)
            {
                case DemoCaption.Grab: return Grab;
                case DemoCaption.Fire: return cursorAim ? TriggerFire : PinchFire;
                case DemoCaption.Charge: return HoldCharge;
                case DemoCaption.Shockwave: return PushShockwave;
                default: return "";
            }
        }
    }

    [Serializable]
    public struct DemoCaptionParams
    {
        [Tooltip("Seconds a caption shows at full strength")]
        public float ShowTime;
        [Tooltip("Seconds it then takes to fade out")]
        public float FadeTime;

        public static DemoCaptionParams Default => new DemoCaptionParams
        {
            ShowTime = 0.8f,
            FadeTime = 0.3f,
        };
    }

    // One frame of what the player's hands and controllers did (GestureCaptions fills it).
    public struct DemoGestureFrame
    {
        // The clutch (GRAB) is closed (level).
        public bool ClutchHeld;
        // The player's beam is charging (level, after the charge HoldDelay).
        public bool Charging;
        // The player's beam: running totals since its last ResetShotStats (normal + charged).
        public int ShotsFired;
        public int ChargeShotsFired;
        // A shockwave fired this frame, from the clutch hand's push (else the aim hand's).
        public bool Shockwave;
        public bool ShockwaveByClutchHand;
    }

    // Which caption shows beside each hand (round 5, T4 / D6). Edges only: GRAB
    // when the clutch closes, PINCH = FIRE on a normal shot, HOLD = CHARGE when a
    // charge starts or a charged shot fires, PUSH = SHOCKWAVE beside the pushing
    // hand. A newer caption replaces the one shown and restarts its time; within
    // one frame Shockwave beats Charge beats Fire, and Shockwave beats Grab.
    // The shot counts are running totals: a count that goes down (a new run reset
    // them) only re-bases. The first Step after Reset only takes the baseline, so
    // a fist or charge already held when the demo starts shows nothing.
    public class DemoCaptionModel
    {
        private bool _synced;
        private bool _clutchWas;
        private bool _chargingWas;
        private int _shots;
        private int _chargeShots;
        private DemoCaption _clutch;
        private DemoCaption _aim;
        private float _clutchAge;
        private float _aimAge;
        private DemoCaptionParams _p = DemoCaptionParams.Default;

        // Caption beside the clutch hand / the aim hand.
        public DemoCaption Clutch => _clutch;
        public DemoCaption Aim => _aim;
        public float ClutchAlpha => Alpha(_clutch, _clutchAge, _p);
        public float AimAlpha => Alpha(_aim, _aimAge, _p);

        public void Reset()
        {
            _synced = false;
            _clutch = DemoCaption.None;
            _aim = DemoCaption.None;
            _clutchAge = 0f;
            _aimAge = 0f;
        }

        public void Step(in DemoGestureFrame frame, float dt, DemoCaptionParams p)
        {
            _p = p;
            Age(ref _clutch, ref _clutchAge, dt, p);
            Age(ref _aim, ref _aimAge, dt, p);

            if (!_synced)
            {
                _synced = true;
                Rebase(frame);
                return;
            }

            bool grab = frame.ClutchHeld && !_clutchWas;
            bool chargeStart = frame.Charging && !_chargingWas;
            int shots = frame.ShotsFired - _shots;
            int charged = frame.ChargeShotsFired - _chargeShots;
            if (shots < 0 || charged < 0)
            {
                shots = 0;
                charged = 0;
            }
            int normal = shots - charged;
            Rebase(frame);

            if (grab) Show(ref _clutch, ref _clutchAge, DemoCaption.Grab);
            if (normal > 0) Show(ref _aim, ref _aimAge, DemoCaption.Fire);
            if (chargeStart || charged > 0) Show(ref _aim, ref _aimAge, DemoCaption.Charge);
            if (frame.Shockwave)
            {
                if (frame.ShockwaveByClutchHand) Show(ref _clutch, ref _clutchAge, DemoCaption.Shockwave);
                else Show(ref _aim, ref _aimAge, DemoCaption.Shockwave);
            }
        }

        // 1 while showing, then a linear fade to 0; 0 without a caption.
        public static float Alpha(DemoCaption caption, float age, DemoCaptionParams p)
        {
            if (caption == DemoCaption.None) return 0f;
            if (age <= p.ShowTime) return 1f;
            return p.FadeTime > 0f ? Mathf.Clamp01(1f - (age - p.ShowTime) / p.FadeTime) : 0f;
        }

        private void Rebase(in DemoGestureFrame frame)
        {
            _clutchWas = frame.ClutchHeld;
            _chargingWas = frame.Charging;
            _shots = frame.ShotsFired;
            _chargeShots = frame.ChargeShotsFired;
        }

        private static void Show(ref DemoCaption slot, ref float age, DemoCaption caption)
        {
            slot = caption;
            age = 0f;
        }

        private static void Age(ref DemoCaption caption, ref float age, float dt, DemoCaptionParams p)
        {
            if (caption == DemoCaption.None) return;
            age += Mathf.Max(0f, dt);
            if (age < p.ShowTime + Mathf.Max(0f, p.FadeTime)) return;
            caption = DemoCaption.None;
            age = 0f;
        }
    }

    [Serializable]
    public struct DemoCaptionLayoutParams
    {
        [Tooltip("Meters from the palm to the caption's inner edge, toward the outside of the view")]
        public float SideOffset;
        [Tooltip("Meters the caption sits above the palm")]
        public float UpOffset;
        [Tooltip("Caption width the hero check assumes, meters (about the longest caption)")]
        public float LabelWidth;
        [Tooltip("The caption keeps at least this many degrees from the line of sight to the player's hero")]
        public float HeroClearAngle;
        [Tooltip("Meters the caption moves down per try while it would cover the hero")]
        public float StepDown;
        [Tooltip("Tries before the caption stays where it is")]
        public int MaxSteps;

        public static DemoCaptionLayoutParams Default => new DemoCaptionLayoutParams
        {
            SideOffset = 0.07f,
            UpOffset = 0.04f,
            LabelWidth = 0.24f,
            HeroClearAngle = 6f,
            StepDown = 0.05f,
            MaxSteps = 6,
        };
    }

    // Where a caption goes (round 5, T4): beside the palm on the outer side of the
    // view (left hand -> further left, right hand -> further right), a little above
    // it, stepped down while it would cover the player's hero. Offsets are physical
    // meters times worldScale (the tabletop view). Returns the caption's inner
    // edge; the text runs outward from it.
    public static class DemoCaptionLayout
    {
        public static Vector3 Place(Vector3 head, Vector3 palm, Vector3 viewRight, bool leftHand, bool hasHero,
            Vector3 hero, float worldScale, DemoCaptionLayoutParams p)
        {
            float s = worldScale > 0f ? worldScale : 1f;
            Vector3 side = OuterSide(viewRight, leftHand);
            Vector3 anchor = palm + (side * p.SideOffset + Vector3.up * p.UpOffset) * s;
            if (!hasHero) return anchor;
            for (int i = 0; i < p.MaxSteps && CoversHero(head, anchor, side, hero, s, p); i++)
                anchor += Vector3.down * (p.StepDown * s);
            return anchor;
        }

        // The view's right, levelled (a tilted head doesn't tilt the captions).
        public static Vector3 OuterSide(Vector3 viewRight, bool leftHand)
        {
            var right = new Vector3(viewRight.x, 0f, viewRight.z);
            if (right.sqrMagnitude < 1e-8f) right = Vector3.right;
            right.Normalize();
            return leftHand ? -right : right;
        }

        // The caption's inner edge, middle or outer edge is within HeroClearAngle
        // of the line of sight from the head to the hero.
        public static bool CoversHero(Vector3 head, Vector3 anchor, Vector3 side, Vector3 hero, float worldScale,
            DemoCaptionLayoutParams p)
        {
            Vector3 toHero = hero - head;
            if (toHero.sqrMagnitude < 1e-8f) return false;
            for (int k = 0; k <= 2; k++)
            {
                Vector3 toPoint = anchor + side * (p.LabelWidth * worldScale * 0.5f * k) - head;
                if (toPoint.sqrMagnitude < 1e-8f) continue;
                if (Vector3.Angle(toPoint, toHero) < p.HeroClearAngle) return true;
            }
            return false;
        }
    }

    // Which hand's palm push fired the shockwave (the input only says that one
    // did): the only tracked hand, else the one moving faster along its palm
    // normal (the push recognizers' measure). A tie or no tracked hand -> fallbackLeft.
    public static class DemoPushHand
    {
        public static bool IsLeft(bool leftTracked, float leftPushSpeed, bool rightTracked, float rightPushSpeed,
            bool fallbackLeft)
        {
            if (leftTracked != rightTracked) return leftTracked;
            if (!leftTracked || Mathf.Approximately(leftPushSpeed, rightPushSpeed)) return fallbackLeft;
            return leftPushSpeed > rightPushSpeed;
        }
    }
}
