using UnityEngine;

namespace HandHero.Core
{
    // Red edge flash on damage taken (P12, D15). Starts at full alpha and fades out
    // (quadratic, so the red leaves quickly). The comfort caps hold whatever the
    // inspector asks for: never brighter than MaxAlpha, never longer than MaxDuration.
    public class FlashEnvelope
    {
        public const float MaxAlpha = 0.35f;
        public const float MaxDuration = 0.25f;

        private float _time = float.PositiveInfinity;

        public bool IsActive => !float.IsPositiveInfinity(_time);

        public void Trigger() => _time = 0f;

        public void Cancel() => _time = float.PositiveInfinity;

        // Alpha for this frame, then advances by dt (use unscaled time: a pause
        // must never freeze the red edge on screen).
        public float Step(float dt, float alpha, float duration)
        {
            if (!IsActive) return 0f;
            float d = Mathf.Min(duration, MaxDuration);
            if (d <= 0f || _time >= d)
            {
                Cancel();
                return 0f;
            }

            float left = 1f - _time / d;
            float a = Mathf.Clamp(alpha, 0f, MaxAlpha) * left * left;
            _time += Mathf.Max(0f, dt);
            return a;
        }
    }

    // Kill burst shards (P12, D15): fly out fast, ease to a stop and shrink away.
    public static class KillBurstMotion
    {
        // Distance from the center after `age` seconds; stops growing at the lifetime.
        public static float Distance(float age, float lifetime, float speed)
        {
            if (lifetime <= 0f) return 0f;
            float t = Mathf.Clamp(age, 0f, lifetime);
            return speed * t * (1f - t / (2f * lifetime));
        }

        // Shard scale factor: 1 at the start, 0 at the lifetime.
        public static float Scale(float age, float lifetime)
        {
            if (lifetime <= 0f) return 0f;
            return Mathf.Clamp01(1f - age / lifetime);
        }
    }
}
