using System;

namespace HandHero.Core
{
    // How an item effect grows with its level (design doc §1.5).
    public enum ScalingType
    {
        Linear,           // base * level, added
        LinearMultiplier, // 1 + base * level, a multiplier
        Hyperbolic,       // level 1 = base, approaches 1 and never reaches it
        Exponential       // grows faster every level
    }

    // Item level curves (design doc §6.2). Capped stats (chances, cooldown and
    // damage reductions) use Hyperbolic so they can never reach 100%.
    public static class Scaling
    {
        // Damage taken never drops below this share, whatever the reductions.
        public const float DamageTakenFloor = 0.25f;

        // Hyperbolic base is kept below 1 (1 would divide by zero = instant 100%).
        private const float MaxHyperbolicBase = 0.99f;

        public static float Evaluate(ScalingType type, float baseValue, int level, float k = 1f)
        {
            if (level < 0) level = 0;
            switch (type)
            {
                case ScalingType.Linear:
                    return baseValue * level;
                case ScalingType.LinearMultiplier:
                    return 1f + baseValue * level;
                case ScalingType.Hyperbolic:
                    float b = Math.Min(baseValue, MaxHyperbolicBase);
                    return 1f - 1f / (1f + b / (1f - b) * level);
                case ScalingType.Exponential:
                    return baseValue * ((float)Math.Pow(1f + k, level) - 1f) / k;
            }
            return 0f;
        }

        // Incoming damage multiplier for a total reduction (0..1), floored.
        public static float DamageTakenMultiplier(float reduction)
        {
            return Math.Max(DamageTakenFloor, 1f - reduction);
        }
    }
}
