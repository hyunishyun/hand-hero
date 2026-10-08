using System;
using System.Collections.Generic;

namespace HandHero.Core
{
    // One owned item and its level.
    public readonly struct OwnedItem
    {
        public readonly ItemDefinition Item;
        public readonly int Level;

        public OwnedItem(ItemDefinition item, int level)
        {
            Item = item;
            Level = level;
        }
    }

    // The run's items. Picking an item already owned levels it up (§3.1); unique
    // items (relics) are owned once; MaxLevel caps the rest.
    public class Inventory
    {
        private readonly Func<string, ItemDefinition> _lookup;
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();
        private readonly List<OwnedItem> _items = new List<OwnedItem>(); // pick order
        private readonly HashSet<string> _tags = new HashSet<string>();

        public Inventory(Func<string, ItemDefinition> lookup)
        {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        }

        public IReadOnlyList<OwnedItem> Items => _items;
        public IReadOnlyCollection<string> OwnedTags => _tags;
        public int TotalLevels { get; private set; }

        public int Level(string id) => id != null && _levels.TryGetValue(id, out int level) ? level : 0;

        public bool Owns(string id) => Level(id) > 0;

        public bool CanAdd(string id)
        {
            ItemDefinition item = id != null ? _lookup(id) : null;
            if (item == null) return false;
            int level = Level(id);
            if (item.Unique && level > 0) return false;
            return item.MaxLevel <= 0 || level < item.MaxLevel;
        }

        // True when the item was added or levelled up.
        public bool Add(string id)
        {
            if (!CanAdd(id)) return false;
            ItemDefinition item = _lookup(id);
            int level = Level(id) + 1;
            _levels[id] = level;
            TotalLevels++;

            int index = _items.FindIndex(o => o.Item.Id == id);
            if (index >= 0) _items[index] = new OwnedItem(item, level);
            else _items.Add(new OwnedItem(item, level));

            foreach (string tag in item.Tags) _tags.Add(tag);
            return true;
        }

        public bool HasRequiredTags(ItemDefinition item)
        {
            foreach (string tag in item.RequiredTags)
                if (!_tags.Contains(tag)) return false;
            return true;
        }
    }
}
