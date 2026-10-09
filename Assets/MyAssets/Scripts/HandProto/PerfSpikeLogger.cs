using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HandHero.Core;
using UnityEngine;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using InputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;

// Freeze hunt (round 3, P1): records every frame whose real interval is over
// the threshold, plus edge events (hand / head tracking, headset presence,
// focus, app pause, match / run phase, game pause, system gesture), into a ring
// buffer of structs. Nothing is written to disk mid-fight: the buffer goes to
// <persistentDataPath>/perf_log.txt on app pause, focus loss, return to the
// menu, run end, quit, and every few seconds out of combat. Compiled into
// every build (editor, dev, release), because only the release APK has
// representative timing. Spikes are measured on real time, not deltaTime.
[DefaultExecutionOrder(-1000)]
public class PerfSpikeLogger : MonoBehaviour
{
    public const string FileName = "perf_log.txt";
    public const string PreviousFileName = "perf_log_prev.txt";

    [Tooltip("Record and write the perf log (on in every build by default)")]
    [SerializeField] private bool logEnabled = true;
    [Tooltip("A frame whose real interval is longer than this (ms) is logged as a spike")]
    [SerializeField] private float spikeThresholdMs = 50f;
    [Tooltip("Records kept between flushes; the oldest are overwritten (and counted) when full")]
    [SerializeField] private int bufferCapacity = 1024;
    [Tooltip("Seconds between flushes while out of combat (never flushes mid-fight)")]
    [SerializeField] private float flushInterval = 10f;
    [Tooltip("perf_log.txt is moved to perf_log_prev.txt once it grows past this many bytes")]
    [SerializeField] private int maxFileBytes = 1024 * 1024;

    [Header("References (optional)")]
    [SerializeField] private MatchDirector match;
    [SerializeField] private RunDirector run;
    [Tooltip("Falls back to HandGestureTracker.Instance")]
    [SerializeField] private HandGestureTracker tracker;

    private static PerfSpikeLogger s_instance;

    private FrameSpikeDetector _detector;
    private RingBuffer<PerfSample> _buffer;
    private StringBuilder _text;
    private readonly FrameTiming[] _timings = new FrameTiming[1];
    private readonly List<XRInputDevice> _heads = new List<XRInputDevice>();

    private readonly TrackedEdge _leftEdge = new TrackedEdge();
    private readonly TrackedEdge _rightEdge = new TrackedEdge();
    private readonly TrackedEdge _headEdge = new TrackedEdge();
    private readonly TrackedEdge _presenceEdge = new TrackedEdge();
    private readonly TrackedEdge _gamePauseEdge = new TrackedEdge();
    private bool _leftTracked, _rightTracked, _headTracked;
    private MatchPhase _lastMatch = MatchPhase.Boot;
    private RunPhase _lastRun = RunPhase.Idle;

    private int _lastGcCount;
    private double _lastFlush;
    private bool _headerWritten;
    private string _path;

    // Since the last flush, for the flush summary line.
    private int _frames;
    private int _spikes;
    private float _worstMs;

    // Other systems log their edge events here (e.g. the Meta system gesture, P9).
    public static void Mark(PerfRecordKind kind, int detail = 0)
    {
        if (s_instance != null) s_instance.Record(kind, detail);
    }

    public static string LogPath => Path.Combine(Application.persistentDataPath, FileName);

    private void Awake()
    {
        s_instance = this;
        _detector = new FrameSpikeDetector(spikeThresholdMs);
        _buffer = new RingBuffer<PerfSample>(Mathf.Max(16, bufferCapacity));
        _text = new StringBuilder(256 * 160); // ~160 chars per line; grows on a big flush only
        _path = LogPath;
        _lastGcCount = GC.CollectionCount(0);
        _lastFlush = Time.realtimeSinceStartupAsDouble;
    }

    private void OnDestroy()
    {
        if (s_instance == this) s_instance = null;
    }

