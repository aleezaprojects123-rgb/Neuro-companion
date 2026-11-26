using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace NeuroCompanion.Backend.Editor
{
    public static class TestPatientTouchdown
    {
        [MenuItem("NeuroCompanion/Test Click Patient Mode")]
        public static void ClickPatientMode()
        {
            var btn = GameObject.Find("Btn_PatientMode");
            if (btn != null)
            {
                var b = btn.GetComponent<Button>();
                if (b != null)
                {
                    b.onClick.Invoke();
                    Debug.Log("[TestPatientTouchdown] Clicked Btn_PatientMode!");
                }
            }
        }
    }
}
