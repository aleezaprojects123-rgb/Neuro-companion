#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class GardenTerrainBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AlbedoPath = "Assets/Textures/Environment/Terrain/Grass_Natural_Albedo.png";
        private const string NormalPath = "Assets/Textures/Environment/Terrain/Grass_Natural_Normal.png";
        private const string MaterialPath = "Assets/Materials/Environment/Terrain/Garden_Grass_PBR.mat";
        private const string MeshAssetPath = "Assets/Materials/Environment/Terrain/Garden_Terrain_Mesh.asset";

        [MenuItem("NeuroCompanion/Build Cognitive Garden Ground & Terrain")]
        public static void BuildGardenTerrain()
        {
            Debug.Log("[GardenTerrain] Building realistic, spacious, Alzheimer-friendly ground & terrain...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CONFIGURE TEXTURE IMPORTERS
            ConfigureTexture(AlbedoPath, isNormal: false);
            ConfigureTexture(NormalPath, isNormal: true);

            // 2. CREATE / CONFIGURE PBR GRASS MATERIAL
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetTextureScale("_BaseMap", new Vector2(24f, 24f)); // Natural real-world blade scale
            mat.SetColor("_BaseColor", Color.white);

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetTextureScale("_BumpMap", new Vector2(24f, 24f));
                mat.SetFloat("_BumpScale", 1.0f);
            }

            mat.SetFloat("_Smoothness", 0.12f); // Matte natural turf
            mat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(mat);

            // 3. GENERATE SUBDIVIDED ACCESSIBLE TERRAIN MESH (120m x 120m)
            const int resolution = 100; // 100x100 quads = 10,201 vertices
            const float size = 120f;    // 120 meters wide
            const float halfSize = size * 0.5f;

            var vertices = new Vector3[(resolution + 1) * (resolution + 1)];
            var uvs = new Vector2[vertices.Length];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[resolution * resolution * 6];

            int vIdx = 0;
            for (int z = 0; z <= resolution; z++)
            {
                float zPos = ((float)z / resolution) * size - halfSize;
                for (int x = 0; x <= resolution; x++)
                {
                    float xPos = ((float)x / resolution) * size - halfSize;

                    // Distance from central activity zone
                    float distFromCenter = Mathf.Sqrt(xPos * xPos + zPos * zPos);

                    // Height calculation:
                    // - Inner 22m radius (activity/path area): FLAT (0.0m) for elderly safety
                    // - 22m to 55m: subtle natural ground undulation (max 0.25m - 0.45m gentle swell)
                    float height = 0f;
                    if (distFromCenter > 22f)
                    {
                        float blend = Mathf.Clamp01((distFromCenter - 22f) / 30f);
                        // Very gentle low-frequency natural rolling earth
                        float wave1 = Mathf.Sin(xPos * 0.06f) * Mathf.Cos(zPos * 0.05f) * 0.22f;
                        float wave2 = Mathf.Cos(xPos * 0.03f + 1.2f) * Mathf.Sin(zPos * 0.04f + 0.8f) * 0.18f;
                        height = (wave1 + wave2) * blend;
                    }

                    vertices[vIdx] = new Vector3(xPos, height, zPos);
                    uvs[vIdx] = new Vector2((float)x / resolution, (float)z / resolution);
                    normals[vIdx] = Vector3.up;
                    vIdx++;
                }
            }

            int tIdx = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int row1 = z * (resolution + 1);
                    int row2 = (z + 1) * (resolution + 1);

                    triangles[tIdx++] = row1 + x;
                    triangles[tIdx++] = row2 + x;
                    triangles[tIdx++] = row1 + x + 1;

                    triangles[tIdx++] = row1 + x + 1;
                    triangles[tIdx++] = row2 + x;
                    triangles[tIdx++] = row2 + x + 1;
                }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Garden_Terrain_Mesh";
                AssetDatabase.CreateAsset(mesh, MeshAssetPath);
            }
            else
            {
                mesh.Clear();
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh);

            // 4. FIND OR CREATE [GARDEN_GAME] ROOT & TERRAIN GAMEOBJECT
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                var roots = scene.GetRootGameObjects();
                foreach (var r in roots)
                {
                    if (r.name == "[GARDEN_GAME]") { gardenRoot = r; break; }
                }
            }
            if (gardenRoot == null) gardenRoot = new GameObject("[GARDEN_GAME]");

            // Remove old primitive placeholder if present
            var oldMeadow = gardenRoot.transform.Find("Meadow_Ground");
            if (oldMeadow != null) Object.DestroyImmediate(oldMeadow.gameObject);

            // Create / update Garden_Terrain
            var terrainTr = gardenRoot.transform.Find("Garden_Terrain");
            GameObject terrainGo = terrainTr != null ? terrainTr.gameObject : new GameObject("Garden_Terrain");
            terrainGo.transform.SetParent(gardenRoot.transform, false);
            terrainGo.transform.position = Vector3.zero;
            terrainGo.transform.rotation = Quaternion.identity;
            terrainGo.transform.localScale = Vector3.one;

            var mf = terrainGo.GetComponent<MeshFilter>();
            if (mf == null) mf = terrainGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = terrainGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = terrainGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            var mc = terrainGo.GetComponent<MeshCollider>();
            if (mc == null) mc = terrainGo.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[GardenTerrain] Spacious realistic terrain created successfully (120m x 120m, level central zone, PBR turf)!");
        }

        private static void ConfigureTexture(string path, bool isNormal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.sRGBTexture = !isNormal;
            importer.SaveAndReimport();
        }
    }
}
#endif
