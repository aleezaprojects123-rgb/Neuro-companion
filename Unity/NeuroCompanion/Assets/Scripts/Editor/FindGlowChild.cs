using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class FindGlowChild
    {
        [MenuItem("NeuroCompanion/Find Exact Glow Child")]
        public static void FindExact()
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

            if (station == null)
            {
                Debug.LogError("No Gardening_Planting_Station found");
                return;
            }

            station.SetActive(true);

            // First: enable all children
            for (int i = 0; i < station.transform.childCount; i++)
            {
                station.transform.GetChild(i).gameObject.SetActive(true);
            }

            // Disable each child one at a time and see
            string log = "CHILDREN IN STATION:\n";
            for (int i = 0; i < station.transform.childCount; i++)
            {
                var c = station.transform.GetChild(i);
                log += $"Child {i}: {c.name}\n";
            }

            File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch\station_children.txt", log);
            Debug.Log("Station children written.");
        }
    }
}
