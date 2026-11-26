using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class FullGlowFinder
    {
        [MenuItem("NeuroCompanion/Full Glow Finder")]
        public static void FindAll()
        {
            using (var sw = new StreamWriter("D:/glow_objects.txt"))
            {
                // 1. All Renderers
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    bool suspicious = false;
                    string reason = "";
                    foreach (var mat in r.sharedMaterials)
                    {
                        if (mat == null) continue;
                        if (mat.shader != null && (mat.shader.name.Contains("Unlit") || mat.shader.name.Contains("Particles") || mat.shader.name.Contains("Flare")))
                        {
                            suspicious = true;
                            reason += $"Shader({mat.shader.name}) ";
                        }
                        if (mat.HasProperty("_EmissionColor"))
                        {
                            Color c = mat.GetColor("_EmissionColor");
                            if (c.r > 0.5f || c.g > 0.5f || c.b > 0.5f)
                            {
                                suspicious = true;
                                reason += $"Emission({c}) ";
                            }
                        }
                    }
                    if (r.gameObject.name.ToLower().Contains("light") || r.gameObject.name.ToLower().Contains("glow") || r.gameObject.name.ToLower().Contains("flare") || r.gameObject.name.ToLower().Contains("spot") || r.gameObject.name.ToLower().Contains("sun") || r.gameObject.name.ToLower().Contains("orb"))
                    {
                        suspicious = true;
                        reason += "NameMatch ";
                    }

                    if (suspicious)
                    {
                        sw.WriteLine($"RENDERER: {r.gameObject.name} | Pos: {r.transform.position} | Active: {r.gameObject.activeInHierarchy} | Reason: {reason} | Path: {GetPath(r.transform)}");
                    }
                }

                // 2. All Lights
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    sw.WriteLine($"LIGHT: {l.gameObject.name} | Type: {l.type} | Pos: {l.transform.position} | Color: {l.color} | Intensity: {l.intensity} | Path: {GetPath(l.transform)}");
                }

                // 3. Skybox
                if (RenderSettings.skybox != null)
                {
                    sw.WriteLine($"SKYBOX: {RenderSettings.skybox.name} | Shader: {RenderSettings.skybox.shader.name}");
                }
            }
            Debug.Log("Full Glow Finder written to D:/glow_objects.txt");
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
