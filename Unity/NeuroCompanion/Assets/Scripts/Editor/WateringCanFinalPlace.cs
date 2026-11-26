using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanFinalPlace
    {
        static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Final Place Watering Can")]
        public static void Run()
        {
            // Activate garden
            GameObject gardenRoot = null; bool wasActive = false;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "[GARDEN_GAME]" && o.scene.IsValid()) { gardenRoot = o; break; }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            // Find objects
            GameObject can = null, station = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (o.name == "Watering_Can" && o.scene.IsValid()) can = o;
                if (o.name == "Gardening_Planting_Station" && o.scene.IsValid()) station = o;
            }
            if (can == null || station == null) { Debug.LogError("[FinalPlace] Not found"); return; }

            // --- Reposition ---
            // Place the watering can on the GROUND beside the RIGHT FRONT leg of the station
            // Snugly tucked next to it, clearly separated from shelf pots
            // Station frame is about 0.6m wide from center. Right outer leg at ~X=+0.52 local.
            // We want the can body center at X=+0.60 (just outside leg), Y=0.12 (half body height on ground), Z=+0.1 (slightly forward)
            Undo.RecordObject(can.transform, "Final Place Watering Can");
            can.transform.localPosition = new Vector3(0.60f, 0.0f, 0.15f);
            can.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);  // turned slightly inward

            Debug.Log($"[FinalPlace] Can placed: local={can.transform.localPosition} world={can.transform.position}");

            // Save
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Vector3 sw = station.transform.position;

            // --- Screenshots ---
            // A: Right-front corner view — shows can on ground beside right leg, pots on shelf
            Shoot(OutDir + @"\wcan_final_a_corner.png",
                pos: sw + new Vector3(1.1f, 0.65f, -0.9f),
                target: sw + new Vector3(0.4f, 0.2f, 0.1f));

            // B: Full station front view
            Shoot(OutDir + @"\wcan_final_b_front.png",
                pos: sw + new Vector3(0f, 0.85f, -1.9f),
                target: sw + new Vector3(0.1f, 0.3f, 0.1f));

            // C: Low ground-level view — can clearly on grass beside station
            Shoot(OutDir + @"\wcan_final_c_ground.png",
                pos: sw + new Vector3(1.2f, 0.22f, 0.5f),
                target: sw + new Vector3(0.6f, 0.18f, 0.15f));

            if (gardenRoot != null) gardenRoot.SetActive(wasActive);
            Debug.Log("[FinalPlace] Done.");
        }

        static void Shoot(string path, Vector3 pos, Vector3 target)
        {
            int W = 1280, H = 720;
            var go = new GameObject("_TC");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.LookAt(target);
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.Skybox;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            byte[] b = tex.EncodeToPNG();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, b);
            Debug.Log($"[FinalPlace] Shot: {path} ({b.Length}b)");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
