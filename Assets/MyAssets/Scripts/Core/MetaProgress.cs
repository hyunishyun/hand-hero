using System;
using System.Collections.Generic;

namespace HandHero.Core
{
    // Starting relics a player has unlocked (bit order = the fixed card order).
    [Flags]
    public enum MetaUnlocks
    {
        None = 0,
        SecondWind = 1 << 0,
        BigChests = 1 << 1,
        Dividends = 1 << 2,
    }

    // What one finished run changed, for the end panel.
    public struct MetaChanges
    {
        // Unlocked by this run only (an unlock is reported once).
        public MetaUnlocks NewUnlocks;
        // Beat this aim mode's best island (a first record counts).
        public bool NewBestIsland;
        // Beat this aim mode's fastest Victory (a first Victory counts).
        public bool NewBestTime;
    }

    // Meta progression A (round 4, D4; design doc 2026-10-09-meta-progression
    // section 5): starting relics unlocked by progress and personal bests per aim
    // mode, stored under hh.meta.* in an IKeyValueStore. Unlocks: reaching
    // island 5 (any result, a quit too) -> Second Wind; the first Victory -> Big
    // Chests; a Victory in another aim mode than the first one -> Dividends.
    // Bests never get worse; only a Victory sets a time. Written once per run end
    // (one Save), never mid-fight. A save from another version is reset.
    public class MetaProgress
    {
        public const int Version = 1;
        // Stored best island for a Victory (islands are 1-9).
        public const int VictoryIsland = 10;
        // Reaching this island unlocks the Second Wind start.
        public const int SecondWindIsland = 5;

        private const string Prefix = "hh.meta.";
        private const string VersionKey = Prefix + "v";
        private const string RunsKey = Prefix + "runs";
        private const string WinsKey = Prefix + "wins";
        private const string UnlocksKey = Prefix + "unlocks";
        private const string FirstWinAimKey = Prefix + "firstWinAim";
        // Aim modes with a stored best, comma-separated: Reset deletes their keys
        // (PlayerPrefs cannot list keys).
        private const string AimsKey = Prefix + "aims";

        // Fixed card order of the starting relic choice.
        private static readonly MetaUnlocks[] RelicOrder =
            { MetaUnlocks.SecondWind, MetaUnlocks.BigChests, MetaUnlocks.Dividends };

        private readonly IKeyValueStore _store;

        public MetaProgress(IKeyValueStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            if (_store.Has(VersionKey) && _store.GetInt(VersionKey) != Version) Reset();
        }

        public int Runs => _store.GetInt(RunsKey);
        public int Wins => _store.GetInt(WinsKey);
        public bool HasPlayed => Runs > 0;
        public MetaUnlocks Unlocked => (MetaUnlocks)_store.GetInt(UnlocksKey);

        // 1-9, VictoryIsland after a Victory, 0 = no run in this aim mode yet.
        public int BestIsland(string aimMode) => _store.GetInt(IslandKey(Aim(aimMode)));

        // Fastest Victory in seconds, 0 = none yet.
        public float BestWinSeconds(string aimMode) => _store.GetFloat(TimeKey(Aim(aimMode)));

        public MetaChanges OnRunEnded(RunResult result, int islandReached, float runSeconds, string aimMode)
        {
            string aim = Aim(aimMode);
            bool victory = result == RunResult.Victory;
            int reached = victory ? VictoryIsland : Math.Max(0, Math.Min(islandReached, VictoryIsland - 1));
            var changes = new MetaChanges();

            _store.SetInt(VersionKey, Version);
            _store.SetInt(RunsKey, Runs + 1);
            RememberAim(aim);

            if (reached > BestIsland(aim))
            {
                _store.SetInt(IslandKey(aim), reached);
                changes.NewBestIsland = true;
            }

            MetaUnlocks before = Unlocked;
            MetaUnlocks after = before;
            if (reached >= SecondWindIsland) after |= MetaUnlocks.SecondWind;

            if (victory)
            {
                float best = BestWinSeconds(aim);
                if (runSeconds > 0f && (best <= 0f || runSeconds < best))
                {
                    _store.SetFloat(TimeKey(aim), runSeconds);
                    changes.NewBestTime = true;
                }

                if (!_store.Has(FirstWinAimKey)) _store.SetString(FirstWinAimKey, aim);
                else if (_store.GetString(FirstWinAimKey) != aim) after |= MetaUnlocks.Dividends;
                after |= MetaUnlocks.BigChests;
                _store.SetInt(WinsKey, Wins + 1);
            }

            _store.SetInt(UnlocksKey, (int)after);
            changes.NewUnlocks = after & ~before;
            _store.Save();
            return changes;
        }

        // Unlocked starting relics as item ids, in the fixed card order.
        public IReadOnlyList<string> StartingRelicChoices() => RelicIds(Unlocked);

        public static List<string> RelicIds(MetaUnlocks unlocks)
        {
            var ids = new List<string>(RelicOrder.Length);
            foreach (MetaUnlocks relic in RelicOrder)
                if ((unlocks & relic) != 0) ids.Add(RelicId(relic));
            return ids;
        }

        public static string RelicId(MetaUnlocks relic)
        {
            switch (relic)
            {
                case MetaUnlocks.SecondWind: return ItemCatalog.SecondWind;
                case MetaUnlocks.BigChests: return ItemCatalog.BigChests;
                case MetaUnlocks.Dividends: return ItemCatalog.Dividends;
                default: return null;
            }
        }

        // Clears every hh.meta.* key (the aim mode, tutorial flag and other
        // settings live under other keys and stay), then saves once.
        public void Reset()
        {
            foreach (string aim in StoredAims())
            {
                _store.Delete(IslandKey(aim));
                _store.Delete(TimeKey(aim));
            }
            _store.Delete(AimsKey);
            _store.Delete(VersionKey);
            _store.Delete(RunsKey);
            _store.Delete(WinsKey);
            _store.Delete(UnlocksKey);
            _store.Delete(FirstWinAimKey);
            _store.Save();
        }

        private void RememberAim(string aim)
        {
            string[] aims = StoredAims();
            if (Array.IndexOf(aims, aim) >= 0) return;
            string list = _store.GetString(AimsKey);
            _store.SetString(AimsKey, list.Length == 0 ? aim : list + "," + aim);
        }

        private string[] StoredAims()
        {
            string list = _store.GetString(AimsKey);
            return list.Length == 0 ? Array.Empty<string>() : list.Split(',');
        }

        // Aim mode names become key parts: no separators, never empty.
        private static string Aim(string aimMode)
        {
            string aim = aimMode == null ? "" : aimMode.Replace(",", "").Replace(".", "");
            return aim.Length > 0 ? aim : "Unknown";
        }

        private static string IslandKey(string aim) => Prefix + "best." + aim + ".island";
        private static string TimeKey(string aim) => Prefix + "best." + aim + ".time";
    }
}
