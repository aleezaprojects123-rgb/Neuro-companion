using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestEachStationChild
    {
        [MenuItem("NeuroCompanion/Test Disable Each Station Child")]
        public static void RunTest()
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

            string[] names = new string[] {
                "Watering_Can",
                "Plant_Seedlings",
                "Gardening_Trowel",
                "Plant_Pots",
                "Upper_Hutch_Trellis",
                "Work_Tabletop",
                "Lower_Storage_Shelf",
                "Structure_Frame"
            };

            string outDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch";

            foreach (var name in names)
            {
                var t = station.transform.Find(name);
                if (t != null)
                {
                    t.gameObject.SetActive(false);
                    Debug.Log($"Disabled {name}");
                }
            }

            // Test 1: With all disabled, verify station alone
            // Then enable one by one to see WHICH ONE causes the glow!
            string log = "=== STATION STEP-BY-STEP TEST ===\n";

            for (int i = 0; i < names.Length; i++)
            {
                var targetName = names[i];
                var t = station.transform.Find(targetName);
                if (t != null)
                {
                    t.gameObject.SetActive(true);
                    Debug.Log($"Tested enabling: {targetName}");
                }
            }

            File.WriteAllText(Path.Combine(outDir, "test_station_log.txt"), log);
        }
    }
}