    private void Update()
    {
        if (!logEnabled) return;

        double now = Time.realtimeSinceStartupAsDouble;
        FrameTimingManager.CaptureFrameTimings();
        _detector.ThresholdMs = spikeThresholdMs;
        bool spike = _detector.Step(now);

        _frames++;
        if (_detector.LastFrameMs > _worstMs) _worstMs = _detector.LastFrameMs;
        if (spike)
        {
            _spikes++;
            Record(PerfRecordKind.Spike, 0);
        }

        PollTracking();
        PollPhases();

        if (PerfFlushPolicy.PeriodicFlushDue(InCombat(), now, _lastFlush, flushInterval))
            Flush("periodic");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        Record(hasFocus ? PerfRecordKind.FocusGained : PerfRecordKind.FocusLost, 0);
        if (!hasFocus) Flush("focus lost");
    }

    private void OnApplicationPause(bool paused)
    {
        Record(paused ? PerfRecordKind.AppPaused : PerfRecordKind.AppResumed, 0);
        if (paused) Flush("app pause");
        else _detector.Reset(); // the suspended time is not a frame
    }

    private void OnApplicationQuit()
    {
        Flush("quit");
    }

    private void OnDisable()
    {
        Flush("disabled");
    }

    private void PollTracking()
    {
        HandGestureTracker t = tracker != null ? tracker : HandGestureTracker.Instance;
        if (t != null)
        {
            _leftTracked = t.Left.IsTracked;
            _rightTracked = t.Right.IsTracked;
            RecordEdge(_leftEdge.Step(_leftTracked), PerfRecordKind.HandFound, PerfRecordKind.HandLost, 0);
            RecordEdge(_rightEdge.Step(_rightTracked), PerfRecordKind.HandFound, PerfRecordKind.HandLost, 1);
        }

        // Same query as MatchDirector's presence check; it allocates nothing with a reused list.
        _heads.Clear();
        XRInputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, _heads);
        if (_heads.Count == 0)
        {
            _headTracked = false;
            return;
        }
        if (_heads[0].TryGetFeatureValue(XRCommonUsages.isTracked, out bool tracked))
        {
            _headTracked = tracked;
            RecordEdge(_headEdge.Step(tracked), PerfRecordKind.HeadFound, PerfRecordKind.HeadLost, 0);
        }
        if (_heads[0].TryGetFeatureValue(XRCommonUsages.userPresence, out bool present))
            RecordEdge(_presenceEdge.Step(present), PerfRecordKind.UserPresent, PerfRecordKind.UserAbsent, 0);
    }

    private void PollPhases()
    {
        MatchStateMachine m = match != null ? match.Match : null;
        if (m != null)
        {
            RecordEdge(_gamePauseEdge.Step(m.IsPaused), PerfRecordKind.GamePaused, PerfRecordKind.GameResumed, 0);
            if (m.Phase != _lastMatch)
            {
                _lastMatch = m.Phase;
                Record(PerfRecordKind.MatchPhase, 0);
                if (m.Phase == MatchPhase.Menu) Flush("menu");
            }
        }

        RunStateMachine r = run != null ? run.Run : null;
        if (r != null && r.Phase != _lastRun)
        {
            _lastRun = r.Phase;
            Record(PerfRecordKind.RunPhase, 0);
            if (r.Phase == RunPhase.Victory || r.Phase == RunPhase.Defeat) Flush("run end");
        }
    }

    private void RecordEdge(int edge, PerfRecordKind becameTrue, PerfRecordKind becameFalse, int detail)
    {
        if (edge > 0) Record(becameTrue, detail);
        else if (edge < 0) Record(becameFalse, detail);
    }

    private bool InCombat()
    {
        MatchStateMachine m = match != null ? match.Match : null;
        if (m == null) return false;
        RunStateMachine r = run != null ? run.Run : null;
        return PerfFlushPolicy.InCombat(m.Phase, r != null ? r.Phase : RunPhase.Idle, m.IsPaused);
    }

    private void Record(PerfRecordKind kind, int detail)
    {
        if (!logEnabled || _buffer == null) return;

        int gc = GC.CollectionCount(0);
        var s = new PerfSample
        {
            Kind = kind,
            Detail = detail,
            Time = Time.realtimeSinceStartupAsDouble,
            FrameMs = _detector.LastFrameMs,
            CpuMs = -1f,
            RenderMs = -1f,
            GpuMs = -1f,
            GcCollections = gc - _lastGcCount,
            HeapBytes = GC.GetTotalMemory(false),
            TimeScale = Time.timeScale,
            Focused = Application.isFocused,
            LeftTracked = _leftTracked,
            RightTracked = _rightTracked,
            HeadTracked = _headTracked,
        };
        _lastGcCount = gc;

        // Timings lag a few frames behind; 0 means the platform reported nothing.
        if (FrameTimingManager.GetLatestTimings(1, _timings) > 0)
        {
            s.CpuMs = Timing(_timings[0].cpuMainThreadFrameTime);
            s.RenderMs = Timing(_timings[0].cpuRenderThreadFrameTime);
            s.GpuMs = Timing(_timings[0].gpuFrameTime);
        }

        MatchStateMachine m = match != null ? match.Match : null;
        if (m != null)
        {
            s.Match = m.Phase;
            s.MatchPaused = m.IsPaused;
        }
        RunStateMachine r = run != null ? run.Run : null;
        if (r != null)
        {
            s.Run = r.Phase;
            s.Island = r.Island;
        }
        if (run != null) s.BotsAlive = run.BotsAlive;

        _buffer.Add(s);
    }

    private static float Timing(double ms) => ms > 0.0 ? (float)ms : -1f;

    // Allowed to allocate (string, file IO): it only runs at safe moments.
    private void Flush(string reason)
    {
        if (!logEnabled || _buffer == null) return;
        _lastFlush = Time.realtimeSinceStartupAsDouble;
        if (_buffer.Count == 0 && _headerWritten) return;

        try
        {
            RotateIfLarge();

            double clockAtZero = DateTime.Now.TimeOfDay.TotalSeconds - Time.realtimeSinceStartupAsDouble;
            _text.Clear();
            if (!_headerWritten) AppendHeader();
            for (int i = 0; i < _buffer.Count; i++)
                PerfLogFormatter.AppendLine(_text, _buffer[i], clockAtZero);
            int records = _buffer.Count;
            _text.Append("--- flush (").Append(reason).Append(") records=").Append(records)
                .Append(" dropped=").Append(_buffer.Dropped)
                .Append(" frames=").Append(_frames)
                .Append(" spikes=").Append(_spikes)
                .Append(" worst=");
            PerfLogFormatter.AppendFixed(_text, _worstMs, 1);
            _text.Append("ms\n");

            File.AppendAllText(_path, _text.ToString());
            _headerWritten = true;
            Debug.Log($"[PerfSpikeLogger] {reason}: {records} records, {_spikes} spikes, worst {_worstMs:F1} ms -> {_path}");

            _buffer.Clear();
            _frames = 0;
            _spikes = 0;
            _worstMs = 0f;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PerfSpikeLogger] could not write {_path}: {e.Message}");
        }
    }

    private void RotateIfLarge()
    {
        var info = new FileInfo(_path);
        if (!info.Exists || info.Length < maxFileBytes) return;

        string prev = Path.Combine(Path.GetDirectoryName(_path) ?? "", PreviousFileName);
        if (File.Exists(prev)) File.Delete(prev);
        File.Move(_path, prev);
        _headerWritten = false; // the new file starts with its own header
    }

    private void AppendHeader()
    {
        string build = Application.isEditor ? "editor" : Debug.isDebugBuild ? "dev" : "release";
        float refresh = UnityEngine.XR.XRDevice.refreshRate;
        if (refresh <= 0f) refresh = (float)Screen.currentResolution.refreshRateRatio.value;
        _text.Append("=== Hand Hero perf log | ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            .Append(" | app ").Append(Application.version)
            .Append(" | build ").Append(build)
            .Append(" | device ").Append(SystemInfo.deviceModel)
            .Append(" | refresh ").Append(refresh.ToString("F1")).Append(" Hz")
            .Append(" | unity ").Append(Application.unityVersion)
            .Append(" | spike > ").Append(spikeThresholdMs.ToString("F0")).Append(" ms")
            .Append(" | frame timing ").Append(FrameTimingManager.IsFeatureEnabled() ? "on" : "off")
            .Append('\n');
    }
}
