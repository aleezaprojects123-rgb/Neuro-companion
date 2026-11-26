#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class GardenGrassTuftBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TuftTexPath = "Assets/Textures/Environment/Terrain/Grass_Tuft_Cutout.png";
        private const string TuftMatPath = "Assets/Materials/Environment/Terrain/Garden_Grass_Tuft_PBR.mat";
        private const string TuftMeshPath = "Assets/Materials/Environment/Terrain/Garden_Grass_Tufts_Combined.asset";

        [MenuItem("NeuroCompanion/Build 3D Furry Grass Field")]
        public static void BuildGrassField()
        {
            Debug.Log("[GardenGrassTuft] Building lush 3D furry grass tufts...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CONFIGURE TEXTURE
            var importer = AssetImporter.GetAtPath(TuftTexPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }

            var tuftTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TuftTexPath);

            // 2. CREATE SHADOW-LIT MATERIAL (WITH FOREST GREEN AMBIENT FILL SO BLADES ARE NEVER BLACK)
            var mat = AssetDatabase.LoadAssetAtPath<Material>(TuftMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, TuftMatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", tuftTex);
            mat.SetColor("_BaseColor", new Color(1.10f, 1.15f, 0.90f, 1f));
            mat.SetFloat("_Cutoff", 0.40f);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Cull", 0f); // Double-sided
            mat.EnableKeyword("_ALPHATEST_ON");
            // Rich soft forest green ambient emission fill so grass in shadow is lush green, NEVER black!
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.28f, 0.48f, 0.14f, 1f));
            mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(mat);

            // 3. GENERATE NEAT, MANICURED 3D FLUFFY GRASS FRINGE STRICTLY OUTSIDE THE PATHWAY
            var pathPoints = GetPathwaySplinePoints();

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            Random.InitState(42);

            // Dense manicured lawn fringe running right alongside the curbs (strictly on grass side: dist >= 1.50m)
            for (int i = 0; i < pathPoints.Count; i++)
            {
                var pt = pathPoints[i];
                var fwd = (i < pathPoints.Count - 1) ? (pathPoints[i + 1] - pt).normalized : Vector3.forward;
                var right = Vector3.Cross(Vector3.up, fwd).normalized;

                // 8 tufts per slice along the pathway edge: 4 left, 4 right
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int layer = 0; layer < 4; layer++)
                    {
                        // Curb ends at 1.45m. Grass starts at 1.48m to 2.80m (NEVER on pathway!)
                        float dist = 1.48f + layer * 0.35f + Random.Range(0.02f, 0.18f);
                        Vector3 pos = pt + right * (side * dist) + fwd * Random.Range(-0.30f, 0.30f);

                        if (Mathf.Abs(pos.x) > 30f || Mathf.Abs(pos.z) > 30f) continue;

                        float gy = SampleTerrainHeight(pos.x, pos.z);
                        pos.y = gy;

                        // Neat manicured lawn height (10cm - 16cm)
                        float tuftW = Random.Range(0.30f, 0.45f);
                        float tuftH = Random.Range(0.10f, 0.16f);

                        AddCrossQuadTuft(verts, uvs, normals, tris, pos, tuftW, tuftH);
                    }
                }
            }

            // 4. COMBINE AND SAVE MESH
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TuftMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "Garden_Grass_Tufts_Combined";
                AssetDatabase.CreateAsset(mesh, TuftMeshPath);
            }
            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.normals = normals.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            // 5. ATTACH TO HIERARCHY
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                foreach (var r in scene.GetRootGameObjects())
                {
                    if (r.name == "[GARDEN_GAME]") { gardenRoot = r; break; }
                }
            }
            if (gardenRoot == null) gardenRoot = new GameObject("[GARDEN_GAME]");

            var grassTr = gardenRoot.transform.Find("Garden_Grass_Field");
            GameObject grassGo = grassTr != null ? grassTr.gameObject : new GameObject("Garden_Grass_Field");
            grassGo.transform.SetParent(gardenRoot.transform, false);
            grassGo.transform.position = Vector3.zero;

            var mf = grassGo.GetComponent<MeshFilter>();
            if (mf == null) mf = grassGo.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = grassGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = grassGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // High performance
            mr.receiveShadows = true;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log($"[GardenGrassTuft] Success! Created {verts.Count / 8} 3D furry grass tufts ({verts.Count} vertices).");
        }

        private static void AddCrossQuadTuft(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris, Vector3 center, float width, float height)
        {
            float rotY = Random.Range(0f, 180f);
            Quaternion q1 = Quaternion.Euler(0f, rotY, 0f);
            Quaternion q2 = Quaternion.Euler(0f, rotY + 60f, 0f);
            Quaternion q3 = Quaternion.Euler(0f, rotY + 120f, 0f);

            AddSingleQuad(verts, uvs, normals, tris, center, q1, width, height);
            AddSingleQuad(verts, uvs, normals, tris, center, q2, width * 0.9f, height * 0.95f);
            AddSingleQuad(verts, uvs, normals, tris, center, q3, width * 0.85f, height * 0.9f);
        }

        private static void AddSingleQuad(List<Vector3> verts, List<Vector2> uvs, List<Vector3> normals, List<int> tris, Vector3 center, Quaternion rot, float width, float height)
        {
            int baseIdx = verts.Count;
            float hw = width * 0.5f;

            Vector3 right = rot * Vector3.right;
            Vector3 up = Vector3.up;

            // 4 vertices per quad: bottom-left, bottom-right, top-left, top-right
            // Slightly fan out top vertices for natural tuft spread
            Vector3 v0 = center - right * hw;
            Vector3 v1 = center + right * hw;
            Vector3 v2 = center - right * (hw * 1.15f) + up * height;
            Vector3 v3 = center + right * (hw * 1.15f) + up * height;

            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));

            Vector3 n = Vector3.up; // Pure upward normal for bright, even sunlit illumination
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);

            // Front face
            tris.Add(baseIdx);
            tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 1);

            tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 3);

            // Back face
            tris.Add(baseIdx);
            tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 2);

            tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 3);
            tris.Add(baseIdx + 2);
        }

        private static List<Vector3> GetPathwaySplinePoints()
        {
            var cps = new List<Vector2>
            {
                new Vector2(  0.0f, -32.0f),
                new Vector2(  0.0f, -27.0f),
                new Vector2(  7.5f, -19.0f),
                new Vector2( 13.5f,  -8.0f),
                new Vector2( 14.0f,   4.5f),
                new Vector2(  8.5f,  15.0f),
                new Vector2( -3.0f,  21.0f),
                new Vector2(  0.0f,  27.0f),
                new Vector2(  0.0f,  32.0f)
            };

            const int slices = 160;
            var list = new List<Vector3>(slices);
            int n = cps.Count;

            for (int i = 0; i < slices; i++)
            {
                float tGlobal = ((float)i / (slices - 1)) * (n - 1);
                int seg = Mathf.Clamp(Mathf.FloorToInt(tGlobal), 0, n - 2);
                float t = tGlobal - seg;

                Vector2 p0 = cps[Mathf.Max(0, seg - 1)];
                Vector2 p1 = cps[seg];
                Vector2 p2 = cps[seg + 1];
                Vector2 p3 = cps[Mathf.Min(n - 1, seg + 2)];

                float t2 = t * t;
                float t3 = t2 * t;
                Vector2 pos2D = 0.5f * (
                    (2.0f * p1) +
                    (-p0 + p2) * t +
                    (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
                    (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
                );

                list.Add(new Vector3(pos2D.x, 0f, pos2D.y));
            }
            return list;
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
