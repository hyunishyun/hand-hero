namespace HandHero.Core
{
    // Minimal persistent key-value storage (round 4, D4): HandProto passes a
    // PlayerPrefs adapter, tests a dictionary. Reading a missing key, or a key
    // written as another type, returns the fallback (PlayerPrefs semantics).
    public interface IKeyValueStore
    {
        bool Has(string key);
        int GetInt(string key, int fallback = 0);
        float GetFloat(string key, float fallback = 0f);
        string GetString(string key, string fallback = "");
        void SetInt(string key, int value);
        void SetFloat(string key, float value);
        void SetString(string key, string value);
        void Delete(string key);
        // Writes pending changes to disk.
        void Save();
    }
}
