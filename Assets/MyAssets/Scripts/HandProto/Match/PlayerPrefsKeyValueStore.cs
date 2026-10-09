using HandHero.Core;
using UnityEngine;

// IKeyValueStore over PlayerPrefs (round 4, D4: meta progression). Save() is
// PlayerPrefs.Save(), a disk write: callers do it at safe moments only (run end,
// menu), never mid-fight.
public sealed class PlayerPrefsKeyValueStore : IKeyValueStore
{
    public static readonly PlayerPrefsKeyValueStore Instance = new PlayerPrefsKeyValueStore();

    public bool Has(string key) => PlayerPrefs.HasKey(key);
    public int GetInt(string key, int fallback = 0) => PlayerPrefs.GetInt(key, fallback);
    public float GetFloat(string key, float fallback = 0f) => PlayerPrefs.GetFloat(key, fallback);
    public string GetString(string key, string fallback = "") => PlayerPrefs.GetString(key, fallback);
    public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
    public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
    public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
    public void Delete(string key) => PlayerPrefs.DeleteKey(key);
    public void Save() => PlayerPrefs.Save();
}
