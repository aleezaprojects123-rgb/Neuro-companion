using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class PatientViewShot2
    {
        [MenuItem("NeuroCompanion/Capture Patient View Exact")]
        public static void Capture()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject gardenGame = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "[GARDEN_GAME]") { gardenGame = root; break; }
            }

            bool wasActive = false;
            if (gardenGame != null)
            {
                wasActive = gardenGame.activeSelf;
                gardenGame.SetActive(true);
            }

            var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/Garden_Panoramic_Sky.mat");
            var oldSky = RenderSettings.skybox;
            if (skyMat != null) RenderSettings.skybox = skyMat;

            var go = new GameObject("_PatientCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = new Vector3(2.6f, 1.32f, -28.5f);
            cam.transform.rotation = Quaternion.Euler(-6.5f, 45f, 0f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.Skybox;

            int W = 1920, H = 1080;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            byte[] b = tex.EncodeToPNG();
            string outPath = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\patient_view_diagnostic_2.png";
            File.WriteAllBytes(outPath, b);
            Debug.Log($"[PatientViewShot2] Saved {outPath}");

            RenderSettings.skybox = oldSky;
            if (gardenGame != null) gardenGame.SetActive(wasActive);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
