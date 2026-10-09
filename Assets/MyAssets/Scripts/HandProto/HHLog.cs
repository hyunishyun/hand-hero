using System.Diagnostics;

// Chatty diagnostics that only exist in the editor and development builds
// (round 3, D3). In a release build the compiler removes the whole call,
// including building its message string. Warnings and errors keep using
// UnityEngine.Debug directly so they reach logcat in every build.
public static class HHLog
{
    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    public static void Info(string message)
    {
        UnityEngine.Debug.Log(message);
    }
}
