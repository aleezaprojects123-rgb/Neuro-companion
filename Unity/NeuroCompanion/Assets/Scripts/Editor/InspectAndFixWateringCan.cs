using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class InspectAndFixWateringCan
    {
        [MenuItem("NeuroCompanion/Inspect Watering Can")]
        public static void Inspect()
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

            Transform can = station.Find("Watering_Can");
            if (can == null) { Debug.LogError("No Watering_Can under station"); return; }

            string log = $"WATERING CAN: pos={can.localPosition}, rot={can.localEulerAngles}, scale={can.localScale}\n";
            foreach (Transform c in can)
            {
                log += $"  - {c.name}: localPos={c.localPosition}, localRot={c.localEulerAngles}, localScale={c.localScale}, worldPos={c.position}\n";
                foreach (Transform cc in c)
                {
                    log += $"      * {cc.name}: localPos={cc.localPosition}, localRot={cc.localEulerAngles}, localScale={cc.localScale}, worldPos={cc.position}\n";
                }
            }

            // Also check for any loose WateringCan objects under station or in scene
            foreach (Transform t in station)
            {
                if (t.name.ToLower().Contains("water") || t.name.ToLower().Contains("rose") || t.name.ToLower().Contains("spout"))
                {
                    log += $"[Station direct child matching water/rose/spout]: {t.name} at {t.localPosition}\n";
                }
            }

            File.WriteAllText("Assets/watering_can_debug.txt", log);
            Debug.Log("[InspectAndFixWateringCan] Written to Assets/watering_can_debug.txt");
        }
    }
}
