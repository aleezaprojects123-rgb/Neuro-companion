// ============================================================
// BossSetupAutomation.cs — Unity 6.3 LTS Editor Automation
// Extracts embedded FBX textures, sets up front-facing camera,
// and configures The Boss character scene.
// ============================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public static class BossSetupAutomation
{
    private const string BOSS_FBX_PATH = "Assets/Characters/TheBoss/The Boss.fbx";
    private const string TEXTURE_FOLDER = "Assets/Characters/TheBoss/Textures";
    private const string AUDIO_PATH = "Assets/Audio/carl_speech_sample.wav";
    private const string SCENE_PATH = "Assets/Scenes/BossTestScene.unity";

    [MenuItem("Tools/NeuroCompanion/Setup The Boss Scene")]
    public static void BuildBossScene()
    {
        Debug.Log("[BossSetup] Extracting textures and configuring The Boss...");

        // 1. Ensure texture directory exists
        if (!AssetDatabase.IsValidFolder(TEXTURE_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Characters/TheBoss", "Textures");
        }

        // 2. Configure Model Importer & Extract Embedded Textures
        ModelImporter importer = AssetImporter.GetAtPath(BOSS_FBX_PATH) as ModelImporter;
        if (importer != null)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.External;
            
            // Extract textures from inside the FBX
            importer.ExtractTextures(TEXTURE_FOLDER);
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 3. Load The Boss prefab
        GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BOSS_FBX_PATH);
        if (bossPrefab == null)
        {
            Debug.LogError($"[BossSetup] Could not load FBX at {BOSS_FBX_PATH}");
            return;
        }

        // 4. Create a clean test scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "BossTestScene";

        // 5. Lighting: Key Light & Fill Light
        GameObject lightGo = new GameObject("Key Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;
        light.color = new Color(1f, 0.98f, 0.95f);
        lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

        GameObject fillGo = new GameObject("Fill Light");
        Light fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.6f;
        fill.color = new Color(0.85f, 0.9f, 1f);
        fillGo.transform.rotation = Quaternion.Euler(20f, -30f, 0f);

        // 6. Instantiate The Boss facing forward (0, 0, 0)
        GameObject bossInstance = Object.Instantiate(bossPrefab);
        bossInstance.name = "TheBoss_Character";
        bossInstance.transform.position = Vector3.zero;
        bossInstance.transform.rotation = Quaternion.identity; // Natural forward facing (+Z)

        // Upgrade materials to URP Lit if needed
        Renderer[] renderers = bossInstance.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat != null && mat.shader.name != "Universal Render Pipeline/Lit")
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpShader != null) mat.shader = urpShader;
                }
            }
        }

        // 7. Connect AudioSource
        AudioClip speechClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AUDIO_PATH);
        AudioSource audioSource = bossInstance.AddComponent<AudioSource>();
        if (speechClip != null)
        {
            audioSource.clip = speechClip;
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.volume = 1.0f;
        }

        // 8. Add life-like talking controller
        BossTalkingController controller = bossInstance.AddComponent<BossTalkingController>();
        controller.audioSource = audioSource;

        // 9. Frame Camera from FRONT facing him
        // Boss is at (0, 0, 0) facing +Z. His head is around Y = 1.55m.
        // Camera placed at Z = +1.35m in front of his face, looking toward him (-Z)
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        camGo.transform.position = new Vector3(0f, 1.55f, 1.35f);
        camGo.transform.rotation = Quaternion.Euler(2f, 180f, 0f); // Looking towards -Z (his face)
        cam.fieldOfView = 36f;
        cam.nearClipPlane = 0.05f;

        // 10. Save Scene
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.SaveAssets();

        Debug.Log($"[BossSetup] SUCCESS! Front-facing scene saved to: {SCENE_PATH}");
    }
}
