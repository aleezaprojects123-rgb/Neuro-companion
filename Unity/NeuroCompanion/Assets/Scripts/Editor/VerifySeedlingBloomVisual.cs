using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class VerifySeedlingBloomVisual
    {
        static string ArtifactDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Capture Visual Verifications")]
        public static void CaptureAll()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null)
            {
                foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var t = r.transform.Find("Gardening_Planting_Station");
                    if (t != null) { station = t.gameObject; break; }
                }
            }

            // Ensure everything is active
            if (station != null)
            {
                station.SetActive(true);
                for (int i = 0; i < station.transform.childCount; i++)
                    station.transform.GetChild(i).gameObject.SetActive(true);

                var seedlings = station.transform.Find("Plant_Seedlings");
                if (seedlings != null)
                {
                    seedlings.gameObject.SetActive(true);
                    for (int i = 0; i < seedlings.childCount; i++)
                        seedlings.GetChild(i).gameObject.SetActive(true);
                }
            }

            // Shot 1: Entrance View (~32m away)
            Vector3 entrancePos = new Vector3(2.6f, 1.32f, -28.5f);
            Quaternion entranceRot = Quaternion.Euler(-6.5f, 45f, 0f);
            CaptureCamera(Path.Combine(ArtifactDir, "seedlings_bloom_fixed_entrance.png"), entrancePos, entranceRot, 60f);

            // Shot 2: Close-up of planting station and seedlings
            Vector3 closePos = new Vector3(9.2f, 1.35f, 1.8f);
            Quaternion closeRot = Quaternion.Euler(18f, 15f, 0f);
            CaptureCamera(Path.Combine(ArtifactDir, "seedlings_closeup_inspection.png"), closePos, closeRot, 45f);

            Debug.Log("[VerifySeedlingBloomVisual] Both verification screenshots captured.");
        }

        static void CaptureCamera(string path, Vector3 pos, Quaternion rot, float fov)
        {
            int W = 1280, H = 720;
            var go = new GameObject("_VerifyCam");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = rot;
            cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;

            var camData = go.AddComponent<UniversalAdditionalCameraData>();
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
