using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class CheckStationMaxBrightness
    {
        [MenuItem("NeuroCompanion/Check Station Max Brightness")]
        public static void Check()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;

            string report = "=== STATION MESH RENDERERS AND MATERIALS ===\n";
            var renderers = station.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                report += $"Renderer: '{r.gameObject.name}' at pos {r.transform.position}\n";
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null)
                    {
                        Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.black);
                        float s = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0f;
                        float met = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                        report += $"   Mat: {m.name}, Shader: {m.shader.name}, Color: {c}, Smoothness: {s}, Metallic: {met}\n";
                    }
                }
            }

            File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch\station_mats_report.txt", report);
            Debug.Log("Wrote station_mats_report.txt");
        }
    }
}
