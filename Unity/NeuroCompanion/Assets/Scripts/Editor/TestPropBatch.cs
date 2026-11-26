using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestPropBatch
    {
        [MenuItem("NeuroCompanion/Run Prop Batch Test")]
        public static void RunBatch()
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
            if (station == null) return;
            station.SetActive(true);

            string[] props = new string[] { "Watering_Can", "Plant_Seedlings", "Gardening_Trowel", "Plant_Pots" };

            // First disable all 4
            foreach (var p in props)
            {
                var t = station.transform.Find(p);
                if (t != null) t.gameObject.SetActive(false);
            }

            string report = "=== PROP BATCH TEST ===\n";

            // Now test each one individually
            foreach (var p in props)
            {
                var t = station.transform.Find(p);
                if (t != null)
                {
                    t.gameObject.SetActive(true);

                    // Check all renderers inside this prop
                    var renderers = t.GetComponentsInChildren<Renderer>(true);
                    report += $"Prop '{p}' has {renderers.Length} renderers:\n";
                    foreach (var r in renderers)
                    {
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m != null)
                            {
                                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.black);
                                float s = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0f;
                                float met = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
                                report += $"   Renderer: {r.name}, Mat: {m.name}, Shader: {m.shader.name}, Color: {c}, Smoothness: {s}, Metallic: {met}\n";
                            }
                        }
                    }

                    t.gameObject.SetActive(false);
                }
            }

            File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch\prop_batch_report.txt", report);
            Debug.Log("[TestPropBatch] Done. Wrote prop_batch_report.txt");
        }
    }
}
