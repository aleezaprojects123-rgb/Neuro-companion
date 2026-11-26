using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NeuroCompanion.Backend.Editor
{
    public static class ApplyWaterFix
    {
        [MenuItem("NeuroCompanion/Fix Water Pouring And Glow")]
        public static void Fix()
        {
            var waterObj = GameObject.Find("Water_Pour_Point");
            if (waterObj == null)
            {
                // Find inactive
                foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var found = FindInChildrenRecursive(r.transform, "Water_Pour_Point");
                    if (found != null)
                    {
                        waterObj = found.gameObject;
                        break;
                    }
                }
            }

            if (waterObj != null)
            {
                Undo.RecordObject(waterObj, "Configure Water Pour Point");
                var ps = waterObj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    Undo.RecordObject(ps, "Disable Play On Awake");
                    var main = ps.main;
                    main.playOnAwake = false;

                    // Set natural water droplet color with realistic transparency
                    main.startColor = new Color(0.75f, 0.88f, 0.98f, 0.35f);

                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    EditorUtility.SetDirty(ps);
                }

                // Deactivate by default until Carl interacts
                waterObj.SetActive(false);
                EditorUtility.SetDirty(waterObj);
                Debug.Log("[ApplyWaterFix] Water_Pour_Point set to playOnAwake=false, natural water color, and inactive.");
            }
            else
            {
                Debug.LogWarning("[ApplyWaterFix] Water_Pour_Point not found in scene.");
            }

            // Also check the particle material to ensure it is not pure glowing white
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/WateringCan/Water_Droplets_Particle.mat");
            if (mat != null)
            {
                Undo.RecordObject(mat, "Adjust Water Particle Material");
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", new Color(0.75f, 0.88f, 0.98f, 0.35f));
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", new Color(0.75f, 0.88f, 0.98f, 0.35f));
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", Color.black);
                EditorUtility.SetDirty(mat);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[ApplyWaterFix] Scene saved successfully.");
        }

        private static Transform FindInChildrenRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var result = FindInChildrenRecursive(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
