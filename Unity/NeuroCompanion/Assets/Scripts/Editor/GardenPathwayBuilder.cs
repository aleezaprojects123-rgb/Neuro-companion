#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class GardenPathwayBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AlbedoPath = "Assets/Textures/Environment/Pathway/Pathway_Gravel_Albedo.png";
        private const string NormalPath = "Assets/Textures/Environment/Pathway/Pathway_Gravel_Normal.png";
        private const string SmoothnessPath = "Assets/Textures/Environment/Pathway/Pathway_Gravel_Smoothness.png";
        private const string MaterialPath = "Assets/Materials/Environment/Pathway/Garden_Pathway_PBR.mat";
        private const string MeshAssetPath = "Assets/Materials/Environment/Pathway/Garden_Pathway_Mesh.asset";

        // Cobblestone paver curb assets
        private const string CurbAlbedoPath = "Assets/Textures/Environment/Pathway/Cobblestone_Paver_Albedo.png";
        private const string CurbNormalPath = "Assets/Textures/Environment/Pathway/Cobblestone_Paver_Normal.png";
        private const string CurbMaterialPath = "Assets/Materials/Environment/Pathway/Garden_Pathway_Curb_PBR.mat";
        private const string CurbMeshAssetPath = "Assets/Materials/Environment/Pathway/Garden_Pathway_Curb_Mesh.asset";

        [MenuItem("NeuroCompanion/Build Realistic 3D Curved Pathway & Cobblestones")]
        public static void BuildPathway()
        {
            Debug.Log("[GardenPathway] Building realistic 3D pathway with granite cobblestone curb edging...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CONFIGURE TEXTURE IMPORTERS
            ConfigureTexture(AlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(NormalPath, isNormal: true, sRGB: false);
            ConfigureTexture(SmoothnessPath, isNormal: false, sRGB: false);
            ConfigureTexture(CurbAlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(CurbNormalPath, isNormal: true, sRGB: false);

            // 2. CONFIGURE PBR MATERIALS
            // A. Gravel Path Material
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            var pathMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (pathMat == null)
            {
                pathMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(pathMat, MaterialPath);
            }
            pathMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            pathMat.SetTexture("_BaseMap", albedoTex);
            pathMat.SetColor("_BaseColor", new Color(1.10f, 0.98f, 0.86f, 1f)); // Warm rich golden-brown pea gravel matching Picture 1
            if (normalTex != null)
            {
                pathMat.EnableKeyword("_NORMALMAP");
                pathMat.SetTexture("_BumpMap", normalTex);
                pathMat.SetFloat("_BumpScale", 1.35f);
            }
            pathMat.SetFloat("_Smoothness", 0.16f);
            pathMat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(pathMat);

            // B. Granite Cobblestone Curb Material
            var curbAlbedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(CurbAlbedoPath);
            var curbNormalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(CurbNormalPath);

            var curbMat = AssetDatabase.LoadAssetAtPath<Material>(CurbMaterialPath);
            if (curbMat == null)
            {
                curbMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(curbMat, CurbMaterialPath);
            }
            curbMat.shader = Shader.Find("Universal Render Pipeline/Lit");
            curbMat.SetTexture("_BaseMap", curbAlbedoTex);
            curbMat.SetColor("_BaseColor", Color.white);
            if (curbNormalTex != null)
            {
                curbMat.EnableKeyword("_NORMALMAP");
                curbMat.SetTexture("_BumpMap", curbNormalTex);
                curbMat.SetFloat("_BumpScale", 1.4f);
            }
            curbMat.SetFloat("_Smoothness", 0.22f);
            curbMat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(curbMat);

            // 3. ORGANIC SPLINE CONTROL POINTS (Connected seamlessly to South and North fence openings)
            var controlPoints = new List<Vector2>
            {
                new Vector2(  0.0f, -32.0f), // South Entrance threshold (fence opening at Z = -32)
                new Vector2(  0.0f, -27.0f), // Entering smoothly through gate
                new Vector2(  7.5f, -19.0f), // Sweeping Southeast curve into garden (matches Pic 1 foreground)
                new Vector2( 13.5f,  -8.0f), // East foreground curve
                new Vector2( 14.0f,   4.5f), // East resting alcove
                new Vector2(  8.5f,  15.0f), // Bending towards center
                new Vector2( -3.0f,  21.0f), // Gentle Northwest curve
                new Vector2(  0.0f,  27.0f), // Aligning with North gate
                new Vector2(  0.0f,  32.0f)  // North Exit threshold (fence opening at Z = +32)
            };

            // 4. SAMPLE SMOOTH OPEN SPLINE FROM SOUTH TO NORTH
            const int slicesCount = 320;
            var splinePoints = new List<Vector3>(slicesCount);
            var splineTangents = new List<Vector3>(slicesCount);
            var splineArcLengths = new List<float>(slicesCount);

            float totalArcLength = 0f;
            Vector3 prevPt = Vector3.zero;

            int n = controlPoints.Count;
            for (int i = 0; i < slicesCount; i++)
            {
                float tGlobal = ((float)i / (slicesCount - 1)) * (n - 1);
                int seg = Mathf.Clamp(Mathf.FloorToInt(tGlobal), 0, n - 2);
                float t = tGlobal - seg;

                Vector2 p0 = controlPoints[Mathf.Max(0, seg - 1)];
                Vector2 p1 = controlPoints[seg];
                Vector2 p2 = controlPoints[seg + 1];
                Vector2 p3 = controlPoints[Mathf.Min(n - 1, seg + 2)];

                Vector2 pos2D = CatmullRom(p0, p1, p2, p3, t);
                Vector2 tan2D = CatmullRomTangent(p0, p1, p2, p3, t).normalized;

                Vector3 pos = new Vector3(pos2D.x, 0f, pos2D.y);
                Vector3 tan = new Vector3(tan2D.x, 0f, tan2D.y);

                splinePoints.Add(pos);
                splineTangents.Add(tan);

                if (i > 0)
                {
                    totalArcLength += Vector3.Distance(pos, prevPt);
                }
                splineArcLengths.Add(totalArcLength);
                prevPt = pos;
            }

            // 5. BUILD GRAVEL PATHWAY MESH (Between curbs: u from -1.35m to +1.35m, width = 2.7m)
            var pathOffsets = new float[] { -1.36f, -1.25f, -0.75f, 0.00f, 0.75f, 1.25f, 1.36f };
            var pathHeights = new float[] {  0.022f, 0.032f, 0.042f, 0.048f, 0.042f, 0.032f, 0.022f };
            var pathU =       new float[] {  0.00f,  0.10f,  0.30f,  0.50f,  0.70f,  0.90f,  1.00f };

            int pVertsCount = pathOffsets.Length;
            int totalPathVerts = slicesCount * pVertsCount;

            var pathVertices = new Vector3[totalPathVerts];
            var pathUVs = new Vector2[totalPathVerts];
            var pathNormals = new Vector3[totalPathVerts];
            var pathTangents = new Vector4[totalPathVerts];

            const float pathMetersPerTile = 1.25f;

            for (int s = 0; s < slicesCount; s++)
            {
                Vector3 center = splinePoints[s];
                Vector3 fwd = splineTangents[s];
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                float vCoord = splineArcLengths[s] / pathMetersPerTile;

                for (int p = 0; p < pVertsCount; p++)
                {
                    int idx = s * pVertsCount + p;
                    float uOff = pathOffsets[p];
                    float yOff = pathHeights[p];

                    Vector3 pos = center + right * uOff;
                    float gy = SampleTerrainHeight(pos.x, pos.z);
                    float microNoise = (Mathf.PerlinNoise(pos.x * 0.9f, pos.z * 0.9f) - 0.5f) * 0.005f;

                    pos.y = gy + yOff + microNoise;
                    pathVertices[idx] = pos;
                    pathUVs[idx] = new Vector2(pathU[p] * (2.7f / pathMetersPerTile), vCoord);

                    if (p > 0 && p < pVertsCount - 1)
                    {
                        float du = pathOffsets[p + 1] - pathOffsets[p - 1];
                        float dy = pathHeights[p + 1] - pathHeights[p - 1];
                        Vector3 slope = (right * du + Vector3.up * dy).normalized;
                        pathNormals[idx] = Vector3.Cross(fwd, slope).normalized;
                    }
                    else
                    {
                        pathNormals[idx] = Vector3.up;
                    }
                    pathTangents[idx] = new Vector4(fwd.x, fwd.y, fwd.z, 1f);
                }
            }

            var pathTriangles = new List<int>();
            for (int s = 0; s < slicesCount - 1; s++)
            {
                int nextS = s + 1;
                int row1 = s * pVertsCount;
                int row2 = nextS * pVertsCount;

                for (int p = 0; p < pVertsCount - 1; p++)
                {
                    int a = row1 + p;
                    int b = row2 + p;
                    int c = row1 + p + 1;
                    int d = row2 + p + 1;

                    pathTriangles.Add(a); pathTriangles.Add(b); pathTriangles.Add(c);
                    pathTriangles.Add(c); pathTriangles.Add(b); pathTriangles.Add(d);
                }
            }

            var pathMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAssetPath);
            if (pathMesh == null)
            {
                pathMesh = new Mesh();
                pathMesh.name = "Garden_Pathway_Mesh";
                AssetDatabase.CreateAsset(pathMesh, MeshAssetPath);
            }
            pathMesh.Clear();
            pathMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            pathMesh.vertices = pathVertices;
            pathMesh.uv = pathUVs;
            pathMesh.normals = pathNormals;
            pathMesh.tangents = pathTangents;
            pathMesh.triangles = pathTriangles.ToArray();
            pathMesh.RecalculateNormals();
            pathMesh.RecalculateBounds();
            pathMesh.RecalculateTangents();
            EditorUtility.SetDirty(pathMesh);

            // 6. BUILD 3D GRANITE COBBLESTONE CURB EDGING MESH
            // 5 profile vertices per curb (Left and Right):
            // 0: Sub-surface outer drop into turf (-2cm)
            // 1: Outer top bevel (+6.5cm)
            // 2: Top face center (+7.0cm)
            // 3: Inner top bevel (+6.5cm)
            // 4: Inner drop meeting gravel (+1.8cm)
            const float curbW = 0.16f; // 16cm wide cobblestone curb block
            const float curbMetersPerTile = 0.80f; // 4 paver blocks per tile = 20cm per block

            var curbProfileOffsetsL = new float[] { -1.35f - curbW - 0.02f, -1.35f - curbW, -1.35f - curbW * 0.5f, -1.35f, -1.35f };
            var curbProfileHeightsL = new float[] { -0.015f, 0.065f, 0.070f, 0.065f, 0.018f };
            var curbProfileUL       = new float[] {  0.00f,  0.15f,  0.50f,  0.85f,  1.00f };

            var curbProfileOffsetsR = new float[] { 1.35f, 1.35f, 1.35f + curbW * 0.5f, 1.35f + curbW, 1.35f + curbW + 0.02f };
            var curbProfileHeightsR = new float[] { 0.018f, 0.065f, 0.070f, 0.065f, -0.015f };
            var curbProfileUR       = new float[] { 1.00f, 0.85f, 0.50f, 0.15f, 0.00f };

            int cVertsPerSide = 5;
            int totalCurbVerts = slicesCount * cVertsPerSide * 2; // Left + Right

            var curbVertices = new Vector3[totalCurbVerts];
            var curbUVs = new Vector2[totalCurbVerts];
            var curbNormals = new Vector3[totalCurbVerts];
            var curbTangents = new Vector4[totalCurbVerts];

            int leftBase = 0;
            int rightBase = slicesCount * cVertsPerSide;

            for (int s = 0; s < slicesCount; s++)
            {
                Vector3 center = splinePoints[s];
                Vector3 fwd = splineTangents[s];
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                float vCoord = splineArcLengths[s] / curbMetersPerTile;

                // Left curb
                for (int p = 0; p < cVertsPerSide; p++)
                {
                    int idx = leftBase + s * cVertsPerSide + p;
                    Vector3 pos = center + right * curbProfileOffsetsL[p];
                    float gy = SampleTerrainHeight(pos.x, pos.z);
                    pos.y = gy + curbProfileHeightsL[p];

                    curbVertices[idx] = pos;
                    curbUVs[idx] = new Vector2(curbProfileUL[p], vCoord);
                    curbTangents[idx] = new Vector4(fwd.x, fwd.y, fwd.z, 1f);
                }

                // Right curb
                for (int p = 0; p < cVertsPerSide; p++)
                {
                    int idx = rightBase + s * cVertsPerSide + p;
                    Vector3 pos = center + right * curbProfileOffsetsR[p];
                    float gy = SampleTerrainHeight(pos.x, pos.z);
                    pos.y = gy + curbProfileHeightsR[p];

                    curbVertices[idx] = pos;
                    curbUVs[idx] = new Vector2(curbProfileUR[p], vCoord);
                    curbTangents[idx] = new Vector4(fwd.x, fwd.y, fwd.z, 1f);
                }
            }

            var curbTriangles = new List<int>();
            // Left curb triangles (open strip)
            for (int s = 0; s < slicesCount - 1; s++)
            {
                int nextS = s + 1;
                int row1 = leftBase + s * cVertsPerSide;
                int row2 = leftBase + nextS * cVertsPerSide;

                for (int p = 0; p < cVertsPerSide - 1; p++)
                {
                    int a = row1 + p; int b = row2 + p; int c = row1 + p + 1; int d = row2 + p + 1;
                    curbTriangles.Add(a); curbTriangles.Add(b); curbTriangles.Add(c);
                    curbTriangles.Add(c); curbTriangles.Add(b); curbTriangles.Add(d);
                }
            }
            // Right curb triangles (open strip)
            for (int s = 0; s < slicesCount - 1; s++)
            {
                int nextS = s + 1;
                int row1 = rightBase + s * cVertsPerSide;
                int row2 = rightBase + nextS * cVertsPerSide;

                for (int p = 0; p < cVertsPerSide - 1; p++)
                {
                    int a = row1 + p; int b = row2 + p; int c = row1 + p + 1; int d = row2 + p + 1;
                    curbTriangles.Add(a); curbTriangles.Add(b); curbTriangles.Add(c);
                    curbTriangles.Add(c); curbTriangles.Add(b); curbTriangles.Add(d);
                }
            }

            var curbMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CurbMeshAssetPath);
            if (curbMesh == null)
            {
                curbMesh = new Mesh();
                curbMesh.name = "Garden_Pathway_Curb_Mesh";
                AssetDatabase.CreateAsset(curbMesh, CurbMeshAssetPath);
            }
            curbMesh.Clear();
            curbMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            curbMesh.vertices = curbVertices;
            curbMesh.uv = curbUVs;
            curbMesh.tangents = curbTangents;
            curbMesh.triangles = curbTriangles.ToArray();
            curbMesh.RecalculateNormals();
            curbMesh.RecalculateBounds();
            curbMesh.RecalculateTangents();
            EditorUtility.SetDirty(curbMesh);

            // 7. INSTANTIATE GAMEOBJECTS IN SCENE
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

            // A. Pathway
            var pathwayObj = gardenRoot.transform.Find("Garden_Pathway");
            GameObject pathGO = pathwayObj != null ? pathwayObj.gameObject : new GameObject("Garden_Pathway");
            pathGO.transform.SetParent(gardenRoot.transform, false);
            pathGO.transform.localPosition = Vector3.zero;
            pathGO.transform.localRotation = Quaternion.identity;
            pathGO.transform.localScale = Vector3.one;

            var mf = pathGO.GetComponent<MeshFilter>();
            if (mf == null) mf = pathGO.AddComponent<MeshFilter>();
            mf.sharedMesh = pathMesh;

            var mr = pathGO.GetComponent<MeshRenderer>();
            if (mr == null) mr = pathGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = pathMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            var mc = pathGO.GetComponent<MeshCollider>();
            if (mc == null) mc = pathGO.AddComponent<MeshCollider>();
            mc.sharedMesh = pathMesh;

            // B. Cobblestone Curb Edging
            var curbObj = pathGO.transform.Find("Garden_Pathway_Edging");
            GameObject curbGO = curbObj != null ? curbObj.gameObject : new GameObject("Garden_Pathway_Edging");
            curbGO.transform.SetParent(pathGO.transform, false);
            curbGO.transform.localPosition = Vector3.zero;
            curbGO.transform.localRotation = Quaternion.identity;
            curbGO.transform.localScale = Vector3.one;

            var curbMf = curbGO.GetComponent<MeshFilter>();
            if (curbMf == null) curbMf = curbGO.AddComponent<MeshFilter>();
            curbMf.sharedMesh = curbMesh;

            var curbMr = curbGO.GetComponent<MeshRenderer>();
            if (curbMr == null) curbMr = curbGO.AddComponent<MeshRenderer>();
            curbMr.sharedMaterial = curbMat;
            curbMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            curbMr.receiveShadows = true;

            var curbMc = curbGO.GetComponent<MeshCollider>();
            if (curbMc == null) curbMc = curbGO.AddComponent<MeshCollider>();
            curbMc.sharedMesh = curbMesh;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log($"[GardenPathway] Success! Created 3D pathway ({pathVertices.Length} verts) and cobblestone curb edging ({curbVertices.Length} verts).");
        }

        private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                (2.0f * p1) +
                (-p0 + p2) * t +
                (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
                (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
            );
        }

        private static Vector2 CatmullRomTangent(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                2.0f * (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t +
                3.0f * (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t2
            );
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

        private static void ConfigureTexture(string path, bool isNormal, bool sRGB)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 16;
            importer.sRGBTexture = sRGB;
            importer.SaveAndReimport();
        }
    }
}
#endif
