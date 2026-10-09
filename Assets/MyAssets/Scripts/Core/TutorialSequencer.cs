using System;
using UnityEngine;

namespace HandHero.Core
{
    public enum TutorialStep
    {
        Grab,       // make a fist (clutch) and hold it
        DragToRing, // drag the hero into the ring while holding
        Glide,      // let go: the hero keeps gliding
        Aim,        // point at the target
        Shoot,      // pinch to hit it
        Dodge,      // a telegraphed beam locks on: move away before it fires
        Done,
    }

    // What the scene saw this frame; the sequencer stays free of Unity objects.
    public struct TutorialObservation
    {
        public bool ClutchHeld;
        public bool HeroInRing;
        public bool HeroMoving;   // hero speed above the scene's glide threshold
        public bool AimOnTarget;
        public bool TargetHit;    // the target was hit this frame
        public Vector3 HeroPosition;
    }

    [Serializable]
    public struct TutorialParams
    {
        [Tooltip("Seconds the fist must stay closed")]
        public float GrabHoldTime;
        [Tooltip("Seconds the released hero must keep moving")]
        public float GlideTime;
        [Tooltip("Seconds the aim ray must stay on the target")]
        public float AimHoldTime;
        [Tooltip("Pause before each practice beam locks on")]
        public float DodgeWaitTime;
        [Tooltip("Warning time before the practice beam fires (same as the bot's telegraph)")]
        public float DodgeTelegraphTime;
        [Tooltip("The practice beam hits if the hero is still within this distance of the locked point")]
        public float DodgeHitRadius;
        [Tooltip("Seconds of \"nice!\" after each step")]
        public float CelebrateTime;

        public static TutorialParams Default => new TutorialParams
        {
            GrabHoldTime = 0.3f,
            GlideTime = 0.6f,
            AimHoldTime = 0.5f,
            DodgeWaitTime = 1.0f,
            DodgeTelegraphTime = 0.6f,
            DodgeHitRadius = 1.5f,
            CelebrateTime = 0.8f,
        };
    }

    // 30-second tutorial (T8): one gesture per step, each detected automatically.
    // Hold-type steps need the condition continuously (a flicker restarts the
    // count). The dodge step repeats the practice shot until the player avoids it.
    public class TutorialSequencer
    {
        private float _holdTimer;
        private float _celebrateTimer;
        private float _dodgeTimer;

        public TutorialSequencer(TutorialParams p)
        {
            Params = p;
            Step = TutorialStep.Grab;
        }

        public event Action<TutorialStep> StepChanged;
        // true = the practice beam hit the hero (the step repeats), false = dodged.
        public event Action<bool> DodgeShotFired;

        public TutorialParams Params { get; set; }
        public TutorialStep Step { get; private set; }
        public bool IsDone => Step == TutorialStep.Done;
        public bool IsCelebrating { get; private set; }

        public bool IsTelegraphing { get; private set; }
        public Vector3 LockedPoint { get; private set; }
        public float TelegraphProgress => IsTelegraphing ? Progress(_dodgeTimer, Params.DodgeTelegraphTime) : 0f;

        // elapsed / duration in 0..1; a zero duration is already complete (BC-9, no NaN).
        public static float Progress(float elapsed, float duration)
        {
            return duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        }

        // 0..1 for hold steps (fills a progress ring), 0 otherwise.
        public float StepProgress
        {
            get
            {
                float need = HoldTime(Step);
                return need > 0f ? Mathf.Clamp01(_holdTimer / need) : 0f;
            }
        }

        public void Skip()
        {
            if (IsDone) return;
            IsCelebrating = false;
            Enter(TutorialStep.Done);
        }

        public void Tick(float dt, TutorialObservation o)
        {
            if (IsDone) return;

            if (IsCelebrating)
            {
                _celebrateTimer += dt;
                if (_celebrateTimer >= Params.CelebrateTime)
                {
                    IsCelebrating = false;
                    Enter(Step + 1);
                }
                return;
            }

            switch (Step)
            {
                case TutorialStep.Grab: Hold(o.ClutchHeld, dt); break;
                case TutorialStep.DragToRing: if (o.ClutchHeld && o.HeroInRing) Succeed(); break;
                case TutorialStep.Glide: Hold(!o.ClutchHeld && o.HeroMoving, dt); break;
                case TutorialStep.Aim: Hold(o.AimOnTarget, dt); break;
                case TutorialStep.Shoot: if (o.TargetHit) Succeed(); break;
                case TutorialStep.Dodge: TickDodge(dt, o.HeroPosition); break;
            }
        }

        private void Hold(bool condition, float dt)
        {
            _holdTimer = condition ? _holdTimer + dt : 0f;
            if (_holdTimer >= HoldTime(Step)) Succeed();
        }

        private void TickDodge(float dt, Vector3 hero)
        {
            _dodgeTimer += dt;

            if (!IsTelegraphing)
            {
                if (_dodgeTimer < Params.DodgeWaitTime) return;
                IsTelegraphing = true;
                LockedPoint = hero;
                _dodgeTimer = 0f;
                return;
            }

            if (_dodgeTimer < Params.DodgeTelegraphTime) return;

            bool hit = Vector3.Distance(hero, LockedPoint) <= Params.DodgeHitRadius;
            IsTelegraphing = false;
            _dodgeTimer = 0f;
            DodgeShotFired?.Invoke(hit);
            if (!hit) Succeed();
        }

        private float HoldTime(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Grab: return Params.GrabHoldTime;
                case TutorialStep.Glide: return Params.GlideTime;
                case TutorialStep.Aim: return Params.AimHoldTime;
                default: return 0f;
            }
        }

        private void Succeed()
        {
            IsCelebrating = true;
            _celebrateTimer = 0f;
        }

        private void Enter(TutorialStep step)
        {
            Step = step;
            _holdTimer = 0f;
            _dodgeTimer = 0f;
            IsTelegraphing = false;
            StepChanged?.Invoke(step);
        }
    }
}
