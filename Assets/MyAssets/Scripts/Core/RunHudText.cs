using UnityEngine;

namespace HandHero.Core
{
    // RUN HUD text (autonomous plan R10): the status line above the arena, the
    // island countdown banner and the VICTORY / DEFEAT summary. ASCII only.
    public static class RunHudText
    {
        public static string IslandName(IslandType type) => type.ToString().ToUpperInvariant();

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

        public static string EndBanner(bool victory, int islandsCleared, int items, float runSeconds)
        {
            string islands = islandsCleared == 1 ? "1 ISLAND" : $"{islandsCleared} ISLANDS";
            string picked = items == 1 ? "1 ITEM" : $"{items} ITEMS";
            return (victory ? "VICTORY" : "DEFEAT")
                + $"\n<size=50%>{islands}    {picked}    {Clock(runSeconds)}</size>";
        }

        // m:ss, rounded up so a countdown never shows 0:00 while time is left.
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
