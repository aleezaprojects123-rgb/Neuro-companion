using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

namespace NeuroCompanion.Backend.Editor
{
    /// <summary>
    /// Phase 4.5 — Builds 6 realistic plant seedlings and places them on the gardening station.
    /// Each seedling is a separate, interaction-ready GameObject under Plant_Seedlings parent.
    /// </summary>
    public static class Phase45SeedlingBuilder
    {
        static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";
        static string MatDir = "Assets/Materials/Environment/Seedlings";
        static string TexDir = "Assets/Textures/Environment/Seedlings";

        // ─────────────────────────────────────────────────────────────────
        [MenuItem("NeuroCompanion/Build Phase 4.5 Seedlings")]
        public static void Build()
        {
            // ── 0. Activate garden root ──────────────────────────────────
            GameObject gardenRoot = null; bool wasActive = false;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "[GARDEN_GAME]" && o.scene.IsValid()) { gardenRoot = o; break; }
            if (gardenRoot != null) { wasActive = gardenRoot.activeSelf; gardenRoot.SetActive(true); }

            // ── 1. Find station reference ────────────────────────────────
            GameObject station = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "Gardening_Planting_Station" && o.scene.IsValid()) { station = o; break; }

            if (station == null) { Debug.LogError("[Phase45] Gardening_Planting_Station not found!"); return; }
            Debug.Log($"[Phase45] Station found at world: {station.transform.position}");

