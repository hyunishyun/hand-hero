using System.Collections.Generic;

namespace HandHero.Core
{
    // Starter item list (autonomous plan §5). Names are placeholders. Spawn
    // weights follow the design doc: 1.0 normal, 1.5 synergy, 0.2 relic.
    public static class ItemCatalog
    {
        public const string PowerCell = "power_cell";
        public const string RapidCoil = "rapid_coil";
        public const string FocusLens = "focus_lens";
        public const string SharpFocus = "sharp_focus";
        public const string Overcharge = "overcharge";
        public const string QuickCharge = "quick_charge";
        public const string ShockAmp = "shock_amp";
        public const string StunLock = "stun_lock";
        public const string HullPlating = "hull_plating";
        public const string NanoRepair = "nano_repair";
        public const string Afterburner = "afterburner";
        public const string Deflector = "deflector";
        public const string CrystalMagnet = "crystal_magnet";
        public const string Paycheck = "paycheck";
        public const string BigChests = "big_chests";
        public const string Dividends = "dividends";
        public const string SecondWind = "second_wind";
        public const string GlassCannon = "glass_cannon";
        public const string BloodPrice = "blood_price";

        private static readonly string[] None = new string[0];

        private static readonly ItemDefinition[] Items =
        {
            Stack(PowerCell, "Power Cell", "Beam damage +15% per level.", ItemCategory.WeaponMod,
                ItemTheme.Damage, ItemRarity.Common, 1f, new[] { "Damage" }, None,
                new StatEffect(Stat.Damage, 0.15f, ScalingType.LinearMultiplier)),
            Stack(RapidCoil, "Rapid Coil", "Fire cooldown -12%, diminishing.", ItemCategory.WeaponMod,
                ItemTheme.Damage, ItemRarity.Common, 1f, new[] { "FireRate" }, None,
                new StatEffect(Stat.FireCooldownCut, 0.12f, ScalingType.Hyperbolic)),
            Stack(FocusLens, "Focus Lens", "10% chance to crit for double damage.", ItemCategory.WeaponMod,
                ItemTheme.Critical, ItemRarity.Common, 1f, new[] { "Critical" }, None,
                new StatEffect(Stat.CritChance, 0.10f, ScalingType.Hyperbolic)),
            Stack(SharpFocus, "Sharp Focus", "Crit damage +25% per level.", ItemCategory.WeaponMod,
                ItemTheme.Critical, ItemRarity.Epic, 1.5f, new[] { "Critical" }, new[] { "Critical" },
                new StatEffect(Stat.CritDamage, 0.25f, ScalingType.LinearMultiplier)),
            Stack(Overcharge, "Overcharge", "Charge shot damage +20% per level.", ItemCategory.AbilityMod,
                ItemTheme.Ability, ItemRarity.Common, 1f, new[] { "Charge" }, None,
                new StatEffect(Stat.ChargeDamage, 0.20f, ScalingType.LinearMultiplier)),
            Stack(QuickCharge, "Quick Charge", "Charge time -15%, diminishing.", ItemCategory.AbilityMod,
                ItemTheme.Ability, ItemRarity.Common, 1.5f, new[] { "Charge" }, new[] { "Charge" },
                new StatEffect(Stat.ChargeTimeCut, 0.15f, ScalingType.Hyperbolic)),
            Stack(ShockAmp, "Shock Amp", "Shockwave radius +20% per level.", ItemCategory.AbilityMod,
                ItemTheme.Ability, ItemRarity.Common, 1f, new[] { "Shockwave" }, None,
                new StatEffect(Stat.ShockwaveRadius, 0.20f, ScalingType.LinearMultiplier)),
            Stack(StunLock, "Stun Lock", "Shockwave stun +0.3 s per level.", ItemCategory.AbilityMod,
                ItemTheme.Ability, ItemRarity.Epic, 1.5f, new[] { "Shockwave" }, new[] { "Shockwave" },
                new StatEffect(Stat.StunDuration, 0.3f, ScalingType.Linear)),
            Stack(HullPlating, "Hull Plating", "Max health +20 per level.", ItemCategory.Perk,
                ItemTheme.Health, ItemRarity.Common, 1f, new[] { "Health" }, None,
                new StatEffect(Stat.MaxHealth, 20f, ScalingType.Linear)),
            Stack(NanoRepair, "Nano Repair", "Heal +10 more after each island.", ItemCategory.Perk,
                ItemTheme.Health, ItemRarity.Common, 1f, new[] { "Healing" }, None,
                new StatEffect(Stat.HealOnClear, 10f, ScalingType.Linear)),
            Stack(Afterburner, "Afterburner", "Flight speed +10% per level.", ItemCategory.Perk,
                ItemTheme.Speed, ItemRarity.Common, 1f, new[] { "Speed" }, None,
                new StatEffect(Stat.Speed, 0.10f, ScalingType.LinearMultiplier)),
            Stack(Deflector, "Deflector", "Damage taken -10%, diminishing.", ItemCategory.Perk,
                ItemTheme.Defense, ItemRarity.Epic, 1f, new[] { "Defense" }, None,
                new StatEffect(Stat.DamageReduction, 0.10f, ScalingType.Hyperbolic)),
            Stack(CrystalMagnet, "Crystal Magnet", "Crystals from kills +25% per level.", ItemCategory.Perk,
                ItemTheme.Economy, ItemRarity.Common, 1f, new[] { "Economy" }, None,
                new StatEffect(Stat.CrystalGain, 0.25f, ScalingType.LinearMultiplier)),
            // 20 = 0.2 x the common base price (R6).
            Stack(Paycheck, "Paycheck", "+20 crystals per island cleared.", ItemCategory.Perk,
                ItemTheme.Economy, ItemRarity.Common, 1f, new[] { "Economy" }, None,
                new StatEffect(Stat.CrystalPerClear, 20f, ScalingType.Linear)),

            Relic(BigChests, "Big Chests", "Every chest offers one more choice.", ItemTheme.Economy,
                new[] { "Relic" }, new StatEffect(Stat.ExtraChoices, 1f, ScalingType.Linear)),
            Relic(Dividends, "Dividends", "Earn 10% interest on crystals each island.", ItemTheme.Economy,
                new[] { "Relic", "Economy" }, new StatEffect(Stat.InterestRate, 0.10f, ScalingType.Linear)),
            Relic(SecondWind, "Second Wind", "Revive once at half health.", ItemTheme.Health,
                new[] { "Relic" }, new StatEffect(Stat.Revives, 1f, ScalingType.Linear)),

            Greed(GlassCannon, "Glass Cannon", "Damage +60%, but max health -30%.", ItemTheme.Damage,
                new StatEffect(Stat.Damage, 0.60f, ScalingType.LinearMultiplier),
                new StatEffect(Stat.MaxHealthLoss, 0.30f, ScalingType.Hyperbolic)),
            Greed(BloodPrice, "Blood Price", "Crystals +100%, but damage taken +25%.", ItemTheme.Economy,
                new StatEffect(Stat.CrystalGain, 1.0f, ScalingType.LinearMultiplier),
                new StatEffect(Stat.DamageTakenIncrease, 0.25f, ScalingType.Linear)),
        };

