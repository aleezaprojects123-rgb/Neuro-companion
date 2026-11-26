using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class DisableGardenGame
    {
        [MenuItem("NeuroCompanion/Toggle Garden Game Off")]
        public static void ToggleOff()
        {
            var gg = GameObject.Find("[GARDEN_GAME]");
            if (gg != null)
            {
                gg.SetActive(false);
                Debug.Log("Disabled [GARDEN_GAME]");
            }
        }

        [MenuItem("NeuroCompanion/Toggle Garden Game On")]
        public static void ToggleOn()
        {
            // Find inactive
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "[GARDEN_GAME]")
                {
                    root.SetActive(true);
                    Debug.Log("Enabled [GARDEN_GAME]");
                }
            }
        }
    }
}
