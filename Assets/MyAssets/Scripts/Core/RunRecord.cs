using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HandHero.Core
{
    public enum RunResult { Victory, Defeat, Quit }

    public enum ItemSource { Chest, Shop }

    public class IslandRecord
    {
        public int Number;
        public IslandType Type;
        // Seconds from FIGHT to clear (or to the run's end while still fighting).
        public float FightSeconds;
        public float DamageTaken;
        public int Deaths;
    }

    public struct ItemPick
    {
        public string Id;
        public int Level;
        public int Island;
        public ItemSource Source;
    }

    // Counters the run keeps elsewhere (state machine, wallet, beam), handed over at the finish.
    public struct RunTotals
    {
        public int Kills;
        public int CrystalsEarned;
        public int CrystalsSpent;
        public int ShotsFired;
        public int ShotsHit;
        public int ChargeShots;
        public PinchHoldStats Holds;
    }

    // One finished run (round 3, D16): one JSON line in run_log.jsonl.
    public class RunRecord
    {
        public int Seed;
        public string AimMode;
        public string ViewMode;
        public string StartTime;
        public RunResult Result;
        public float TotalSeconds;
        public readonly List<IslandRecord> Islands = new List<IslandRecord>();
        // Island number the run was lost on; 0 unless Result is Defeat.
        public int DeathIsland;
        public int RevivesUsed;
        public readonly List<ItemPick> Items = new List<ItemPick>();
        public int ShopBuys;
        public int ShopRerolls;
        public int Kills;
        public int CrystalsEarned;
        public int CrystalsSpent;
        public int ShotsFired;
        public int ShotsHit;
        public float HitRate;
        public int ChargeShots;
        public float[] HoldSeconds = Array.Empty<float>();
        // Per hold: did it fire a charge shot (same order as HoldSeconds).
        public bool[] HoldCharged = Array.Empty<bool>();
    }

    // Collects run events into a RunRecord. Begin at run start, then island /
    // fight / damage / item events as they happen, Finish once at the end
    // (Victory, Defeat or Quit). Events outside a run or before the first
    // island are ignored, and a run is finished only once.
    public class RunRecorder
    {
        private RunRecord _record;
        private IslandRecord _island;
        private float _fightStart = -1f;

        public bool IsRecording => _record != null;

        public void Begin(int seed, string aimMode, string viewMode, string startTime)
        {
            _record = new RunRecord { Seed = seed, AimMode = aimMode, ViewMode = viewMode, StartTime = startTime };
            _island = null;
            _fightStart = -1f;
        }

        public void IslandStarted(int number, IslandType type)
        {
            if (_record == null) return;
            _island = new IslandRecord { Number = number, Type = type };
            _record.Islands.Add(_island);
            _fightStart = -1f;
        }

        // Times are run seconds (RunStateMachine.RunTime).
        public void FightStarted(float runTime)
        {
            if (_island != null) _fightStart = runTime;
        }

        public void FightEnded(float runTime)
        {
            if (_island == null || _fightStart < 0f) return;
            _island.FightSeconds += Math.Max(0f, runTime - _fightStart);
            _fightStart = -1f;
        }

        public void DamageTaken(float amount)
        {
            if (_island != null && amount > 0f) _island.DamageTaken += amount;
        }

        public void PlayerDied(bool revived)
        {
            if (_island == null) return;
            _island.Deaths++;
            if (revived) _record.RevivesUsed++;
        }

        public void ItemPicked(string id, int level, ItemSource source)
        {
            if (_island == null) return;
            _record.Items.Add(new ItemPick { Id = id, Level = level, Island = _island.Number, Source = source });
            if (source == ItemSource.Shop) _record.ShopBuys++;
        }

        public void ShopRerolled()
        {
            if (_record != null) _record.ShopRerolls++;
        }

        // Closes the run; null when no run was recording (already finished or never begun).
        public RunRecord Finish(RunResult result, float runTime, RunTotals totals)
        {
            if (_record == null) return null;
            FightEnded(runTime);

            RunRecord r = _record;
            r.Result = result;
            r.TotalSeconds = runTime;
            r.DeathIsland = result == RunResult.Defeat && _island != null ? _island.Number : 0;
            r.Kills = totals.Kills;
            r.CrystalsEarned = totals.CrystalsEarned;
            r.CrystalsSpent = totals.CrystalsSpent;
            r.ShotsFired = totals.ShotsFired;
            r.ShotsHit = totals.ShotsHit;
            r.HitRate = totals.ShotsFired > 0 ? (float)totals.ShotsHit / totals.ShotsFired : 0f;
            r.ChargeShots = totals.ChargeShots;
            if (totals.Holds != null)
            {
                r.HoldSeconds = Copy(totals.Holds.Durations);
                r.HoldCharged = Copy(totals.Holds.ChargeShotFlags);
            }

            _record = null;
            _island = null;
            _fightStart = -1f;
            return r;
        }

        private static T[] Copy<T>(IReadOnlyList<T> list)
        {
            var copy = new T[list.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = list[i];
            return copy;
        }
    }

    // Single-line JSON for run_log.jsonl (read by AUTO/tools/run_summary.py).
    // Invariant culture, at most 3 decimals; "v" bumps when a field changes meaning.
    public static class RunRecordJson
    {
        public const int Version = 1;

        public static string ToJson(RunRecord r)
        {
            var sb = new StringBuilder(512);
            sb.Append("{\"v\":").Append(Version);
            Int(sb, "seed", r.Seed);
            Str(sb, "aim", r.AimMode);
            Str(sb, "view", r.ViewMode);
            Str(sb, "start", r.StartTime);
            Str(sb, "result", r.Result.ToString());
            Num(sb, "total_s", r.TotalSeconds);
            Int(sb, "death_island", r.DeathIsland);
            Int(sb, "revives", r.RevivesUsed);
            Int(sb, "kills", r.Kills);
            Int(sb, "crystals_earned", r.CrystalsEarned);
            Int(sb, "crystals_spent", r.CrystalsSpent);
            Int(sb, "shop_buys", r.ShopBuys);
            Int(sb, "shop_rerolls", r.ShopRerolls);
            Int(sb, "shots", r.ShotsFired);
            Int(sb, "hits", r.ShotsHit);
            Num(sb, "hit_rate", r.HitRate);
            Int(sb, "charge_shots", r.ChargeShots);

            sb.Append(",\"islands\":[");
            for (int i = 0; i < r.Islands.Count; i++)
            {
                IslandRecord island = r.Islands[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"n\":").Append(island.Number);
                Str(sb, "type", island.Type.ToString());
                Num(sb, "fight_s", island.FightSeconds);
                Num(sb, "damage", island.DamageTaken);
                Int(sb, "deaths", island.Deaths);
                sb.Append('}');
            }
            sb.Append(']');

            sb.Append(",\"items\":[");
            for (int i = 0; i < r.Items.Count; i++)
            {
                ItemPick item = r.Items[i];
                if (i > 0) sb.Append(',');
                sb.Append('{');
                Quote(sb, "id").Append(':');
                Quote(sb, item.Id);
                Int(sb, "level", item.Level);
                Int(sb, "island", item.Island);
                Str(sb, "from", item.Source == ItemSource.Shop ? "shop" : "chest");
                sb.Append('}');
            }
            sb.Append(']');

            sb.Append(",\"hold_s\":[");
            for (int i = 0; i < r.HoldSeconds.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Format(r.HoldSeconds[i]));
            }
            sb.Append("],\"hold_charged\":[");
            for (int i = 0; i < r.HoldCharged.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(r.HoldCharged[i] ? '1' : '0');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static void Int(StringBuilder sb, string key, int value)
        {
            sb.Append(',');
            Quote(sb, key).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Num(StringBuilder sb, string key, float value)
        {
            sb.Append(',');
            Quote(sb, key).Append(':').Append(Format(value));
        }

        private static void Str(StringBuilder sb, string key, string value)
        {
            sb.Append(',');
            Quote(sb, key).Append(':');
            Quote(sb, value);
        }

        private static string Format(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return "0";
            return ((double)value).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static StringBuilder Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            if (s != null)
            {
                foreach (char c in s)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                            else sb.Append(c);
                            break;
                    }
                }
            }
            return sb.Append('"');
        }
    }
}
