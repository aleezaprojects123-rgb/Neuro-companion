using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class SurveyBirdEnvironment
    {
        [MenuItem("NeuroCompanion/Survey Bird Environment")]
        public static void Run()
        {
            var scene = SceneManager.GetActiveScene();
            GameObject gardenGame = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "[GARDEN_GAME]") { gardenGame = root; break; }
            }
            if (gardenGame == null) { Debug.LogError("No [GARDEN_GAME]"); return; }

            string log = "=== SURVEY BIRD ENVIRONMENT ===\n";

            // 1. Feeder
            Transform feeder = gardenGame.transform.Find("Bird_Feeder");
            if (feeder != null)
            {
                log += $"Bird_Feeder WorldPos: {feeder.position}, Rot: {feeder.eulerAngles}\n";
                foreach (Transform c in feeder)
                {
                    log += $"  - Feeder child: {c.name} worldPos: {c.position}\n";
                    foreach (Transform cc in c)
                    {
                        if (cc.name.ToLower().Contains("perch") || cc.name.ToLower().Contains("table") || cc.name.ToLower().Contains("roof"))
                            log += $"      * {cc.name} worldPos: {cc.position}\n";
                    }
                }
            }

            // 2. Bench
            Transform bench = gardenGame.transform.Find("Garden_Bench");
            if (bench != null)
            {
                log += $"Garden_Bench WorldPos: {bench.position}, Rot: {bench.eulerAngles}\n";
            }

            // 3. Trees
            Transform trees = gardenGame.transform.Find("Garden_Trees");
            if (trees != null)
            {
                foreach (Transform t in trees)
                {
                    log += $"Tree: {t.name} at {t.position}, rot={t.eulerAngles}\n";
                }
            }

            // 4. Bushes & Beds
            Transform bushes = gardenGame.transform.Find("Garden_Bushes");
            if (bushes != null)
            {
                foreach (Transform b in bushes) log += $"Bush: {b.name} at {b.position}\n";
            }
            Transform beds = gardenGame.transform.Find("Garden_FlowerBeds");
            if (beds != null)
            {
                foreach (Transform fb in beds) log += $"Bed: {fb.name} at {fb.position}\n";
            }

            File.WriteAllText("Assets/bird_env_survey.txt", log);
            Debug.Log("[SurveyBirdEnvironment] Written to Assets/bird_env_survey.txt");
        }
    }
}
