using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class DumpSceneRoots
    {
        [MenuItem("NeuroCompanion/Dump Scene Roots")]
        public static void Run()
        {
            var activeScene = SceneManager.GetActiveScene();
            string report = "Active Scene: " + activeScene.name + " (path: " + activeScene.path + ")\n";
            var rootObjs = activeScene.GetRootGameObjects();
            report += $"Root GameObjects count: {rootObjs.Length}\n";
            foreach (var go in rootObjs)
            {
                report += $"Root: {go.name} (active: {go.activeSelf})\n";
                foreach (Transform child in go.transform)
                {
                    report += $"  -> {child.name} (active: {child.gameObject.activeSelf})\n";
                    foreach (Transform grandChild in child)
                    {
                        report += $"     --> {grandChild.name}\n";
                    }
                }
            }

            File.WriteAllText("Assets/scene_dump.txt", report);
            Debug.Log("[DumpSceneRoots] Done");
        }
    }
}
