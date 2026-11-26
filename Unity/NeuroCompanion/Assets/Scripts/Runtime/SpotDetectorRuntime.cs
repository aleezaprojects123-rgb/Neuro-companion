using UnityEngine;
using System.IO;
using System.Text;

namespace NeuroCompanion.Backend
{
    public class SpotDetectorRuntime : MonoBehaviour
    {
        private bool captured = false;
        private float timer = 0f;

        void Update()
        {
            timer += Time.deltaTime;
            // Wait 6 seconds for touchdown flight to finish
            if (!captured && timer > 6.0f)
            {
                captured = true;
                Detect();
            }
        }

        void Detect()
        {
            var cam = Camera.main;
            if (cam == null) return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[SpotDetectorRuntime] Cam pos: {cam.transform.position}, rot: {cam.transform.eulerAngles}");

            // The spot is at normalized viewport (0.28, 0.38) [0,0 bottom-left]
            Ray ray = cam.ViewportPointToRay(new Vector3(0.28f, 0.38f, 0f));
            sb.AppendLine($"Spot Ray: origin={ray.origin}, dir={ray.direction}");

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 500f))
            {
                sb.AppendLine($"PHYSICS HIT: {hit.collider.gameObject.name} at {hit.point}, dist={hit.distance}, tag={hit.collider.tag}");
                var r = hit.collider.GetComponent<Renderer>();
                if (r != null)
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m != null)
                            sb.AppendLine($"   Mat: {m.name}, Shader: {m.shader.name}");
                    }
                }
            }
            else
            {
                sb.AppendLine("PHYSICS HIT: None");
            }

            // Also check all renderers whose screen bounding box contains (0.28, 0.38)
            var allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sb.AppendLine($"Total active renderers in Play Mode: {allRenderers.Length}");
            foreach (var r in allRenderers)
            {
                Vector3 screenPos = cam.WorldToViewportPoint(r.bounds.center);
                if (screenPos.z > 0 && Vector2.Distance(new Vector2(screenPos.x, screenPos.y), new Vector2(0.28f, 0.38f)) < 0.15f)
                {
                    sb.AppendLine($"RENDERER NEAR SPOT: {r.gameObject.name} at {r.bounds.center}, viewport=({screenPos.x:F3}, {screenPos.y:F3}, depth={screenPos.z:F1}m)");
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m != null)
                        {
                            sb.AppendLine($"   Mat: {m.name}, Shader: {m.shader.name}");
                            if (m.HasProperty("_BaseColor")) sb.AppendLine($"   _BaseColor: {m.GetColor("_BaseColor")}");
                            if (m.HasProperty("_Color")) sb.AppendLine($"   _Color: {m.GetColor("_Color")}");
                            if (m.HasProperty("_EmissionColor")) sb.AppendLine($"   _EmissionColor: {m.GetColor("_EmissionColor")}");
                        }
                    }
                }
            }

            // Check all lights
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            sb.AppendLine($"Total active lights: {lights.Length}");
            foreach (var l in lights)
            {
                Vector3 screenPos = cam.WorldToViewportPoint(l.transform.position);
                sb.AppendLine($"LIGHT: {l.gameObject.name}, type={l.type}, pos={l.transform.position}, color={l.color}, intensity={l.intensity}, viewport=({screenPos.x:F3}, {screenPos.y:F3}, z={screenPos.z:F1}m)");
            }

            File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch\runtime_detect.txt", sb.ToString());
            Debug.Log("[SpotDetectorRuntime] Detection report written to runtime_detect.txt");
        }
    }
}
