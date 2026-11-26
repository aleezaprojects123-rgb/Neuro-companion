using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestSelectionClear
    {
        [MenuItem("NeuroCompanion/Clear Selection")]
        public static void Clear()
        {
            Selection.activeGameObject = null;
            Debug.Log("Selection cleared.");
        }
    }
}
