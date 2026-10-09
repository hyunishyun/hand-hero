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
