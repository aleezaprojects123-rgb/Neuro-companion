using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class ScreenProjectFinder
    {
        [MenuItem("NeuroCompanion/Find Object At Spot")]
        public static void FindObject()
        {
            var cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("No camera found!");
                return;
            }

            Vector2 spotNorm = new Vector2(365f / 1280f, 1f - (240f / 720f)); // Screen normalized (0..1, bottom-left=0,0)
            Debug.Log($"Camera: {cam.name}, pos={cam.transform.position}, rot={cam.transform.eulerAngles}, spotNorm={spotNorm}");

            Ray ray = cam.ViewportPointToRay(new Vector3(spotNorm.x, spotNorm.y, 0f));
            Debug.Log($"Ray origin: {ray.origin}, dir: {ray.direction}");

            // Check every single active renderer in the scene
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            string report = $"Found {renderers.Length} active renderers\n";

            float minPerpDist = 999f;
            Renderer closestR = null;

            foreach (var r in renderers)
            {
                Bounds b = r.bounds;
                // Distance from ray to center of bounds
                Vector3 toCenter = b.center - ray.origin;
                float proj = Vector3.Dot(toCenter, ray.direction);
                if (proj > 0)
                {
                    Vector3 closestPtOnRay = ray.origin + ray.direction * proj;
                    float dist = Vector3.Distance(closestPtOnRay, b.center);
                    if (dist < 1.0f) // within 1 meter of ray
                    {
                        report += $"NEAR RAY: {r.gameObject.name} | Depth: {proj:F2}m | PerpDist: {dist:F2}m | BoundsCenter: {b.center} | Path: {GetPath(r.transform)}\n";
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m != null)
                                report += $"   Material: {m.name}, Shader: {m.shader.name}, Color: {(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : (m.HasProperty("_Color") ? m.GetColor("_Color").ToString() : "none"))}\n";
                        }
                    }
                }
            }

            File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch\spot_near_objects.txt", report);
            Debug.Log("Wrote spot_near_objects.txt");
        }

        private static string GetPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                p = t.parent != null ? t.name + "/" + p : p;
            }
            return p;
        }
    }
}
