using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanVerifyShot
    {
        [MenuItem("NeuroCompanion/Verify Watering Can Position")]
        public static void Run()
        {
            string outDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

            // Activate garden root
            GameObject gardenRoot = null;
            bool wasActive = false;
            foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj.name == "[GARDEN_GAME]" && obj.scene.IsValid()) { gardenRoot = obj; break; }
            }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            GameObject can = null;
            GameObject station = null;
            foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj.name == "Watering_Can" && obj.scene.IsValid()) can = obj;
                if (obj.name == "Gardening_Planting_Station" && obj.scene.IsValid()) station = obj;
            }

            if (can == null || station == null)
            {
                Debug.LogError("[Verify] Cannot find objects");
                return;
            }

            Debug.Log($"[Verify] Can localPos={can.transform.localPosition} worldPos={can.transform.position}");

            var sv = SceneView.lastActiveSceneView;

            // Shot 1: Right-side close-up showing can beside station (not on shelf)
            Shoot(sv, outDir + @"\wcan_new_pos_right_side.png",
                station.transform.position + new Vector3(1.1f, 0.5f, -0.4f),
                Quaternion.Euler(15f, 230f, 0f), 1.4f);

            // Shot 2: Front 3/4 view of full station
            Shoot(sv, outDir + @"\wcan_new_pos_full_station.png",
                station.transform.position + new Vector3(-0.4f, 0.7f, -1.8f),
                Quaternion.Euler(18f, 165f, 0f), 2.5f);

            // Shot 3: Low angle ground-level showing can sits on ground
            Shoot(sv, outDir + @"\wcan_new_pos_ground_level.png",
                station.transform.position + new Vector3(1.5f, 0.3f, 0.5f),
                Quaternion.Euler(5f, 240f, 0f), 1.2f);

            if (gardenRoot != null) gardenRoot.SetActive(wasActive);

            EditorUtility.DisplayDialog("Verify Done", "3 screenshots captured to brain folder.", "OK");
        }

        static void Shoot(SceneView sv, string path, Vector3 pivot, Quaternion rot, float size)
        {
            if (sv == null) return;
            sv.LookAt(pivot, rot, size);
            sv.Repaint();
            System.Threading.Thread.Sleep(300);

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var cam = sv.camera;
            if (cam == null) return;
            var oldRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0,0,w,h),0,0);
            tex.Apply();
            cam.targetTexture = oldRT;
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log("[Verify] Saved: " + path);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
