using System;
using UnityEngine;

namespace HandHero.Core
{
    // Both palms together = charge shot held (T5). Hysteresis on palm distance
    // (ADR 8). Hands that touch often occlude each other, so a short tracking
    // loss keeps the charge; a longer one releases it.
    public class PalmsTogetherRecognizer
    {
        private HysteresisGate _gate;
        private float _lostTime;

        public bool IsHeld => _gate.IsOn;

        // Returns true while the palms count as together.
        public bool Step(bool leftTracked, bool rightTracked, Vector3 leftPalm, Vector3 rightPalm,
            float joinDistance, float separateDistance, float lostGraceTime, float dt)
        {
            if (!leftTracked || !rightTracked)
            {
                if (!_gate.IsOn) return false;
                _lostTime += dt;
                if (_lostTime > lostGraceTime) _gate.Reset();
                return _gate.IsOn;
            }

            _lostTime = 0f;
            float distance = Vector3.Distance(leftPalm, rightPalm);
            // Closer = stronger: negated so the gate's "value >= on" reads "distance <= join".
            _gate.Step(-distance, -joinDistance, -separateDistance);
            return _gate.IsOn;
        }
    }

    [Serializable]
    public struct PalmPushParams
    {
        [Tooltip("Palm speed (m/s) along the palm's facing direction that triggers a push")]
        public float PushSpeed;
        [Tooltip("Palm speed below this re-arms the next push (lower than PushSpeed = no flicker)")]
        public float RearmSpeed;
        [Tooltip("Fist strength must be at or below this: an open palm, not a punch or a clutch drag")]
        public float MaxFistStrength;

        public static PalmPushParams Default => new PalmPushParams
        {
            PushSpeed = 1.2f,
            RearmSpeed = 0.4f,
            MaxFistStrength = 0.35f,
        };
    }

    // Open palm shoved quickly toward where it faces = shockwave (T5, Q7).
    // Hysteresis on the palm's speed along its normal: one trigger per push,
    // re-armed only after the hand slows down. A push that starts as a fist
    // never fires later in the same motion.
    public class PalmPushRecognizer
    {
        private HysteresisGate _gate;
        private Vector3 _lastPosition;
        private bool _hasLast;

        // Returns true on the single frame a push is recognized.
        public bool Step(bool tracked, Vector3 palmPosition, Vector3 palmNormal, float fistStrength, float dt,
            PalmPushParams p)
        {
            if (!tracked)
            {
                // The reacquired hand can be anywhere: never read that jump as speed.
                _gate.Reset();
                _hasLast = false;
                return false;
            }

            if (!_hasLast)
            {
                _lastPosition = palmPosition;
                _hasLast = true;
                return false;
            }

            if (dt <= 0f) return false;

            float speed = Vector3.Dot(palmPosition - _lastPosition, palmNormal.normalized) / dt;
            _lastPosition = palmPosition;

            bool rising = _gate.Step(speed, p.PushSpeed, p.RearmSpeed) == GateEdge.Rising;
            return rising && fistStrength <= p.MaxFistStrength;
        }
    }
}
