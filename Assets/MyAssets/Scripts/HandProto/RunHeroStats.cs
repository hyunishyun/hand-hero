using HandHero.Core;
using UnityEngine;

// The player hero's run items (autonomous plan R7). The run binds its inventory
// here; the combat controllers read Stats through StatsOf(). Unbound (Quick
// Match, the tutorial) or missing (the bot) = HeroStats.Neutral, which leaves
// every tuned value exactly as it was.
public class RunHeroStats : MonoBehaviour
{
    private Inventory _inventory;
    private readonly HeroStatsCache _cache = new HeroStatsCache();

    public Inventory Inventory => _inventory;
    public bool IsBound => _inventory != null;

    // Recomputed only when the inventory changed (HeroStatsCache).
    public HeroStats Stats => _cache.Get(_inventory);

    // Run start: the run's inventory. Null = back to neutral (run over, menu).
    public void Bind(Inventory inventory)
    {
        _inventory = inventory;
        _cache.Invalidate();
    }

    public void Unbind() => Bind(null);

    public static HeroStats StatsOf(RunHeroStats stats) => stats != null ? stats.Stats : HeroStats.Neutral;

    // The optional field if set, else one on the hero itself.
    public static RunHeroStats Find(RunHeroStats assigned, Component hero)
    {
        if (assigned != null) return assigned;
        return hero != null ? hero.GetComponent<RunHeroStats>() : null;
    }
}
