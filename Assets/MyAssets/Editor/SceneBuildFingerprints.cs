using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace HandHero.EditorTools
{
    // Deep review DR-4: remembers what the scene builder last wrote (a hash per file),
    // so an APK build that is about to regenerate the scenes from code can say that a
    // saved inspector edit (or a git checkout) will be reset to the C# defaults.
    // Stored in Library/ (this checkout only, not in git); a file never recorded on
    // this PC counts as unknown, not as edited.
    internal static class SceneBuildFingerprints
    {
        private const string StorePath = "Library/HandHero/SceneBuildFingerprints.txt";

        public enum State { NoRecord, AsBuilt, Edited, Missing }

        // Called by HandHeroSceneBuilder right after it saved these assets.
        public static void Record(params string[] assetPaths)
        {
            try
            {
                SortedDictionary<string, string> store = Load();
                foreach (string path in assetPaths)
                {
                    string hash = Hash(path);
                    if (hash == null) store.Remove(path);
                    else store[path] = hash;
                }
                var text = new StringBuilder();
                foreach (KeyValuePair<string, string> entry in store)
                    text.Append(entry.Key).Append('\t').Append(entry.Value).Append('\n');
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath) ?? "Library");
                File.WriteAllText(StorePath, text.ToString());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SceneBuildFingerprints] could not record the scene build: {e.Message}");
            }
        }

        public static State Check(string assetPath)
        {
            string current = Hash(assetPath);
            if (current == null) return State.Missing;
            SortedDictionary<string, string> store;
            try
            {
                store = Load();
            }
            catch (Exception)
            {
                return State.NoRecord;
            }
            if (!store.TryGetValue(assetPath, out string recorded)) return State.NoRecord;
            return recorded == current ? State.AsBuilt : State.Edited;
        }

        // The given files whose saved content differs from what the builder last wrote.
        public static List<string> Edited(params string[] assetPaths)
        {
            var edited = new List<string>();
            foreach (string path in assetPaths)
                if (Check(path) == State.Edited) edited.Add(path);
            return edited;
        }

        private static SortedDictionary<string, string> Load()
        {
            var store = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(StorePath)) return store;
            foreach (string line in File.ReadAllLines(StorePath))
            {
                int tab = line.IndexOf('\t');
                if (tab > 0) store[line.Substring(0, tab)] = line.Substring(tab + 1).Trim();
            }
            return store;
        }

        // Project-relative asset path (the editor's working directory is the project root).
        private static string Hash(string assetPath)
        {
            try
            {
                if (!File.Exists(assetPath)) return null;
                using (SHA1 sha = SHA1.Create())
                {
                    byte[] digest = sha.ComputeHash(File.ReadAllBytes(assetPath));
                    var hex = new StringBuilder(digest.Length * 2);
                    foreach (byte b in digest) hex.Append(b.ToString("x2"));
                    return hex.ToString();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
