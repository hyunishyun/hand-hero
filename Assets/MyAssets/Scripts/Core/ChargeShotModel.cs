using System;
using UnityEngine;

namespace HandHero.Core
{
    [Serializable]
    public struct ChargeParams
    {
        [Tooltip("Shorter holds release nothing (no accidental charge shots)")]
        public float MinChargeTime;
        [Tooltip("Charge stops growing after this many seconds")]
        public float MaxChargeTime;

        public static ChargeParams Default => new ChargeParams
        {
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
    }

    // Charge shot (T5): charge grows while the gesture is held and fires on
    // release, with power proportional to the charge time.
    public class ChargeShotModel
    {
        private float _time;
        private bool _charging;

        public ChargeStep Step(bool held, float dt, ChargeParams p)
        {
            var step = new ChargeStep();

            if (held)
            {
                _charging = true;
                _time = Mathf.Min(_time + dt, p.MaxChargeTime);
                step.Charging = true;
                step.Fraction = p.MaxChargeTime > 0f ? _time / p.MaxChargeTime : 1f;
                step.Ready = _time >= p.MinChargeTime;
                return step;
            }

            if (_charging)
            {
                if (_time >= p.MinChargeTime)
                {
                    step.Released = true;
                    step.Power = Power(_time, p);
                }
                Cancel();
            }

            return step;
        }

        // Drops the charge without firing (hero died).
        public void Cancel()
        {
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
