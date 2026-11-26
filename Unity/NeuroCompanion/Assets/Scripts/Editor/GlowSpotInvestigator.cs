using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Text;

namespace NeuroCompanion.Backend.Editor
{
    public static class GlowSpotInvestigator
    {
        private static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

        [MenuItem("NeuroCompanion/Investigate Glow Spot")]
        public static void Investigate()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== GLOW SPOT INVESTIGATION ===");

            // 1. Setup Camera exactly at GroundPos & GroundRot
            Vector3 camPos = new Vector3(2.6f, 1.32f, -28.5f);
            Quaternion camRot = Quaternion.Euler(-6.5f, 45f, 0f);

            var camGo = new GameObject("_TestCam");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = camPos;
            cam.transform.rotation = camRot;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.Skybox;

            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;

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

            File.WriteAllBytes(Path.Combine(OutDir, "investigate_urp_postproc.png"), tex.EncodeToPNG());
            sb.AppendLine("Rendered investigate_urp_postproc.png with PostProcessing = TRUE");

            // 2. Raycast from normalized screen pos: nx = 0.189, ny = 0.610 (from top, so y = 1 - 0.610 = 0.390 from bottom)
            Vector3 screenPoint = new Vector3(0.189f * W, (1f - 0.610f) * H, 0f);
            Ray ray = cam.ScreenPointToRay(screenPoint);
            sb.AppendLine($"Spot Screen Point: {screenPoint}");
            sb.AppendLine($"Spot Ray: origin={ray.origin}, dir={ray.direction}");

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 500f))
            {
                sb.AppendLine($"RAYCAST HIT: '{hit.collider.gameObject.name}', point={hit.point}, distance={hit.distance}");
                var root = hit.collider.transform;
                while (root.parent != null) root = root.parent;
                sb.AppendLine($"Hit Root: '{root.name}'");
            }
            else
            {
                sb.AppendLine("Physics.Raycast did not hit any collider.");
            }

            // RaycastAll to find all colliders along this line
            RaycastHit[] allHits = Physics.RaycastAll(ray, 500f);
            sb.AppendLine($"RaycastAll hit {allHits.Length} objects:");
            foreach (var h in allHits)
            {
                sb.AppendLine($"  - '{h.collider.gameObject.name}' at {h.point}, dist={h.distance}");
            }

            // Check distance to all Renderers in scene
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            sb.AppendLine($"\nChecking closest renderers to ray line:");
            System.Collections.Generic.List<(float dist, float depth, string name, Vector3 pos)> closeList = new();
            foreach (var r in renderers)
            {
                Vector3 p = r.bounds.center;
                Vector3 toP = p - ray.origin;
                float depth = Vector3.Dot(toP, ray.direction);
                if (depth < 0.5f) continue;
                Vector3 proj = ray.origin + ray.direction * depth;
                float perpDist = Vector3.Distance(p, proj);
                if (perpDist < 2.5f)
                {
                    closeList.Add((perpDist, depth, r.gameObject.name, p));
                }
            }
            closeList.Sort((a, b) => a.dist.CompareTo(b.dist));
            foreach (var item in closeList.GetRange(0, Mathf.Min(20, closeList.Count)))
            {
                sb.AppendLine($"  PerpDist: {item.dist:F2}m, Depth: {item.depth:F2}m -> '{item.name}' at {item.pos}");
            }

            // Check Light components
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            sb.AppendLine($"\nLights in scene ({lights.Length}):");
            foreach (var l in lights)
            {
                Vector3 toP = l.transform.position - ray.origin;
                float depth = Vector3.Dot(toP, ray.direction);
                Vector3 proj = ray.origin + ray.direction * depth;
                float perpDist = Vector3.Distance(l.transform.position, proj);
                sb.AppendLine($"  Light '{l.gameObject.name}': Type={l.type}, Pos={l.transform.position}, Active={l.gameObject.activeInHierarchy}, Depth={depth:F2}m, PerpDist={perpDist:F2}m, Intensity={l.intensity}");
            }

            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            string logPath = Path.Combine(OutDir, "investigation_report.txt");
            File.WriteAllText(logPath, sb.ToString());
            Debug.Log(sb.ToString());
        }
    }
}
