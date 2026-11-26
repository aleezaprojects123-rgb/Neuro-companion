using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NeuroCompanion.Backend.Editor
{
    public static class AttachSpotDetector
    {
        [MenuItem("NeuroCompanion/Attach Spot Detector")]
        public static void Attach()
        {
            var cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                if (cam.GetComponent<SpotDetectorRuntime>() == null)
                {
                    cam.gameObject.AddComponent<SpotDetectorRuntime>();
                    EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
                    EditorSceneManager.SaveScene(cam.gameObject.scene);
                    Debug.Log("[AttachSpotDetector] Attached SpotDetectorRuntime to Camera.");
                }
            }
        }
    }
}
