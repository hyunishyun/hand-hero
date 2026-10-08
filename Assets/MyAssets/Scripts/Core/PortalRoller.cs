using System;
using System.Collections.Generic;

namespace HandHero.Core
{
    // Island kinds of a run (decision Q3). Portals offer Arena / Horde / Elite;
    // the run inserts Shop (before island 5) and Boss (island 9) itself.
    public enum IslandType { Arena, Horde, Elite, Shop, Boss }

    // A portal = the next island and the chest its clear rewards (design doc §2.2).
    public readonly struct Portal : IEquatable<Portal>
    {
        public readonly IslandType Island;
        public readonly ChestType Chest;

        public Portal(IslandType island, ChestType chest)
        {
            Island = island;
            Chest = chest;
        }

        public bool Equals(Portal other) => Island == other.Island && Chest == other.Chest;
        public override bool Equals(object obj) => obj is Portal other && Equals(other);
        public override int GetHashCode() => (int)Island * 31 + (int)Chest;
        public override string ToString() => Island + " -> " + Chest;
    }

    // Rolls the portals offered after a clear.
    public static class PortalRoller
    {
        public const int BasePortals = 2;
        // From this island on, a third portal shows up at ThirdPortalChance.
        public const int ThirdPortalFromIsland = 3;
        public const double ThirdPortalChance = 0.5;

        private const int MaxAttempts = 64;

        // Island 1 has no portal choice: an Arena that rewards a Damage chest,
        // so every run starts with a build direction (§2.2).
        public static readonly Portal FirstIsland = new Portal(IslandType.Arena, ChestType.Damage);

        private static readonly (IslandType island, float weight)[] IslandWeights =
        {
            (IslandType.Arena, 0.5f),
            (IslandType.Horde, 0.3f),
            (IslandType.Elite, 0.2f),
        };

        // Arena and Horde rewards. Elite always rewards an Epic chest.
        private static readonly (ChestType chest, float weight)[] ChestWeights =
        {
            (ChestType.Damage, 1f),
            (ChestType.Ability, 1f),
            (ChestType.Health, 1f),
            (ChestType.Random, 1f),
            (ChestType.Economy, 0.7f),
            (ChestType.Upgrade, 0.6f),
            (ChestType.Spiked, 0.4f),
            (ChestType.Greed, 0.3f),
            (ChestType.Relic, 0.2f),
        };

        // nextIsland: 1-based index of the island the portals lead to.
        public static List<Portal> Roll(int nextIsland, Inventory inventory, Random rng)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int count = BasePortals;
            if (nextIsland >= ThirdPortalFromIsland && rng.NextDouble() < ThirdPortalChance) count++;

            var portals = new List<Portal>(count);
            for (int attempt = 0; attempt < MaxAttempts && portals.Count < count; attempt++)
            {
                IslandType island = Pick(IslandWeights, rng);
                ChestType chest = island == IslandType.Elite ? ChestType.Epic : Pick(ChestWeights, rng);
                var portal = new Portal(island, chest);
                if (portals.Contains(portal)) continue;
                if (ChestRoller.Pool(chest, inventory).Count == 0) continue; // would open empty
                portals.Add(portal);
            }
            return portals;
        }

        private static T Pick<T>((T value, float weight)[] table, Random rng)
        {
            float total = 0f;
            foreach (var entry in table) total += entry.weight;
            double r = rng.NextDouble() * total;
            foreach (var entry in table)
            {
                r -= entry.weight;
                if (r < 0) return entry.value;
            }
            return table[table.Length - 1].value;
        }
    }
}
