using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class InspectStationDetails
    {
        [MenuItem("NeuroCompanion/Inspect Station Details")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            GameObject gardenGame = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "[GARDEN_GAME]") { gardenGame = root; break; }
            }

            if (gardenGame == null) { Debug.LogError("No [GARDEN_GAME]"); return; }

            Transform station = gardenGame.transform.Find("Gardening_Planting_Station");
            if (station == null) { Debug.LogError("No Gardening_Planting_Station"); return; }

            string log = "STATION: " + station.name + " WorldPos: " + station.position.ToString("F3") + "\n";
            foreach (Transform c in station)
            {
                log += $"- {c.name}: localPos={c.localPosition.ToString("F3")}, localRot={c.localEulerAngles.ToString("F1")}, localScale={c.localScale.ToString("F3")}\n";
                if (c.name == "Plant_Pots" || c.name == "Plant_Seedlings")
                {
                    foreach (Transform sub in c)
                    {
                        log += $"   * {sub.name}: localPos={sub.localPosition.ToString("F3")}, scale={sub.localScale.ToString("F3")}, worldPos={sub.position.ToString("F3")}\n";
                    }
                }
            }

            File.WriteAllText("Assets/station_details.txt", log);
            Debug.Log("[InspectStationDetails] Written to Assets/station_details.txt");
        }
    }
}
