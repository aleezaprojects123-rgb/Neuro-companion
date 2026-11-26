using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanSizeFix
    {
        static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Fix Can Size And Water")]
        public static void Run()
        {
            // Activate garden
            GameObject gardenRoot = null; bool wasActive = false;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "[GARDEN_GAME]" && o.scene.IsValid()) { gardenRoot = o; break; }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            // Find watering can root and station
            GameObject can = null, station = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (o.name == "Watering_Can" && o.scene.IsValid()) can = o;
                if (o.name == "Gardening_Planting_Station" && o.scene.IsValid()) station = o;
            }

            if (can == null) { Debug.LogError("[SizeFix] Watering_Can not found!"); return; }

            // --- FIX 1: Scale up the watering can ---
            // Current scale is likely (1,1,1) making it ~20cm tall — too small.
            // Real garden watering can is ~30-35cm tall. Scale to 1.8x to look realistic on tabletop.
            Undo.RecordObject(can.transform, "Fix Can Size");
            can.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
            // Adjust Y position slightly up to account for bigger scale sitting on tabletop surface
            Vector3 p = can.transform.localPosition;
            can.transform.localPosition = new Vector3(p.x, 0.78f, p.z);
            Debug.Log($"[SizeFix] Scale set to 1.8. LocalPos={can.transform.localPosition}");

            // --- FIX 2: Kill all particle systems (stop water) ---
            var allPS = can.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allPS)
            {
                Undo.RecordObject(ps, "Stop Water PS");

                // Force stop and clear all particles
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = ps.main;
                main.playOnAwake = false;

                var emission = ps.emission;
                emission.enabled = false;

                Debug.Log($"[SizeFix] Stopped PS: {ps.gameObject.name}, isPlaying={ps.isPlaying}, particleCount={ps.particleCount}");
            }

            // Also find the WateringCanController and ensure IsWatering = false
            var ctrl = can.GetComponentInChildren<NeuroCompanion.Environment.WateringCanController>(true);
            if (ctrl != null)
            {
                ctrl.StopWatering();
                Debug.Log($"[SizeFix] WateringCanController.StopWatering() called. IsWatering={ctrl.IsWatering}");
            }
            else
            {
                Debug.LogWarning("[SizeFix] WateringCanController not found on can.");
            }

            // --- Save ---
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();

            Vector3 sw = station != null ? station.transform.position : can.transform.position;

            // --- Screenshots ---
            // A: Front view — full station showing correctly sized can on tabletop, no water
            Shoot(OutDir + @"\wcan_sizefix_a_front.png",
                pos: sw + new Vector3(0f, 1.1f, -1.8f),
                target: sw + new Vector3(-0.05f, 0.9f, 0f));

            // B: Close-up of tabletop — can size vs pot size comparison
            Shoot(OutDir + @"\wcan_sizefix_b_closeup.png",
                pos: sw + new Vector3(-0.1f, 1.4f, -1.0f),
                target: sw + new Vector3(-0.05f, 0.82f, 0.05f));

            // C: Side beauty shot
            Shoot(OutDir + @"\wcan_sizefix_c_side.png",
                pos: sw + new Vector3(-1.4f, 1.0f, -0.8f),
                target: sw + new Vector3(-0.1f, 0.85f, 0.05f));

            if (gardenRoot != null) gardenRoot.SetActive(wasActive);
            Debug.Log("[SizeFix] Done — can scaled up, water stopped.");
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
            Debug.Log($"[SizeFix] Shot: {path} ({b.Length}b)");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
