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
    // Quest APK build for the competition (hands only, no controllers needed).
    // Batch mode (the editor must start on Android so no in-process target switch):
    //   Unity.exe -batchmode -quit -buildTarget Android -projectPath <A_4>
    //     -executeMethod HandHero.EditorTools.BuildScript.BuildQuestApk
    // Output: <MetaAwards>\Build\HandHero_<yyyyMMdd>.apk
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

        [MenuItem("HandHero/Build Quest APK")]
        public static void BuildQuestApk()
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
            string apkPath = Path.Combine(buildDir, $"HandHero_{DateTime.Now:yyyyMMdd}.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { HandHeroSceneBuilder.ArenaMainScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Fail($"Build {summary.result}: {summary.totalErrors} errors. See the log above.");
                return;
            }
            Debug.Log($"[BuildScript] BUILD OK {apkPath} ({summary.totalSize / (1024f * 1024f):F1} MB, " +
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
