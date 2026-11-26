#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class TreeAssetConfigurator
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("NeuroCompanion/Setup and Place Trees")]
        public static void SetupAndPlaceTrees()
        {
            Debug.Log("[TreeConfigurator] Setting up URP materials and placing trees...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CONFIGURE TEXTURES
            ConfigureTexture("Assets/Models/Environment/Trees/PalmTree/Tree.png", isNormal: false, alphaIsTrans: true);
            ConfigureTexture("Assets/Models/Environment/Trees/RealisticTree/leaves_RGBA.png", isNormal: false, alphaIsTrans: true);
            ConfigureTexture("Assets/Models/Environment/Trees/RealisticTree/leaves nm.png", isNormal: true, alphaIsTrans: false);
            ConfigureTexture("Assets/Models/Environment/Trees/RealisticTree/trunk_Base_color.png", isNormal: false, alphaIsTrans: false);
            ConfigureTexture("Assets/Models/Environment/Trees/RealisticTree/trunk_Normal_OpenGL.png", isNormal: true, alphaIsTrans: false);
            ConfigureTexture("Assets/Models/Environment/Trees/CherryTree/v176CherryFlower_RGBA.png", isNormal: false, alphaIsTrans: true);
            ConfigureTexture("Assets/Models/Environment/Trees/CherryTree/v176CherryBark01_BC.jpeg", isNormal: false, alphaIsTrans: false);
            ConfigureTexture("Assets/Models/Environment/Trees/CherryTree/v176CherryBranch01_BC.jpeg", isNormal: false, alphaIsTrans: false);
            ConfigureTexture("Assets/Models/Environment/Trees/CherryTree/v176FCT_Rock_BC.jpeg", isNormal: false, alphaIsTrans: false);
            ConfigureTexture("Assets/Models/Environment/Trees/CherryTree/v176TreeCap_BC.jpeg", isNormal: false, alphaIsTrans: false);

            AssetDatabase.SaveAssets();

            // 2. CREATE URP MATERIALS
            // --- Realistic Tree ---
            var realTrunkMat = CreateMaterial("Assets/Materials/Environment/RealisticTree_Trunk.mat",
                "Assets/Models/Environment/Trees/RealisticTree/trunk_Base_color.png",
                "Assets/Models/Environment/Trees/RealisticTree/trunk_Normal_OpenGL.png",
                isCutout: false, cutoff: 0.5f, smoothness: 0.12f, Color.white);

            var realLeavesMat = CreateMaterial("Assets/Materials/Environment/RealisticTree_Leaves.mat",
                "Assets/Models/Environment/Trees/RealisticTree/leaves_RGBA.png",
                "Assets/Models/Environment/Trees/RealisticTree/leaves nm.png",
                isCutout: true, cutoff: 0.42f, smoothness: 0.20f, new Color(1.05f, 1.08f, 0.95f, 1f));

            // --- Palm Tree ---
            var palmMat = CreateMaterial("Assets/Materials/Environment/PalmTree_Pro.mat",
                "Assets/Models/Environment/Trees/PalmTree/Tree.png",
                normalPath: null,
                isCutout: true, cutoff: 0.45f, smoothness: 0.20f, Color.white);

            // --- Cherry Tree ---
            var cherryBarkMat = CreateMaterial("Assets/Materials/Environment/CherryTree_Bark.mat",
                "Assets/Models/Environment/Trees/CherryTree/v176CherryBark01_BC.jpeg",
                normalPath: null, isCutout: false, cutoff: 0.5f, smoothness: 0.15f, Color.white);

            var cherryBranchMat = CreateMaterial("Assets/Materials/Environment/CherryTree_Branch.mat",
                "Assets/Models/Environment/Trees/CherryTree/v176CherryBranch01_BC.jpeg",
                normalPath: null, isCutout: false, cutoff: 0.5f, smoothness: 0.15f, Color.white);

            var cherryRockMat = CreateMaterial("Assets/Materials/Environment/CherryTree_Rock.mat",
                "Assets/Models/Environment/Trees/CherryTree/v176FCT_Rock_BC.jpeg",
                normalPath: null, isCutout: false, cutoff: 0.5f, smoothness: 0.10f, Color.white);

            var cherryCapMat = CreateMaterial("Assets/Materials/Environment/CherryTree_Cap.mat",
                "Assets/Models/Environment/Trees/CherryTree/v176TreeCap_BC.jpeg",
                normalPath: null, isCutout: false, cutoff: 0.5f, smoothness: 0.15f, Color.white);

            var cherryFlowerMat = CreateMaterial("Assets/Materials/Environment/CherryTree_Flower.mat",
                "Assets/Models/Environment/Trees/CherryTree/v176CherryFlower_RGBA.png",
                normalPath: null, isCutout: true, cutoff: 0.38f, smoothness: 0.22f, new Color(1.08f, 1.02f, 1.04f, 1f));

            AssetDatabase.SaveAssets();

            // 3. FIND OR CREATE [GARDEN_GAME] ROOT & TREES CONTAINER
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                foreach (var r in scene.GetRootGameObjects())
                {
                    if (r.name == "[GARDEN_GAME]") { gardenRoot = r; break; }
                }
            }
            if (gardenRoot == null) gardenRoot = new GameObject("[GARDEN_GAME]");

            var treesParentTr = gardenRoot.transform.Find("Garden_Trees");
            if (treesParentTr != null)
            {
                GameObject.DestroyImmediate(treesParentTr.gameObject);
            }
            var treesGo = new GameObject("Garden_Trees");
            treesGo.transform.SetParent(gardenRoot.transform, false);
            treesGo.transform.position = Vector3.zero;

            // Load FBX sources
            var cherryFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/Trees/CherryTree/Cherry Tree.fbx");
            var palmFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/Trees/PalmTree/Palm Tree Pro.fbx");
            var realFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/Trees/RealisticTree/TREE.fbx");

            // 4. INSTANTIATE AND PLACE TREES
            // --- A. Realistic Deciduous Shade Trees (Majestic 7-9m trees around perimeter & lawn) ---
            var realTreePositions = new Vector3[]
            {
                new Vector3( 24.0f, 0f, -16.0f),
                new Vector3( 26.0f, 0f,   4.0f),
                new Vector3( 18.0f, 0f,  22.0f),
                new Vector3( -8.0f, 0f,  26.0f),
                new Vector3(-22.0f, 0f,  18.0f),
                new Vector3(-24.0f, 0f,   2.0f),
                new Vector3(-20.0f, 0f, -14.0f),
                new Vector3(-16.0f, 0f, -25.0f)
            };

            for (int i = 0; i < realTreePositions.Length; i++)
            {
                var basePos = realTreePositions[i];
                float gy = SampleTerrainHeight(basePos.x, basePos.z);
                var treeInst = (GameObject)PrefabUtility.InstantiatePrefab(realFbx, treesGo.transform);
                treeInst.name = $"RealisticTree_{i + 1}";
                treeInst.transform.position = new Vector3(basePos.x, gy, basePos.z);
                // Raw TREE.fbx size is ~16m tall at scale 1.0. Scale of 0.45f - 0.60f gives a realistic 7.5m - 9.5m shade tree
                float s = 0.55f * Random.Range(0.88f, 1.15f);
                treeInst.transform.localScale = new Vector3(s, s, s);
                treeInst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                // Assign materials
                var renderers = treeInst.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                    if (r.name.Contains("crown") || r.name.Contains("leaves"))
                    {
                        r.sharedMaterial = realLeavesMat;
                    }
                    else
                    {
                        r.sharedMaterial = realTrunkMat;
                    }
                }
            }

            // --- B. Cherry Blossom Trees (Graceful 5-6m pink blossoming trees framing pathway alcoves) ---
            var cherryPositions = new Vector3[]
            {
                new Vector3( 17.5f, 0f, -10.0f), // East foreground near curve
                new Vector3( 19.0f, 0f,  12.0f), // Northeast garden corner
                new Vector3( -9.0f, 0f,  12.0f), // Northwest inner curve
                new Vector3(-12.0f, 0f, -10.0f)  // West garden sensory alcove
            };

            var cherryMatArray = new Material[]
            {
                cherryRockMat,   // 0: Rock
                cherryBarkMat,   // 1: Bark
                cherryBranchMat, // 2: Branch
                cherryBranchMat, // 3: FlowerStalk
                cherryFlowerMat, // 4: Bud
                cherryCapMat,    // 5: Tree_Knot
                cherryFlowerMat  // 6: Flower Petals
            };

            for (int i = 0; i < cherryPositions.Length; i++)
            {
                var basePos = cherryPositions[i];
                float gy = SampleTerrainHeight(basePos.x, basePos.z);
                var cherryInst = (GameObject)PrefabUtility.InstantiatePrefab(cherryFbx, treesGo.transform);
                cherryInst.name = $"CherryTree_{i + 1}";
                cherryInst.transform.position = new Vector3(basePos.x, gy, basePos.z);
                float s = 0.95f * Random.Range(0.90f, 1.12f);
                cherryInst.transform.localScale = new Vector3(s, s, s);
                cherryInst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                var r = cherryInst.GetComponentInChildren<Renderer>(true);
                if (r != null)
                {
                    r.sharedMaterials = cherryMatArray;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
            }

            // --- C. Palm Trees (Stately 7m palms accenting the sunny entrance & southern garden) ---
            var palmPositions = new Vector3[]
            {
                new Vector3(  7.0f, 0f, -28.0f), // Right of South entrance
                new Vector3( -7.0f, 0f, -28.0f), // Left of South entrance
                new Vector3( 12.0f, 0f, -24.0f), // Southeast approach
                new Vector3(-18.0f, 0f, -22.0f)  // Southwest corner
            };

            for (int i = 0; i < palmPositions.Length; i++)
            {
                var basePos = palmPositions[i];
                float gy = SampleTerrainHeight(basePos.x, basePos.z);
                var palmInst = (GameObject)PrefabUtility.InstantiatePrefab(palmFbx, treesGo.transform);
                palmInst.name = $"PalmTree_{i + 1}";
                palmInst.transform.position = new Vector3(basePos.x, gy, basePos.z);
                float s = 1.0f * Random.Range(0.92f, 1.18f);
                palmInst.transform.localScale = new Vector3(s, s, s);
                palmInst.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                var r = palmInst.GetComponentInChildren<Renderer>(true);
                if (r != null)
                {
                    r.sharedMaterial = palmMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveShadows = true;
                }
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[TreeConfigurator] Successfully placed {realTreePositions.Length} realistic trees, {cherryPositions.Length} cherry blossom trees, and {palmPositions.Length} palm trees!");
        }

        private static Material CreateMaterial(string matPath, string albedoPath, string normalPath, bool isCutout, float cutoff, float smoothness, Color color)
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

            if (!string.IsNullOrEmpty(normalPath))
            {
                var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normalTex != null)
                {
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.SetFloat("_BumpScale", 1.0f);
                }
            }

            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0.0f);

            if (isCutout)
            {
                mat.SetFloat("_Surface", 0f); // Opaque
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", cutoff);
                mat.SetFloat("_Cull", 0f); // Double-sided leaves
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                mat.SetFloat("_Surface", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.SetFloat("_Cull", 2f); // Backface culling for solid trunk
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
