// ============================================================
// CleanCarlSetupAutomation.cs — Unity 6.3 LTS Editor Automation
// Sets up Carl Fredricksen with his 100% solid, original mesh,
// double-sided matte materials (NO see-through holes),
// clean speech audio, and conversational head gestures.
// ============================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class CleanCarlSetupAutomation
{
    private const string OBJ_PATH = "Assets/Characters/CarlFredricksen/Carl Fredricksen.obj";
    private const string DIFF_TEX_PATH = "Assets/Characters/CarlFredricksen/Textures/carl_fradricksen_diff.png";
    private const string PROPS_TEX_PATH = "Assets/Characters/CarlFredricksen/Textures/IMG_20240311_144256.jpeg";
    private const string AUDIO_PATH = "Assets/Audio/carl_speech_sample.wav";
    private const string SCENE_PATH = "Assets/Scenes/CarlCleanScene.unity";

    [MenuItem("Tools/NeuroCompanion/Setup Clean Carl Scene (No Distortion)")]
    public static void BuildCleanCarlScene()
    {
        Debug.Log("[CarlClean] Building 100% solid, distortion-free Carl scene...");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Prepare Double-Sided Matte Materials
        Material matBody = GetOrCreateMatteMaterial("Assets/Characters/CarlFredricksen/M_Carl_Body.mat", DIFF_TEX_PATH);
        Material matProps = GetOrCreateMatteMaterial("Assets/Characters/CarlFredricksen/M_Carl_Props.mat", PROPS_TEX_PATH);

        // 2. Load the Carl OBJ prefab
        GameObject carlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OBJ_PATH);
        if (carlPrefab == null)
        {
            Debug.LogError($"[CarlClean] Could not load Carl OBJ prefab at {OBJ_PATH}");
            return;
        }

        // 3. Create Scene
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "CarlCleanScene";

        // 4. Lighting: Soft Key + Fill Light
        GameObject lightGo = new GameObject("Key Light");
        Light keyLight = lightGo.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.0f;
        keyLight.color = new Color(1f, 0.98f, 0.95f);
        lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

        GameObject fillGo = new GameObject("Fill Light");
        Light fillLight = fillGo.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.45f;
        fillLight.color = new Color(0.85f, 0.9f, 1f);
        fillGo.transform.rotation = Quaternion.Euler(20f, -30f, 0f);

        // 5. Instantiate Carl with his original SOLID meshes (No broken skinning!)
        GameObject carlInstance = Object.Instantiate(carlPrefab);
        carlInstance.name = "Carl_Fredricksen_Clean";
        carlInstance.transform.position = Vector3.zero;
        carlInstance.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Default orientation

        // Find child meshes and assign materials properly
        MeshRenderer[] renderers = carlInstance.GetComponentsInChildren<MeshRenderer>();
        GameObject headGo = null;

        foreach (var r in renderers)
        {
            string n = r.gameObject.name.ToLower();
            if (n.Contains("default") || n.Contains("head"))
            {
                r.sharedMaterial = matBody; // Face, skin, hair, bowtie
                headGo = r.gameObject;
            }
            else
            {
                r.sharedMaterial = matProps; // Tweed coat, cane, glasses, shoes
            }
        }

        // 6. Connect AudioSource
        AudioClip speechClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AUDIO_PATH);
        AudioSource audioSource = carlInstance.AddComponent<AudioSource>();
        if (speechClip != null)
        {
            audioSource.clip = speechClip;
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.volume = 1.0f;
        }

        // 7. Add Conversational Speech Gestures to Head (ZERO mesh distortion!)
        if (headGo != null)
        {
            CarlCleanTalkingController controller = carlInstance.AddComponent<CarlCleanTalkingController>();
            controller.headObject = headGo.transform;
            controller.audioSource = audioSource;
        }

        // 8. Position Camera for a perfect portrait bust of Carl
        // Carl head is around Y = 7.8. 
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        // Place camera facing his front at a dignified distance
        camGo.transform.position = new Vector3(0f, 7.2f, -5.5f);
        camGo.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
        cam.fieldOfView = 42f;
        cam.nearClipPlane = 0.1f;

        // 9. Save Scene
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CarlClean] SUCCESS! Clean, distortion-free Carl scene saved to: {SCENE_PATH}");
    }

    private static Material GetOrCreateMatteMaterial(string matPath, string texPath)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        // Force Two-Sided (No invisible/see-through polygons)
        mat.SetFloat("_Cull", 0f); // 0 = Both faces rendered
        mat.EnableKeyword("_DOUBLESIDED_ON");

        // Force Matte (No plastic shine)
        mat.SetFloat("_Smoothness", 0.12f);
        mat.SetFloat("_Glossiness", 0.12f);
        mat.SetFloat("_Metallic", 0.0f);

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            else if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
