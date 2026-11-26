using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanFix
    {
        [MenuItem("NeuroCompanion/Fix Watering Can Position")]
        public static void FixPosition()
        {
            string outDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

            // --- 1. Activate garden root ---
            GameObject gardenRoot = null;
            bool wasActive = false;
            foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
                if (obj.name == "[GARDEN_GAME]" && obj.scene.IsValid()) { gardenRoot = obj; break; }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            // --- 2. Find objects ---
            GameObject can = null;
            GameObject station = null;
            GameObject lowerShelf = null;
            foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj.name == "Watering_Can" && obj.scene.IsValid()) can = obj;
                if (obj.name == "Gardening_Planting_Station" && obj.scene.IsValid()) station = obj;
                if (obj.name == "Lower_Storage_Shelf" && obj.scene.IsValid()) lowerShelf = obj;
            }

            if (can == null) { Debug.LogError("[Fix] Watering_Can not found!"); return; }
            if (station == null) { Debug.LogError("[Fix] Station not found!"); return; }

            Debug.Log($"[Fix] Before - Can parent={can.transform.parent?.name}, localPos={can.transform.localPosition}");

            // --- 3. Reposition ---
            // The can should sit on the GROUND to the RIGHT of the station, 
            // separate from the shelf pots. Keep same parent (station), 
            // move to right side at ground level (Y=0 local since station sits at ground).
            Undo.RecordObject(can.transform, "Fix Watering Can Pos");
            // X=+0.72 puts it right of the station frame, Y=0 = ground level, Z=0.05 slightly forward
            can.transform.localPosition = new Vector3(0.72f, 0.0f, 0.05f);
            can.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);

            Debug.Log($"[Fix] After  - Can parent={can.transform.parent?.name}, localPos={can.transform.localPosition}, worldPos={can.transform.position}");

            // --- 4. Mark dirty and save ---
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Fix] Scene saved.");

            // --- 5. Screenshots via RenderTexture on scene camera ---
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                // Force open a scene view
                SceneView.FocusWindowIfItsOpen<SceneView>();
                sv = SceneView.lastActiveSceneView;
            }

            Vector3 stationWorld = station.transform.position;

            // Shot A: Right-side view — shows can standing on ground beside station, pots visible on shelf
            TakeShot(sv, outDir + @"\watering_can_fixed_right_side.png",
                pivot: stationWorld + new Vector3(0.5f, 0.4f, 0f),
                rot: Quaternion.Euler(18f, 240f, 0f), size: 1.6f);

            // Shot B: Front 3/4 — full station with can at ground right
            TakeShot(sv, outDir + @"\watering_can_fixed_full_station.png",
                pivot: stationWorld + new Vector3(0.3f, 0.5f, 0f),
                rot: Quaternion.Euler(20f, 200f, 0f), size: 2.4f);

            // Shot C: Low ground-level close-up of can
            TakeShot(sv, outDir + @"\watering_can_fixed_ground_closeup.png",
                pivot: stationWorld + new Vector3(0.75f, 0.25f, 0.1f),
                rot: Quaternion.Euler(8f, 250f, 0f), size: 0.9f);

            // --- 6. Restore ---
            if (gardenRoot != null) gardenRoot.SetActive(wasActive);

            Debug.Log("[Fix] All done! Watering can repositioned to right of station at ground level.");
        }

        static void TakeShot(SceneView sv, string path, Vector3 pivot, Quaternion rot, float size)
        {
            if (sv == null) return;
            sv.LookAt(pivot, rot, size);
            sv.Repaint();
            System.Threading.Thread.Sleep(500);

            int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            Camera cam = sv.camera;
            if (cam == null) { Debug.LogWarning("[Fix] Scene cam null, skipping shot: " + path); return; }

            var prevRT = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            cam.targetTexture = prevRT;
            RenderTexture.active = null;

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
            Debug.Log("[Fix] Screenshot saved: " + path + " (" + bytes.Length + " bytes)");

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
