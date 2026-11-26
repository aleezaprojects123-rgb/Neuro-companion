#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class GardeningTrowelBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SteelAlbedoPath = "Assets/Textures/Environment/GardeningTools/Trowel_Steel_Albedo.png";
        private const string SteelNormalPath = "Assets/Textures/Environment/GardeningTools/Trowel_Steel_Normal.png";
        private const string WoodAlbedoPath = "Assets/Textures/Environment/GardeningTools/Trowel_Handle_Wood_Albedo.png";
        private const string WoodNormalPath = "Assets/Textures/Environment/GardeningTools/Trowel_Handle_Wood_Normal.png";

        private const string SteelMatPath = "Assets/Materials/Environment/GardeningTools/Trowel_Steel_PBR.mat";
        private const string BrassMatPath = "Assets/Materials/Environment/GardeningTools/Trowel_Brass_PBR.mat";
        private const string WoodMatPath = "Assets/Materials/Environment/GardeningTools/Trowel_Wood_PBR.mat";

        [MenuItem("NeuroCompanion/Build Phase 4.3 Gardening Trowel")]
        public static void BuildGardeningTrowel()
        {
            Debug.Log("[GardeningTrowelBuilder] Building Phase 4.3 Small Gardening Trowel...");

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Configure Textures
            ConfigureTexture(SteelAlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(SteelNormalPath, isNormal: true, sRGB: false);
            ConfigureTexture(WoodAlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(WoodNormalPath, isNormal: true, sRGB: false);

            // 2. Setup PBR Materials
            Material steelMat = SetupSteelMaterial();
            Material brassMat = SetupBrassMaterial();
            Material woodMat = SetupWoodMaterial();

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
                Debug.LogError("[GardeningTrowelBuilder] Gardening_Planting_Station not found! Please build Phase 4.1 first.");
                return;
            }

            // Remove existing Gardening_Trowel if present
            Transform existingTrowel = station.transform.Find("Gardening_Trowel");
            if (existingTrowel != null)
            {
                GameObject.DestroyImmediate(existingTrowel.gameObject);
            }

            // 4. Create Parent GameObject Gardening_Trowel under Gardening_Planting_Station
            // Placement: On Tabletop (Y = 0.88m) beside the seedling pot (X = -0.05m, Z = 0.10m)
            // Tilted naturally at ~35 degrees yaw so the handle points towards the front-right of the table
            GameObject trowelRoot = new GameObject("Gardening_Trowel");
            trowelRoot.transform.SetParent(station.transform, false);
            trowelRoot.transform.localPosition = new Vector3(-0.06f, 0.8805f, 0.12f);
            trowelRoot.transform.localRotation = Quaternion.Euler(2.5f, 38f, -6f); // Natural slight tool tilt on wooden slats

            // 5. Generate Trowel Components
            BuildTrowelBlade(trowelRoot.transform, steelMat);
            BuildTrowelNeckTang(trowelRoot.transform, steelMat);
            BuildTrowelFerrule(trowelRoot.transform, brassMat);
            BuildTrowelHandle(trowelRoot.transform, woodMat);

            // Add Interaction BoxCollider encapsulating the tool
            var colGo = new GameObject("Trowel_Interaction_Collider");
            colGo.transform.SetParent(trowelRoot.transform, false);
            var box = colGo.AddComponent<BoxCollider>();
            box.size = new Vector3(0.08f, 0.05f, 0.30f);
            box.center = new Vector3(0f, 0.02f, 0.05f);

            // Save Scene
            GameObject gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot != null) gardenRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[GardeningTrowelBuilder] Phase 4.3 Gardening Trowel successfully built and placed!");
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

        private static Material SetupSteelMaterial()
        {
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SteelAlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SteelNormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SteelMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, SteelMatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.95f, 1f));

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.0f);
            }

            mat.SetFloat("_Metallic", 0.88f);
            mat.SetFloat("_Smoothness", 0.42f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SetupBrassMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(BrassMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, BrassMatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", new Color(0.85f, 0.72f, 0.38f, 1f)); // Polished brass ferrule
            mat.SetFloat("_Metallic", 0.85f);
            mat.SetFloat("_Smoothness", 0.65f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SetupWoodMaterial()
        {
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodAlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodNormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(WoodMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, WoodMatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetColor("_BaseColor", new Color(1.0f, 0.96f, 0.92f, 1f));

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 1.1f);
            }

            mat.SetFloat("_Metallic", 0.0f);
            mat.SetFloat("_Smoothness", 0.40f); // Satin ergonomic wood tool finish
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------------
        // 1. Curved Metal Scoop Blade
        // ---------------------------------------------------------------------
        private static void BuildTrowelBlade(Transform parent, Material mat)
        {
            GameObject bladeGo = new GameObject("Trowel_Blade");
            bladeGo.transform.SetParent(parent, false);
            bladeGo.transform.localPosition = Vector3.zero;
            bladeGo.transform.localRotation = Quaternion.identity;

            var mf = bladeGo.AddComponent<MeshFilter>();
            var mr = bladeGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateTrowelBladeMesh(length: 0.145f, maxWidth: 0.075f, scoopDepth: 0.018f, segmentsL: 16, segmentsW: 10);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        /// <summary>
        /// Generates realistic curved trowel scoop mesh with pointed spade tip and curved cross section.
        /// </summary>
        private static Mesh GenerateTrowelBladeMesh(float length, float maxWidth, float scoopDepth, int segmentsL, int segmentsW)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Trowel_Blade_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // The blade extends along -Z from Z = 0 (blade base/heel) to Z = -length (pointed tip)
            // X is width across the scoop
            // Y is scoop curve depth
            for (int l = 0; l <= segmentsL; l++)
            {
                float tL = (float)l / segmentsL; // 0 at base, 1 at tip
                float z = -tL * length;

                // Width tapers gently then sharpens to a point at the tip
                float currentWidth;
                if (tL < 0.65f)
                    currentWidth = Mathf.Lerp(maxWidth * 0.75f, maxWidth, tL / 0.65f);
                else
                    currentWidth = Mathf.Lerp(maxWidth, 0.002f, (tL - 0.65f) / 0.35f);

                // Scoop cross-section depth
                float currentDepth = Mathf.Sin(tL * Mathf.PI * 0.85f) * scoopDepth;

                for (int w = 0; w <= segmentsW; w++)
                {
                    float tW = (float)w / segmentsW; // 0 to 1
                    float x = (tW - 0.5f) * currentWidth;

                    // Parabolic scoop curvature across width
                    float uDist = (tW - 0.5f) * 2f; // -1 to +1
                    float y = (uDist * uDist) * currentDepth;

                    verts.Add(new Vector3(x, y, z));

                    // Surface normal
                    Vector3 normal = new Vector3(-uDist * 0.6f, 1.0f, -0.1f).normalized;
                    norms.Add(normal);
                    uvs.Add(new Vector2(tW, tL));
                }
            }

            int stride = segmentsW + 1;
            for (int l = 0; l < segmentsL; l++)
            {
                int r0 = l * stride;
                int r1 = (l + 1) * stride;

                for (int w = 0; w < segmentsW; w++)
                {
                    int a = r0 + w;
                    int b = r1 + w;
                    int c = r1 + w + 1;
                    int d = r0 + w + 1;

                    // Double sided for realistic thin metal blade
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(a); tris.Add(d); tris.Add(c);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        // ---------------------------------------------------------------------
        // 2. Angled Swan Neck Tang (forged steel connector)
        // ---------------------------------------------------------------------
        private static void BuildTrowelNeckTang(Transform parent, Material mat)
        {
            GameObject neckGo = new GameObject("Trowel_Neck_Tang");
            neckGo.transform.SetParent(parent, false);

            // Rises from (0, 0, 0) up and back to (0, 0.022, 0.045)
            neckGo.transform.localPosition = new Vector3(0f, 0.012f, 0.022f);
            neckGo.transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);
            neckGo.transform.localScale = Vector3.one;

            var mf = neckGo.AddComponent<MeshFilter>();
            var mr = neckGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateCylinderMesh(radius: 0.0055f, length: 0.048f, segments: 12);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        // ---------------------------------------------------------------------
        // 3. Brass Ferrule (reinforced joint collar)
        // ---------------------------------------------------------------------
        private static void BuildTrowelFerrule(Transform parent, Material mat)
        {
            GameObject ferruleGo = new GameObject("Trowel_Ferrule");
            ferruleGo.transform.SetParent(parent, false);

            ferruleGo.transform.localPosition = new Vector3(0f, 0.024f, 0.045f);
            ferruleGo.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            ferruleGo.transform.localScale = Vector3.one;

            var mf = ferruleGo.AddComponent<MeshFilter>();
            var mr = ferruleGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateCylinderMesh(radius: 0.011f, length: 0.016f, segments: 16);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        // ---------------------------------------------------------------------
        // 4. Ergonomic Ash Wood Handle
        // ---------------------------------------------------------------------
        private static void BuildTrowelHandle(Transform parent, Material mat)
        {
            GameObject handleGo = new GameObject("Trowel_Handle");
            handleGo.transform.SetParent(parent, false);

            // Handle extends back from the ferrule at Z = 0.055m, angled upward ~18 degrees
            handleGo.transform.localPosition = new Vector3(0f, 0.028f, 0.058f);
            handleGo.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            handleGo.transform.localScale = Vector3.one;

            var mf = handleGo.AddComponent<MeshFilter>();
            var mr = handleGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateContouredHandleMesh(length: 0.125f, minRadius: 0.011f, maxRadius: 0.0155f, segmentsL: 14, segmentsR: 16);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        /// <summary>
        /// Generates a smooth, ergonomically contoured wooden tool handle with palm swell and rounded pommel knob.
        /// </summary>
        private static Mesh GenerateContouredHandleMesh(float length, float minRadius, float maxRadius, int segmentsL, int segmentsR)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Trowel_Handle_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int l = 0; l <= segmentsL; l++)
            {
                float t = (float)l / segmentsL; // 0 at ferrule to 1 at pommel end
                float z = t * length;

                // Ergonomic contouring: slim at collar, swells in palm, slight taper before rounded knob end
                float profile;
                if (t < 0.75f)
                {
                    // Gentle palm swell curve
                    profile = Mathf.Lerp(minRadius, maxRadius, Mathf.Sin(t / 0.75f * Mathf.PI * 0.85f));
                }
                else
                {
                    // Rounded pommel end
                    float endT = (t - 0.75f) / 0.25f;
                    profile = Mathf.Lerp(maxRadius * 0.95f, 0.002f, endT * endT);
                }

                for (int r = 0; r <= segmentsR; r++)
                {
                    float angle = (float)r / segmentsR * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    verts.Add(new Vector3(cos * profile, sin * profile, z));
                    norms.Add(new Vector3(cos, sin, (t > 0.85f ? 0.6f : 0f)).normalized);
                    uvs.Add(new Vector2((float)r / segmentsR * 2f, t * 2f));
                }
            }

            int stride = segmentsR + 1;
            for (int l = 0; l < segmentsL; l++)
            {
                int r0 = l * stride;
                int r1 = (l + 1) * stride;

                for (int r = 0; r < segmentsR; r++)
                {
                    int a = r0 + r;
                    int b = r1 + r;
                    int c = r1 + r + 1;
                    int d = r0 + r + 1;

                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(a); tris.Add(c); tris.Add(d);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Mesh GenerateCylinderMesh(float radius, float length, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "Cylinder_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int r = 0; r <= segments; r++)
            {
                float angle = (float)r / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                verts.Add(new Vector3(cos * radius, sin * radius, 0f));
                norms.Add(new Vector3(cos, sin, 0f));
                uvs.Add(new Vector2((float)r / segments, 0f));

                verts.Add(new Vector3(cos * radius, sin * radius, length));
                norms.Add(new Vector3(cos, sin, 0f));
                uvs.Add(new Vector2((float)r / segments, 1f));
            }

            for (int r = 0; r < segments; r++)
            {
                int a = r * 2;
                int b = a + 1;
                int c = a + 3;
                int d = a + 2;

                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(a); tris.Add(c); tris.Add(d);
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