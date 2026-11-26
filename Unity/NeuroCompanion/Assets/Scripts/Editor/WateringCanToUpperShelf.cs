using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanToUpperShelf
    {
        static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Move Can To Upper Shelf")]
        public static void Run()
        {
            // Activate garden
            GameObject gardenRoot = null; bool wasActive = false;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "[GARDEN_GAME]" && o.scene.IsValid()) { gardenRoot = o; break; }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            // Find objects
            GameObject can = null, station = null, tabletop = null, upperHutch = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (o.name == "Watering_Can" && o.scene.IsValid()) can = o;
                if (o.name == "Gardening_Planting_Station" && o.scene.IsValid()) station = o;
                if (o.name == "Work_Tabletop" && o.scene.IsValid()) tabletop = o;
                if (o.name == "Upper_Hutch_Trellis" && o.scene.IsValid()) upperHutch = o;
            }

            if (can == null || station == null) { Debug.LogError("[UpperShelf] Objects not found"); return; }

            // Log current state
            Debug.Log($"[UpperShelf] Station world pos: {station.transform.position}");
            if (tabletop != null) Debug.Log($"[UpperShelf] Tabletop world pos: {tabletop.transform.position}");
            if (upperHutch != null) Debug.Log($"[UpperShelf] UpperHutch world pos: {upperHutch.transform.position}");

            // The Work_Tabletop surface is roughly at Y=0.75 above the station base.
            // The upper hutch/trellis shelf sits above the tabletop at roughly Y=1.1 to 1.2.
            // We place the watering can on the tabletop surface (the main working surface = "above shelf"),
            // to the LEFT side so the trowel (which is in center) is not blocked.
            // Local position relative to station: tabletop surface is at localY ~0.78, 
            // left side X ~ -0.25, slight Z offset.

            Undo.RecordObject(can.transform, "Move Can To Upper Shelf");

            // Place on the work tabletop — left side, clear of the trowel
            // localY = 0.78 (tabletop surface height), X = -0.22 (left), Z = -0.05 (slightly back)
            can.transform.localPosition = new Vector3(-0.22f, 0.78f, -0.05f);
            can.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);

            Debug.Log($"[UpperShelf] Can placed: local={can.transform.localPosition} world={can.transform.position}");

            // Save
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Vector3 sw = station.transform.position;

            // Screenshots
            // A: Front view of tabletop showing can + trowel + pots on shelf below
            Shoot(OutDir + @"\wcan_upper_a_front.png",
                pos: sw + new Vector3(0f, 1.1f, -1.8f),
                target: sw + new Vector3(-0.1f, 0.85f, 0f));

            // B: Side-angle beauty shot
            Shoot(OutDir + @"\wcan_upper_b_side.png",
                pos: sw + new Vector3(-1.3f, 1.0f, -1.0f),
                target: sw + new Vector3(-0.1f, 0.85f, 0f));

            // C: Close-up of tabletop surface showing can on left, trowel in middle
            Shoot(OutDir + @"\wcan_upper_c_closeup.png",
                pos: sw + new Vector3(-0.2f, 1.3f, -0.9f),
                target: sw + new Vector3(-0.05f, 0.78f, 0.05f));

            if (gardenRoot != null) gardenRoot.SetActive(wasActive);
            Debug.Log("[UpperShelf] Done.");
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
            Debug.Log($"[UpperShelf] Shot saved: {path} ({b.Length}b)");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
