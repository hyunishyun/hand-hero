using System;
using System.IO;
using UnityEngine;

// Run telemetry file (round 3, D16): one JSON line per run in
// <persistentDataPath>/run_log.jsonl, read by AUTO/tools/run_summary.py.
// Written only at a run's end (end screen or quit), never during a fight.
public static class RunLogFile
{
    public const string FileName = "run_log.jsonl";

    public static string LogPath => Path.Combine(Application.persistentDataPath, FileName);

    // Deep review DR-7: which APK wrote a run. The file survives `adb install -r`, so
    // each run carries this. BuildScript stamps the APK's version for the build's
    // duration ("9.2.0+20261010_1500_release", the APK file name's stamp); a build
    // made from Build Profiles has the plain version. The editor writes "editor".
    public static string BuildId => Application.isEditor ? "editor" : Application.version;

    public static void Append(string jsonLine)
    {
        string path = LogPath;
        try
        {
            File.AppendAllText(path, jsonLine + "\n");
            HHLog.Info($"[RunLog] run written -> {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RunLog] could not write {path}: {e.Message}");
        }
    }
}
