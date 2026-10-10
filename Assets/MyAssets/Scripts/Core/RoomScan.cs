using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HandHero.Core
{
    // Round 4 (S8, D9): MR room-scan spike. Pure rules behind RoomScanProbe:
    // when to run the AR plane / bounding box managers and ask for the scene
    // permission, time to first result, and a one-line summary that holds only
    // counts, labels and sizes (never raw room geometry).

    public enum RoomItemKind : byte
    {
        Plane,
        Box,
    }

    // Detail code of a PerfRecordKind.RoomScan record.
    public enum RoomScanEvent
    {
        None = 0,
        Started,           // managers enabled (MR TABLE + permission + subsystems)
        Stopped,           // left MR TABLE
        PermissionAsked,   // the system dialog was requested
        PermissionGranted,
        PermissionDenied,  // MR TABLE stays exactly as before; never asked again this session
        Unavailable,       // no plane / bounding box subsystem (feature off or rejected by the runtime)
        FirstPlanes,       // first non-empty plane result
        FirstBoxes,        // first non-empty bounding box result
        Summary,
        PermissionDismissed, // round 5 (S8-1-2): dialog closed without an answer; a denial for this session
    }

    public struct RoomScanCommand
    {
        public bool RunManagers;
        public bool RequestPermission;
        public RoomScanEvent Event;
    }

    // Decides, once per frame, whether the AR managers run. Never asks for the
    // permission outside MR TABLE, asks at most once per app session, and a
    // denial, a dismissed dialog, a missing subsystem or a failure leaves
    // everything off.
    public class RoomScanFlow
    {
        private enum PermissionState { Unknown, Asked, Granted, Denied }

        private PermissionState _permission;
        private bool _running;
        private bool _reportedUnavailable;
        private bool _failed;
        private int _stopWait = -1;

        public bool Enabled = true;
        // Round 5 (F2-2): frames the managers keep running after MR TABLE or the
        // menu ends before Stopped comes (the probe then logs the summary and
        // switches the managers off), so that work never lands on the match-start
        // frame. 0 = the same frame (round 4).
        public int StopDelayFrames;

        public bool Running => _running;
        public bool Failed => _failed;

        // Round 5 (S8-2-1): the permission query (a JNI call on Android) only
        // matters until the answer is known; after that Step ignores `granted`.
        public bool NeedsPermissionQuery =>
            Enabled && !_failed && !_reportedUnavailable
            && (_permission == PermissionState.Unknown || _permission == PermissionState.Asked);

        public RoomScanCommand Step(bool tabletop, bool available, bool granted)
        {
            var c = new RoomScanCommand();
            if (_failed) return c;
            if (!Enabled || !tabletop)
            {
                if (!_running) return c;
                if (_stopWait < 0) _stopWait = Math.Max(0, StopDelayFrames);
                if (_stopWait > 0)
                {
                    _stopWait--;
                    c.RunManagers = true; // still on: Stopped comes a few frames later
                    return c;
                }
                _stopWait = -1;
                _running = false;
                c.Event = RoomScanEvent.Stopped;
                return c;
            }
            _stopWait = -1; // back before a deferred stop: the managers just keep running

            if (!available)
            {
                _running = false;
                if (!_reportedUnavailable)
                {
                    _reportedUnavailable = true;
                    c.Event = RoomScanEvent.Unavailable;
                }
                return c;
            }

            if (granted && _permission != PermissionState.Denied) _permission = PermissionState.Granted;

            switch (_permission)
            {
                case PermissionState.Unknown:
                    _permission = PermissionState.Asked;
                    c.RequestPermission = true;
                    c.Event = RoomScanEvent.PermissionAsked;
                    return c;
                case PermissionState.Granted:
                    c.RunManagers = true;
                    if (!_running) c.Event = RoomScanEvent.Started;
                    _running = true;
                    return c;
                default: // Asked (waiting for the dialog) or Denied
                    return c;
            }
        }

        public RoomScanEvent OnPermissionResult(bool granted)
        {
            _permission = granted ? PermissionState.Granted : PermissionState.Denied;
            return granted ? RoomScanEvent.PermissionGranted : RoomScanEvent.PermissionDenied;
        }

        // Round 5 (S8-1-2): the dialog was closed without an answer. A denial for
        // this session (never asked again), logged as its own event.
        public RoomScanEvent OnPermissionDismissed()
        {
            _permission = PermissionState.Denied;
            return RoomScanEvent.PermissionDismissed;
        }

        // Round 5 (S8-1-3): switching the managers threw, or every subsystem died.
        // Off for the rest of the session with no further events (no Started, no
        // Stopped, so no summary either): returns Unavailable the first time, so
        // the probe logs exactly one line, and None after that.
        public RoomScanEvent OnFailed()
        {
            _failed = true;
            _running = false;
            _stopWait = -1;
            if (_reportedUnavailable) return RoomScanEvent.None;
            _reportedUnavailable = true;
            return RoomScanEvent.Unavailable;
        }
    }

    // Round 5 (S8-2-1 / F2-3 / F4-2): when RoomScanProbe asks Android whether the
    // scene permission is granted (a JNI call). Only while the flow still needs
    // the answer (RoomScanFlow.NeedsPermissionQuery), the first time at once and
    // then at most once per Interval; in between the last answer stands.
    public class RoomScanPermissionPoll
    {
        private double _next = double.NegativeInfinity;

        // Seconds between two queries.
        public double Interval = 0.5;

        public bool LastAnswer { get; private set; }

        // True when the caller should query now and pass the result to Answer.
        public bool Due(bool needed, double now)
        {
            if (!needed || now < _next) return false;
            _next = now + Math.Max(0.0, Interval);
            return true;
        }

        public void Answer(bool granted) => LastAnswer = granted;
    }

    // Time from Start to the first non-empty result of each kind.
    public class RoomScanTimer
    {
        private double _start = -1.0;
        private float _firstPlane = -1f;
        private float _firstBox = -1f;

        public bool Started => _start >= 0.0;

        public void Start(double now)
        {
            _start = now;
            _firstPlane = -1f;
            _firstBox = -1f;
        }

        // True only for the first non-empty result of that kind since Start.
        public bool Observe(RoomItemKind kind, int count, double now)
        {
            if (!Started || count <= 0) return false;
            float seconds = (float)(now - _start);
            if (kind == RoomItemKind.Plane)
            {
                if (_firstPlane >= 0f) return false;
                _firstPlane = seconds;
            }
            else
            {
                if (_firstBox >= 0f) return false;
                _firstBox = seconds;
            }
            return true;
        }

        public float FirstSeconds(RoomItemKind kind) => kind == RoomItemKind.Plane ? _firstPlane : _firstBox;
    }

    // One plane or bounding box in tracking space (real meters, y up).
    public struct RoomItem
    {
        public RoomItemKind Kind;
        public string Label;       // classification name, see RoomScanSummary.CleanLabel
        public bool HorizontalUp;  // planes only: faces up (a box's top face always does)
        public Vector3 Center;
        public Vector3 Size;       // plane: (width, 0, depth); box: (x, y, z) with y vertical
    }

    public struct RoomSurface
    {
        public bool Found;
        public RoomItemKind Kind;
        public string Label;
        public float Width;
        public float Depth;
        public float Area;
        public float HeightAboveTable; // surface height minus the table point height, meters
        public float Distance;         // horizontal distance from the table point, meters
    }

    public static class RoomScanSummary
    {
        // "Table, Couch" (a flags ToString) -> "Table+Couch"; empty -> "None".
        public static string CleanLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "None";
            return label.Replace(", ", "+");
        }

        // The biggest up-facing surface (plane, or box top) whose center is within
        // radius (horizontally) and maxHeightGap (vertically) of the table point:
        // where the virtual arena floor appears in MR TABLE.
        public static RoomSurface LargestHorizontalNear(IReadOnlyList<RoomItem> items, Vector3 tablePoint,
            float radius, float maxHeightGap)
        {
            var best = new RoomSurface();
            for (int i = 0; i < items.Count; i++)
            {
                RoomItem it = items[i];
                float top;
                if (it.Kind == RoomItemKind.Plane)
                {
                    if (!it.HorizontalUp) continue;
                    top = it.Center.y;
                }
                else
                {
                    top = it.Center.y + it.Size.y * 0.5f;
                }

                float dy = top - tablePoint.y;
                float dx = it.Center.x - tablePoint.x;
                float dz = it.Center.z - tablePoint.z;
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > radius || Mathf.Abs(dy) > maxHeightGap) continue;

                float area = it.Size.x * it.Size.z;
                if (best.Found && area <= best.Area) continue;
                best = new RoomSurface
                {
                    Found = true,
                    Kind = it.Kind,
                    Label = it.Label,
                    Width = it.Size.x,
                    Depth = it.Size.z,
                    Area = area,
                    HeightAboveTable = dy,
                    Distance = d,
                };
            }
            return best;
        }

        // "[Table:1,WallFace:2]" (labels in ordinal order) or "[-]".
        public static void AppendLabelCounts(StringBuilder sb, IReadOnlyList<RoomItem> items, RoomItemKind kind)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Kind != kind) continue;
                string label = CleanLabel(items[i].Label);
                counts.TryGetValue(label, out int n);
                counts[label] = n + 1;
            }

            sb.Append('[');
            if (counts.Count == 0) sb.Append('-');
            bool first = true;
            foreach (KeyValuePair<string, int> kv in counts)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(kv.Key).Append(':');
                PerfLogFormatter.AppendLong(sb, kv.Value);
            }
            sb.Append(']');
        }

        public static int Count(IReadOnlyList<RoomItem> items, RoomItemKind kind)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
                if (items[i].Kind == kind) n++;
            return n;
        }

        // e.g. planes=2 [Floor:1,Table:1] boxes=1 [Table:1] first_plane=0.42s first_box=0.50s
        //      near_table=Table(plane) 1.20x0.60m area=0.72 dy=0.03 d=0.00
        public static string Format(IReadOnlyList<RoomItem> items, RoomScanTimer timer, Vector3 tablePoint,
            float radius, float maxHeightGap)
        {
            var sb = new StringBuilder(160);
            sb.Append("planes=");
            PerfLogFormatter.AppendLong(sb, Count(items, RoomItemKind.Plane));
            sb.Append(' ');
            AppendLabelCounts(sb, items, RoomItemKind.Plane);
            sb.Append(" boxes=");
            PerfLogFormatter.AppendLong(sb, Count(items, RoomItemKind.Box));
            sb.Append(' ');
            AppendLabelCounts(sb, items, RoomItemKind.Box);
            sb.Append(" first_plane=");
            PerfLogFormatter.AppendFixed(sb, timer.FirstSeconds(RoomItemKind.Plane), 2);
            sb.Append("s first_box=");
            PerfLogFormatter.AppendFixed(sb, timer.FirstSeconds(RoomItemKind.Box), 2);
            sb.Append("s near_table=");

            RoomSurface s = LargestHorizontalNear(items, tablePoint, radius, maxHeightGap);
            if (!s.Found)
            {
                sb.Append("none");
                return sb.ToString();
            }
            sb.Append(CleanLabel(s.Label)).Append(s.Kind == RoomItemKind.Plane ? "(plane) " : "(box) ");
            PerfLogFormatter.AppendFixed(sb, s.Width, 2);
            sb.Append('x');
            PerfLogFormatter.AppendFixed(sb, s.Depth, 2);
            sb.Append("m area=");
            PerfLogFormatter.AppendFixed(sb, s.Area, 2);
            sb.Append(" dy=");
            PerfLogFormatter.AppendFixed(sb, s.HeightAboveTable, 2);
            sb.Append(" d=");
            PerfLogFormatter.AppendFixed(sb, s.Distance, 2);
            return sb.ToString();
        }

        public static string EventName(RoomScanEvent e)
        {
            switch (e)
            {
                case RoomScanEvent.Started: return "STARTED";
                case RoomScanEvent.Stopped: return "STOPPED";
                case RoomScanEvent.PermissionAsked: return "PERMISSION_ASKED";
                case RoomScanEvent.PermissionGranted: return "PERMISSION_GRANTED";
                case RoomScanEvent.PermissionDenied: return "PERMISSION_DENIED";
                case RoomScanEvent.Unavailable: return "UNAVAILABLE";
                case RoomScanEvent.FirstPlanes: return "FIRST_PLANES";
                case RoomScanEvent.FirstBoxes: return "FIRST_BOXES";
                case RoomScanEvent.Summary: return "SUMMARY";
                case RoomScanEvent.PermissionDismissed: return "PERMISSION_DISMISSED";
                default: return "NONE";
            }
        }
    }
}
