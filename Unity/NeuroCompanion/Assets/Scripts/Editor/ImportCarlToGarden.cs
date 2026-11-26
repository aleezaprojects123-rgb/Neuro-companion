using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using NeuroCompanion.Carl;

namespace NeuroCompanion.Backend.Editor
{
    public static class ImportCarlToGarden
    {
        [MenuItem("NeuroCompanion/Import Carl To Garden")]
        public static void Import()
        {
            Debug.Log("[ImportCarlToGarden] Configuring Carl in Garden...");

            // 1. Find Garden Root
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                foreach (var r in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (r.name == "[GARDEN_GAME]" && r.scene.IsValid()) { gardenRoot = r; break; }
                }
            }

            if (gardenRoot == null)
            {
                Debug.LogError("[ImportCarlToGarden] [GARDEN_GAME] root not found!");
                return;
            }

            // 2. Remove any previous instance of Carl
            var prevCarl = gardenRoot.transform.Find("Carl_Fredricksen");
            if (prevCarl != null)
            {
                Undo.DestroyObjectImmediate(prevCarl.gameObject);
            }

            // 3. Load assets
            var carlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/CarlFredricksen/Carl Fredricksen.obj");
            var animMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Characters/CarlFredricksen/Carl_Fredricksen_Animated.asset");
            var matBody = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/CarlFredricksen/M_Carl_Body.mat");
            var matProps = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/CarlFredricksen/M_Carl_Props.mat");
            var greetingAudio = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_joyful_greeting.wav");

            // 4. Instantiate Carl under [GARDEN_GAME]
            var carlInstance = Object.Instantiate(carlPrefab, gardenRoot.transform);
            carlInstance.name = "Carl_Fredricksen";
            Undo.RegisterCreatedObjectUndo(carlInstance, "Import Carl To Garden");

            // 5. Scale to natural human proportion (~1.6m tall)
            carlInstance.transform.localScale = new Vector3(0.165f, 0.165f, 0.165f);

            // 6. Set up SkinnedMeshRenderer for Mouth/Lip BlendShapes
            SkinnedMeshRenderer faceSMR = null;
            Transform headTransform = null;

            var meshRenderers = carlInstance.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var mr in meshRenderers)
            {
                var go = mr.gameObject;
                headTransform = go.transform;

                // Replace MeshRenderer + MeshFilter with SkinnedMeshRenderer using animMesh
                Object.DestroyImmediate(mr.GetComponent<MeshFilter>());
                Object.DestroyImmediate(mr);

                faceSMR = go.AddComponent<SkinnedMeshRenderer>();
                faceSMR.sharedMesh = animMesh;
                faceSMR.sharedMaterial = matBody;
                break;
            }

            // 7. Add & Configure AudioSource
            var audioSrc = carlInstance.GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = carlInstance.AddComponent<AudioSource>();
            audioSrc.clip = greetingAudio;
            audioSrc.playOnAwake = false;
            audioSrc.spatialBlend = 0.35f;
            audioSrc.volume = 1.0f;

            // 8. Add & Configure Welcome Controller
            var welcomeCtrl = carlInstance.GetComponent<CarlGardenWelcomeController>();
            if (welcomeCtrl == null) welcomeCtrl = carlInstance.AddComponent<CarlGardenWelcomeController>();
            welcomeCtrl.audioSource = audioSrc;
            welcomeCtrl.welcomeClip = greetingAudio;
            welcomeCtrl.headTransform = headTransform;
            welcomeCtrl.faceSkinnedMesh = faceSMR;
            welcomeCtrl.walkSpeed = 2.4f;
            welcomeCtrl.stepFrequency = 3.2f;
            welcomeCtrl.stepHopHeight = 0.035f;
            welcomeCtrl.waddleRollAngle = 3.2f;
            welcomeCtrl.initialStartDelay = 1.0f; // Starts walking when breeze sound begins

            // Pathway waypoints: from far away along the curve to the front of player
            welcomeCtrl.pathWaypoints = new Vector3[]
            {
                new Vector3(14.5f, 0.05f, -7.0f),    // Far away on pathway
                new Vector3(11.8f, 0.05f, -12.5f),   // Mid pathway
                new Vector3(7.5f, 0.05f, -17.5f),    // Approaching curve
                new Vector3(5.2f, 0.05f, -23.5f),    // Near curve
                new Vector3(3.6f, 0.05f, -26.2f)     // Final spot facing player
            };

            // Set Carl at the far start position
            carlInstance.transform.position = welcomeCtrl.pathWaypoints[0];
            Vector3 lookFirst = (welcomeCtrl.pathWaypoints[1] - welcomeCtrl.pathWaypoints[0]).normalized;
            lookFirst.y = 0f;
            if (lookFirst.sqrMagnitude > 0.001f)
            {
                carlInstance.transform.rotation = Quaternion.LookRotation(lookFirst);
            }

            // 9. Save Scene & Assets
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[ImportCarlToGarden] Carl Fredricksen successfully configured with breeze timing, bow greeting, and lip-sync talking mouth!");
        }
    }
}
