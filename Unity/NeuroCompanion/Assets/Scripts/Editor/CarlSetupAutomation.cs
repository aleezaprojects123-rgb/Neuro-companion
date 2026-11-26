// ============================================================
// CarlSetupAutomation.cs — Unity 6.3 LTS Editor Automation
// Automatically imports, rigs Carl with Jaw Bone, creates materials,
// connects sample audio and saves CarlJawTestScene.unity.
// ============================================================

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

public static class CarlSetupAutomation
{
    private const string OBJ_PATH = "Assets/Characters/CarlFredricksen/Carl Fredricksen.obj";
    private const string DIFF_TEX_PATH = "Assets/Characters/CarlFredricksen/Textures/carl_fradricksen_diff.png";
    private const string PROPS_TEX_PATH = "Assets/Characters/CarlFredricksen/Textures/IMG_20240311_144256.jpeg";
    private const string AUDIO_PATH = "Assets/Audio/carl_speech_sample.wav";
    private const string SCENE_PATH = "Assets/Scenes/CarlJawTestScene.unity";

    [MenuItem("Tools/NeuroCompanion/Setup Carl Scene Automatically")]
    public static void BuildAndSaveScene()
    {
        Debug.Log("[CarlSetup] Starting automated Carl talking scene setup...");

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 1. Create or load materials
        Material matBody = GetOrCreateMaterial("Assets/Characters/CarlFredricksen/M_Carl_Body.mat", DIFF_TEX_PATH);
        Material matProps = GetOrCreateMaterial("Assets/Characters/CarlFredricksen/M_Carl_Props.mat", PROPS_TEX_PATH);

        // 2. Load the imported Carl model prefab
        GameObject carlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OBJ_PATH);
        if (carlPrefab == null)
        {
            Debug.LogError($"[CarlSetup] Could not load Carl OBJ prefab at {OBJ_PATH}");
            return;
        }

        // 3. Create a clean test scene
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        testScene.name = "CarlJawTestScene";

        // 4. Create Directional Light
        GameObject lightGo = new GameObject("Directional Light");
        Light lightComp = lightGo.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.2f;
        lightComp.color = new Color(1f, 0.98f, 0.92f);
        lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        // 5. Instantiate Carl in the scene
        GameObject carlRoot = new GameObject("Carl_Fredricksen_TalkingCharacter");
        carlRoot.transform.position = Vector3.zero;

        // Instantiate original model children under our container
        GameObject originalModel = Object.Instantiate(carlPrefab, carlRoot.transform);
        originalModel.name = "Carl_Original_Geometry";

        // 6. Find Head and Body meshes
        MeshFilter[] meshFilters = originalModel.GetComponentsInChildren<MeshFilter>();
        MeshFilter headFilter = null;
        MeshFilter bodyFilter = null;

        foreach (var mf in meshFilters)
        {
            string n = mf.gameObject.name.ToLower();
            if (n.Contains("default") || n.Contains("head"))
            {
                headFilter = mf;
            }
            else if (n.Contains("carl_fredricksen_lod0") || n.Contains("plane") || n.Contains("body"))
            {
                bodyFilter = mf;
            }
        }

        if (headFilter == null && meshFilters.Length > 0)
        {
            headFilter = meshFilters[0];
        }

        // Assign materials
        if (bodyFilter != null)
        {
            MeshRenderer mr = bodyFilter.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = matProps;
        }