        private static readonly Dictionary<string, ItemDefinition> ById = BuildIndex();

        public static IReadOnlyList<ItemDefinition> All => Items;

        // Null for an unknown id.
        public static ItemDefinition Get(string id)
        {
            return id != null && ById.TryGetValue(id, out ItemDefinition item) ? item : null;
        }

        private static Dictionary<string, ItemDefinition> BuildIndex()
        {
            var index = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in Items) index.Add(item.Id, item);
            return index;
        }

        private static ItemDefinition Stack(string id, string name, string description, ItemCategory category,
            ItemTheme theme, ItemRarity rarity, float weight, string[] tags, string[] requiredTags, StatEffect effect)
        {
            return new ItemDefinition(id, name, description, category, rarity, theme, weight, tags, requiredTags,
                false, 0, new[] { effect });
        }

        private static ItemDefinition Relic(string id, string name, string description, ItemTheme theme,
            string[] tags, StatEffect effect)
        {
            return new ItemDefinition(id, name, description, ItemCategory.Relic, ItemRarity.Legendary, theme, 0.2f,
                tags, None, true, 1, new[] { effect });
        }

        // Greed: offered by the Greed chest only (R4), always with a debuff.
        private static ItemDefinition Greed(string id, string name, string description, ItemTheme theme,
            StatEffect effect, StatEffect debuff)
        {
            return new ItemDefinition(id, name, description, ItemCategory.Perk, ItemRarity.Greed, theme, 1f,
                new[] { "Greed" }, None, false, 0, new[] { effect }, new[] { debuff });
        }
    }
}
