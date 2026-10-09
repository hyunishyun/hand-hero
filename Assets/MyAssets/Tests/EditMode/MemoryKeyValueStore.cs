using System.Collections.Generic;
using HandHero.Core;

namespace HandHero.Tests
{
    // Dictionary-backed IKeyValueStore for tests: PlayerPrefs semantics (a key
    // read as another type than it was written gives the fallback) plus a save
    // counter and the key list.
    public class MemoryKeyValueStore : IKeyValueStore
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public int SaveCount { get; private set; }
        public IEnumerable<string> Keys => _values.Keys;

        public bool Has(string key) => _values.ContainsKey(key);

        public int GetInt(string key, int fallback = 0) =>
            _values.TryGetValue(key, out object v) && v is int i ? i : fallback;

        public float GetFloat(string key, float fallback = 0f) =>
            _values.TryGetValue(key, out object v) && v is float f ? f : fallback;

        public string GetString(string key, string fallback = "") =>
            _values.TryGetValue(key, out object v) && v is string s ? s : fallback;

        public void SetInt(string key, int value) => _values[key] = value;
        public void SetFloat(string key, float value) => _values[key] = value;
        public void SetString(string key, string value) => _values[key] = value;
        public void Delete(string key) => _values.Remove(key);
        public void Save() => SaveCount++;
    }
}
