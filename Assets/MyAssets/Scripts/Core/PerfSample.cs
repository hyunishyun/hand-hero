using System;
using System.Text;

namespace HandHero.Core
{
    public enum PerfRecordKind : byte
    {
        Spike,            // a frame over the threshold
        HandLost,         // Detail: 0 = left, 1 = right
        HandFound,
        HeadLost,
        HeadFound,
        UserAbsent,       // headset proximity sensor
        UserPresent,
        FocusLost,        // OnApplicationFocus
        FocusGained,
        AppPaused,        // OnApplicationPause
        AppResumed,
        MatchPhase,       // the new phase is in the snapshot
        RunPhase,
        GamePaused,       // MatchDirector pause (Time.timeScale 0)
        GameResumed,
        SystemGestureStart, // Detail: hand
        SystemGestureEnd,
        Hitch,            // the first frame over the hitch threshold after a phase change (round 4)
        RoomScan,         // MR room-scan spike (round 4, S8). Detail: RoomScanEvent; Note: counts / sizes
    }

    // One perf log record: what happened plus a snapshot of the game and
    // tracking state at that frame. A plain struct, so a ring buffer of them
    // allocates nothing per frame.
    public struct PerfSample
    {
        public PerfRecordKind Kind;
        public int Detail;
        public double Time;      // Time.realtimeSinceStartupAsDouble
        public float FrameMs;    // real interval of this frame
        public float CpuMs;      // FrameTimingManager main thread, -1 = not available
        public float RenderMs;   // render thread, -1 = not available
        public float GpuMs;      // -1 = not available
        public int GcCollections; // gen-0 collections since the previous record
        public long HeapBytes;
        public MatchPhase Match;
        public bool MatchPaused;
        public RunPhase Run;
        public int Island;
        public int BotsAlive;
        public float TimeScale;
        public bool Focused;
        public bool LeftTracked;
        public bool RightTracked;
        public bool HeadTracked;
        public string Note;      // optional text at the end of the line (null for frame records)
    }

    // Writes one PerfSample per line into a reused StringBuilder without
    // allocating (no ToString on numbers or enums), e.g.
    // 21:24:34.567 t=1234.568 SPIKE frame=87.3 cpu=12.1 rt=3.2 gpu=-1.0 gc=+1 heap=45.2MB match=Run run=Island isl=3 bots=2 ts=1.00 focus=1 L=1 R=0 head=1
    public static class PerfLogFormatter
    {
        private const double SecondsPerDay = 86400.0;
        private const double BytesPerMB = 1024.0 * 1024.0;

        private static readonly string[] MatchNames = Enum.GetNames(typeof(MatchPhase));
        private static readonly string[] RunNames = Enum.GetNames(typeof(RunPhase));

        // clockAtZero: local time of day, in seconds, at realtime 0
        // (DateTime.Now.TimeOfDay.TotalSeconds - realtime, taken when flushing).
        public static void AppendLine(StringBuilder sb, in PerfSample s, double clockAtZero)
        {
            AppendClock(sb, clockAtZero + s.Time);
            sb.Append(" t=");
            AppendFixed(sb, s.Time, 3);
            sb.Append(' ').Append(KindName(s.Kind));
            if (HasHand(s.Kind)) sb.Append(" hand=").Append(s.Detail == 0 ? 'L' : 'R');
            if (s.Kind == PerfRecordKind.RoomScan)
                sb.Append(" scan=").Append(RoomScanSummary.EventName((RoomScanEvent)s.Detail));

            sb.Append(" frame=");
            AppendFixed(sb, s.FrameMs, 1);
            sb.Append(" cpu=");
            AppendFixed(sb, s.CpuMs, 1);
            sb.Append(" rt=");
            AppendFixed(sb, s.RenderMs, 1);
            sb.Append(" gpu=");
            AppendFixed(sb, s.GpuMs, 1);
            sb.Append(" gc=+");
            AppendLong(sb, s.GcCollections);
            sb.Append(" heap=");
            AppendFixed(sb, s.HeapBytes / BytesPerMB, 1);
            sb.Append("MB match=").Append(Name(MatchNames, (int)s.Match));
            if (s.MatchPaused) sb.Append("(paused)");
            sb.Append(" run=").Append(Name(RunNames, (int)s.Run));
            sb.Append(" isl=");
            AppendLong(sb, s.Island);
            sb.Append(" bots=");
            AppendLong(sb, s.BotsAlive);
            sb.Append(" ts=");
            AppendFixed(sb, s.TimeScale, 2);
            sb.Append(" focus=").Append(s.Focused ? '1' : '0');
            sb.Append(" L=").Append(s.LeftTracked ? '1' : '0');
            sb.Append(" R=").Append(s.RightTracked ? '1' : '0');
            sb.Append(" head=").Append(s.HeadTracked ? '1' : '0');
            if (s.Note != null) sb.Append(" | ").Append(s.Note);
            sb.Append('\n');
        }

