using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using MetaPlanes = UnityEngine.XR.OpenXR.Features.Meta.ARPlaneFeature;
using MetaBoundingBoxes = UnityEngine.XR.OpenXR.Features.Meta.ARBoundingBoxFeature;
using MetaMeshing = UnityEngine.XR.OpenXR.Features.Meta.ARMeshFeature;

namespace HandHero.EditorTools
{
    // Round 4 (S8, D9): the MR room-scan spike needs the Meta OpenXR "Planes" and
    // "Bounding Boxes" features on Android. Enabling them is the only settings
    // change; Meta's own build step (ModifyAndroidManifest) then adds the
    // com.oculus.permission.USE_SCENE and USE_ANCHOR_API manifest entries, so no
    // manifest is edited by hand. Meshing stays off (the probe does not use it).
    // Idempotent; run with
    //   AUTO\tools\compile_check.ps1 -ExecuteMethod HandHero.EditorTools.RoomScanXRSettings.Apply
    public static class RoomScanXRSettings
    {
        [MenuItem("Hand Hero/Room Scan/Enable Meta Planes + Bounding Boxes (Android)")]
        public static void Apply()
        {
            OpenXRSettings android = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (android == null)
                throw new System.InvalidOperationException("[RoomScanXRSettings] no OpenXR settings for Android");

            bool changed = false;
            changed |= Enable<MetaPlanes>(android, "Meta Quest: Planes");
            changed |= Enable<MetaBoundingBoxes>(android, "Meta Quest: Bounding Boxes");

            var mesh = android.GetFeature<MetaMeshing>();
            Debug.Log($"[RoomScanXRSettings] Meta Quest: Meshing stays {(mesh != null && mesh.enabled ? "ON (left as found)" : "off")}");

            if (changed)
            {
                EditorUtility.SetDirty(android);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"[RoomScanXRSettings] done ({(changed ? "changed" : "already set")})");
        }

        private static bool Enable<T>(OpenXRSettings settings, string name) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            T feature = settings.GetFeature<T>();
            if (feature == null)
                throw new System.InvalidOperationException($"[RoomScanXRSettings] feature not found: {name}");
            if (feature.enabled)
            {
                Debug.Log($"[RoomScanXRSettings] {name}: already on");
                return false;
            }
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            Debug.Log($"[RoomScanXRSettings] {name}: off -> on");
            return true;
        }
    }
}
