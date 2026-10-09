using System;

namespace HandHero.Core
{
    // Fixed-size buffer that overwrites its oldest item when full. Allocates
    // only in the constructor, so it is safe to fill every frame.
    public class RingBuffer<T> where T : struct
    {
        private readonly T[] _items;
        private int _start;

        public int Count { get; private set; }
        public int Capacity => _items.Length;

        // Items overwritten since the last Clear.
        public int Dropped { get; private set; }

        public RingBuffer(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        public void Add(in T item)
        {
            if (Count < _items.Length)
            {
                _items[(_start + Count) % _items.Length] = item;
                Count++;
                return;
            }
            _items[_start] = item;
            _start = (_start + 1) % _items.Length;
            Dropped++;
        }

        // 0 = oldest.
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
                return _items[(_start + index) % _items.Length];
            }
        }

        public void Clear()
        {
            _start = 0;
            Count = 0;
            Dropped = 0;
        }
    }
}
