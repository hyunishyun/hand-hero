using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
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
    // The APK's version name carries the same stamp ("9.2.0+20261010_1500_release"),
    // so run_log.jsonl and perf_log.txt say which APK wrote them (deep review DR-7).
    // Scenes (deep review DR-4): the usual builds regenerate Arena_Main and the RunBot
    // prefab from code first, which resets inspector edits to the C# defaults. The
    // *KeepScenes builds use the saved scene and prefab as they are.
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

        // Clean builds (BuildOptions.CleanBuildCache): Gradle's incremental packaging
        // can append a new libil2cpp.so and keep the old copy inside the APK file
        // (dev APK 207 MB for 126 MB of entries). A clean build drops the stale bytes.
        [MenuItem("HandHero/Build Quest APK (dev, clean)")]
        public static void BuildQuestApkDevClean() => BuildQuestApk(development: true, clean: true);

        [MenuItem("HandHero/Build Quest APK (release, clean)")]
        public static void BuildQuestApkReleaseClean() => BuildQuestApk(development: false, clean: true);

        // Deep review DR-4: build with the saved Arena_Main scene and RunBot prefab, so
        // values changed in the inspector (and saved) ship. Builder or serialized-field
        // changes in code are not applied until the scenes are rebuilt (Build All Scenes).
        [MenuItem("HandHero/Build Quest APK (release, keep scene edits)")]
        public static void BuildQuestApkReleaseKeepScenes() => BuildQuestApk(development: false, keepScenes: true);

        [MenuItem("HandHero/Build Quest APK (dev, keep scene edits)")]
        public static void BuildQuestApkDevKeepScenes() => BuildQuestApk(development: true, keepScenes: true);

        // Old command name, kept so earlier scripts still work: the release build.
        public static void BuildQuestApk() => BuildQuestApkRelease();

        // The saved files a rebuild overwrites: the APK's scene and the run bot prefab.
        private static readonly string[] GeneratedSceneFiles =
        {
            HandHeroSceneBuilder.ArenaMainScenePath,
            HandHeroSceneBuilder.RunBotPrefabPath,
        };

        // Says whether the saved scene and prefab still match what the scene builder last
        // wrote on this PC. Batch mode:
        //   -executeMethod HandHero.EditorTools.BuildScript.CheckSceneEdits
        [MenuItem("HandHero/Check Scene Edits")]
        public static void CheckSceneEdits()
        {
            foreach (string path in GeneratedSceneFiles)
            {
                string state;
                switch (SceneBuildFingerprints.Check(path))
                {
                    case SceneBuildFingerprints.State.AsBuilt: state = "as the scene builder wrote it"; break;
                    case SceneBuildFingerprints.State.Edited:
                        state = "EDITED since the scene builder wrote it (inspector edit or git checkout); " +
                                "the usual APK builds reset it, the keep-scene-edits builds keep it";
                        break;
                    case SceneBuildFingerprints.State.Missing: state = "missing"; break;
                    default: state = "no scene build recorded on this PC yet"; break;
                }
                Debug.Log($"[BuildScript] NOTE: {path}: {state}");
            }
        }

        private static void BuildQuestApk(bool development, bool clean = false, bool keepScenes = false)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Fail("Active build target is not Android. Start Unity with -buildTarget Android " +
                     "(or switch platform in Build Profiles) and run again.");
                return;
            }

            if (!PrepareScenes(keepScenes)) return;
            ConfigurePlayer();
            ConfigureOpenXR();
            if (!CheckOpenXRValidation()) return;

            string buildDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Build"));
            Directory.CreateDirectory(buildDir);
            string suffix = development ? "dev" : "release";
            string stamp = $"{DateTime.Now:yyyyMMdd_HHmm}_{suffix}";
            string apkPath = Path.Combine(buildDir, $"HandHero_{stamp}.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { HandHeroSceneBuilder.ArenaMainScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = (development ? BuildOptions.Development | BuildOptions.AllowDebugging : BuildOptions.None)
                          | (clean ? BuildOptions.CleanBuildCache : BuildOptions.None),
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

            // Deep review DR-7: the version name carries the APK's stamp for this build only
            // (Application.version on the device; ProjectSettings keeps the plain version).
            // A "+..." left by an interrupted build is dropped first.
            string baseVersion = BaseVersion(PlayerSettings.bundleVersion);
            PlayerSettings.bundleVersion = $"{baseVersion}+{stamp}";

            Debug.Log($"[BuildScript] Building {suffix}{(clean ? " (clean)" : "")} APK -> {apkPath} " +
                      $"(version {PlayerSettings.bundleVersion})");
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            finally
            {
                PlayerSettings.bundleVersion = baseVersion;
                if (!development)
                {
                    PlayerSettings.SetStackTraceLogType(LogType.Log, logTrace);
                    PlayerSettings.SetStackTraceLogType(LogType.Warning, warningTrace);
                }
                AssetDatabase.SaveAssets();
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
                      $"{summary.totalTime.TotalMinutes:F1} min, version {baseVersion}+{stamp})");
        }

        // "9.2.0+20261010_1500_release" -> "9.2.0".
        private static string BaseVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return "0";
            int plus = version.IndexOf('+');
            return plus > 0 ? version.Substring(0, plus) : version;
        }

        // Deep review DR-4. Rebuilding stays the default (the scenes always match the code).
        // When the saved scene or prefab differs from what the builder last wrote, or
        // nothing was recorded on this PC yet (second pass), the rebuild would or might
        // reset those edits: the menu asks, batch mode logs a NOTE (compile_check prints
        // it) and rebuilds. keepScenes uses the saved files and builds them only when
        // they don't exist yet.
        private static bool PrepareScenes(bool keepScenes)
        {
            // The build reads the saved scene: offer to save unsaved inspector edits first.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[BuildScript] Build cancelled.");
                return false;
            }

            bool scenesExist = Array.TrueForAll(GeneratedSceneFiles, File.Exists);
            if (keepScenes && scenesExist)
            {
                Debug.Log("[BuildScript] Keeping the saved Arena_Main scene and RunBot prefab: inspector edits " +
                          "ship; builder changes in code are not applied until Build All Scenes.");
                return true;
            }
            if (keepScenes)
                Debug.Log("[BuildScript] No saved Arena_Main scene or RunBot prefab yet: building them.");

            List<string> edited = keepScenes ? new List<string>() : SceneBuildFingerprints.Edited(GeneratedSceneFiles);
            // Second pass: with no record (Library deleted, PC reset, another checkout) an
            // edit can't be told apart, and the rebuild used to reset it without a word.
            List<string> unknown = keepScenes ? new List<string>() : SceneBuildFingerprints.Unrecorded(GeneratedSceneFiles);
            if (edited.Count > 0 || unknown.Count > 0)
            {
                var why = new StringBuilder();
                if (edited.Count > 0)
                    why.Append(string.Join(", ", edited)).Append(" changed since the scene builder last wrote it " +
                                                                 "(an inspector edit or a git checkout). ");
                if (unknown.Count > 0)
                    why.Append(string.Join(", ", unknown)).Append(": no scene build recorded on this PC (Library " +
                                                                  "deleted, PC reset or a new checkout), so this " +
                                                                  "build can't tell whether it holds inspector edits. ");
                why.Append("This build regenerates the scenes from code, so any edits go back to the C# defaults.");
                string message = why.ToString();
                if (!Application.isBatchMode)
                {
                    int choice = EditorUtility.DisplayDialogComplex(edited.Count > 0
                            ? "Hand Hero: scene edits will be reset"
                            : "Hand Hero: scene edits may be reset",
                        message, "Rebuild scenes", "Cancel", "Keep my scene edits");
                    if (choice == 1)
                    {
                        Debug.Log("[BuildScript] Build cancelled.");
                        return false;
                    }
                    if (choice == 2)
                    {
                        Debug.Log("[BuildScript] Keeping the saved Arena_Main scene and RunBot prefab (chosen in the dialog).");
                        return true;
                    }
                }
                else
                {
                    Debug.LogWarning($"[BuildScript] NOTE: {message} To keep them, build with " +
                                     "BuildQuestApkReleaseKeepScenes or BuildQuestApkDevKeepScenes.");
                }
            }

            HandHeroSceneBuilder.BuildAll();
            return true;
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
