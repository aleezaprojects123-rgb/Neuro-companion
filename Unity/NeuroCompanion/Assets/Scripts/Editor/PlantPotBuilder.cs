#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class PlantPotBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string ClayAlbedoPath = "Assets/Textures/Environment/PlantPots/Terracotta_Clay_Albedo.png";
        private const string ClayNormalPath = "Assets/Textures/Environment/PlantPots/Terracotta_Clay_Normal.png";
        private const string SoilAlbedoPath = "Assets/Textures/Environment/PlantPots/Potting_Soil_Albedo.png";
        private const string SoilNormalPath = "Assets/Textures/Environment/PlantPots/Potting_Soil_Normal.png";

        private const string ClayMatPath = "Assets/Materials/Environment/PlantPots/Terracotta_Clay_PBR.mat";
        private const string SoilMatPath = "Assets/Materials/Environment/PlantPots/Potting_Soil_PBR.mat";

        [MenuItem("NeuroCompanion/Build Phase 4.2 Plant Pots")]
        public static void BuildPlantPots()
        {
            Debug.Log("[PlantPotBuilder] Building Phase 4.2 Realistic Terracotta Plant Pots...");

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Configure Textures
            ConfigureTexture(ClayAlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(ClayNormalPath, isNormal: true, sRGB: false);
            ConfigureTexture(SoilAlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(SoilNormalPath, isNormal: true, sRGB: false);

            // 2. Setup PBR Materials
            Material clayMat = SetupClayMaterial();
            Material soilMat = SetupSoilMaterial();

            // 3. Locate Gardening_Planting_Station
            GameObject station = GameObject.Find("Gardening_Planting_Station");
            if (station == null)
            {
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var go in all)
                {
                    if (go.name == "Gardening_Planting_Station") { station = go; break; }
                }
            }

            if (station == null)
            {
                Debug.LogError("[PlantPotBuilder] Gardening_Planting_Station not found! Please build Phase 4.1 first.");
                return;
            }

            // Remove existing Plant_Pots if present
            Transform existingPots = station.transform.Find("Plant_Pots");
            if (existingPots != null)
            {
                GameObject.DestroyImmediate(existingPots.gameObject);
            }

            // 4. Create Parent GameObject Plant_Pots under Gardening_Planting_Station
            GameObject potsRoot = new GameObject("Plant_Pots");
            potsRoot.transform.SetParent(station.transform, false);
            potsRoot.transform.localPosition = Vector3.zero;
            potsRoot.transform.localRotation = Quaternion.identity;

            // Generate Pre-calculated Pot Meshes
            Mesh medPotMesh = GeneratePotMesh(bottomRadius: 0.07f, topRadius: 0.10f, rimRadius: 0.115f, height: 0.20f, rimHeight: 0.035f, wallThickness: 0.012f, segments: 24);
            Mesh medSoilMesh = GenerateSoilDiskMesh(radius: 0.092f, segments: 24);

            Mesh smallPotMesh = GeneratePotMesh(bottomRadius: 0.04f, topRadius: 0.058f, rimRadius: 0.068f, height: 0.115f, rimHeight: 0.022f, wallThickness: 0.008f, segments: 20);
            Mesh smallSoilMesh = GenerateSoilDiskMesh(radius: 0.053f, segments: 20);

            // -----------------------------------------------------------------
            // Tabletop Surface (Height = 0.88m)
            // Left-rear arrangement preserves generous open workspace on center and right
            // -----------------------------------------------------------------
            // 1. Medium Pot 01 (Tabletop Left-Rear, Empty)
            CreatePotInstance(potsRoot.transform, "PlantPot_Medium_01", medPotMesh, null,
                new Vector3(-0.48f, 0.88f, 0.16f), Quaternion.Euler(0f, 15f, 0f), clayMat, soilMat, hasSoil: false);

            // 2. Small Pot 01 (Tabletop Left-Mid, Filled with Rich Soil)
            CreatePotInstance(potsRoot.transform, "PlantPot_Small_01", smallPotMesh, smallSoilMesh,
                new Vector3(-0.28f, 0.88f, 0.20f), Quaternion.Euler(0f, -30f, 0f), clayMat, soilMat, hasSoil: true, soilRelY: 0.095f);

            // -----------------------------------------------------------------
            // Lower Storage Shelf (Height = 0.242m)
            // -----------------------------------------------------------------
            // 3. Medium Pot 02 (Lower Shelf Left, Empty)
            CreatePotInstance(potsRoot.transform, "PlantPot_Medium_02", medPotMesh, null,
                new Vector3(-0.42f, 0.242f, -0.05f), Quaternion.Euler(0f, 45f, 0f), clayMat, soilMat, hasSoil: false);

            // 4. Medium Pot 03 (Lower Shelf Left-Center, Filled with Soil)
            CreatePotInstance(potsRoot.transform, "PlantPot_Medium_03", medPotMesh, medSoilMesh,
                new Vector3(-0.16f, 0.242f, -0.05f), Quaternion.Euler(0f, -10f, 0f), clayMat, soilMat, hasSoil: true, soilRelY: 0.168f);

            // 5. Small Pot 02 (Lower Shelf Right-Center, Empty Seedling Pot)
            CreatePotInstance(potsRoot.transform, "PlantPot_Small_02", smallPotMesh, null,
                new Vector3(0.24f, 0.242f, -0.08f), Quaternion.Euler(0f, 60f, 0f), clayMat, soilMat, hasSoil: false);

            // 6. Small Pot 03 (Lower Shelf Right, Empty Seedling Pot)
            CreatePotInstance(potsRoot.transform, "PlantPot_Small_03", smallPotMesh, null,
                new Vector3(0.40f, 0.242f, -0.08f), Quaternion.Euler(0f, 20f, 0f), clayMat, soilMat, hasSoil: false);

            // -----------------------------------------------------------------
            // Upper Utility Shelf (Top surface Y = 1.451m, Z = 0.235m)
            // -----------------------------------------------------------------
            // 7. Small Pot 04 (Top Shelf Right, Filled with Soil & ready for plant markers)
            CreatePotInstance(potsRoot.transform, "PlantPot_Small_04", smallPotMesh, smallSoilMesh,
                new Vector3(0.35f, 1.451f, 0.235f), Quaternion.Euler(0f, -45f, 0f), clayMat, soilMat, hasSoil: true, soilRelY: 0.095f);

            // Save Scene
            GameObject gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot != null) gardenRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[PlantPotBuilder] Phase 4.2 Plant Pots successfully built and verified!");
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
            if (dirty) importer.SaveAndReimport();
        }

        private static Material SetupClayMaterial()
        {
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ClayAlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ClayNormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(ClayMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, ClayMatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetColor("_BaseColor", new Color(1.05f, 0.95f, 0.90f, 1.0f)); // Warm natural earthy terracotta tone

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.2f);
            }

            mat.SetFloat("_Smoothness", 0.18f); // Matte fired clay
            mat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SetupSoilMaterial()
        {
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SoilAlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SoilNormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SoilMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, SoilMatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetColor("_BaseColor", Color.white);

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.4f);
            }

            mat.SetFloat("_Smoothness", 0.10f); // Crumbly matte organic soil
            mat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void CreatePotInstance(Transform parent, string name, Mesh potMesh, Mesh soilMesh,
            Vector3 localPos, Quaternion localRot, Material clayMat, Material soilMat, bool hasSoil, float soilRelY = 0f)
        {
            GameObject potGo = new GameObject(name);
            potGo.transform.SetParent(parent, false);
            potGo.transform.localPosition = localPos;
            potGo.transform.localRotation = localRot;
            potGo.transform.localScale = Vector3.one;

            var mf = potGo.AddComponent<MeshFilter>();
            var mr = potGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = potMesh;
            mr.sharedMaterial = clayMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            // Accurate MeshCollider or BoxCollider for future activity interaction
            var col = potGo.AddComponent<MeshCollider>();
            col.sharedMesh = potMesh;
            col.convex = true;

            if (hasSoil && soilMesh != null)
            {
                GameObject soilGo = new GameObject("Potting_Soil");
                soilGo.transform.SetParent(potGo.transform, false);
                soilGo.transform.localPosition = new Vector3(0f, soilRelY, 0f);
                soilGo.transform.localRotation = Quaternion.identity;
                soilGo.transform.localScale = Vector3.one;

                var smf = soilGo.AddComponent<MeshFilter>();
                var smr = soilGo.AddComponent<MeshRenderer>();
                smf.sharedMesh = soilMesh;
                smr.sharedMaterial = soilMat;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
            }
        }

        /// <summary>
        /// Generates an authentic terracotta pot with a tapered body, rolled rim collar, and hollow interior.
        /// </summary>
        private static Mesh GeneratePotMesh(float bottomRadius, float topRadius, float rimRadius, float height, float rimHeight, float wallThickness, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Terracotta_Pot_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Profile rings (from bottom to top outside, over rim, then down inside to inner bottom)
            // Ring 0: Bottom outside edge (Y = 0, R = bottomRadius)
            // Ring 1: Under-rim neck (Y = height - rimHeight, R = Lerp(bottom, top, (h-rh)/h))
            // Ring 2: Rim bottom flare (Y = height - rimHeight, R = rimRadius)
            // Ring 3: Rim top outer edge (Y = height, R = rimRadius)
            // Ring 4: Rim top inner edge (Y = height, R = topRadius - wallThickness)
            // Ring 5: Inner bottom edge (Y = wallThickness, R = bottomRadius - wallThickness)

            float neckRadius = Mathf.Lerp(bottomRadius, topRadius, (height - rimHeight) / height);
            float innerTopRadius = topRadius - wallThickness;
            float innerBottomRadius = Mathf.Max(0.015f, bottomRadius - wallThickness);

            var rings = new (float y, float r, bool isInside)[]
            {
                (0f, bottomRadius, false),
                (height - rimHeight, neckRadius, false),
                (height - rimHeight, rimRadius, false),
                (height, rimRadius, false),
                (height, innerTopRadius, true),
                (wallThickness, innerBottomRadius, true)
            };

            // Build cylinder rings
            int ringCount = rings.Length;
            for (int ring = 0; ring < ringCount; ring++)
            {
                float y = rings[ring].y;
                float r = rings[ring].r;
                float v = (float)ring / (ringCount - 1);

                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    verts.Add(new Vector3(cos * r, y, sin * r));

                    Vector3 n;
                    if (!rings[ring].isInside)
                        n = new Vector3(cos, (ring == 0 ? -0.2f : (ring == 3 ? 0.3f : 0f)), sin).normalized;
                    else
                        n = new Vector3(-cos, (ring == 4 ? 0.5f : 0f), -sin).normalized;

                    norms.Add(n);
                    uvs.Add(new Vector2((float)s / segments * 2f, v));
                }
            }

            int stride = segments + 1;
            // Generate side quads between consecutive rings
            for (int ring = 0; ring < ringCount - 1; ring++)
            {
                int r0 = ring * stride;
                int r1 = (ring + 1) * stride;

                for (int s = 0; s < segments; s++)
                {
                    int a = r0 + s;
                    int b = r1 + s;
                    int c = r1 + s + 1;
                    int d = r0 + s + 1;

                    if (ring < 4) // Outside faces
                    {
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(a); tris.Add(c); tris.Add(d);
                    }
                    else // Inside faces (inverted winding)
                    {
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(a); tris.Add(d); tris.Add(c);
                    }
                }
            }

            // Bottom Outside Disk (cap)
            int bottomCenterIdx = verts.Count;
            verts.Add(Vector3.zero);
            norms.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s < segments; s++)
            {
                tris.Add(bottomCenterIdx);
                tris.Add(s + 1);
                tris.Add(s);
            }

            // Bottom Inside Disk (cap)
            int innerCapCenterIdx = verts.Count;
            verts.Add(new Vector3(0f, wallThickness, 0f));
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            int innerRingStart = 5 * stride;
            for (int s = 0; s < segments; s++)
            {
                tris.Add(innerCapCenterIdx);
                tris.Add(innerRingStart + s);
                tris.Add(innerRingStart + s + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>
        /// Generates a slightly domed potting soil disk with realistic crumbly surface.
        /// </summary>
        private static Mesh GenerateSoilDiskMesh(float radius, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Potting_Soil_Disk";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            int centerIdx = 0;
            verts.Add(new Vector3(0f, 0.005f, 0f)); // Slight mound in center
            norms.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s <= segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                float jitter = Mathf.Sin(s * 1.5f) * 0.002f;
                verts.Add(new Vector3(cos * radius, jitter, sin * radius));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            for (int s = 0; s < segments; s++)
            {
                tris.Add(centerIdx);
                tris.Add(s + 1);
                tris.Add(s + 2);
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
#endif