        // 7. Rig the head mesh with Jaw Bone
        Transform jawBoneTransform = null;
        if (headFilter != null && headFilter.sharedMesh != null)
        {
            Mesh srcMesh = headFilter.sharedMesh;
            Mesh riggedMesh = Object.Instantiate(srcMesh);
            riggedMesh.name = "Carl_Head_JawRig";

            // Save rigged mesh asset
            string meshAssetPath = "Assets/Characters/CarlFredricksen/Carl_Head_JawRig.asset";
            AssetDatabase.CreateAsset(riggedMesh, meshAssetPath);

            // Create bones
            Transform headTransform = headFilter.transform;
            Transform rootBone = new GameObject("Root_Bone").transform;
            rootBone.SetParent(carlRoot.transform, false);
            rootBone.position = headTransform.position;

            Transform jawBone = new GameObject("Jaw_Bone").transform;
            jawBone.SetParent(rootBone, false);

            // Calculate jaw centroid with anatomical hinge offset (back towards ears)
            Vector3[] verts = riggedMesh.vertices;
            Vector3 sum = Vector3.zero;
            int jawVertCount = 0;

            float jawYLower = 7.0f;
            float jawYUpper = 8.1f;
            float jawZMin = 0.5f;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 world = headTransform.TransformPoint(verts[i]);
                if (world.y >= jawYLower && world.y < jawYUpper && world.z > jawZMin)
                {
                    sum += world;
                    jawVertCount++;
                }
            }

            Vector3 centroid = jawVertCount > 0 ? (sum / jawVertCount) : headTransform.position;
            // Shift pivot back along Z towards mandibular joint
            centroid.z -= 0.35f;
            jawBone.position = centroid;
            jawBoneTransform = jawBone;

            // Bindposes
            Matrix4x4[] bindposes = new Matrix4x4[2];
            bindposes[0] = rootBone.worldToLocalMatrix * headTransform.localToWorldMatrix;
            bindposes[1] = jawBone.worldToLocalMatrix * headTransform.localToWorldMatrix;
            riggedMesh.bindposes = bindposes;

            // Bone weights
            BoneWeight[] weights = new BoneWeight[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 world = headTransform.TransformPoint(verts[i]);
                bool isJaw = (world.y >= jawYLower && world.y < jawYUpper && world.z > jawZMin);
                weights[i] = new BoneWeight
                {
                    boneIndex0 = isJaw ? 1 : 0,
                    weight0 = 1.0f
                };
            }
            riggedMesh.boneWeights = weights;

            // Replace MeshRenderer on head with SkinnedMeshRenderer
            GameObject headGo = headFilter.gameObject;
            Object.DestroyImmediate(headFilter);
            MeshRenderer oldMr = headGo.GetComponent<MeshRenderer>();
            if (oldMr != null) Object.DestroyImmediate(oldMr);

            SkinnedMeshRenderer smr = headGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = riggedMesh;
            smr.bones = new Transform[] { rootBone, jawBone };
            smr.rootBone = rootBone;
            smr.sharedMaterial = matBody;

            Debug.Log($"[CarlSetup] Successfully rigged head mesh: {jawVertCount} vertices assigned to Jaw_Bone.");
        }

        // 8. Add AudioSource & Sample Clip
        AudioClip speechClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AUDIO_PATH);
        AudioSource audioSource = carlRoot.AddComponent<AudioSource>();
        if (speechClip != null)
        {
            audioSource.clip = speechClip;
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.volume = 1.0f;
            Debug.Log("[CarlSetup] AudioSource connected with carl_speech_sample.wav.");
        }
        else
        {
            Debug.LogWarning($"[CarlSetup] Audio clip not found at {AUDIO_PATH}");
        }

        // 9. Attach & Configure CarlJawTestController
        CarlJawTestController controller = carlRoot.AddComponent<CarlJawTestController>();
        controller.jawBone = jawBoneTransform;
        controller.audioSource = audioSource;
        controller.mode = CarlJawTestController.JawMode.AudioAmplitudeDriven;
        controller.maxJawAngle = 18f;
        controller.amplitudeSensitivity = 3.5f;

        // 10. Frame Camera on Carl's Face
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";
        camGo.AddComponent<AudioListener>();

        // Frame Camera for upper-body bust view of Carl
        camGo.transform.position = new Vector3(0f, 6.5f, -7.0f);
        camGo.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.1f;

        // 11. Save Scene
        EditorSceneManager.SaveScene(testScene, SCENE_PATH);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CarlSetup] COMPLETE! Scene successfully built and saved to: {SCENE_PATH}");
    }

    private static Material GetOrCreateMaterial(string matPath, string texPath)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

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
