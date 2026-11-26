#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class GardeningStationBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string AlbedoPath = "Assets/Textures/Environment/GardeningStation/PottingBench_Wood_Albedo.png";
        private const string NormalPath = "Assets/Textures/Environment/GardeningStation/PottingBench_Wood_Normal.png";
        private const string SmoothnessPath = "Assets/Textures/Environment/GardeningStation/PottingBench_Wood_Smoothness.png";
        private const string MaterialPath = "Assets/Materials/Environment/GardeningStation/PottingBench_Wood_PBR.mat";

        // Placement in open garden turf accessible from the curved pathway
        // Distance to pathway centerline: ~4.5m; distance to path edge: ~2.9m
        private static readonly Vector3 StationBasePosition = new Vector3(9.5f, 0.0f, 3.0f);
        private static readonly Vector3 StationRotationEuler = new Vector3(0f, -65f, 0f);

        [MenuItem("NeuroCompanion/Build Phase 4.1 Gardening Station")]
        public static void BuildGardeningStation()
        {
            Debug.Log("[GardeningStationBuilder] Building Phase 4.1 Gardening / Planting Station...");

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Configure Textures
            ConfigureTexture(AlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(NormalPath, isNormal: true, sRGB: false);
            ConfigureTexture(SmoothnessPath, isNormal: false, sRGB: false);

            // 2. Setup PBR Material
            Material woodMat = SetupWoodMaterial();

            // 3. Locate or create [GARDEN_GAME] root
            GameObject gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                foreach (var r in scene.GetRootGameObjects())
                {
                    if (r.name == "[GARDEN_GAME]") { gardenRoot = r; break; }
                }
            }
            if (gardenRoot == null) gardenRoot = new GameObject("[GARDEN_GAME]");
            gardenRoot.SetActive(true);

            // Remove existing Gardening_Planting_Station if present
            Transform existing = gardenRoot.transform.Find("Gardening_Planting_Station");
            if (existing != null)
            {
                GameObject.DestroyImmediate(existing.gameObject);
            }

            // 4. Create Main Parent
            GameObject stationRoot = new GameObject("Gardening_Planting_Station");
            stationRoot.transform.SetParent(gardenRoot.transform, false);
            stationRoot.transform.position = StationBasePosition;
            stationRoot.transform.rotation = Quaternion.Euler(StationRotationEuler);

            // 5. Construct Modular Subgroups
            BuildStructureFrame(stationRoot.transform, woodMat);
            BuildWorkTabletop(stationRoot.transform, woodMat);
            BuildLowerStorageShelf(stationRoot.transform, woodMat);
            BuildUpperHutchTrellis(stationRoot.transform, woodMat);

            // Keep garden inactive for clean bootstrap
            gardenRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[GardeningStationBuilder] Gardening / Planting Station successfully built and placed!");
        }

        private static void ConfigureTexture(string path, bool isNormal, bool sRGB)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.textureType != (isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default))
            {
                importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                dirty = true;
            }
            if (importer.sRGBTexture != sRGB)
            {
                importer.sRGBTexture = sRGB;
                dirty = true;
            }
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                dirty = true;
            }
            if (dirty)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material SetupWoodMaterial()
        {
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetColor("_BaseColor", new Color(1.0f, 0.95f, 0.88f, 1.0f)); // Warm rich natural cedar tone

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.25f);
            }

            mat.SetFloat("_Smoothness", 0.22f); // Matte outdoor wood satin sheen
            mat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------------
        // 1. Structure Frame (4 sturdy timber legs + cross aprons)
        // ---------------------------------------------------------------------
        private static void BuildStructureFrame(Transform parent, Material mat)
        {
            GameObject frame = new GameObject("Structure_Frame");
            frame.transform.SetParent(parent, false);

            float legWidth = 0.075f; // 7.5cm square timber posts
            float halfX = 1.48f * 0.5f - legWidth * 0.5f; // X offset = ~0.70m
            float halfZ = 0.68f * 0.5f - legWidth * 0.5f; // Z offset = ~0.30m
            float frontLegHeight = 0.86f;

            // Front Legs
            CreateTimberPiece(frame.transform, "Leg_FrontLeft",
                new Vector3(-halfX, frontLegHeight * 0.5f, -halfZ),
                new Vector3(legWidth, frontLegHeight, legWidth), mat, hasCollider: true);

            CreateTimberPiece(frame.transform, "Leg_FrontRight",
                new Vector3(halfX, frontLegHeight * 0.5f, -halfZ),
                new Vector3(legWidth, frontLegHeight, legWidth), mat, hasCollider: true);

            // Rear Lower Legs (support up to tabletop apron; hutch posts continue above)
            CreateTimberPiece(frame.transform, "Leg_BackLeft",
                new Vector3(-halfX, frontLegHeight * 0.5f, halfZ),
                new Vector3(legWidth, frontLegHeight, legWidth), mat, hasCollider: true);

            CreateTimberPiece(frame.transform, "Leg_BackRight",
                new Vector3(halfX, frontLegHeight * 0.5f, halfZ),
                new Vector3(legWidth, frontLegHeight, legWidth), mat, hasCollider: true);

            // Tabletop Apron Rails (sturdy support directly underneath tabletop)
            float apronY = 0.83f;
            float apronH = 0.07f;
            float apronThick = 0.035f;

            CreateTimberPiece(frame.transform, "Apron_Front",
                new Vector3(0f, apronY, -halfZ),
                new Vector3(halfX * 2f - legWidth, apronH, apronThick), mat);

            CreateTimberPiece(frame.transform, "Apron_Back",
                new Vector3(0f, apronY, halfZ),
                new Vector3(halfX * 2f - legWidth, apronH, apronThick), mat);

            CreateTimberPiece(frame.transform, "Apron_Left",
                new Vector3(-halfX, apronY, 0f),
                new Vector3(apronThick, apronH, halfZ * 2f - legWidth), mat);

            CreateTimberPiece(frame.transform, "Apron_Right",
                new Vector3(halfX, apronY, 0f),
                new Vector3(apronThick, apronH, halfZ * 2f - legWidth), mat);

            // Mid-height Side Crossbraces for maximum physical stability
            float braceY = 0.45f;
            float braceH = 0.045f;
            CreateTimberPiece(frame.transform, "Crossbrace_Left",
                new Vector3(-halfX, braceY, 0f),
                new Vector3(apronThick, braceH, halfZ * 2f - legWidth), mat);

            CreateTimberPiece(frame.transform, "Crossbrace_Right",
                new Vector3(halfX, braceY, 0f),
                new Vector3(apronThick, braceH, halfZ * 2f - legWidth), mat);
        }

        // ---------------------------------------------------------------------
        // 2. Work Tabletop (Slatted planting surface with protective retaining rails)
        // ---------------------------------------------------------------------
        private static void BuildWorkTabletop(Transform parent, Material mat)
        {
            GameObject tabletop = new GameObject("Work_Tabletop");
            tabletop.transform.SetParent(parent, false);

            float tableWidth = 1.50f;
            float tableDepth = 0.70f;
            float tableTopHeight = 0.88f; // Ergonomic height for seniors
            float plankThickness = 0.028f; // 2.8cm solid timber

            // 6 Parallel Tabletop Planks with 0.8cm drainage gaps
            int plankCount = 6;
            float plankWidth = 0.108f;
            float gap = (tableDepth - (plankCount * plankWidth)) / (plankCount - 1);
            float startZ = -tableDepth * 0.5f + plankWidth * 0.5f;

            for (int i = 0; i < plankCount; i++)
            {
                float z = startZ + i * (plankWidth + gap);
                CreateTimberPiece(tabletop.transform, $"Tabletop_Plank_{i + 1}",
                    new Vector3(0f, tableTopHeight - plankThickness * 0.5f, z),
                    new Vector3(tableWidth, plankThickness, plankWidth), mat);
            }

            // Protective Safety Retaining Rails (prevents pots from slipping off)
            float railHeight = 0.10f; // 10cm safety lip
            float railThick = 0.024f;
            float railCenterY = tableTopHeight + railHeight * 0.5f;

            // Back Retaining Wall
            CreateTimberPiece(tabletop.transform, "Back_Retaining_Rail",
                new Vector3(0f, railCenterY, tableDepth * 0.5f - railThick * 0.5f),
                new Vector3(tableWidth, railHeight, railThick), mat);

            // Left & Right Side Safety Rails
            float sideRailLength = tableDepth - railThick;
            CreateTimberPiece(tabletop.transform, "Side_Retaining_Rail_Left",
                new Vector3(-tableWidth * 0.5f + railThick * 0.5f, railCenterY, -railThick * 0.5f),
                new Vector3(railThick, railHeight, sideRailLength), mat);

            CreateTimberPiece(tabletop.transform, "Side_Retaining_Rail_Right",
                new Vector3(tableWidth * 0.5f - railThick * 0.5f, railCenterY, -railThick * 0.5f),
                new Vector3(railThick, railHeight, sideRailLength), mat);

            // Front Edge Bullnose Soft Trim (rounded front for comfortable forearm resting)
            CreateTimberPiece(tabletop.transform, "Front_Comfort_Trim",
                new Vector3(0f, tableTopHeight - plankThickness * 0.5f, -tableDepth * 0.5f - 0.012f),
                new Vector3(tableWidth + 0.01f, plankThickness + 0.008f, 0.024f), mat);

            // Tabletop Surface BoxCollider for future activity pot placement
            var colliderGo = new GameObject("Tabletop_WorkSurface_Collider");
            colliderGo.transform.SetParent(tabletop.transform, false);
            colliderGo.transform.localPosition = new Vector3(0f, tableTopHeight, 0f);
            var box = colliderGo.AddComponent<BoxCollider>();
            box.size = new Vector3(tableWidth, 0.05f, tableDepth);
            box.center = new Vector3(0f, -0.025f, 0f);
        }

        // ---------------------------------------------------------------------
        // 3. Lower Storage Shelf (Slatted deck for spare pots & gardening trays)
        // ---------------------------------------------------------------------
        private static void BuildLowerStorageShelf(Transform parent, Material mat)
        {
            GameObject shelf = new GameObject("Lower_Storage_Shelf");
            shelf.transform.SetParent(parent, false);

            float shelfY = 0.22f; // 22cm above grass
            float shelfWidth = 1.36f;
            float shelfDepth = 0.56f;
            float plankThickness = 0.022f;
            float supportH = 0.05f;
            float supportThick = 0.035f;

            // Longitudinal Shelf Support Rails
            CreateTimberPiece(shelf.transform, "Shelf_Support_Front",
                new Vector3(0f, shelfY - supportH * 0.5f, -shelfDepth * 0.5f),
                new Vector3(shelfWidth, supportH, supportThick), mat);

            CreateTimberPiece(shelf.transform, "Shelf_Support_Back",
                new Vector3(0f, shelfY - supportH * 0.5f, shelfDepth * 0.5f),
                new Vector3(shelfWidth, supportH, supportThick), mat);

            // 5 Slatted Shelf Planks
            int count = 5;
            float plankW = 0.095f;
            float gap = (shelfDepth - (count * plankW)) / (count - 1);
            float startZ = -shelfDepth * 0.5f + plankW * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float z = startZ + i * (plankW + gap);
                CreateTimberPiece(shelf.transform, $"Shelf_Plank_{i + 1}",
                    new Vector3(0f, shelfY + plankThickness * 0.5f, z),
                    new Vector3(shelfWidth, plankThickness, plankW), mat);
            }
        }

        // ---------------------------------------------------------------------
        // 4. Upper Hutch & Tool Trellis (Back trellis, top utility shelf & pegs)
        // ---------------------------------------------------------------------
        private static void BuildUpperHutchTrellis(Transform parent, Material mat)
        {
            GameObject hutch = new GameObject("Upper_Hutch_Trellis");
            hutch.transform.SetParent(parent, false);

            float halfX = 1.48f * 0.5f - 0.075f * 0.5f;
            float backZ = 0.68f * 0.5f - 0.075f * 0.5f;
            float startY = 0.86f;
            float totalH = 1.45f;
            float postH = totalH - startY; // 0.59m post height above table
            float postWidth = 0.065f;

            // Vertical Hutch Posts extending upward from back legs
            CreateTimberPiece(hutch.transform, "Hutch_Post_Left",
                new Vector3(-halfX, startY + postH * 0.5f, backZ),
                new Vector3(postWidth, postH, postWidth), mat);

            CreateTimberPiece(hutch.transform, "Hutch_Post_Right",
                new Vector3(halfX, startY + postH * 0.5f, backZ),
                new Vector3(postWidth, postH, postWidth), mat);

            // Horizontal Back Trellis Slats (spaced between table and top shelf)
            int slatCount = 4;
            float slatYMin = 1.05f;
            float slatYMax = 1.34f;
            float slatThick = 0.018f;
            float slatHeight = 0.045f;

            for (int i = 0; i < slatCount; i++)
            {
                float y = Mathf.Lerp(slatYMin, slatYMax, (float)i / (slatCount - 1));
                CreateTimberPiece(hutch.transform, $"Trellis_Slat_{i + 1}",
                    new Vector3(0f, y, backZ),
                    new Vector3(halfX * 2f + postWidth, slatHeight, slatThick), mat);
            }

            // Top Utility Shelf (narrow top canopy shelf for small items / plant tags)
            float topShelfY = 1.44f;
            float topShelfW = 1.50f;
            float topShelfD = 0.20f;
            float topShelfThick = 0.022f;

            CreateTimberPiece(hutch.transform, "Top_Utility_Shelf",
                new Vector3(0f, topShelfY, backZ - topShelfD * 0.5f + postWidth * 0.5f),
                new Vector3(topShelfW, topShelfThick, topShelfD), mat);

            // Top Shelf Back Lip
            CreateTimberPiece(hutch.transform, "Top_Shelf_Lip",
                new Vector3(0f, topShelfY + 0.03f, backZ + postWidth * 0.5f - 0.01f),
                new Vector3(topShelfW, 0.045f, 0.018f), mat);

            // Hanging Peg Rail & Pegs for hanging gardening tools in future phases
            float pegRailY = 1.00f;
            CreateTimberPiece(hutch.transform, "Peg_Rail",
                new Vector3(0f, pegRailY, backZ - 0.015f),
                new Vector3(halfX * 2f, 0.045f, 0.022f), mat);

            int pegCount = 4;
            float pegSpan = 0.90f;
            for (int i = 0; i < pegCount; i++)
            {
                float x = -pegSpan * 0.5f + i * (pegSpan / (pegCount - 1));
                CreateTimberPiece(hutch.transform, $"Hanging_Peg_{i + 1}",
                    new Vector3(x, pegRailY, backZ - 0.055f),
                    new Vector3(0.022f, 0.022f, 0.07f), mat);
            }
        }

        // ---------------------------------------------------------------------
        // Helper: Create Timber Piece with UV Mapped Cube Mesh
        // ---------------------------------------------------------------------
        private static GameObject CreateTimberPiece(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, bool hasCollider = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            mf.sharedMesh = GenerateTimberBoxMesh(size);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            if (hasCollider)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = size;
                box.center = Vector3.zero;
            }

            return go;
        }

        /// <summary>
        /// Generates a box mesh with natural UV texture mapping along the length of the timber.
        /// </summary>
        private static Mesh GenerateTimberBoxMesh(Vector3 size)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Timber_Plank_Mesh";

            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            // 6 faces * 4 vertices = 24 vertices
            Vector3[] vertices = new Vector3[]
            {
                // Front (+Z)
                new Vector3(-hx, -hy,  hz), new Vector3( hx, -hy,  hz), new Vector3( hx,  hy,  hz), new Vector3(-hx,  hy,  hz),
                // Back (-Z)
                new Vector3( hx, -hy, -hz), new Vector3(-hx, -hy, -hz), new Vector3(-hx,  hy, -hz), new Vector3( hx,  hy, -hz),
                // Top (+Y)
                new Vector3(-hx,  hy,  hz), new Vector3( hx,  hy,  hz), new Vector3( hx,  hy, -hz), new Vector3(-hx,  hy, -hz),
                // Bottom (-Y)
                new Vector3(-hx, -hy, -hz), new Vector3( hx, -hy, -hz), new Vector3( hx, -hy,  hz), new Vector3(-hx, -hy,  hz),
                // Right (+X)
                new Vector3( hx, -hy,  hz), new Vector3( hx, -hy, -hz), new Vector3( hx,  hy, -hz), new Vector3( hx,  hy,  hz),
                // Left (-X)
                new Vector3(-hx, -hy, -hz), new Vector3(-hx, -hy,  hz), new Vector3(-hx,  hy,  hz), new Vector3(-hx,  hy, -hz)
            };

            Vector3[] normals = new Vector3[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                Vector3.down, Vector3.down, Vector3.down, Vector3.down,
                Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                Vector3.left, Vector3.left, Vector3.left, Vector3.left
            };

            // UV mapping scaled naturally by world dimension (1 meter = 1 UV repeat)
            Vector2[] uvs = new Vector2[24];
            // Front (+Z)
            uvs[0] = new Vector2(0, 0); uvs[1] = new Vector2(size.x, 0); uvs[2] = new Vector2(size.x, size.y); uvs[3] = new Vector2(0, size.y);
            // Back (-Z)
            uvs[4] = new Vector2(0, 0); uvs[5] = new Vector2(size.x, 0); uvs[6] = new Vector2(size.x, size.y); uvs[7] = new Vector2(0, size.y);
            // Top (+Y)
            uvs[8] = new Vector2(0, 0); uvs[9] = new Vector2(size.x, 0); uvs[10] = new Vector2(size.x, size.z); uvs[11] = new Vector2(0, size.z);
            // Bottom (-Y)
            uvs[12] = new Vector2(0, 0); uvs[13] = new Vector2(size.x, 0); uvs[14] = new Vector2(size.x, size.z); uvs[15] = new Vector2(0, size.z);
            // Right (+X)
            uvs[16] = new Vector2(0, 0); uvs[17] = new Vector2(size.z, 0); uvs[18] = new Vector2(size.z, size.y); uvs[19] = new Vector2(0, size.y);
            // Left (-X)
            uvs[20] = new Vector2(0, 0); uvs[21] = new Vector2(size.z, 0); uvs[22] = new Vector2(size.z, size.y); uvs[23] = new Vector2(0, size.y);

            int[] triangles = new int[]
            {
                0, 2, 1,  0, 3, 2,
                4, 6, 5,  4, 7, 6,
                8, 10, 9, 8, 11, 10,
                12, 14, 13, 12, 15, 14,
                16, 18, 17, 16, 19, 18,
                20, 22, 21, 20, 23, 22
            };

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
#endif