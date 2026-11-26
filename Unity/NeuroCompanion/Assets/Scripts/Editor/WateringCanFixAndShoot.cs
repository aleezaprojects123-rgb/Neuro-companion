using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Reflection;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanFixAndShoot
    {
        static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Fix Can And Shoot")]
        public static void Run()
        {
            // ---- 1. Activate garden root ----
            GameObject gardenRoot = null;
            bool wasActive = false;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "[GARDEN_GAME]" && o.scene.IsValid()) { gardenRoot = o; break; }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            // ---- 2. Find objects ----
            GameObject can = null, station = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (o.name == "Watering_Can" && o.scene.IsValid()) can = o;
                if (o.name == "Gardening_Planting_Station" && o.scene.IsValid()) station = o;
            }

            if (can == null || station == null)
            {
                Debug.LogError("[FixShoot] Objects not found"); return;
            }

            // ---- 3. Reposition ----
            Undo.RecordObject(can.transform, "Fix Watering Can");
            // Move to right side of station on the ground, completely clear of shelf pots
            can.transform.localPosition = new Vector3(0.72f, 0.0f, 0.05f);
            can.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            Debug.Log($"[FixShoot] Can moved: localPos={can.transform.localPosition} worldPos={can.transform.position}");

            // ---- 4. Save scene ----
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            // ---- 5. Place camera at several angles and capture via ScreenCapture ----
            // We use a temporary camera we create in the scene
            Vector3 stationWorld = station.transform.position;

            CaptureWithTempCamera(OutDir + @"\wcan_fix_a_right_side.png",
                pos: stationWorld + new Vector3(1.3f, 0.55f, -0.3f),
                target: stationWorld + new Vector3(0.5f, 0.15f, 0f));

            CaptureWithTempCamera(OutDir + @"\wcan_fix_b_full_station.png",
                pos: stationWorld + new Vector3(-0.4f, 1.0f, -2.0f),
                target: stationWorld + new Vector3(0.3f, 0.4f, 0f));

            CaptureWithTempCamera(OutDir + @"\wcan_fix_c_ground_closeup.png",
                pos: stationWorld + new Vector3(1.6f, 0.35f, 0.6f),
                target: stationWorld + new Vector3(0.75f, 0.15f, 0.1f));

            // ---- 6. Restore ----
            if (gardenRoot != null) gardenRoot.SetActive(wasActive);
            Debug.Log("[FixShoot] Done.");
        }

        static void CaptureWithTempCamera(string path, Vector3 pos, Vector3 target)
        {
            int W = 1280, H = 720;
            var camGO = new GameObject("_TempCaptureCam");
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.LookAt(target);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.5f, 0.7f, 1f);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] bytes = tex.EncodeToPNG();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
            Debug.Log($"[FixShoot] Saved {path} ({bytes.Length} bytes)");

            cam.targetTexture = null;
            Object.DestroyImmediate(camGO);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
