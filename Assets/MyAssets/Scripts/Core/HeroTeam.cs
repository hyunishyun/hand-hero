namespace HandHero.Core
{
    // Which side a hero fights for. Beams never hit their own side (BR-6): run
    // bots shoot through each other, and their kills never pay the player.
    public enum HeroTeam
    {
        Player,
        Bot,
    }

    public static class HeroTeams
    {
        public static bool CanHit(HeroTeam shooter, HeroTeam target) => shooter != target;
    }
}
