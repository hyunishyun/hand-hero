using UnityEngine;

namespace HandHero.Core
{
    // RUN HUD text (autonomous plan R10): the status line above the arena, the
    // island countdown banner and the VICTORY / DEFEAT summary. ASCII only.
    public static class RunHudText
    {
        // Literals, not enum ToString + ToUpperInvariant (GM-2): no allocation per call.
        public static string IslandName(IslandType type)
        {
            switch (type)
            {
                case IslandType.Arena: return "ARENA";
                case IslandType.Horde: return "HORDE";
                case IslandType.Elite: return "ELITE";
                case IslandType.Shop: return "SHOP";
                case IslandType.Boss: return "BOSS";
                default: return type.ToString().ToUpperInvariant();
            }
        }

        // Change keys (GM-1, GM-3, GM-4): the shown integers of each line, so the
        // HUD rebuilds a string only when one of them changes.
        public static HudKey StatusKey(int island, IslandType type, bool showObjective, int botsLeft,
            float hordeSecondsLeft, int crystals, float health, float maxHealth)
        {
            int objective = !showObjective ? -1
                : type == IslandType.Horde ? Mathf.Max(0, Mathf.CeilToInt(hordeSecondsLeft))
                : botsLeft;
            return new HudKey(HudKeyKind.RunStatus, island, (int)type, objective, crystals,
                Mathf.Max(0, Mathf.RoundToInt(health)), Mathf.RoundToInt(maxHealth));
        }

        public static HudKey IntroKey(int island, IslandType type, float secondsLeft) =>
            new HudKey(HudKeyKind.IntroBanner, island, (int)type, Mathf.CeilToInt(secondsLeft));

        // metaKey: MetaText.ChangesKey of the lines EndBanner shows under the summary.
        public static HudKey EndKey(bool victory, int islandsCleared, int items, float runSeconds, int metaKey = 0) =>
            new HudKey(HudKeyKind.EndBanner, victory ? 1 : 0, islandsCleared, items,
                Mathf.Max(0, Mathf.CeilToInt(runSeconds)), metaKey);

        // Arena / Elite: bots still to defeat; Boss: just BOSS; Horde: time left to survive.
        public static string Objective(IslandType type, int botsLeft, float hordeSecondsLeft)
        {
            switch (type)
            {
                case IslandType.Horde: return "SURVIVE " + Clock(hordeSecondsLeft);
                case IslandType.Boss: return "BOSS";
                default: return botsLeft == 1 ? "1 BOT LEFT" : $"{botsLeft} BOTS LEFT";
            }
        }

        public static string Status(int island, IslandType type, string objective, int crystals, float health,
            float maxHealth)
        {
            string head = $"ISLAND {island}/{RunRules.IslandCount}  {IslandName(type)}";
            if (!string.IsNullOrEmpty(objective)) head += "  -  " + objective;
            int hp = Mathf.Max(0, Mathf.RoundToInt(health));
            return head + $"\n<size=80%>HP {hp}/{Mathf.RoundToInt(maxHealth)}    {crystals} CRYSTALS</size>";
        }

        public static string IntroBanner(int island, IslandType type, float secondsLeft)
        {
            return $"ISLAND {island}/{RunRules.IslandCount}\n<size=50%>{IslandName(type)}</size>\n"
                + Mathf.CeilToInt(secondsLeft);
        }

        // metaLines (MetaText.EndLines): NEW BEST / UNLOCKED lines under the summary, same size.
        public static string EndBanner(bool victory, int islandsCleared, int items, float runSeconds,
            string metaLines = null)
        {
            string islands = islandsCleared == 1 ? "1 ISLAND" : $"{islandsCleared} ISLANDS";
            string picked = items == 1 ? "1 ITEM" : $"{items} ITEMS";
            string banner = (victory ? "VICTORY" : "DEFEAT")
                + $"\n<size=50%>{islands}    {picked}    {Clock(runSeconds)}</size>";
            return string.IsNullOrEmpty(metaLines) ? banner : banner + "\n<size=50%>" + metaLines + "</size>";
        }

        // m:ss, rounded up so a countdown never shows 0:00 while time is left.
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
