using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class LightSpotFinder
    {
        [MenuItem("NeuroCompanion/Find Lights and Glows")]
        public static void FindLights()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"=== Total Lights in Scene: {lights.Length} ===");
            foreach (var l in lights)
            {
                Debug.Log($"LIGHT: '{l.gameObject.name}' | Type: {l.type} | Pos: {l.transform.position} | Active: {l.gameObject.activeInHierarchy} | Intensity: {l.intensity} | Path: {GetPath(l.transform)}");
            }

            var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"=== Total Particle Systems: {particles.Length} ===");
            foreach (var p in particles)
            {
                Debug.Log($"PARTICLE: '{p.gameObject.name}' | Pos: {p.transform.position} | Active: {p.gameObject.activeInHierarchy} | Path: {GetPath(p.transform)}");
            }

            // Look for any object near (x=5..15, y=0..4, z=-25..0) that has emission or mesh
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in renderers)
            {
                Vector3 p = r.transform.position;
                if (p.x >= 2f && p.x <= 15f && p.z >= -25f && p.z <= -5f)
                {
                    if (r.sharedMaterial != null && (r.sharedMaterial.IsKeywordEnabled("_EMISSION") || r.sharedMaterial.name.ToLower().Contains("glow") || r.sharedMaterial.name.ToLower().Contains("light") || r.name.ToLower().Contains("light") || r.name.ToLower().Contains("glow") || r.name.ToLower().Contains("flare") || r.name.ToLower().Contains("spot") || r.name.ToLower().Contains("orb")))
                    {
                        Debug.Log($"POTENTIAL GLOW MESH: '{r.gameObject.name}' | Mat: {r.sharedMaterial.name} | Pos: {p} | Path: {GetPath(r.transform)}");
                    }
                }
            }
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
