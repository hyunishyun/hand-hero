using System;
using System.Collections.Generic;

namespace HandHero.Core
{
    // Reward chests (design doc §2.3). Theme chests roll from their themes;
    // Spiked uses the Epic pool and costs health (applied by the run, R6).
    public enum ChestType { Damage, Ability, Health, Economy, Random, Epic, Upgrade, Relic, Greed, Spiked }

    // Rolls the item choices of a chest (design doc §6.3): pool filter by chest
    // type + required tags + unique/max level, owned items weighted up, then
    // weighted sampling without replacement. The RNG is injected so tests and
    // replays are deterministic.
    public static class ChestRoller
    {
        // Owned items show up more often so duplicates (= level ups) happen (§1.4).
        public const float OwnedWeightBonus = 1.75f;

        public const int DefaultChoices = 3;
        public const int UpgradeChoices = 2;
        public const int SingleChoice = 1;

        // Choices offered; extraChoices (Big Chests) adds to every chest.
        public static int ChoiceCount(ChestType type, int extraChoices)
        {
            int count;
            switch (type)
            {
                case ChestType.Upgrade: count = UpgradeChoices; break;
                case ChestType.Relic:
                case ChestType.Greed: count = SingleChoice; break;
                default: count = DefaultChoices; break;
            }
            return count + Math.Max(0, extraChoices);
        }

        public static List<ItemDefinition> Roll(ChestType type, Inventory inventory, Random rng)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            return Roll(type, inventory, ChoiceCount(type, HeroStats.From(inventory).ExtraChoices), rng);
        }

        // Same pool and weights, a fixed number of picks (shop pedestals, R6).
        public static List<ItemDefinition> Roll(ChestType type, Inventory inventory, int choices, Random rng)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            List<ItemDefinition> pool = Pool(type, inventory);
            var weights = new List<float>(pool.Count);
            foreach (ItemDefinition item in pool) weights.Add(Weight(item, inventory));

            int count = Math.Min(Math.Max(0, choices), pool.Count);
            var picks = new List<ItemDefinition>(count);
            for (int n = 0; n < count; n++)
            {
                int index = PickWeighted(weights, rng);
                picks.Add(pool[index]);
                pool.RemoveAt(index);
                weights.RemoveAt(index);
            }
            return picks;
        }

        // Every catalog item this chest can offer right now, in catalog order.
        public static List<ItemDefinition> Pool(ChestType type, Inventory inventory)
        {
            var pool = new List<ItemDefinition>();
            foreach (ItemDefinition item in ItemCatalog.All)
            {
                if (!inventory.CanAdd(item.Id)) continue;          // unique owned, max level
                if (!inventory.HasRequiredTags(item)) continue;
                if (InPool(type, item, inventory)) pool.Add(item);
            }
            return pool;
        }

        public static float Weight(ItemDefinition item, Inventory inventory)
        {
            float w = item.SpawnWeight;
            if (inventory != null && inventory.Owns(item.Id)) w *= OwnedWeightBonus;
            return w;
        }

        // Chest type <-> theme / rarity rules (before the shared filters).
        private static bool InPool(ChestType type, ItemDefinition item, Inventory inventory)
        {
            bool greed = item.Rarity == ItemRarity.Greed;
            bool relic = item.Category == ItemCategory.Relic;
            bool themed = !greed && !relic;
            switch (type)
            {
                case ChestType.Damage:
                    return themed && (item.Theme == ItemTheme.Damage || item.Theme == ItemTheme.Critical);
                case ChestType.Ability:
                    return themed && item.Theme == ItemTheme.Ability;
                case ChestType.Health:
                    return themed && (item.Theme == ItemTheme.Health || item.Theme == ItemTheme.Defense
                        || item.Theme == ItemTheme.Speed);
                case ChestType.Economy:
                    return themed && item.Theme == ItemTheme.Economy;
                case ChestType.Random:
                    return !greed;
                case ChestType.Epic:
                case ChestType.Spiked:
                    return item.Rarity == ItemRarity.Epic || item.Rarity == ItemRarity.Legendary;
                case ChestType.Upgrade:
                    return !greed && inventory.Owns(item.Id);
                case ChestType.Relic:
                    return relic;
                case ChestType.Greed:
                    return greed;
            }
            return false;
        }

        private static int PickWeighted(List<float> weights, Random rng)
        {
            float total = 0f;
            foreach (float w in weights) total += w;
            double r = rng.NextDouble() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= weights[i];
                if (r < 0) return i;
            }
            return weights.Count - 1;
        }
    }
}
