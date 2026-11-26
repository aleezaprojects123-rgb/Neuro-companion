using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class Phase45SeedlingScaleFix
    {
        static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Fix Phase 4.5 Seedling Scale")]
        public static void Run()
        {
            // Activate garden
            GameObject gardenRoot = null; bool wasActive = false;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "[GARDEN_GAME]" && o.scene.IsValid()) { gardenRoot = o; break; }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            // Find Plant_Seedlings parent
            GameObject seedlingParent = null;
            GameObject station = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (o.name == "Plant_Seedlings" && o.scene.IsValid()) seedlingParent = o;
                if (o.name == "Gardening_Planting_Station" && o.scene.IsValid()) station = o;
            }

            if (seedlingParent == null) { Debug.LogError("[ScaleFix] Plant_Seedlings not found!"); return; }
            if (station == null) { Debug.LogError("[ScaleFix] Station not found!"); return; }

            // Scale up all seedlings by 4x (they were ~3-8cm, need ~12-18cm for visibility)
            // Also reposition them spread more visibly on the tabletop
            // Tabletop local Y = 0.78, seedlings span X 0.05 to 0.40
            // After 4x scale the leaf meshes (~3-8cm each) become 12-32cm - good seedling size

            Vector3[] positions = new Vector3[]
            {
                new Vector3(-0.30f, 0.78f,  0.00f), // Seedling_01 - left side
                new Vector3(-0.18f, 0.78f,  0.02f), // Seedling_02
                new Vector3(-0.08f, 0.78f, -0.02f), // Seedling_03
                new Vector3( 0.05f, 0.78f,  0.00f), // Seedling_04
                new Vector3(-0.25f, 0.78f,  0.08f), // Seedling_05 - back row
                new Vector3(-0.10f, 0.78f,  0.09f), // Seedling_06
            };

            int idx = 0;
            foreach (Transform child in seedlingParent.transform)
            {
                Undo.RecordObject(child, "Scale Seedling");
                // Scale up 4x
                child.localScale = new Vector3(4f, 4f, 4f);
                // Reposition to spread across tabletop left-side (can is at X=-0.22, so place seedlings at X -0.35 to 0.0)
                if (idx < positions.Length)
                    child.localPosition = positions[idx];
                idx++;
                Debug.Log($"[ScaleFix] {child.name}: scale=4, localPos={child.localPosition}");
            }

            // Save
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Vector3 sw = station.transform.position;

            // A: Full station front - seedlings clearly visible on tabletop left, can right
            Shoot(OutDir + @"\phase45_fix_a_full.png",
                pos: sw + new Vector3(0f, 1.1f, -1.9f),
                target: sw + new Vector3(-0.05f, 0.85f, 0.03f));

            // B: Close-up seedlings from front-left
            Shoot(OutDir + @"\phase45_fix_b_closeup.png",
                pos: sw + new Vector3(-0.6f, 1.3f, -0.9f),
                target: sw + new Vector3(-0.18f, 0.84f, 0.02f));

            // C: Tabletop angle showing variety
            Shoot(OutDir + @"\phase45_fix_c_tabletop.png",
                pos: sw + new Vector3(-0.1f, 1.5f, -0.6f),
                target: sw + new Vector3(-0.12f, 0.80f, 0.03f));

            // D: Patient view - full garden with station
            Shoot(OutDir + @"\phase45_fix_d_patient.png",
                pos: sw + new Vector3(-0.5f, 1.6f, -2.6f),
                target: sw + new Vector3(0f, 0.82f, 0.03f));

            if (gardenRoot != null) gardenRoot.SetActive(wasActive);
            Debug.Log("[ScaleFix] Done - seedlings scaled up 4x and repositioned.");
        }

        static void Shoot(string path, Vector3 pos, Vector3 target)
        {
            int W = 1280, H = 720;
            var go = new GameObject("_TC");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.LookAt(target);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.Skybox;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0,0,W,H),0,0); tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null;

            byte[] b = tex.EncodeToPNG();
            File.WriteAllBytes(path, b);
            Debug.Log($"[ScaleFix] Shot: {path} ({b.Length}b)");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
