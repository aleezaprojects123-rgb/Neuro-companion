using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestStationPieces
    {
        [MenuItem("NeuroCompanion/Capture Pieces Test")]
        public static void CapturePieces()
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

            // Test 1: Disable Watering_Can
            var can = station.transform.Find("Watering_Can");
            if (can != null) can.gameObject.SetActive(false);

            // Test 2: Disable Plant_Seedlings
            var seed = station.transform.Find("Plant_Seedlings");
            if (seed != null) seed.gameObject.SetActive(false);

            // Test 3: Disable Gardening_Trowel
            var trowel = station.transform.Find("Gardening_Trowel");
            if (trowel != null) trowel.gameObject.SetActive(false);

            // Test 4: Disable Plant_Pots
            var pots = station.transform.Find("Plant_Pots");
            if (pots != null) pots.gameObject.SetActive(false);

            Debug.Log("[TestStationPieces] Disabled props (Can, Seedlings, Trowel, Pots). Frame and table left.");
        }
    }
}
