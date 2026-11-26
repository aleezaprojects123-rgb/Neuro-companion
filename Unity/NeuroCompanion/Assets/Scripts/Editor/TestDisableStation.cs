using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestDisableStation
    {
        [MenuItem("NeuroCompanion/Test Disable Station Only")]
        public static void DisableStation()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station != null)
            {
                station.SetActive(false);
                Debug.Log("[TestDisableStation] Gardening_Planting_Station disabled!");
            }
            else
            {
                Debug.LogError("[TestDisableStation] Gardening_Planting_Station NOT FOUND!");
            }
        }

        [MenuItem("NeuroCompanion/Test Enable Station Only")]
        public static void EnableStation()
        {
            // Find inactive
            foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var t = r.transform.Find("Gardening_Planting_Station");
                if (t != null)
                {
                    t.gameObject.SetActive(true);
                    Debug.Log("[TestDisableStation] Gardening_Planting_Station enabled!");
                }
            }
        }
    }
}
