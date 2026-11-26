using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class InspectGardenForBench
    {
        [MenuItem("NeuroCompanion/Inspect Garden For Bench")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            GameObject gardenGame = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name == "[GARDEN_GAME]") { gardenGame = r; break; }
            }
            if (gardenGame == null) { Debug.LogError("No [GARDEN_GAME]"); return; }

            string log = "=== GARDEN SPATIAL SURVEY FOR BENCH PLACEMENT ===\n";

            // 1. Station
            var station = gardenGame.transform.Find("Gardening_Planting_Station");
            if (station != null) log += $"Station WorldPos: {station.position.ToString("F2")}\n";

            // 2. Flower Beds
            var beds = gardenGame.transform.Find("Garden_FlowerBeds");
            if (beds != null)
            {
                foreach (Transform b in beds)
                {
                    log += $"FlowerBed {b.name}: WorldPos={b.position.ToString("F2")}\n";
                }
            }

            // 3. Pathway
            var path = gardenGame.transform.Find("Garden_Pathway");
            if (path != null)
            {
                log += $"Pathway WorldPos: {path.position.ToString("F2")}\n";
            }

            // 4. Cherry Trees / Key Trees
            var trees = gardenGame.transform.Find("Garden_Trees");
            if (trees != null)
            {
                foreach (Transform t in trees)
                {
                    if (t.name.Contains("Cherry") || t.name.Contains("1") || t.name.Contains("2"))
                    {
                        log += $"Tree {t.name}: WorldPos={t.position.ToString("F2")}\n";
                    }
                }
            }

            // 5. Pathway points or bounds
            var pathwayMesh = path != null ? path.GetComponent<MeshFilter>() : null;
            if (pathwayMesh != null && pathwayMesh.sharedMesh != null)
            {
                var bounds = pathwayMesh.sharedMesh.bounds;
                log += $"Pathway Mesh Bounds: min={bounds.min.ToString("F2")}, max={bounds.max.ToString("F2")}, center={bounds.center.ToString("F2")}\n";
            }

            File.WriteAllText("Assets/garden_bench_survey.txt", log);
            Debug.Log("[InspectGardenForBench] Survey saved to Assets/garden_bench_survey.txt");
        }
    }
}
