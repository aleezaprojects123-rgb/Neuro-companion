using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestWhichPropGlows
    {
        [MenuItem("NeuroCompanion/Test Which Prop Glows")]
        public static void TestWhich()
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

            // Disable all props
            foreach (var p in props)
            {
                var t = station.transform.Find(p);
                if (t != null) t.gameObject.SetActive(false);
            }

            // Test 1: Only Watering_Can
            SetOne(station, props, "Watering_Can");
            Debug.Log("[TestWhichPropGlows] Set to Watering_Can only.");
        }

        [MenuItem("NeuroCompanion/Set Seedlings Only")]
        public static void SetSeedlingsOnly()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;
            string[] props = new string[] { "Watering_Can", "Plant_Seedlings", "Gardening_Trowel", "Plant_Pots" };
            SetOne(station, props, "Plant_Seedlings");
            Debug.Log("[TestWhichPropGlows] Set to Plant_Seedlings only.");
        }

        [MenuItem("NeuroCompanion/Set Trowel Only")]
        public static void SetTrowelOnly()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;
            string[] props = new string[] { "Watering_Can", "Plant_Seedlings", "Gardening_Trowel", "Plant_Pots" };
            SetOne(station, props, "Gardening_Trowel");
            Debug.Log("[TestWhichPropGlows] Set to Gardening_Trowel only.");
        }

        [MenuItem("NeuroCompanion/Set Pots Only")]
        public static void SetPotsOnly()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;
            string[] props = new string[] { "Watering_Can", "Plant_Seedlings", "Gardening_Trowel", "Plant_Pots" };
            SetOne(station, props, "Plant_Pots");
            Debug.Log("[TestWhichPropGlows] Set to Plant_Pots only.");
        }

        private static void SetOne(GameObject station, string[] props, string target)
        {
            foreach (var p in props)
            {
                var t = station.transform.Find(p);
                if (t != null) t.gameObject.SetActive(p == target);
            }
        }
    }
}
