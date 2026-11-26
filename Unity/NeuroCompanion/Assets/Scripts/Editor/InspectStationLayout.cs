using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class InspectStationLayout
    {
        [MenuItem("NeuroCompanion/Inspect Station Layout")]
        public static void Run()
        {
            GameObject gardenRoot = null;
            bool wasActive = false;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (o.name == "[GARDEN_GAME]" && o.scene.IsValid())
                {
                    gardenRoot = o;
                    break;
                }
            }

            if (gardenRoot != null)
            {
                wasActive = gardenRoot.activeSelf;
                gardenRoot.SetActive(true);
            }

            GameObject station = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (o.name == "Gardening_Planting_Station" && o.scene.IsValid())
                {
                    station = o;
                    break;
                }
            }

            if (station == null)
            {
                File.WriteAllText("Assets/station_layout.txt", "Gardening_Planting_Station not found!");
                if (gardenRoot != null) gardenRoot.SetActive(wasActive);
                return;
            }

            string report = "STATION: " + station.name + " WorldPos: " + station.transform.position.ToString("F3") + "\n";
            report += "Children of station:\n";
            PrintHierarchy(station.transform, "", ref report);

            File.WriteAllText("Assets/station_layout.txt", report);
            Debug.Log("[InspectStationLayout] Saved to Assets/station_layout.txt");

            if (gardenRoot != null) gardenRoot.SetActive(wasActive);
        }

        static void PrintHierarchy(Transform t, string indent, ref string report)
        {
            foreach (Transform child in t)
            {
                report += $"{indent}- {child.name} [LocalPos: {child.localPosition.ToString("F3")}, LocalRot: {child.localEulerAngles.ToString("F1")}, LocalScale: {child.localScale.ToString("F3")}, WorldPos: {child.position.ToString("F3")}]\n";
                if (child.childCount > 0 && child.name != "Structure_Frame")
                {
                    PrintHierarchy(child, indent + "  ", ref report);
                }
            }
        }
    }
}