            // ── 2. Remove old Plant_Seedlings if rebuilding ──────────────
            foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>())
                if (o.name == "Plant_Seedlings" && o.scene.IsValid()) { Undo.DestroyObjectImmediate(o); break; }

            // ── 3. Create directories ────────────────────────────────────
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Materials/Environment/Seedlings"));
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Textures/Environment/Seedlings"));

            // ── 4. Create PBR leaf textures (3 color variants) ───────────
            var leafTex_BrightGreen  = CreateLeafTexture("Leaf_BrightGreen",  new Color(0.22f, 0.60f, 0.18f), new Color(0.16f, 0.48f, 0.14f));
            var leafTex_DarkGreen    = CreateLeafTexture("Leaf_DarkGreen",    new Color(0.12f, 0.42f, 0.14f), new Color(0.08f, 0.32f, 0.10f));
            var leafTex_YellowGreen  = CreateLeafTexture("Leaf_YellowGreen",  new Color(0.45f, 0.65f, 0.15f), new Color(0.36f, 0.55f, 0.12f));
            var leafTex_PaleGreen    = CreateLeafTexture("Leaf_PaleGreen",    new Color(0.55f, 0.72f, 0.38f), new Color(0.44f, 0.60f, 0.28f));
            var stemTex              = CreateStemTexture();

            // ── 5. Create PBR materials ──────────────────────────────────
            var matLeafBright  = CreateLeafMat("SeedlingLeaf_Bright",  leafTex_BrightGreen);
            var matLeafDark    = CreateLeafMat("SeedlingLeaf_Dark",    leafTex_DarkGreen);
            var matLeafYellow  = CreateLeafMat("SeedlingLeaf_Yellow",  leafTex_YellowGreen);
            var matLeafPale    = CreateLeafMat("SeedlingLeaf_Pale",    leafTex_PaleGreen);
            var matStem        = CreateStemMat("SeedlingSystem_Stem",  stemTex);
            var matSoil        = CreateSoilMat("SeedlingSoil");

            // ── 6. Build Plant_Seedlings parent ──────────────────────────
            var seedlingParent = new GameObject("Plant_Seedlings");
            Undo.RegisterCreatedObjectUndo(seedlingParent, "Create Plant_Seedlings");
            seedlingParent.transform.SetParent(station.transform);

            // Tabletop surface local Y relative to station = 0.78 (confirmed in earlier phases)
            // Seedlings sit ON the tabletop, spread left side (can is right, trowel center)
            // We'll lay them along the front edge of the tabletop
            seedlingParent.transform.localPosition = Vector3.zero;
            seedlingParent.transform.localRotation = Quaternion.identity;

            // ── 7. Build each seedling ───────────────────────────────────
            // Positions on tabletop (local to station):
            // Tabletop is about 0.9m wide (X: -0.42 to +0.42), depth ~0.5m (Z: -0.2 to +0.3)
            // Can is at X=-0.22, so place seedlings right side: X=+0.05 to +0.40, tabletop Y=0.78
            var defs = new SeedlingDef[]
            {
                // Seedling_01: Bushy basil-like herb — 3 large oval leaves, bright green
                new SeedlingDef("Seedling_01", "Young Basil",
                    localPos: new Vector3( 0.05f, 0.78f,  0.00f),
                    leafMat: matLeafBright, stemMat: matStem, soilMat: matSoil,
                    leafCount: 5, leafSize: 0.038f, stemHeight: 0.055f, spread: 0.040f,
                    leafShape: LeafShape.Oval),

                // Seedling_02: Tall thin seedling — mint-like, dark green, narrow leaves
                new SeedlingDef("Seedling_02", "Young Mint",
                    localPos: new Vector3( 0.14f, 0.78f,  0.00f),
                    leafMat: matLeafDark, stemMat: matStem, soilMat: matSoil,
                    leafCount: 4, leafSize: 0.030f, stemHeight: 0.072f, spread: 0.028f,
                    leafShape: LeafShape.Narrow),

                // Seedling_03: Low sprawling seedling — yellow-green, heart-shaped leaves
                new SeedlingDef("Seedling_03", "Young Violet",
                    localPos: new Vector3( 0.23f, 0.78f,  0.00f),
                    leafMat: matLeafYellow, stemMat: matStem, soilMat: matSoil,
                    leafCount: 6, leafSize: 0.034f, stemHeight: 0.042f, spread: 0.048f,
                    leafShape: LeafShape.Heart),

                // Seedling_04: Medium upright — pale green, slightly pointed leaves
                new SeedlingDef("Seedling_04", "Young Fern",
                    localPos: new Vector3( 0.32f, 0.78f,  0.00f),
                    leafMat: matLeafPale, stemMat: matStem, soilMat: matSoil,
                    leafCount: 7, leafSize: 0.028f, stemHeight: 0.065f, spread: 0.038f,
                    leafShape: LeafShape.Pointed),

                // Seedling_05: Small compact seedling — dark, round leaves (succulent-like)
                new SeedlingDef("Seedling_05", "Young Succulent",
                    localPos: new Vector3( 0.09f, 0.78f,  0.08f),
                    leafMat: matLeafDark, stemMat: matStem, soilMat: matSoil,
                    leafCount: 8, leafSize: 0.022f, stemHeight: 0.028f, spread: 0.035f,
                    leafShape: LeafShape.Round),

                // Seedling_06: Tall wispy seedling — bright green, long narrow leaves, slightly drooping
                new SeedlingDef("Seedling_06", "Young Grass Herb",
                    localPos: new Vector3( 0.20f, 0.78f,  0.08f),
                    leafMat: matLeafBright, stemMat: matStem, soilMat: matSoil,
                    leafCount: 5, leafSize: 0.025f, stemHeight: 0.082f, spread: 0.020f,
                    leafShape: LeafShape.Grass),
            };

            var createdSeedlings = new List<GameObject>();
            foreach (var def in defs)
            {
                var go = BuildSeedling(def, seedlingParent.transform);
                createdSeedlings.Add(go);
                Debug.Log($"[Phase45] Built {def.name} at localPos={def.localPos}");
            }

            // ── 8. Save scene ─────────────────────────────────────────────
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Phase45] Scene saved.");

            // ── 9. Screenshots ────────────────────────────────────────────
            Vector3 sw = station.transform.position;

            // A: Full station with seedlings visible
            Shoot(OutDir + @"\phase45_a_full_station.png",
                pos: sw + new Vector3(0f, 1.1f, -1.9f),
                target: sw + new Vector3(0f, 0.85f, 0.05f));

            // B: Close-up of seedling collection on tabletop
            Shoot(OutDir + @"\phase45_b_seedlings_closeup.png",
                pos: sw + new Vector3(0.25f, 1.35f, -0.85f),
                target: sw + new Vector3(0.20f, 0.80f, 0.03f));

            // C: Right-side view showing full depth of station
            Shoot(OutDir + @"\phase45_c_side_view.png",
                pos: sw + new Vector3(1.2f, 1.0f, -0.8f),
                target: sw + new Vector3(0.1f, 0.85f, 0.04f));

            // D: Top-down view of tabletop layout
            Shoot(OutDir + @"\phase45_d_topdown.png",
                pos: sw + new Vector3(0.1f, 1.9f, 0.05f),
                target: sw + new Vector3(0.1f, 0.78f, 0.04f));

            // E: Game-view style patient perspective
            Shoot(OutDir + @"\phase45_e_patient_view.png",
                pos: sw + new Vector3(-0.5f, 1.6f, -2.5f),
                target: sw + new Vector3(0.1f, 0.80f, 0.04f));

            // ── 10. Restore garden root ───────────────────────────────────
            if (gardenRoot != null) gardenRoot.SetActive(wasActive);
            Debug.Log("[Phase45] Phase 4.5 complete! 6 seedlings created.");
        }

        // ─────────────────────────────────────────────────────────────────
        // SEEDLING BUILDER
        // ─────────────────────────────────────────────────────────────────
        static GameObject BuildSeedling(SeedlingDef def, Transform parent)
        {
            var root = new GameObject(def.name);
            Undo.RegisterCreatedObjectUndo(root, "Create " + def.name);
            root.transform.SetParent(parent);
            root.transform.localPosition = def.localPos;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            // ── Soil disc base ───────────────────────────────────────────
            var soilGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            soilGO.name = "Soil_Base";
            soilGO.transform.SetParent(root.transform);
            soilGO.transform.localPosition = new Vector3(0, 0.005f, 0);
            soilGO.transform.localScale = new Vector3(0.045f, 0.005f, 0.045f);
            soilGO.GetComponent<Renderer>().sharedMaterial = def.soilMat;
            Object.DestroyImmediate(soilGO.GetComponent<Collider>());

            // ── Main stem ────────────────────────────────────────────────
            var stemGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stemGO.name = "Main_Stem";
            stemGO.transform.SetParent(root.transform);
            stemGO.transform.localPosition = new Vector3(0, def.stemHeight * 0.5f, 0);
            stemGO.transform.localScale = new Vector3(0.006f, def.stemHeight * 0.5f, 0.006f);
            stemGO.GetComponent<Renderer>().sharedMaterial = def.stemMat;
            Object.DestroyImmediate(stemGO.GetComponent<Collider>());

            // ── Leaves ───────────────────────────────────────────────────
            int count = def.leafCount;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count; // 0..1
                float angle = t * 360f + Random.Range(-15f, 15f);
                float heightFraction = 0.4f + t * 0.6f + Random.Range(-0.08f, 0.08f);
                float radius = def.spread * (0.5f + t * 0.5f) + Random.Range(-0.005f, 0.005f);
                float yPos = def.stemHeight * heightFraction;

                // Leaf position: radiate outward from stem
                float rad = angle * Mathf.Deg2Rad;
                Vector3 leafPos = new Vector3(Mathf.Sin(rad) * radius, yPos, Mathf.Cos(rad) * radius);

                var leafGO = new GameObject($"Leaf_{i + 1:D2}");
                leafGO.transform.SetParent(root.transform);
                leafGO.transform.localPosition = leafPos;

                // Rotate leaf to face outward and droop naturally
                float droopAngle = GetDroopAngle(def.leafShape, i, count);
                leafGO.transform.localRotation = Quaternion.Euler(droopAngle, angle + 180f, 0f);

                // Build leaf mesh
                var leafMF = leafGO.AddComponent<MeshFilter>();
                var leafMR = leafGO.AddComponent<MeshRenderer>();
                leafMF.sharedMesh = BuildLeafMesh(def.leafShape, def.leafSize, i);
                leafMR.sharedMaterial = def.leafMat;
                leafMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;

                // ── Sub-stem connecting leaf to main stem ─────────────────
                if (def.stemHeight > 0.04f && heightFraction < 0.85f)
                {
                    var subStem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    subStem.name = "SubStem";
                    subStem.transform.SetParent(leafGO.transform);
                    subStem.transform.localPosition = new Vector3(0, -0.012f, 0);
                    subStem.transform.localScale = new Vector3(0.08f, 0.012f, 0.08f);
                    subStem.GetComponent<Renderer>().sharedMaterial = def.stemMat;
                    Object.DestroyImmediate(subStem.GetComponent<Collider>());
                }
            }

            // ── BoxCollider for future pickup interaction ─────────────────
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, def.stemHeight * 0.5f, 0);
            col.size = new Vector3(def.spread * 2.2f, def.stemHeight, def.spread * 2.2f);
            col.isTrigger = false; // solid, interaction-ready

            return root;
        }

        // ─────────────────────────────────────────────────────────────────
        // LEAF MESH BUILDER (6 shapes)
        // ─────────────────────────────────────────────────────────────────
        enum LeafShape { Oval, Narrow, Heart, Pointed, Round, Grass }

        static float GetDroopAngle(LeafShape shape, int index, int total)
        {
            switch (shape)
            {
                case LeafShape.Grass: return -55f + Random.Range(-10f, 10f);
                case LeafShape.Oval:  return -30f + Random.Range(-8f, 8f);
                case LeafShape.Heart: return -20f + Random.Range(-5f, 5f);
                case LeafShape.Round: return -15f + Random.Range(-5f, 5f);
                default:              return -25f + Random.Range(-8f, 8f);
            }
        }

        static Mesh BuildLeafMesh(LeafShape shape, float size, int seed)
        {
            var mesh = new Mesh();
            mesh.name = "LeafMesh_" + shape;

            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();
            List<Vector2> uvs = new List<Vector2>();

            switch (shape)
            {
                case LeafShape.Oval:
                case LeafShape.Round:
                {
                    // Oval leaf: 8-segment smooth oval
                    int segs = 8;
                    float w = size * (shape == LeafShape.Round ? 0.9f : 0.65f);
                    float h = size;
                    // Center
                    verts.Add(Vector3.zero);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    for (int i = 0; i <= segs; i++)
                    {
                        float a = (float)i / segs * Mathf.PI * 2f;
                        float px = Mathf.Sin(a) * w;
                        float py = Mathf.Cos(a) * h;
                        // Squish top to form leaf tip
                        if (py > 0) py *= 1.3f;
                        verts.Add(new Vector3(px, 0, py));
                        uvs.Add(new Vector2(0.5f + px / (w * 2f), 0.5f + py / (h * 2.6f)));
                    }
                    for (int i = 0; i < segs; i++)
                        tris.AddRange(new[] { 0, i + 1, i + 2 });
                    break;
                }
                case LeafShape.Narrow:
                case LeafShape.Pointed:
                {
                    // Narrow / Pointed: diamond shape
                    float w = size * (shape == LeafShape.Pointed ? 0.45f : 0.30f);
                    float h = size * 1.2f;
                    verts.AddRange(new[]{
                        new Vector3(0,    0, h),    // tip
                        new Vector3(-w,   0, 0),    // left
                        new Vector3(w,    0, 0),    // right
                        new Vector3(0,    0, -h*0.3f) // base
                    });
                    uvs.AddRange(new[]{ new Vector2(0.5f,1), new Vector2(0,0.5f), new Vector2(1,0.5f), new Vector2(0.5f,0) });
                    tris.AddRange(new[]{ 0,1,2, 1,3,2 });
                    // Slight mid-vein bump
                    verts.Add(new Vector3(0, 0.003f, h * 0.3f));
                    uvs.Add(new Vector2(0.5f, 0.6f));
                    tris.AddRange(new[]{ 0,4,1, 0,2,4 });
                    break;
                }
                case LeafShape.Heart:
                {
                    // Heart-shaped leaf: two lobes
                    float s = size;
                    int segs = 12;
                    verts.Add(Vector3.zero);
                    uvs.Add(new Vector2(0.5f, 0.5f));
                    for (int i = 0; i <= segs; i++)
                    {
                        float t = (float)i / segs;
                        float a = t * Mathf.PI * 2f;
                        // Heart formula
                        float px = s * 0.65f * Mathf.Sin(a) * Mathf.Sin(a) * Mathf.Sin(a);
                        float py = s * (0.5f*Mathf.Cos(a) - 0.2f*Mathf.Cos(2*a) - 0.08f*Mathf.Cos(3*a));
                        verts.Add(new Vector3(px, 0, py));
                        uvs.Add(new Vector2(0.5f + px/(s*1.4f), 0.5f + py/(s*1.4f)));
                    }
                    for (int i = 0; i < segs; i++)
                        tris.AddRange(new[]{ 0, i+1, i+2 });
                    break;
                }
                case LeafShape.Grass:
                {
                    // Long thin blade
                    float w = size * 0.18f;
                    float h = size * 1.6f;
                    // Slight curve: 4 quads tapering to tip
                    int segs = 4;
                    for (int i = 0; i <= segs; i++)
                    {
                        float t = (float)i / segs;
                        float curveX = Mathf.Sin(t * Mathf.PI * 0.5f) * size * 0.3f;
                        float y = t * h;
                        float width = w * (1f - t * 0.85f);
                        verts.Add(new Vector3(-width + curveX, 0, y));
                        verts.Add(new Vector3( width + curveX, 0, y));
                        uvs.Add(new Vector2(0, t));
                        uvs.Add(new Vector2(1, t));
                    }
                    for (int i = 0; i < segs; i++)
                    {
                        int b = i * 2;
                        tris.AddRange(new[]{ b, b+2, b+1,  b+1, b+2, b+3 });
                    }
                    break;
                }
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ─────────────────────────────────────────────────────────────────
        // TEXTURE BUILDERS
        // ─────────────────────────────────────────────────────────────────
        static Texture2D CreateLeafTexture(string name, Color baseColor, Color veinColor)
        {
            string path = $"{TexDir}/{name}.png";
            string full = $"Assets/../{path}".Replace("Assets/../", "");
            full = Path.Combine(Application.dataPath, $"Textures/Environment/Seedlings/{name}.png");

            int W = 128, H = 128;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var pixels = new Color[W * H];

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                // Base leaf color
                Color c = baseColor;
                // Central vein
                float veinDist = Mathf.Abs(u - 0.5f);
                if (veinDist < 0.03f) c = Color.Lerp(c, veinColor * 0.75f, (0.03f - veinDist) / 0.03f);
                // Side veins
                float sideVein1 = Mathf.Abs(u - 0.3f); float sideVein2 = Mathf.Abs(u - 0.7f);
                if (sideVein1 < 0.015f || sideVein2 < 0.015f)
                    c = Color.Lerp(c, veinColor * 0.85f, 0.4f);
                // Slight brightness variation
                float noise = Mathf.PerlinNoise(u * 6f, v * 6f) * 0.12f;
                c = new Color(c.r + noise * 0.1f, c.g + noise * 0.08f, c.b, 1f);
                // Edge fade (alpha)
                float edgeDist = Mathf.Min(u, 1f-u, v, 1f-v);
                float alpha = Mathf.Clamp01(edgeDist * 8f);
                pixels[y * W + x] = new Color(c.r, c.g, c.b, alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset($"Assets/Textures/Environment/Seedlings/{name}.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath($"Assets/Textures/Environment/Seedlings/{name}.png");
            if (importer != null) {
                importer.alphaIsTransparency = true;
                importer.textureType = TextureImporterType.Default;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/Environment/Seedlings/{name}.png");
        }

        static Texture2D CreateStemTexture()
        {
            string full = Path.Combine(Application.dataPath, "Textures/Environment/Seedlings/Stem_Green.png");
            int W = 32, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (float)x/W, v = (float)y/H;
                float n = Mathf.PerlinNoise(u*4f, v*8f) * 0.1f;
                px[y*W+x] = new Color(0.18f+n, 0.42f+n, 0.12f+n);
            }
            tex.SetPixels(px); tex.Apply();
            File.WriteAllBytes(full, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset("Assets/Textures/Environment/Seedlings/Stem_Green.png");
            return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/Seedlings/Stem_Green.png");
        }

        // ─────────────────────────────────────────────────────────────────
        // MATERIAL BUILDERS
        // ─────────────────────────────────────────────────────────────────
        static Material CreateLeafMat(string name, Texture2D albedo)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = name;
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic", 0f);
            // Enable alpha clipping for leaf transparency
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.3f);
            mat.SetFloat("_Mode", 1f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material CreateStemMat(string name, Texture2D albedo)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = name;
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", new Color(0.20f, 0.44f, 0.14f));
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material CreateSoilMat(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = name;
            mat.SetColor("_BaseColor", new Color(0.28f, 0.18f, 0.10f));
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ─────────────────────────────────────────────────────────────────
        // SCREENSHOT HELPER
        // ─────────────────────────────────────────────────────────────────
        static void Shoot(string path, Vector3 pos, Vector3 target)
        {
            int W = 1280, H = 720;
            var go = new GameObject("_TC");
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.LookAt(target);
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.Skybox;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            byte[] b = tex.EncodeToPNG();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, b);
            Debug.Log($"[Phase45] Screenshot: {path} ({b.Length}b)");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        // ─────────────────────────────────────────────────────────────────
        // DATA STRUCTURE
        // ─────────────────────────────────────────────────────────────────
        struct SeedlingDef
        {
            public string name, label;
            public Vector3 localPos;
            public Material leafMat, stemMat, soilMat;
            public int leafCount;
            public float leafSize, stemHeight, spread;
            public LeafShape leafShape;

            public SeedlingDef(string n, string lbl, Vector3 localPos,
                Material leafMat, Material stemMat, Material soilMat,
                int leafCount, float leafSize, float stemHeight, float spread,
                LeafShape leafShape)
            {
                this.name = n; this.label = lbl;
                this.localPos = localPos;
                this.leafMat = leafMat; this.stemMat = stemMat; this.soilMat = soilMat;
                this.leafCount = leafCount; this.leafSize = leafSize;
                this.stemHeight = stemHeight; this.spread = spread;
                this.leafShape = leafShape;
            }
        }
    }
}
