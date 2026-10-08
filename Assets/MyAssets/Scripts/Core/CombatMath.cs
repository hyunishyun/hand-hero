namespace HandHero.Core
{
    public struct ShotDamage
    {
        public float Damage;
        public bool Crit;
    }

    // Run stats applied to combat numbers (autonomous plan R7). The runtime
    // controllers pass their tuned base values through these; HeroStats.Neutral
    // returns every base value unchanged, so heroes without run stats
    // (Quick Match, the bot) play exactly as before.
    public static class CombatMath
    {
        // roll: uniform 0..1 from the caller's RNG; below CritChance = crit.
        public static ShotDamage Shot(float baseDamage, bool charged, HeroStats s, double roll)
        {
            float damage = baseDamage * s.DamageMult;
            if (charged) damage *= s.ChargeDamageMult;

            bool crit = roll < s.CritChance;
            if (crit) damage *= s.CritHitMultiplier;
            return new ShotDamage { Damage = damage, Crit = crit };
        }

        public static float FireCooldown(float baseCooldown, HeroStats s) => baseCooldown * s.FireCooldownMult;

        // Both thresholds shrink, so the whole charge curve gets faster.
        public static ChargeParams Charge(ChargeParams p, HeroStats s)
        {
            p.MinChargeTime *= s.ChargeTimeMult;
            p.MaxChargeTime *= s.ChargeTimeMult;
            return p;
        }

        public static float DamageTaken(float damage, HeroStats s) => damage * s.DamageTakenMult;

        public static float MaxHealth(float baseMaxHealth, HeroStats s) => s.MaxHealth(baseMaxHealth);

        public static float SpeedMultiplier(HeroStats s) => s.SpeedMult;

        public static float ShockwaveRadius(float baseRadius, HeroStats s) => baseRadius * s.ShockwaveRadiusMult;

        public static float StunDuration(float baseDuration, HeroStats s) => baseDuration + s.StunDurationAdd;
    }
}
