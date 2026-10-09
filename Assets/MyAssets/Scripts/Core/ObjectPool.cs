using System;
using System.Collections.Generic;

namespace HandHero.Core
{
    // Small reuse pool (freeze hunt, P5). Instances are made by `create`, either
    // up front with Prewarm (scene load, never mid-fight) or on demand until
    // maxSize; at the cap Get returns null and the caller skips the effect.
    //
    // Reset contract: onGet runs on every Get and must bring the item to a fresh
    // state; onRelease runs on every Release (and once per prewarmed item) and
    // must stop it. A second Release of the same item, or a foreign item, is
    // ignored, so an item can never be handed out twice.
    public class ObjectPool<T> where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onGet;
        private readonly Action<T> _onRelease;
        private readonly int _maxSize;

        private readonly List<T> _all;
        private readonly Stack<T> _inactive;
        private readonly HashSet<T> _inactiveSet;

        public int CountAll => _all.Count;
        public int CountInactive => _inactive.Count;
        public int CountActive => _all.Count - _inactive.Count;
        public int MaxSize => _maxSize;

        public ObjectPool(Func<T> create, int maxSize, Action<T> onGet = null, Action<T> onRelease = null)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _maxSize = Math.Max(1, maxSize);
            _onGet = onGet;
            _onRelease = onRelease;
            _all = new List<T>(_maxSize);
            _inactive = new Stack<T>(_maxSize);
            _inactiveSet = new HashSet<T>();
        }

        // Creates items until `count` exist in total (capped at maxSize).
        public void Prewarm(int count)
        {
            int target = Math.Min(count, _maxSize);
            while (_all.Count < target)
            {
                T item = _create();
                if (item == null) return;
                _all.Add(item);
                _onRelease?.Invoke(item);
                _inactive.Push(item);
                _inactiveSet.Add(item);
            }
        }

        // A released item, else a new one below the cap, else null.
        public T Get()
        {
            T item;
            if (_inactive.Count > 0)
            {
                item = _inactive.Pop();
                _inactiveSet.Remove(item);
            }
            else if (_all.Count < _maxSize)
            {
                item = _create();
                if (item == null) return null;
                _all.Add(item);
            }
            else
            {
                return null;
            }

            _onGet?.Invoke(item);
            return item;
        }

        // False (and nothing happens) for null, foreign or already released items.
        public bool Release(T item)
        {
            if (item == null || _inactiveSet.Contains(item) || !_all.Contains(item)) return false;
            _onRelease?.Invoke(item);
            _inactive.Push(item);
            _inactiveSet.Add(item);
            return true;
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < _all.Count; i++)
                Release(_all[i]);
        }
    }
}
