using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class PinpointStationChild
    {
        [MenuItem("NeuroCompanion/Enable Station And Test Children")]
        public static void Test()
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

            if (station != null)
            {
                station.SetActive(true);

                // Disable children one by one to see
                for (int i = 0; i < station.transform.childCount; i++)
                {
                    var child = station.transform.GetChild(i);
                    Debug.Log($"Station Child {i}: {child.name}, active={child.gameObject.activeSelf}");
                }
            }
        }

        [MenuItem("NeuroCompanion/Disable Watering Can Only")]
        public static void DisableWateringCan()
        {
            var can = GameObject.Find("Watering_Can");
            if (can != null) can.SetActive(false);
            Debug.Log("Disabled Watering_Can");
        }

        [MenuItem("NeuroCompanion/Disable Seedlings Only")]
        public static void DisableSeedlings()
        {
            var s = GameObject.Find("Plant_Seedlings");
            if (s != null) s.SetActive(false);
            Debug.Log("Disabled Plant_Seedlings");
        }
    }
}
