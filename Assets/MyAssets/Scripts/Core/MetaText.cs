using System.Collections.Generic;
using UnityEngine;

namespace HandHero.Core
{
    // Meta progression A text (round 4, S5): the personal best line in the main
    // menu banner and the NEW BEST / UNLOCKED lines under the run summary.
    // ASCII only, like the other HUD text.
    public static class MetaText
    {
        private const string MenuHint = "point and pinch to choose";

        // The current aim mode's bests; "" (hidden) before its first run. A Victory
        // is stored as island 10 and shows as the last island plus the win time.
        public static string BestLine(int bestIsland, float bestWinSeconds)
        {
            if (bestIsland <= 0) return "";
            string line = "BEST  ISLAND " + Mathf.Min(bestIsland, RunRules.IslandCount);
            return bestWinSeconds > 0f ? line + "  -  WIN " + RunHudText.Clock(bestWinSeconds) : line;
        }

        // Main menu banner: the best line takes the hint's place once there is one.
        public static string MenuBanner(string bestLine)
        {
            return "HAND HERO\n<size=50%>" + (string.IsNullOrEmpty(bestLine) ? MenuHint : bestLine) + "</size>";
        }

        // Mirrors MenuBanner(BestLine(...)): every shown value is in the key.
        public static HudKey MenuKey(int bestIsland, float bestWinSeconds) =>
            new HudKey(HudKeyKind.MenuBanner, Mathf.Max(0, bestIsland),
                bestWinSeconds > 0f ? Mathf.CeilToInt(bestWinSeconds) : 0);

        // One line per change, "" when the run changed nothing.
        public static string EndLines(MetaChanges changes)
        {
            var lines = new List<string>(4);
            if (changes.NewBestIsland || changes.NewBestTime) lines.Add("NEW BEST");
            foreach (string id in MetaProgress.RelicIds(changes.NewUnlocks))
            {
                ItemDefinition item = ItemCatalog.Get(id);
                lines.Add("UNLOCKED: " + (item != null ? item.Name.ToUpperInvariant() : id) + " START");
            }
            return string.Join("\n", lines);
        }

        public static int ChangesKey(MetaChanges changes)
        {
            return (changes.NewBestIsland ? 1 : 0) | (changes.NewBestTime ? 2 : 0) | ((int)changes.NewUnlocks << 2);
        }
    }
}
