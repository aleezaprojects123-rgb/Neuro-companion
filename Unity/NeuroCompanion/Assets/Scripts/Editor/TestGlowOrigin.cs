using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestGlowOrigin
    {
        [MenuItem("NeuroCompanion/Test Move SunLight Far")]
        public static void MoveSunFar()
        {
            var sun = GameObject.Find("Garden_SunLight");
            if (sun != null)
            {
                Undo.RecordObject(sun.transform, "Move SunLight");
                sun.transform.position = new Vector3(0f, 500f, 0f);
                EditorUtility.SetDirty(sun);
                Debug.Log("[TestGlowOrigin] Garden_SunLight moved to (0, 500, 0).");
            }
            else
            {
                Debug.LogError("Garden_SunLight not found!");
            }
        }

        [MenuItem("NeuroCompanion/Test Restore SunLight")]
        public static void RestoreSun()
        {
            var sun = GameObject.Find("Garden_SunLight");
            if (sun != null)
            {
                Undo.RecordObject(sun.transform, "Restore SunLight");
                sun.transform.position = new Vector3(0f, 3f, 0f);
                EditorUtility.SetDirty(sun);
                Debug.Log("[TestGlowOrigin] Garden_SunLight restored to (0, 3, 0).");
            }
        }
    }
}
