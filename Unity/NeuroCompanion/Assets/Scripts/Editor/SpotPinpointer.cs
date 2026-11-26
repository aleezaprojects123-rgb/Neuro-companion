using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class SpotPinpointer
    {
        private static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch";

        [MenuItem("NeuroCompanion/Pinpoint Spot Source")]
        public static void Pinpoint()
        {
            var gardenGame = GameObject.Find("[GARDEN_GAME]");
            if (gardenGame == null)
            {
                Debug.LogError("No [GARDEN_GAME] found!");
                return;
            }

            // Test 1: Disable Garden_Birds
            var birds = gardenGame.transform.Find("Garden_Birds");
            if (birds != null) birds.gameObject.SetActive(false);
            Debug.Log("Disabled Garden_Birds");

            // Test 2: Disable Gardening_Planting_Station
            var station = gardenGame.transform.Find("Gardening_Planting_Station");
            if (station != null) station.gameObject.SetActive(false);
            Debug.Log("Disabled Gardening_Planting_Station");

            // Test 3: Disable Garden_SunLight
            var rig = GameObject.Find("Lighting_Rig");
            var sun = GameObject.Find("Garden_SunLight");
            Debug.Log($"SunLight exists: {sun != null}, Rig exists: {rig != null}");

            // List all objects in root
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Debug.Log($"Root GameObject: '{root.name}', active={root.activeSelf}");
            }
        }

        [MenuItem("NeuroCompanion/Restore All In Spot Pinpointer")]
        public static void Restore()
        {
            var gardenGame = GameObject.Find("[GARDEN_GAME]");
            if (gardenGame != null)
            {
                var birds = gardenGame.transform.Find("Garden_Birds");
                if (birds != null) birds.gameObject.SetActive(true);

                var station = gardenGame.transform.Find("Gardening_Planting_Station");
                if (station != null) station.gameObject.SetActive(true);
            }
        }
    }
}
