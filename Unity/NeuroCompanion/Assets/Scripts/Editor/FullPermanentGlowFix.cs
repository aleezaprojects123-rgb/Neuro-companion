using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NeuroCompanion.Backend.Editor
{
    public static class FullPermanentGlowFix
    {
        [MenuItem("NeuroCompanion/Apply Permanent Glow Fix")]
        public static void Apply()
        {
            // 1. Position Directional Light behind camera frustum so its editor gizmo icon never appears on screen
            var sun = GameObject.Find("Garden_SunLight");
            if (sun != null)
            {
                Undo.RecordObject(sun.transform, "Relocate SunLight Behind Camera");
                // Directional lights only use rotation for garden sunlight calculations.
                // Placing it high and far behind the camera keeps lighting identical while removing the editor gizmo icon from the view.
                sun.transform.position = new Vector3(0f, 150f, -150f);
                EditorUtility.SetDirty(sun);
                Debug.Log("[FullPermanentGlowFix] Moved Garden_SunLight to (0, 150, -150) behind camera.");
            }

            // 2. Clear any active selection so no selection rings or wireframes are drawn
            Selection.activeGameObject = null;

            // 3. Ensure Water_Pour_Point particle system is inactive and playOnAwake=false
            var waterObj = GameObject.Find("Water_Pour_Point");
            if (waterObj == null)
            {
                foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var found = FindInChildrenRecursive(r.transform, "Water_Pour_Point");
                    if (found != null) { waterObj = found.gameObject; break; }
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
                    main.startColor = new Color(0.75f, 0.88f, 0.98f, 0.35f);
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    EditorUtility.SetDirty(ps);
                }
                waterObj.SetActive(false);
                EditorUtility.SetDirty(waterObj);
            }

            // 4. Update water droplets material
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/WateringCan/Water_Droplets_Particle.mat");
            if (mat != null)
            {
                Undo.RecordObject(mat, "Adjust Water Particle Material");
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.75f, 0.88f, 0.98f, 0.35f));
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.75f, 0.88f, 0.98f, 0.35f));
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                EditorUtility.SetDirty(mat);
            }

            // 5. Save the active scene
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[FullPermanentGlowFix] Scene permanently saved. Both gizmo icon and water glow eliminated.");
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
