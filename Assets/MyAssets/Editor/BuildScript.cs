using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.OpenXR;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace HandHero.EditorTools
{
    // Quest APK builds for the competition (hands only, no controllers needed).
    // Batch mode (the editor must start on Android so no in-process target switch):
    //   Unity.exe -batchmode -quit -buildTarget Android -projectPath <A_4>
    //     -executeMethod HandHero.EditorTools.BuildScript.BuildQuestApkRelease (or BuildQuestApkDev)
    // Output: <MetaAwards>\Build\HandHero_<yyyyMMdd_HHmm>_release.apk / _dev.apk, so the
    // two never overwrite each other (round 3, D2). Compare perf numbers only
    // within one build type: a dev build runs slower.
    public static class BuildScript
    {
        public const string ApplicationId = "com.hyun.handhero";
        public const string ProductName = "Hand Hero";

        // Features enabled for Android, matched by full type name so this file
        // doesn't need direct references to the XR Hands / Meta assemblies (the
        // Android XR package has its own ARSessionFeature / ARCameraFeature).
        // XR Hands adds the hand tracking permission + uses-feature to the manifest
        // when MetaQuestFeature and HandTracking are both on. The Meta session +
        // camera features give the passthrough tabletop mode (T10).
        private static readonly string[] RequiredFeatures =
        {
            "UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature",
            "UnityEngine.XR.Hands.OpenXR.HandTracking",
            "UnityEngine.XR.Hands.OpenXR.MetaHandTrackingAim",
            "UnityEngine.XR.OpenXR.Features.Meta.ARSessionFeature",
            "UnityEngine.XR.OpenXR.Features.Meta.ARCameraFeature",
        };
        // Every Android XR (non-Quest) feature is turned off for the Quest APK.
        private const string ConflictingNamespace = "UnityEngine.XR.OpenXR.Features.Android";

        // The representative build: no development overhead, Log / Warning
        // without stack traces (Error, Assert, Exception keep theirs).
        [MenuItem("HandHero/Build Quest APK (release)")]
        public static void BuildQuestApkRelease() => BuildQuestApk(development: false);

        // Development + script debugging; HHLog diagnostics compiled in. The
        // profiler is attached by hand (Window > Analysis > Profiler), not auto-connected.
        [MenuItem("HandHero/Build Quest APK (dev)")]
        public static void BuildQuestApkDev() => BuildQuestApk(development: true);

        // Old command name, kept so earlier scripts still work: the release build.
        public static void BuildQuestApk() => BuildQuestApkRelease();

        private static void BuildQuestApk(bool development)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Fail("Active build target is not Android. Start Unity with -buildTarget Android " +
                     "(or switch platform in Build Profiles) and run again.");
                return;
            }

            HandHeroSceneBuilder.BuildAll();
            ConfigurePlayer();
            ConfigureOpenXR();
            if (!CheckOpenXRValidation()) return;

            string buildDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Build"));
            Directory.CreateDirectory(buildDir);
            string suffix = development ? "dev" : "release";
            string apkPath = Path.Combine(buildDir, $"HandHero_{DateTime.Now:yyyyMMdd_HHmm}_{suffix}.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { HandHeroSceneBuilder.ArenaMainScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None,
            };

            // Stack trace types are one project-wide setting (the editor Console
            // menu edits the same one), so the release build switches them only
            // for its own duration and the project keeps ScriptOnly everywhere.
            StackTraceLogType logTrace = PlayerSettings.GetStackTraceLogType(LogType.Log);
            StackTraceLogType warningTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning);
            if (!development)
            {
                PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            }

            Debug.Log($"[BuildScript] Building {suffix} APK -> {apkPath}");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                if (!development)
                {
                    PlayerSettings.SetStackTraceLogType(LogType.Log, logTrace);
                    PlayerSettings.SetStackTraceLogType(LogType.Warning, warningTrace);
                    AssetDatabase.SaveAssets();
                }
            }
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {summary.result}: {summary.totalErrors} errors. See the log above.");
                return;
            }
            // summary.totalSize is not the APK size (it reported 2 GB for a 207 MB APK).
            long apkBytes = File.Exists(apkPath) ? new FileInfo(apkPath).Length : 0;
            Debug.Log($"[BuildScript] BUILD OK {apkPath} ({apkBytes / (1024f * 1024f):F1} MB, " +
                      $"{summary.totalTime.TotalMinutes:F1} min)");
        }

        // Every value set here is listed in AUTO/PROGRESS.md (T9). Most already
        // matched the project; setting them again keeps the build reproducible.
        private static void ConfigurePlayer()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel32)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            EditorUserBuildSettings.buildAppBundle = false;
            ApplyDiagnosticsSettings();
            ApplyTimeSettings();
        }

        // Freeze hunt (round 3, P1): PerfSpikeLogger needs Frame Timing Stats to
        // split CPU from GPU time in the release APK. Batch mode:
        //   -executeMethod HandHero.EditorTools.BuildScript.ApplyDiagnosticsSettings
        [MenuItem("HandHero/Apply Diagnostics Settings")]
        public static void ApplyDiagnosticsSettings()
        {
            if (!PlayerSettings.enableFrameTimingStats)
            {
                PlayerSettings.enableFrameTimingStats = true;
                Debug.Log("[BuildScript] PlayerSettings.enableFrameTimingStats -> true");
            }
            AssetDatabase.SaveAssets();
        }

        // Hitch recovery (round 3, P7, D5): a long frame advances the game at most
        // 0.1 s (was 0.333) so a stall reads as a slowdown, not a jump; no
        // Rigidbodies, so physics steps at 50 Hz (was 100 Hz). Batch mode:
        //   -executeMethod HandHero.EditorTools.BuildScript.ApplyTimeSettings
        public const float MaximumAllowedTimestep = 0.1f;
        public const float FixedTimestep = 0.02f;

        [MenuItem("HandHero/Apply Time Settings")]
        public static void ApplyTimeSettings()
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[BuildScript] TimeManager.asset not found");
                return;
            }

            var timeManager = new SerializedObject(assets[0]);
            SerializedProperty maxStep = timeManager.FindProperty("Maximum Allowed Timestep");
            if (maxStep != null && !Mathf.Approximately(maxStep.floatValue, MaximumAllowedTimestep))
            {
                Debug.Log($"[BuildScript] Maximum Allowed Timestep {maxStep.floatValue} -> {MaximumAllowedTimestep}");
                maxStep.floatValue = MaximumAllowedTimestep;
                timeManager.ApplyModifiedPropertiesWithoutUndo();
            }

            // Fixed Timestep is stored as a rational; the Time API converts it.
            if (!Mathf.Approximately(Time.fixedDeltaTime, FixedTimestep))
            {
                Debug.Log($"[BuildScript] Fixed Timestep {Time.fixedDeltaTime} -> {FixedTimestep}");
                Time.fixedDeltaTime = FixedTimestep;
            }
            Time.maximumDeltaTime = MaximumAllowedTimestep;

            EditorUtility.SetDirty(assets[0]);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureOpenXR()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogWarning("[BuildScript] No OpenXR settings for Android");
                return;
            }

            var found = new HashSet<string>();
            foreach (OpenXRFeature feature in settings.GetFeatures())
            {
                string typeName = feature.GetType().FullName;
                bool want = Array.IndexOf(RequiredFeatures, typeName) >= 0;
                bool conflict = feature.GetType().Namespace == ConflictingNamespace;
                if (want) found.Add(typeName);
                if (!want && !conflict) continue;

                if (feature.enabled != want)
                {
                    feature.enabled = want;
                    EditorUtility.SetDirty(feature);
                    Debug.Log($"[BuildScript] OpenXR Android: {typeName} -> {(want ? "enabled" : "disabled")}");
                }
            }
            foreach (string required in RequiredFeatures)
                if (!found.Contains(required))
                    Debug.LogWarning($"[BuildScript] OpenXR Android feature not found: {required}");
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        // Logs every failing OpenXR project validation rule, applies the automatic
        // fixes for build-blocking ones once, and stops if any error remains.
        private static bool CheckOpenXRValidation()
        {
            var issues = new List<OpenXRFeature.ValidationRule>();
            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            foreach (OpenXRFeature.ValidationRule issue in issues)
            {
                if (!issue.error || issue.fixIt == null || !issue.fixItAutomatic) continue;
                Debug.Log($"[BuildScript] OpenXR auto-fix: {issue.message}");
                issue.fixIt();
            }
            AssetDatabase.SaveAssets();

            OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            int errors = 0;
            foreach (OpenXRFeature.ValidationRule issue in issues)
            {
                Debug.Log($"[BuildScript] OpenXR validation {(issue.error ? "ERROR" : "warning")}: {issue.message}");
                if (issue.error) errors++;
            }
            if (errors == 0) return true;
            Fail($"{errors} OpenXR validation errors remain (listed above).");
            return false;
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[BuildScript] BUILD FAILED: {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
