using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestPropIndividual
    {
        [MenuItem("NeuroCompanion/Test Can Only")]
        public static void TestCan()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;
            SetProp(station, "Watering_Can", true);
            SetProp(station, "Plant_Seedlings", false);
            SetProp(station, "Gardening_Trowel", false);
            SetProp(station, "Plant_Pots", false);
            Debug.Log("Only Watering_Can is active.");
        }

        [MenuItem("NeuroCompanion/Test Seedlings Only")]
        public static void TestSeedlings()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;
            SetProp(station, "Watering_Can", false);
            SetProp(station, "Plant_Seedlings", true);
            SetProp(station, "Gardening_Trowel", false);
            SetProp(station, "Plant_Pots", false);
            Debug.Log("Only Plant_Seedlings is active.");
        }

        [MenuItem("NeuroCompanion/Test Trowel Only")]
        public static void TestTrowel()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;
            SetProp(station, "Watering_Can", false);
            SetProp(station, "Plant_Seedlings", false);
            SetProp(station, "Gardening_Trowel", true);
            SetProp(station, "Plant_Pots", false);
            Debug.Log("Only Gardening_Trowel is active.");
        }

        [MenuItem("NeuroCompanion/Test Pots Only")]
        public static void TestPots()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null) return;
            SetProp(station, "Watering_Can", false);
            SetProp(station, "Plant_Seedlings", false);
            SetProp(station, "Gardening_Trowel", false);
            SetProp(station, "Plant_Pots", true);
            Debug.Log("Only Plant_Pots is active.");
        }

        private static void SetProp(GameObject station, string name, bool active)
        {
            var t = station.transform.Find(name);
            if (t != null) t.gameObject.SetActive(active);
        }
    }
}