        public static string KindName(PerfRecordKind kind)
        {
            switch (kind)
            {
                case PerfRecordKind.Spike: return "SPIKE";
                case PerfRecordKind.HandLost: return "HAND_LOST";
                case PerfRecordKind.HandFound: return "HAND_FOUND";
                case PerfRecordKind.HeadLost: return "HEAD_LOST";
                case PerfRecordKind.HeadFound: return "HEAD_FOUND";
                case PerfRecordKind.UserAbsent: return "USER_ABSENT";
                case PerfRecordKind.UserPresent: return "USER_PRESENT";
                case PerfRecordKind.FocusLost: return "FOCUS_LOST";
                case PerfRecordKind.FocusGained: return "FOCUS_GAINED";
                case PerfRecordKind.AppPaused: return "APP_PAUSE";
                case PerfRecordKind.AppResumed: return "APP_RESUME";
                case PerfRecordKind.MatchPhase: return "MATCH_PHASE";
                case PerfRecordKind.RunPhase: return "RUN_PHASE";
                case PerfRecordKind.GamePaused: return "GAME_PAUSE";
                case PerfRecordKind.GameResumed: return "GAME_RESUME";
                case PerfRecordKind.SystemGestureStart: return "SYSGESTURE_START";
                case PerfRecordKind.SystemGestureEnd: return "SYSGESTURE_END";
                case PerfRecordKind.Hitch: return "HITCH";
                case PerfRecordKind.RoomScan: return "ROOM_SCAN";
                default: return "?";
            }
        }

        // The line closing each flush, e.g.
        // --- flush (menu) records=4 dropped=0 frames=2 spikes=1 worst=138.5ms hitches=3 worst_hitch=31.2ms
        public static void AppendFlushSummary(StringBuilder sb, string reason, int records, int dropped, int frames,
            int spikes, float worstMs, int hitches, float worstHitchMs)
        {
            sb.Append("--- flush (").Append(reason).Append(") records=");
            AppendLong(sb, records);
            sb.Append(" dropped=");
            AppendLong(sb, dropped);
            sb.Append(" frames=");
            AppendLong(sb, frames);
            sb.Append(" spikes=");
            AppendLong(sb, spikes);
            sb.Append(" worst=");
            AppendFixed(sb, worstMs, 1);
            sb.Append("ms hitches=");
            AppendLong(sb, hitches);
            sb.Append(" worst_hitch=");
            AppendFixed(sb, worstHitchMs, 1);
            sb.Append("ms\n");
        }

        private static bool HasHand(PerfRecordKind kind)
        {
            return kind == PerfRecordKind.HandLost || kind == PerfRecordKind.HandFound
                || kind == PerfRecordKind.SystemGestureStart || kind == PerfRecordKind.SystemGestureEnd;
        }

        private static string Name(string[] names, int index)
        {
            return index >= 0 && index < names.Length ? names[index] : "?";
        }

        // HH:mm:ss.fff, wrapped to one day.
        public static void AppendClock(StringBuilder sb, double secondsOfDay)
        {
            double tod = ((secondsOfDay % SecondsPerDay) + SecondsPerDay) % SecondsPerDay;
            long ms = (long)Math.Floor(tod * 1000.0);
            AppendPadded(sb, ms / 3600000, 2);
            sb.Append(':');
            AppendPadded(sb, ms / 60000 % 60, 2);
            sb.Append(':');
            AppendPadded(sb, ms / 1000 % 60, 2);
            sb.Append('.');
            AppendPadded(sb, ms % 1000, 3);
        }

        // Fixed-point decimal, rounded half away from zero; a negative value
        // keeps its sign even when it rounds to zero.
        public static void AppendFixed(StringBuilder sb, double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                sb.Append("nan");
                return;
            }
            if (value < 0.0)
            {
                sb.Append('-');
                value = -value;
            }
            long scale = 1;
            for (int i = 0; i < decimals; i++) scale *= 10;
            long scaled = (long)Math.Round(value * scale, MidpointRounding.AwayFromZero);
            AppendLong(sb, scaled / scale);
            if (decimals <= 0) return;
            sb.Append('.');
            AppendPadded(sb, scaled % scale, decimals);
        }

        public static void AppendLong(StringBuilder sb, long value)
        {
            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }
            long div = 1;
            while (div <= value / 10) div *= 10;
            for (; div > 0; div /= 10) sb.Append((char)('0' + value / div % 10));
        }

        private static void AppendPadded(StringBuilder sb, long value, int width)
        {
            long div = 1;
            for (int i = 1; i < width; i++) div *= 10;
            for (; div > 0; div /= 10) sb.Append((char)('0' + value / div % 10));
        }
    }
}
