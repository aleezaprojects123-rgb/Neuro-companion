using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;

namespace NeuroCompanion.Backend.Editor
{
    public static class FindLightSpot
    {
        [MenuItem("NeuroCompanion/Diagnose Scene Objects And Lights")]
        public static void Diagnose()
        {
            var scene = EditorSceneManager.GetActiveScene();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== DIAGNOSE SCENE OBJECTS AND LIGHTS ===");

            // Check all lights in scene
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            sb.AppendLine($"Total Lights found: {lights.Length}");
            foreach (var l in lights)
            {
                sb.AppendLine($"Light: '{l.gameObject.name}', Type: {l.type}, Color: {l.color}, Intensity: {l.intensity}, Range: {l.range}, Pos: {l.transform.position}, Active: {l.gameObject.activeInHierarchy}");
            }

            // Check all ParticleSystems
            ParticleSystem[] particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
            sb.AppendLine($"Total ParticleSystems found: {particles.Length}");
            foreach (var p in particles)
            {
                sb.AppendLine($"ParticleSystem: '{p.gameObject.name}', Pos: {p.transform.position}, Active: {p.gameObject.activeInHierarchy}");
            }

            // Check all ReflectionProbes
            ReflectionProbe[] probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None);
            sb.AppendLine($"Total ReflectionProbes: {probes.Length}");
            foreach (var rp in probes)
            {
                sb.AppendLine($"ReflectionProbe: '{rp.gameObject.name}', Pos: {rp.transform.position}, Active: {rp.gameObject.activeInHierarchy}");
            }

            // Check objects with 'light' or 'halo' or 'flare' or 'spot' or 'glow' in name
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                string n = go.name.ToLower();
                if (n.Contains("light") || n.Contains("halo") || n.Contains("flare") || n.Contains("glow") || n.Contains("spot") || n.Contains("sun") || n.Contains("orb") || n.Contains("point"))
                {
                    sb.AppendLine($"Matched Name: '{go.name}' at {go.transform.position}, active: {go.activeInHierarchy}");
                }
            }

            // Also check objects around x in [-12, 0], y in [0, 5], z in [-25, 0]
            sb.AppendLine("=== Objects in Left Lawn Area ===");
            foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Vector3 p = mr.transform.position;
                if (p.x >= -12f && p.x <= 0f && p.z >= -25f && p.z <= 0f)
                {
                    sb.AppendLine($"MeshRenderer: '{mr.gameObject.name}' at {p}, mat: {(mr.sharedMaterial != null ? mr.sharedMaterial.name : "null")}");
                }
            }

            Debug.Log(sb.ToString());
            System.IO.File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771/scene_diagnostic_log.txt", sb.ToString());
        }
    }
}
