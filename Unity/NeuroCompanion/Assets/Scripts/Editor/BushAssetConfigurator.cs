#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class BushAssetConfigurator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("NeuroCompanion/Setup and Place Bushes")]
        public static void SetupAndPlaceBushes()
        {
            Debug.Log("[BushConfigurator] Setting up URP materials and placing bushes...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CONFIGURE TEXTURES
            ConfigureTexture("Assets/Models/Environment/Bushes/TallBush/STBroadleaf_V3Branch.png", isNormal: false, alphaIsTrans: true);
            ConfigureTexture("Assets/Models/Environment/Bushes/TallBush/STConifier_V1Branch.png", isNormal: false, alphaIsTrans: true);
            ConfigureTexture("Assets/Models/Environment/Bushes/GreenBush/Leaf_texture.png", isNormal: false, alphaIsTrans: false);

            AssetDatabase.SaveAssets();

            // 2. CREATE URP MATERIALS
            // --- Tall Bush Materials ---
            var broadleafMat = CreateMaterial("Assets/Materials/Environment/TallBush_Broadleaf.mat",
                "Assets/Models/Environment/Bushes/TallBush/STBroadleaf_V3Branch.png",
                isCutout: true, cutoff: 0.38f, smoothness: 0.18f, new Color(1.02f, 1.05f, 0.95f, 1f));

            var coniferMat = CreateMaterial("Assets/Materials/Environment/TallBush_Conifer.mat",
                "Assets/Models/Environment/Bushes/TallBush/STConifier_V1Branch.png",
                isCutout: true, cutoff: 0.38f, smoothness: 0.18f, new Color(1.02f, 1.05f, 0.95f, 1f));

            // --- Green Bush Material ---
            var greenBushMat = CreateMaterial("Assets/Materials/Environment/GreenBush_Leaf.mat",
                "Assets/Models/Environment/Bushes/GreenBush/Leaf_texture.png",
                isCutout: false, cutoff: 0.5f, smoothness: 0.15f, new Color(1.05f, 1.08f, 0.95f, 1f));

            AssetDatabase.SaveAssets();

            // 3. FIND OR CREATE [GARDEN_GAME] ROOT & BUSHES CONTAINER
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                foreach (var r in scene.GetRootGameObjects())
                {
                    if (r.name == "[GARDEN_GAME]") { gardenRoot = r; break; }
                }
            }
            if (gardenRoot == null) gardenRoot = new GameObject("[GARDEN_GAME]");

            var bushesParentTr = gardenRoot.transform.Find("Garden_Bushes");
            if (bushesParentTr != null)
            {
                GameObject.DestroyImmediate(bushesParentTr.gameObject);
            }
            var bushesGo = new GameObject("Garden_Bushes");
            bushesGo.transform.SetParent(gardenRoot.transform, false);
            bushesGo.transform.position = Vector3.zero;

            // Load FBX sources
            var tallBushFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/Bushes/TallBush/ST_TallBush.fbx");
            var greenBushFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/Bushes/GreenBush/Bush.fbx");

            // 4. INSTANTIATE TALL BUSHES (Safely nestled in lawn near trees, >= 2.8m clear of pathway)
            var tallBushPositions = new Vector3[]
            {
                new Vector3( 22.0f, 0f, -13.0f), // Near RealisticTree_1
                new Vector3( 16.0f, 0f,  -6.0f), // Accenting CherryTree_1 alcove
                new Vector3(-15.0f, 0f,  -8.0f), // Near CherryTree_4 in West garden
                new Vector3( 15.0f, 0f,  20.0f), // Near RealisticTree_3 in Northeast
                new Vector3(-12.0f, 0f,  14.0f)  // Near CherryTree_3 in Northwest
            };

            var tallBushMatArray = new Material[]
            {
                coniferMat,   // Slot 0: ST_Blades_2_Mat
                broadleafMat  // Slot 1: ST_Blades_1_Mat
            };

            for (int i = 0; i < tallBushPositions.Length; i++)
            {
                var basePos = tallBushPositions[i];
                float gy = SampleTerrainHeight(basePos.x, basePos.z);
                var bushInst = (GameObject)PrefabUtility.InstantiatePrefab(tallBushFbx, bushesGo.transform);
                bushInst.name = $"TallBush_{i + 1}";
                bushInst.transform.position = new Vector3(basePos.x, gy, basePos.z);
                float s = 14f * Random.Range(0.88f, 1.15f);
                bushInst.transform.localScale = new Vector3(s, s, s);
                bushInst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                var r = bushInst.GetComponentInChildren<Renderer>(true);
                if (r != null)
                {
                    r.sharedMaterials = tallBushMatArray;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
            }

            // 5. INSTANTIATE GREEN BUSHES (Round lush shrubs framing trees, >= 2.8m clear of pathway)
            var greenBushPositions = new Vector3[]
            {
                new Vector3( 18.5f, 0f, -12.0f), // Flanking CherryTree_1 rock base
                new Vector3( 18.0f, 0f,   2.0f), // East fence alcove
                new Vector3(  9.5f, 0f, -27.0f), // Flanking PalmTree_1 on South lawn
                new Vector3( -9.5f, 0f, -27.0f), // Flanking PalmTree_2 on South lawn
                new Vector3( 17.0f, 0f,  14.0f)  // Flanking CherryTree_2 in Northeast
            };

            for (int i = 0; i < greenBushPositions.Length; i++)
            {
                var basePos = greenBushPositions[i];
                float gy = SampleTerrainHeight(basePos.x, basePos.z);
                var bushInst = (GameObject)PrefabUtility.InstantiatePrefab(greenBushFbx, bushesGo.transform);
                bushInst.name = $"GreenBush_{i + 1}";
                bushInst.transform.position = new Vector3(basePos.x, gy, basePos.z);
                // Raw bounds height ~3.8m -> scale 0.35x gives ~1.3m realistic shrub height
                float s = 0.35f * Random.Range(0.85f, 1.15f);
                bushInst.transform.localScale = new Vector3(s, s, s);
                bushInst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                var renderers = bushInst.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    r.sharedMaterial = greenBushMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[BushConfigurator] Successfully placed {tallBushPositions.Length} tall bushes and {greenBushPositions.Length} green bushes!");
        }

        private static Material CreateMaterial(string matPath, string albedoPath, bool isCutout, float cutoff, float smoothness, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");

            if (!string.IsNullOrEmpty(albedoPath))
            {
                var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                mat.SetTexture("_BaseMap", albedoTex);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0.0f);

            if (isCutout)
            {
                mat.SetFloat("_Surface", 0f); // Opaque
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", cutoff);
                mat.SetFloat("_Cull", 0f); // Double-sided foliage
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_Cull", 0f); // Double-sided leaves
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureTexture(string path, bool isNormal, bool alphaIsTrans)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            if (alphaIsTrans)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
            }
            importer.SaveAndReimport();
        }

        private static float SampleTerrainHeight(float x, float z)
        {
            float distFromCenter = Mathf.Sqrt(x * x + z * z);
            if (distFromCenter <= 22f) return 0f;
            float blend = Mathf.Clamp01((distFromCenter - 22f) / 30f);
            float wave1 = Mathf.Sin(x * 0.06f) * Mathf.Cos(z * 0.05f) * 0.22f;
            float wave2 = Mathf.Cos(x * 0.03f + 1.2f) * Mathf.Sin(z * 0.04f + 0.8f) * 0.18f;
            return (wave1 + wave2) * blend;
        }
    }
}
#endif
