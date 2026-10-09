using System;
using UnityEngine;

namespace HandHero.Core
{
    [Serializable]
    public struct ChargeParams
    {
        [Tooltip("A pinch held shorter than this is a normal shot only: no charge, no slowdown, no orb (D13)")]
        public float HoldDelay;
        [Tooltip("Shorter holds release nothing (no accidental charge shots)")]
        public float MinChargeTime;
        [Tooltip("Charge stops growing after this many seconds")]
        public float MaxChargeTime;

        public static ChargeParams Default => new ChargeParams
        {
            HoldDelay = 0.25f,
            MinChargeTime = 0.3f,
            MaxChargeTime = 1.5f,
        };
    }

    public struct ChargeStep
    {
        public bool Charging;
        // Charge time / MaxChargeTime, for the charge visual.
        public float Fraction;
        // Charged long enough that releasing now fires.
        public bool Ready;
        // True on the single step the charge is released and fires.
        public bool Released;
        // 0 at MinChargeTime .. 1 at MaxChargeTime; valid when Released.
        public float Power;

        // A held gesture ended this step (released, not cancelled): how long it was
        // held in total and whether it got past HoldDelay. For tuning HoldDelay from data.
        public bool HoldEnded;
        public float HoldSeconds;
        public bool ChargeStarted;
    }

    // Charge shot (T5): charge grows while the gesture is held and fires on
    // release, with power proportional to the charge time. The charge only starts
    // after the gesture has been held for HoldDelay (D13): a quick pinch shot that
    // the smoothed pinch reads a little long never turns into a charge.
    // MinChargeTime and MaxChargeTime count from the charge start.
    public class ChargeShotModel
    {
        private bool _held;
        private float _holdTime;
        private float _time;
        private bool _charging;

        public ChargeStep Step(bool held, float dt, ChargeParams p)
        {
            var step = new ChargeStep();

            if (held)
            {
                _held = true;
                _holdTime += dt;
                float over = _holdTime - p.HoldDelay;
                if (over < 0f) return step;

                // The first charging step only counts the time past the delay.
                _time = Mathf.Min(_charging ? _time + dt : over, p.MaxChargeTime);
                _charging = true;
                step.Charging = true;
                step.Fraction = p.MaxChargeTime > 0f ? _time / p.MaxChargeTime : 1f;
                step.Ready = _time >= p.MinChargeTime;
                return step;
            }

            if (_held)
            {
                step.HoldEnded = true;
                step.HoldSeconds = _holdTime;
                step.ChargeStarted = _charging;
                if (_charging && _time >= p.MinChargeTime)
                {
                    step.Released = true;
                    step.Power = Power(_time, p);
                }
                Cancel();
            }

            return step;
        }

        // Drops the hold and any charge without firing (hero died, aim hand lost, pause).
        public void Cancel()
        {
            _held = false;
            _holdTime = 0f;
            _charging = false;
            _time = 0f;
        }

        public static float Power(float chargeTime, ChargeParams p)
        {
            float span = p.MaxChargeTime - p.MinChargeTime;
            return span > 0f ? Mathf.Clamp01((chargeTime - p.MinChargeTime) / span) : 1f;
        }
    }
}
