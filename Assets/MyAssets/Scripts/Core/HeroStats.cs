using System;

namespace HandHero.Core
{
    // The player's run stats, folded from the inventory (autonomous plan R3).
    // Neutral = no items: every multiplier 1, every add 0, so a hero without
    // a run (Quick Match, the bot) plays exactly as before.
    //
    // Combining rules (design doc §1.5):
    // - LinearMultiplier stats multiply across items (they make build synergies).
    // - Capped shares (crit chance, cooldown/charge cuts, damage reduction)
    //   combine as 1 - product(1 - share), so they never reach 100%.
    // - Linear stats add up.
    public struct HeroStats : IEquatable<HeroStats>
    {
        // A crit hits for this many times the damage, before CritDamageMult.
        public const float BaseCritMultiplier = 2f;

        // combat
        public float DamageMult { get; private set; }
        public float FireCooldownMult { get; private set; }
        public float CritChance { get; private set; }
        public float CritDamageMult { get; private set; }
        public float ChargeDamageMult { get; private set; }
        public float ChargeTimeMult { get; private set; }
        public float ShockwaveRadiusMult { get; private set; }
        public float StunDurationAdd { get; private set; }

        // survival
        public float MaxHealthAdd { get; private set; }
        public float MaxHealthMult { get; private set; }
        public float HealOnClear { get; private set; }
        public float SpeedMult { get; private set; }
        public float DamageTakenMult { get; private set; } // floored at Scaling.DamageTakenFloor

        // economy and run
        public float CrystalGainMult { get; private set; }
        public float CrystalPerClear { get; private set; }
        public int ExtraChoices { get; private set; }
        public float InterestRate { get; private set; }
        public int Revives { get; private set; }

        public static HeroStats Neutral => new HeroStats
        {
            DamageMult = 1f,
            FireCooldownMult = 1f,
            CritChance = 0f,
            CritDamageMult = 1f,
            ChargeDamageMult = 1f,
            ChargeTimeMult = 1f,
            ShockwaveRadiusMult = 1f,
            StunDurationAdd = 0f,
            MaxHealthAdd = 0f,
            MaxHealthMult = 1f,
            HealOnClear = 0f,
            SpeedMult = 1f,
            DamageTakenMult = 1f,
            CrystalGainMult = 1f,
            CrystalPerClear = 0f,
            ExtraChoices = 0,
            InterestRate = 0f,
            Revives = 0,
        };

        // Damage multiplier of a critical hit.
        public float CritHitMultiplier => BaseCritMultiplier * CritDamageMult;

        // Max health for a base value: (base + add) * mult.
        public float MaxHealth(float baseMaxHealth) => (baseMaxHealth + MaxHealthAdd) * MaxHealthMult;

        public static HeroStats From(Inventory inventory)
        {
            HeroStats s = Neutral;
            if (inventory == null) return s;

            float critMiss = 1f;  // product of (1 - crit chance)
            float reduction = 1f; // product of (1 - damage reduction)
            float increase = 1f;  // product of (1 + extra damage taken)

            // Index loops: foreach over IReadOnlyList boxes its enumerator (GM-9).
            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                OwnedItem owned = items[i];
                var effects = owned.Item.Effects;
                for (int j = 0; j < effects.Count; j++)
                    s.Apply(effects[j], owned.Level, ref critMiss, ref reduction, ref increase);
                var debuffs = owned.Item.Debuffs;
                for (int j = 0; j < debuffs.Count; j++)
                    s.Apply(debuffs[j], owned.Level, ref critMiss, ref reduction, ref increase);
            }

            s.CritChance = 1f - critMiss;
            s.DamageTakenMult = Math.Max(Scaling.DamageTakenFloor, reduction * increase);
            return s;
        }

        private void Apply(StatEffect e, int level, ref float critMiss, ref float reduction, ref float increase)
        {
            float v = e.Evaluate(level);
            switch (e.Stat)
            {
                case Stat.Damage: DamageMult *= v; break;
                case Stat.FireCooldownCut: FireCooldownMult *= 1f - v; break;
                case Stat.CritChance: critMiss *= 1f - v; break;
                case Stat.CritDamage: CritDamageMult *= v; break;
                case Stat.ChargeDamage: ChargeDamageMult *= v; break;
                case Stat.ChargeTimeCut: ChargeTimeMult *= 1f - v; break;
                case Stat.ShockwaveRadius: ShockwaveRadiusMult *= v; break;
                case Stat.StunDuration: StunDurationAdd += v; break;
                case Stat.MaxHealth: MaxHealthAdd += v; break;
                case Stat.MaxHealthLoss: MaxHealthMult *= 1f - v; break;
                case Stat.HealOnClear: HealOnClear += v; break;
                case Stat.Speed: SpeedMult *= v; break;
                case Stat.DamageReduction: reduction *= 1f - v; break;
                case Stat.DamageTakenIncrease: increase *= 1f + v; break;
                case Stat.CrystalGain: CrystalGainMult *= v; break;
                case Stat.CrystalPerClear: CrystalPerClear += v; break;
                case Stat.ExtraChoices: ExtraChoices += (int)Math.Round(v); break;
                case Stat.InterestRate: InterestRate += v; break;
                case Stat.Revives: Revives += (int)Math.Round(v); break;
            }
        }

        public bool Equals(HeroStats o)
        {
            return DamageMult == o.DamageMult && FireCooldownMult == o.FireCooldownMult
                && CritChance == o.CritChance && CritDamageMult == o.CritDamageMult
                && ChargeDamageMult == o.ChargeDamageMult && ChargeTimeMult == o.ChargeTimeMult
                && ShockwaveRadiusMult == o.ShockwaveRadiusMult && StunDurationAdd == o.StunDurationAdd
                && MaxHealthAdd == o.MaxHealthAdd && MaxHealthMult == o.MaxHealthMult
                && HealOnClear == o.HealOnClear && SpeedMult == o.SpeedMult
                && DamageTakenMult == o.DamageTakenMult && CrystalGainMult == o.CrystalGainMult
                && CrystalPerClear == o.CrystalPerClear && ExtraChoices == o.ExtraChoices
                && InterestRate == o.InterestRate && Revives == o.Revives;
        }

        public override bool Equals(object obj) => obj is HeroStats other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = DamageMult.GetHashCode();
                h = h * 31 + FireCooldownMult.GetHashCode();
                h = h * 31 + CritChance.GetHashCode();
                h = h * 31 + MaxHealthAdd.GetHashCode();
                h = h * 31 + DamageTakenMult.GetHashCode();
                h = h * 31 + CrystalGainMult.GetHashCode();
                return h;
            }
        }
    }
}
