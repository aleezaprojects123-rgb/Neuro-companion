using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NeuroCompanion.Backend.Editor
{
    public static class FixSeedlingMaterials
    {
        [MenuItem("NeuroCompanion/Fix Seedling Materials Glow")]
        public static void Fix()
        {
            string matDir = "Assets/Materials/Environment/Seedlings";

            // 1. Fix Soil (soil was 0.95 smoothness, acting like a mirror!)
            FixMat($"{matDir}/Mat_Seedling_Soil.mat", new Color(0.18f, 0.12f, 0.08f), 0.02f, 0.05f);

            // 2. Fix Stem (was 0.70 smoothness)
            FixMat($"{matDir}/Mat_Seedling_Stem.mat", new Color(0.24f, 0.50f, 0.18f), 0.0f, 0.15f);

            // 3. Fix Bud
            FixMat($"{matDir}/Mat_Seedling_Bud.mat", new Color(0.85f, 0.55f, 0.10f), 0.0f, 0.20f);

            // 4. Fix Leaves (were pure Color.white with high smoothness)
            FixLeaf($"{matDir}/Mat_Leaf_Basil.mat", new Color(0.75f, 0.85f, 0.75f), 0.15f);
            FixLeaf($"{matDir}/Mat_Leaf_Tomato.mat", new Color(0.75f, 0.85f, 0.75f), 0.12f);
            FixLeaf($"{matDir}/Mat_Leaf_Lavender.mat", new Color(0.80f, 0.85f, 0.80f), 0.10f);
            FixLeaf($"{matDir}/Mat_Leaf_Marigold.mat", new Color(0.75f, 0.85f, 0.75f), 0.12f);
            FixLeaf($"{matDir}/Mat_Leaf_Mint.mat", new Color(0.75f, 0.85f, 0.75f), 0.12f);
            FixLeaf($"{matDir}/Mat_Leaf_Strawberry.mat", new Color(0.75f, 0.85f, 0.75f), 0.15f);

            // 5. Ensure all props on station are active and clean
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station != null)
            {
                station.SetActive(true);
                for (int i = 0; i < station.transform.childCount; i++)
                {
                    station.transform.GetChild(i).gameObject.SetActive(true);
                }
                // Keep water pour point inactive until Carl waters
                var water = station.transform.Find("Watering_Can/WateringCan_Spout/WateringCan_Rose/Water_Pour_Point");
                if (water != null) water.gameObject.SetActive(false);
            }

            // Also check Bloom in SampleSceneProfile
            var volumeProfile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Scenes/SampleSceneProfile.asset");
            if (volumeProfile != null)
            {
                UnityEngine.Rendering.Universal.Bloom bloom;
                if (volumeProfile.TryGet(out bloom))
                {
                    // Increase threshold slightly to 1.5 to prevent sub-pixel distant clusters from blooming
                    bloom.threshold.value = 1.5f;
                    bloom.intensity.value = 0.15f;
                    EditorUtility.SetDirty(volumeProfile);
                    Debug.Log("[FixSeedlingMaterials] Bloom threshold set to 1.5, intensity to 0.15.");
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[FixSeedlingMaterials] Complete! Seedling materials and station updated.");
        }

        static void FixMat(string path, Color baseCol, float metallic, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                Undo.RecordObject(mat, "Fix Mat Properties");
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseCol);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseCol);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
                EditorUtility.SetDirty(mat);
            }
        }

        static void FixLeaf(string path, Color tint, float smoothness)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                Undo.RecordObject(mat, "Fix Leaf Properties");
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
                EditorUtility.SetDirty(mat);
            }
        }
    }
}
