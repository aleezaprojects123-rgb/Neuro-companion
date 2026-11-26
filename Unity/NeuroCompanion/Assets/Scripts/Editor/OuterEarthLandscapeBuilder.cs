#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class OuterEarthLandscapeBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string GrassAlbedoPath = "Assets/Textures/Environment/Terrain/Grass_Natural_Albedo.png";
        private const string GrassNormalPath = "Assets/Textures/Environment/Terrain/Grass_Natural_Normal.png";
        private const string MatPath = "Assets/Materials/Environment/Terrain/Outer_Earth_Landscape.mat";
        private const string MeshPath = "Assets/Materials/Environment/Terrain/Outer_Earth_Mesh.asset";

        [MenuItem("NeuroCompanion/Build 1000m Surrounding Earth Landscape")]
        public static void BuildOuterLandscape()
        {
            Debug.Log("[OuterLandscape] Building 1000m surrounding Earth countryside...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CREATE PBR MATERIAL FOR VAST COUNTRYSIDE
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassAlbedoPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(GrassNormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTextureScale("_BaseMap", new Vector2(120f, 120f)); // Broad countryside tiling
            mat.SetColor("_BaseColor", new Color(0.92f, 0.96f, 0.90f, 1f));
            if (normal != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetTextureScale("_BumpMap", new Vector2(120f, 120f));
                mat.SetFloat("_BumpScale", 0.85f);
            }
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(mat);

            // 2. GENERATE VAST 1,000m x 1,000m SUBDIVIDED ROLLING COUNTRYSIDE MESH
            const int resolution = 120; // 120x120 quads = 14,641 vertices
            const float size = 1000f;   // 1 kilometer expanse
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
                    float dist = Mathf.Sqrt(xPos * xPos + zPos * zPos);

                    float y = -0.05f; // Sits just beneath central garden

                    // Beyond garden perimeter (dist > 55m), add natural rolling countryside hills
                    if (dist > 55f)
                    {
                        float blend = Mathf.Clamp01((dist - 55f) / 100f);
                        // Natural broad rolling countryside waves
                        float hill1 = Mathf.Sin(xPos * 0.015f) * Mathf.Cos(zPos * 0.012f) * 2.2f;
                        float hill2 = Mathf.Cos(xPos * 0.008f + 1.5f) * Mathf.Sin(zPos * 0.010f + 0.7f) * 1.8f;
                        // Gentle perimeter ridge that softens into the horizon
                        float rim = Mathf.Clamp01((dist - 80f) / 350f) * 1.5f;
                        y += (hill1 + hill2 + rim) * blend;
                    }

                    vertices[vIdx] = new Vector3(xPos, y, zPos);
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
                    int r1 = z * (resolution + 1);
                    int r2 = (z + 1) * (resolution + 1);

                    triangles[tIdx++] = r1 + x;
                    triangles[tIdx++] = r2 + x;
                    triangles[tIdx++] = r1 + x + 1;

                    triangles[tIdx++] = r1 + x + 1;
                    triangles[tIdx++] = r2 + x;
                    triangles[tIdx++] = r2 + x + 1;
                }
            }

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Outer_Earth_Mesh";
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            // 3. ATTACH TO [GARDEN_GAME]
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

            var outerTr = gardenRoot.transform.Find("Outer_Earth_Landscape");
            GameObject outerGo = outerTr != null ? outerTr.gameObject : new GameObject("Outer_Earth_Landscape");
            outerGo.transform.SetParent(gardenRoot.transform, false);
            outerGo.transform.position = Vector3.zero;

            var mf = outerGo.GetComponent<MeshFilter>(); if (mf == null) mf = outerGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = outerGo.GetComponent<MeshRenderer>(); if (mr == null) mr = outerGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // Save performance
            mr.receiveShadows = true;

            // 4. CALIBRATE FOG DISTANCE FOR 1000m EXPANSE
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.72f, 0.82f, 0.88f, 1f); // Blends with horizon
            RenderSettings.fogStartDistance = 65f;
            RenderSettings.fogEndDistance = 650f;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[OuterLandscape] 1000m surrounding Earth landscape created! Zero floating island effect.");
        }
    }
}
#endif
