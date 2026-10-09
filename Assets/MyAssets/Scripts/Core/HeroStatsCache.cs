namespace HandHero.Core
{
    // HeroStats folded from an inventory only when it changes (round 3, P3 /
    // GM-9, RF-6): a new inventory instance or a new level (Add always adds one).
    // One rule for RunStateMachine and RunHeroStats.
    public class HeroStatsCache
    {
        private Inventory _inventory;
        private int _levels = -1;
        private HeroStats _stats = HeroStats.Neutral;

        public HeroStats Get(Inventory inventory)
        {
            if (inventory == null) return HeroStats.Neutral;
            if (!ReferenceEquals(inventory, _inventory) || inventory.TotalLevels != _levels)
            {
                _stats = HeroStats.From(inventory);
                _inventory = inventory;
                _levels = inventory.TotalLevels;
            }
            return _stats;
        }

        public void Invalidate()
        {
            _inventory = null;
            _levels = -1;
            _stats = HeroStats.Neutral;
        }
    }
}
