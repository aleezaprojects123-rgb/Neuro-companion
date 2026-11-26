using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class SurveyBenchCandidate
    {
        [MenuItem("NeuroCompanion/Survey Bench Candidate")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            GameObject gardenGame = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name == "[GARDEN_GAME]") { gardenGame = r; break; }
            }
            if (gardenGame == null) return;

            Vector3 candidate = new Vector3(12.5f, 0f, -14.5f);
            string report = $"Scanning 10m around candidate {candidate.ToString("F2")}:\n";

            foreach (var renderer in gardenGame.GetComponentsInChildren<Renderer>(true))
            {
                float dist = Vector3.Distance(renderer.transform.position, candidate);
                if (dist < 10f && renderer.gameObject.name != "Garden_Terrain")
                {
                    report += $"- {renderer.gameObject.name} (parent: {renderer.transform.parent?.name}) at {renderer.transform.position.ToString("F2")}, dist={dist:F2}m\n";
                }
            }

            File.WriteAllText("Assets/bench_candidate_survey.txt", report);
            Debug.Log("[SurveyBenchCandidate] Done");
        }
    }
}
