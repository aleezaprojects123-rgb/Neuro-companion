#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class GardenBoundaryBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string HedgeAlbedoPath = "Assets/Textures/Environment/Boundaries/Hedge_Leaf_Albedo.png";
        private const string HedgeNormalPath = "Assets/Textures/Environment/Boundaries/Hedge_Leaf_Normal.png";
        private const string WoodAlbedoPath  = "Assets/Textures/Environment/Boundaries/Cedar_Wood_Albedo.png";
        private const string WoodNormalPath  = "Assets/Textures/Environment/Boundaries/Cedar_Wood_Normal.png";

        private const string HedgeMatPath    = "Assets/Materials/Environment/Boundaries/Garden_Hedge_PBR.mat";
        private const string WoodMatPath     = "Assets/Materials/Environment/Boundaries/Garden_Timber_PBR.mat";

        private const string HedgeMeshPath   = "Assets/Materials/Environment/Boundaries/Garden_Hedge_Combined.asset";
        private const string WoodMeshPath    = "Assets/Materials/Environment/Boundaries/Garden_Timber_Combined.asset";

        [MenuItem("NeuroCompanion/Build Realistic Garden Boundaries")]
        public static void BuildBoundaries()
        {
            Debug.Log("[GardenBoundary] Building prominent, clearly recognizable natural garden fence & low hedge...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CONFIGURE TEXTURES
            ConfigureTexture(HedgeAlbedoPath, false);
            ConfigureTexture(HedgeNormalPath, true);
            ConfigureTexture(WoodAlbedoPath, false);
            ConfigureTexture(WoodNormalPath, true);

            // 2. CREATE PBR MATERIALS
            var hedgeMat = CreatePbrMaterial(HedgeMatPath, HedgeAlbedoPath, HedgeNormalPath, new Vector2(3f, 3f), 0.12f, Color.white);
            // Rich warm honey-cedar color tint on timber for high contrast and prominent visibility
            var woodMat  = CreatePbrMaterial(WoodMatPath, WoodAlbedoPath, WoodNormalPath, new Vector2(1f, 3f), 0.25f, new Color(1.15f, 0.95f, 0.78f, 1f));

            // 3. GENERATE PERIMETER (Spacious 64m x 64m garden perimeter at halfExtent = 32m)
            const float halfExtent = 32f;
            const float cornerRadius = 5f;
            const float postSpacing = 2.4f; // Closer spacing for dense, substantial ranch fence line

            var perimeterPoints = GeneratePerimeterSegments(halfExtent, cornerRadius, postSpacing);

            // 4. MESH COMBINE LISTS
            var hedgeCombines = new List<CombineInstance>();
            var woodCombines = new List<CombineInstance>();

            // SUBSTANTIAL, LARGE RUSTIC ESTATE FENCE — clearly big and prominent across the horizon
            // Posts: 38cm wide x 2.60m tall — massive, chunky estate timber
            // Rails: 22cm tall x 14cm deep — bold heavy timber planks
            const float postWidth  = 0.38f;  // 38cm thick squared timber post
            const float postHeight = 2.60f;  // 2.60m tall — prominent human/estate height
            const float railHeight = 0.22f;  // 22cm tall bold timber board rail
            const float railDepth  = 0.14f;  // 14cm deep (heavy solid plank)
            const float hedgeH     = 0.85f;  // 85cm substantial lush foundation hedge
            const float hedgeW     = 0.80f;  // 80cm wide lush dense hedge

            var postMesh = CreateCappedPostMesh(postWidth, postHeight);
            var gatePostMesh = CreateCappedPostMesh(postWidth * 1.4f, postHeight + 0.40f); // 53cm thick, 3.0m tall gate post
            var railUnitMesh = CreateBoxMesh(railDepth, railHeight, 1f);
            var hedgeUnitMesh = CreateRoundedBoxMesh(hedgeW, hedgeH, 1f);

            for (int i = 0; i < perimeterPoints.Count; i++)
            {
                var pt = perimeterPoints[i];
                var nextPt = perimeterPoints[(i + 1) % perimeterPoints.Count];

                bool isGateEntrance = IsEntranceOpening(pt.position, nextPt.position);
                bool isGatePost = IsGatePostPosition(pt.position);

                // Entrance post flanking
                if (isGatePost)
                {
                    float gateGroundY = SampleTerrainHeight(pt.position.x, pt.position.z);
                    Vector3 gpPos = new Vector3(pt.position.x, gateGroundY + (postHeight + 0.25f) * 0.5f, pt.position.z);
                    woodCombines.Add(new CombineInstance { mesh = gatePostMesh, transform = Matrix4x4.TRS(gpPos, Quaternion.identity, Vector3.one) });
                }

                // If this segment is part of the opening, skip hedge and rails
                if (isGateEntrance)
                    continue;

                Vector3 midPoint = (pt.position + nextPt.position) * 0.5f;
                Vector3 forward = (nextPt.position - pt.position);
                float segLength = forward.magnitude;
                if (segLength < 0.01f) continue;
                forward.Normalize();
                Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);

                float groundY = SampleTerrainHeight(midPoint.x, midPoint.z);

                // --- 1. Low Base Hedge ---
                Vector3 hedgePos = new Vector3(midPoint.x, groundY + hedgeH * 0.5f, midPoint.z);
                var hedgeMtx = Matrix4x4.TRS(hedgePos, rot, new Vector3(1f, 1f, segLength * 1.05f));
                hedgeCombines.Add(new CombineInstance { mesh = hedgeUnitMesh, transform = hedgeMtx });

                // --- 2. Timber Post ---
                if (!isGatePost)
                {
                    float postGroundY = SampleTerrainHeight(pt.position.x, pt.position.z);
                    Vector3 postPos = new Vector3(pt.position.x, postGroundY + postHeight * 0.5f, pt.position.z);
                    woodCombines.Add(new CombineInstance { mesh = postMesh, transform = Matrix4x4.TRS(postPos, Quaternion.identity, Vector3.one) });
                }

                // --- 3. Three Bold Horizontal Rails (prominent and heavy across the horizon) ---
                // Top Rail at 2.25m
                Vector3 r1Pos = new Vector3(midPoint.x, groundY + 2.25f, midPoint.z);
                woodCombines.Add(new CombineInstance { mesh = railUnitMesh, transform = Matrix4x4.TRS(r1Pos, rot, new Vector3(1f, 1f, segLength)) });

                // Middle Rail at 1.50m
                Vector3 r2Pos = new Vector3(midPoint.x, groundY + 1.50f, midPoint.z);
                woodCombines.Add(new CombineInstance { mesh = railUnitMesh, transform = Matrix4x4.TRS(r2Pos, rot, new Vector3(1f, 1f, segLength)) });

                // Lower Rail at 0.75f
                Vector3 r3Pos = new Vector3(midPoint.x, groundY + 0.75f, midPoint.z);
                woodCombines.Add(new CombineInstance { mesh = railUnitMesh, transform = Matrix4x4.TRS(r3Pos, rot, new Vector3(1f, 1f, segLength)) });
            }

            // 5. COMBINE AND SAVE MESHES
            var combinedHedgeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(HedgeMeshPath);
            if (combinedHedgeMesh == null)
            {
                combinedHedgeMesh = new Mesh();
                combinedHedgeMesh.name = "Garden_Hedge_Combined";
                AssetDatabase.CreateAsset(combinedHedgeMesh, HedgeMeshPath);
            }
            combinedHedgeMesh.Clear();
            combinedHedgeMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedHedgeMesh.CombineMeshes(hedgeCombines.ToArray(), true, true);
            combinedHedgeMesh.RecalculateNormals();
            combinedHedgeMesh.RecalculateBounds();
            EditorUtility.SetDirty(combinedHedgeMesh);

            var combinedWoodMesh = AssetDatabase.LoadAssetAtPath<Mesh>(WoodMeshPath);
            if (combinedWoodMesh == null)
            {
                combinedWoodMesh = new Mesh();
                combinedWoodMesh.name = "Garden_Timber_Combined";
                AssetDatabase.CreateAsset(combinedWoodMesh, WoodMeshPath);
            }
            combinedWoodMesh.Clear();
            combinedWoodMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedWoodMesh.CombineMeshes(woodCombines.ToArray(), true, true);
            combinedWoodMesh.RecalculateNormals();
            combinedWoodMesh.RecalculateBounds();
            EditorUtility.SetDirty(combinedWoodMesh);

            // 6. INSTANTIATE IN SCENE
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

            var boundRoot = gardenRoot.transform.Find("Garden_Boundaries");
            GameObject boundGo = boundRoot != null ? boundRoot.gameObject : new GameObject("Garden_Boundaries");
            boundGo.transform.SetParent(gardenRoot.transform, false);
            boundGo.transform.position = Vector3.zero;

            // Hedge Child
            var hedgeTr = boundGo.transform.Find("Hedge_Perimeter");
            GameObject hedgeGo = hedgeTr != null ? hedgeTr.gameObject : new GameObject("Hedge_Perimeter");
            hedgeGo.transform.SetParent(boundGo.transform, false);
            var mfH = hedgeGo.GetComponent<MeshFilter>(); if (mfH == null) mfH = hedgeGo.AddComponent<MeshFilter>();
            mfH.sharedMesh = combinedHedgeMesh;
            var mrH = hedgeGo.GetComponent<MeshRenderer>(); if (mrH == null) mrH = hedgeGo.AddComponent<MeshRenderer>();
            mrH.sharedMaterial = hedgeMat;
            mrH.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrH.receiveShadows = true;

            var mcH = hedgeGo.GetComponent<MeshCollider>(); if (mcH == null) mcH = hedgeGo.AddComponent<MeshCollider>();
            mcH.sharedMesh = combinedHedgeMesh;

            // Timber Fence Child
            var woodTr = boundGo.transform.Find("Timber_Fence_Perimeter");
            GameObject woodGo = woodTr != null ? woodTr.gameObject : new GameObject("Timber_Fence_Perimeter");
            woodGo.transform.SetParent(boundGo.transform, false);
            var mfW = woodGo.GetComponent<MeshFilter>(); if (mfW == null) mfW = woodGo.AddComponent<MeshFilter>();
            mfW.sharedMesh = combinedWoodMesh;
            var mrW = woodGo.GetComponent<MeshRenderer>(); if (mrW == null) mrW = woodGo.AddComponent<MeshRenderer>();
            mrW.sharedMaterial = woodMat;
            mrW.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrW.receiveShadows = true;

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[GardenBoundary] Visible, prominent fence and low hedge built successfully!");
        }

        private static bool IsEntranceOpening(Vector3 a, Vector3 b)
        {
            // South entrance opening (Z ≈ -32, X between -3.2 and 3.2)
            if (Mathf.Abs(a.z + 32f) < 2f && Mathf.Abs(b.z + 32f) < 2f)
            {
                if (Mathf.Abs(a.x) < 3.2f || Mathf.Abs(b.x) < 3.2f) return true;
            }
            // North entrance opening (Z ≈ +32, X between -3.2 and 3.2)
            if (Mathf.Abs(a.z - 32f) < 2f && Mathf.Abs(b.z - 32f) < 2f)
            {
                if (Mathf.Abs(a.x) < 3.2f || Mathf.Abs(b.x) < 3.2f) return true;
            }
            return false;
        }

        private static bool IsGatePostPosition(Vector3 pos)
        {
            // Gate posts flank the opening at X ≈ ±3.2 on South and North
            if (Mathf.Abs(Mathf.Abs(pos.z) - 32f) < 2f)
            {
                if (Mathf.Abs(Mathf.Abs(pos.x) - 3.2f) < 1.0f) return true;
            }
            return false;
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

        private struct PerimeterPoint
        {
            public Vector3 position;
        }

        private static List<PerimeterPoint> GeneratePerimeterSegments(float halfExtent, float cornerRadius, float stepSize)
        {
            var points = new List<PerimeterPoint>();
            float straight = halfExtent - cornerRadius;

            // South edge (-straight to straight at Z = -halfExtent)
            for (float x = -straight; x <= straight; x += stepSize)
                points.Add(new PerimeterPoint { position = new Vector3(x, 0, -halfExtent) });

            // Corner SE arc
            for (float a = 270f + 15f; a < 360f; a += 15f)
            {
                float rad = a * Mathf.Deg2Rad;
                points.Add(new PerimeterPoint {
                    position = new Vector3(straight + Mathf.Cos(rad) * cornerRadius, 0, -straight + Mathf.Sin(rad) * cornerRadius)
                });
            }

            // East edge
            for (float z = -straight; z <= straight; z += stepSize)
                points.Add(new PerimeterPoint { position = new Vector3(halfExtent, 0, z) });

            // Corner NE arc
            for (float a = 15f; a < 90f; a += 15f)
            {
                float rad = a * Mathf.Deg2Rad;
                points.Add(new PerimeterPoint {
                    position = new Vector3(straight + Mathf.Cos(rad) * cornerRadius, 0, straight + Mathf.Sin(rad) * cornerRadius)
                });
            }

            // North edge
            for (float x = straight; x >= -straight; x -= stepSize)
                points.Add(new PerimeterPoint { position = new Vector3(x, 0, halfExtent) });

            // Corner NW arc
            for (float a = 90f + 15f; a < 180f; a += 15f)
            {
                float rad = a * Mathf.Deg2Rad;
                points.Add(new PerimeterPoint {
                    position = new Vector3(-straight + Mathf.Cos(rad) * cornerRadius, 0, straight + Mathf.Sin(rad) * cornerRadius)
                });
            }

            // West edge
            for (float z = straight; z >= -straight; z -= stepSize)
                points.Add(new PerimeterPoint { position = new Vector3(-halfExtent, 0, z) });

            // Corner SW arc
            for (float a = 180f + 15f; a < 270f; a += 15f)
            {
                float rad = a * Mathf.Deg2Rad;
                points.Add(new PerimeterPoint {
                    position = new Vector3(-straight + Mathf.Cos(rad) * cornerRadius, 0, -straight + Mathf.Sin(rad) * cornerRadius)
                });
            }

            return points;
        }

        private static Mesh CreateCappedPostMesh(float width, float height)
        {
            var m = new Mesh();
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float capH = width * 0.4f;

            // 8 body vertices + 1 pyramid apex vertex
            var verts = new Vector3[] {
                new Vector3(-hw, -hh, -hw), new Vector3(hw, -hh, -hw), new Vector3(hw, hh, -hw), new Vector3(-hw, hh, -hw),
                new Vector3(-hw, hh, hw),   new Vector3(hw, hh, hw),   new Vector3(hw, -hh, hw), new Vector3(-hw, -hh, hw),
                new Vector3(0f, hh + capH, 0f) // Pyramid apex
            };

            var tris = new int[] {
                // Post sides
                0, 2, 1, 0, 3, 2, // front
                1, 5, 6, 1, 2, 5, // right
                5, 4, 7, 5, 7, 6, // back
                0, 7, 4, 0, 4, 3, // left
                0, 6, 7, 0, 1, 6, // bottom
                // Pyramid cap
                2, 8, 3, // front cap
                5, 8, 2, // right cap
                4, 8, 5, // back cap
                3, 8, 4  // left cap
            };

            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            return m;
        }

        private static Mesh CreateBoxMesh(float width, float height, float length)
        {
            var m = new Mesh();
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float hl = length * 0.5f;

            m.vertices = new Vector3[] {
                new Vector3(-hw, -hh, -hl), new Vector3(hw, -hh, -hl), new Vector3(hw, hh, -hl), new Vector3(-hw, hh, -hl),
                new Vector3(-hw, hh, hl), new Vector3(hw, hh, hl), new Vector3(hw, -hh, hl), new Vector3(-hw, -hh, hl)
            };
            m.triangles = new int[] {
                0, 2, 1, 0, 3, 2,
                2, 3, 4, 2, 4, 5,
                1, 2, 5, 1, 5, 6,
                0, 7, 4, 0, 4, 3,
                5, 4, 7, 5, 7, 6,
                0, 6, 7, 0, 1, 6
            };
            m.RecalculateNormals();
            return m;
        }

        private static Mesh CreateRoundedBoxMesh(float width, float height, float length)
        {
            var m = new Mesh();
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float hl = length * 0.5f;

            m.vertices = new Vector3[] {
                new Vector3(-hw, -hh, -hl), new Vector3(hw, -hh, -hl),
                new Vector3(hw * 0.85f, hh, -hl), new Vector3(-hw * 0.85f, hh, -hl),
                new Vector3(-hw * 0.85f, hh, hl), new Vector3(hw * 0.85f, hh, hl),
                new Vector3(hw, -hh, hl), new Vector3(-hw, -hh, hl)
            };
            m.triangles = new int[] {
                0, 2, 1, 0, 3, 2,
                2, 3, 4, 2, 4, 5,
                1, 2, 5, 1, 5, 6,
                0, 7, 4, 0, 4, 3,
                5, 4, 7, 5, 7, 6,
                0, 6, 7, 0, 1, 6
            };
            m.RecalculateNormals();
            return m;
        }

        private static Material CreatePbrMaterial(string matPath, string albedoPath, string normalPath, Vector2 tiling, float smoothness, Color tint)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            var alb = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

            mat.SetTexture("_BaseMap", alb);
            mat.SetTextureScale("_BaseMap", tiling);
            mat.SetColor("_BaseColor", tint);

            if (nrm != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", nrm);
                mat.SetTextureScale("_BumpMap", tiling);
                mat.SetFloat("_BumpScale", 1.0f);
            }

            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(mat);
            return mat;
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
