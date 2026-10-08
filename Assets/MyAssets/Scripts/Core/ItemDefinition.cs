using System.Collections.Generic;

namespace HandHero.Core
{
    public enum ItemCategory { WeaponMod, AbilityMod, Perk, Relic }

    public enum ItemRarity { Common, Epic, Legendary, Greed }

    // Theme chests (Damage chest, Health chest, ...) pick their pool by theme.
    public enum ItemTheme { Damage, Ability, Health, Speed, Economy, Critical, Defense }

    // What an item effect changes. HeroStats (R3) turns these into multipliers.
    public enum Stat
    {
        Damage,              // LinearMultiplier
        FireCooldownCut,     // Hyperbolic share removed from the fire cooldown
        CritChance,          // Hyperbolic
        CritDamage,          // LinearMultiplier on the crit multiplier
        ChargeDamage,        // LinearMultiplier
        ChargeTimeCut,       // Hyperbolic share removed from the charge time
        ShockwaveRadius,     // LinearMultiplier
        StunDuration,        // Linear seconds
        MaxHealth,           // Linear points
        MaxHealthLoss,       // share of max health removed (Greed debuff)
        HealOnClear,         // Linear points
        Speed,               // LinearMultiplier
        DamageReduction,     // Hyperbolic, floored by Scaling.DamageTakenFloor
        DamageTakenIncrease, // share of extra damage taken (Greed debuff)
        CrystalGain,         // LinearMultiplier
        CrystalPerClear,     // Linear crystals
        ExtraChoices,        // Linear count
        InterestRate,        // Linear share of the balance
        Revives              // Linear count
    }

    // One buff or debuff of an item: Scaling.Evaluate(Scaling, Base, level).
    public readonly struct StatEffect
    {
        public readonly Stat Stat;
        public readonly float Base;
        public readonly ScalingType Scaling;

        public StatEffect(Stat stat, float baseValue, ScalingType scaling)
        {
            Stat = stat;
            Base = baseValue;
            Scaling = scaling;
        }

        public float Evaluate(int level) => Core.Scaling.Evaluate(Scaling, Base, level);
    }

    // Shared item data (design doc §1.2, §6.1). Chests, the shop and the hero
    // stats all read this one format.
    public sealed class ItemDefinition
    {
        private static readonly StatEffect[] NoEffects = new StatEffect[0];

        public readonly string Id;
        public readonly string Name;
        public readonly string Description; // one English line, shown on the card
        public readonly ItemCategory Category;
        public readonly ItemRarity Rarity;
        public readonly ItemTheme Theme;
        public readonly float SpawnWeight;
        public readonly IReadOnlyList<string> Tags;
        // Offered only when every one of these tags is already owned (§1.3).
        public readonly IReadOnlyList<string> RequiredTags;
        public readonly bool Unique;
        public readonly int MaxLevel; // 0 = no cap
        public readonly IReadOnlyList<StatEffect> Effects;
        public readonly IReadOnlyList<StatEffect> Debuffs;

        public ItemDefinition(string id, string name, string description, ItemCategory category,
            ItemRarity rarity, ItemTheme theme, float spawnWeight, string[] tags, string[] requiredTags,
            bool unique, int maxLevel, StatEffect[] effects, StatEffect[] debuffs = null)
        {
            Id = id;
            Name = name;
            Description = description;
            Category = category;
            Rarity = rarity;
            Theme = theme;
            SpawnWeight = spawnWeight;
            Tags = tags ?? new string[0];
            RequiredTags = requiredTags ?? new string[0];
            Unique = unique;
            MaxLevel = maxLevel;
            Effects = effects ?? NoEffects;
            Debuffs = debuffs ?? NoEffects;
        }

        // Greed items always carry their downside: they can never be dropped.
        public bool Droppable => Rarity != ItemRarity.Greed;
    }
}
