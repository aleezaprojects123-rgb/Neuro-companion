using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class InspectUpperHutch
    {
        [MenuItem("NeuroCompanion/Inspect Upper Hutch")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            GameObject gardenGame = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "[GARDEN_GAME]") { gardenGame = root; break; }
            }
            if (gardenGame == null) return;
            Transform station = gardenGame.transform.Find("Gardening_Planting_Station");
            if (station == null) return;

            string log = "STATION CHILDREN:\n";
            foreach (Transform c in station)
            {
                var r = c.GetComponent<Renderer>();
                log += $"{c.name}: localPos={c.localPosition}, bounds={(r != null ? r.bounds.ToString() : "none")}\n";
                foreach (Transform sub in c)
                {
                    var sr = sub.GetComponent<Renderer>();
                    log += $"  - {sub.name}: localPos={sub.localPosition}, bounds={(sr != null ? sr.bounds.ToString() : "none")}\n";
                }
            }
            File.WriteAllText("Assets/station_hutch_debug.txt", log);
            Debug.Log("[InspectUpperHutch] Done");
        }
    }
}
