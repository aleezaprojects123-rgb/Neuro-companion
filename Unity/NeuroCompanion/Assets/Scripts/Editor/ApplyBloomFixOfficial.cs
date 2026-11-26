using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NeuroCompanion.Backend.Editor
{
    public static class ApplyBloomFixOfficial
    {
        [MenuItem("NeuroCompanion/Set Bloom Threshold To 2.5 Official")]
        public static void ApplyFix()
        {
            // 1. Load the active VolumeProfile (SampleSceneProfile.asset)
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile == null)
            {
                Debug.LogError("[ApplyBloomFixOfficial] Assets/Settings/SampleSceneProfile.asset not found!");
                return;
            }

            Undo.RecordObject(profile, "Set Bloom Threshold 2.5");
            Bloom bloom;
            if (profile.TryGet(out bloom))
            {
                Debug.Log($"[ApplyBloomFixOfficial] Original Bloom Threshold: {bloom.threshold.value}, Intensity: {bloom.intensity.value}, Active: {bloom.active}");
                
                // Increase Bloom Threshold from 1.0 to 2.5
                bloom.threshold.value = 2.5f;
                bloom.threshold.overrideState = true;

                // Keep everything else unchanged
                EditorUtility.SetDirty(profile);
                Debug.Log($"[ApplyBloomFixOfficial] New Bloom Threshold: {bloom.threshold.value}");
            }
            else
            {
                Debug.LogError("[ApplyBloomFixOfficial] Bloom component not found in profile!");
                return;
            }

            // 2. Ensure Gardening_Planting_Station and Plant_Seedlings are fully enabled
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null)
            {
                foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var t = r.transform.Find("Gardening_Planting_Station");
                    if (t != null) { station = t.gameObject; break; }
                }
            }

            if (station != null)
            {
                station.SetActive(true);
                // Ensure all 8 children are active
                for (int i = 0; i < station.transform.childCount; i++)
                {
                    var child = station.transform.GetChild(i);
                    child.gameObject.SetActive(true);
                }

                // Specifically verify Plant_Seedlings and all 6 seedlings
                var seedlings = station.transform.Find("Plant_Seedlings");
                if (seedlings != null)
                {
                    seedlings.gameObject.SetActive(true);
                    for (int i = 0; i < seedlings.childCount; i++)
                    {
                        seedlings.GetChild(i).gameObject.SetActive(true);
                    }
                    Debug.Log($"[ApplyBloomFixOfficial] Confirmed Plant_Seedlings is active with {seedlings.childCount} seedlings.");
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ApplyBloomFixOfficial] Successfully saved scene and VolumeProfile.");
        }
    }
}
