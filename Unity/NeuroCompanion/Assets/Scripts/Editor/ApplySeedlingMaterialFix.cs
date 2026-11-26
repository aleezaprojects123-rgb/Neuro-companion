using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class ApplySeedlingMaterialFix
    {
        static string ArtifactDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Apply Seedling Material Fix")]
        public static void ApplyFix()
        {
            string[] leafMatPaths = new string[]
            {
                "Assets/Materials/Environment/Seedlings/SeedlingLeaf_Bright.mat",
                "Assets/Materials/Environment/Seedlings/SeedlingLeaf_Dark.mat",
                "Assets/Materials/Environment/Seedlings/SeedlingLeaf_Yellow.mat",
                "Assets/Materials/Environment/Seedlings/SeedlingLeaf_Pale.mat",
                "Assets/Materials/Environment/Seedlings/SeedlingSystem_Stem.mat",
                "Assets/Materials/Environment/Seedlings/SeedlingSoil.mat"
            };

            foreach (var path in leafMatPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    Undo.RecordObject(mat, "Fix Seedling Specular and Smoothness");
                    
                    // Turn OFF specular highlights and reflections
                    if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
                    if (mat.HasProperty("_EnvironmentReflections")) mat.SetFloat("_EnvironmentReflections", 0f);
                    if (mat.HasProperty("_GlossyReflections")) mat.SetFloat("_GlossyReflections", 0f);
                    if (mat.HasProperty("_BlendModePreserveSpecular")) mat.SetFloat("_BlendModePreserveSpecular", 0f);
                    
                    // Reduce smoothness to zero / matte
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
                    if (mat.HasProperty("_GlossMapScale")) mat.SetFloat("_GlossMapScale", 0f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

                    // Ensure emission is disabled
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");

                    EditorUtility.SetDirty(mat);
                    Debug.Log($"[ApplySeedlingMaterialFix] Fixed material: {path}");
                }
                else
                {
                    Debug.LogWarning($"[ApplySeedlingMaterialFix] Material not found at: {path}");
                }
            }

            // Also update any live instances in the scene (active in Play Mode or Edit Mode)
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station != null)
            {
                var renderers = station.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var r in renderers)
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (m.name.Contains("SeedlingLeaf") || m.name.Contains("Seedling"))
                        {
                            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
                            if (m.HasProperty("_EnvironmentReflections")) m.SetFloat("_EnvironmentReflections", 0f);
                            if (m.HasProperty("_GlossyReflections")) m.SetFloat("_GlossyReflections", 0f);
                            if (m.HasProperty("_BlendModePreserveSpecular")) m.SetFloat("_BlendModePreserveSpecular", 0f);
                            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
                            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
                            if (m.HasProperty("_GlossMapScale")) m.SetFloat("_GlossMapScale", 0f);
                            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
                            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                            m.DisableKeyword("_EMISSION");
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            Debug.Log("[ApplySeedlingMaterialFix] All seedling materials updated to matte (no specular, 0 smoothness).");

            // Now capture verification images
            CaptureVerificationShots();
        }

        static void CaptureVerificationShots()
        {
            // Entrance view (~32m away)
            Vector3 entrancePos = new Vector3(2.6f, 1.32f, -28.5f);
            Quaternion entranceRot = Quaternion.Euler(-6.5f, 45f, 0f);
            CaptureCamera(Path.Combine(ArtifactDir, "seedlings_fixed_entrance_shot.png"), entrancePos, entranceRot, 60f);

            // Close-up view of planting station
            Vector3 closePos = new Vector3(9.2f, 1.35f, 1.8f);
            Quaternion closeRot = Quaternion.Euler(18f, 15f, 0f);
            CaptureCamera(Path.Combine(ArtifactDir, "seedlings_fixed_closeup_shot.png"), closePos, closeRot, 45f);

            Debug.Log("[ApplySeedlingMaterialFix] Verification screenshots captured successfully.");
        }

        static void CaptureCamera(string path, Vector3 pos, Quaternion rot, float fov)
        {
            int W = 1280, H = 720;
            var go = new GameObject("_VerifyCamMatFix");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = rot;
            cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            var camData = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }
}
