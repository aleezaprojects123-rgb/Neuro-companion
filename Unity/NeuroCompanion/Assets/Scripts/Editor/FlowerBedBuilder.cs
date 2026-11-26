#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class FlowerBedBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MulchMatPath = "Assets/Materials/Environment/FlowerBeds/Mulch_Base.mat";
        private const string FlowerMatPath = "Assets/Materials/Environment/FlowerBeds/Flower_Blossom.mat";
        private const string FoliageMatPath = "Assets/Materials/Environment/FlowerBeds/Foliage_Base.mat";
        private const string PaverMatPath = "Assets/Materials/Environment/Pathway/Garden_Pathway_Curb_PBR.mat";

        public enum FlowerColorType
        {
            Lavender = 0, // Q0 (TL)
            White = 1,    // Q1 (TR)
            Pink = 2,     // Q2 (BL)
            Yellow = 3    // Q3 (BR)
        }

        public enum FoliageType
        {
            Hosta = 0,       // Q0 (TL): Broadleaf Hosta
            Fern = 1,        // Q1 (TR): Sword Fern
            Groundcover = 2, // Q2 (BL): Creeping Thyme
            Herb = 3         // Q3 (BR): Salvia Herb
        }

        [MenuItem("NeuroCompanion/Build 3 Realistic Flower Beds")]
        public static void BuildFlowerBeds()
        {
            Debug.Log("[FlowerBedBuilder] Building Decongested, Crisp, High-Resolution Botanical Flower Beds...");

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Load Materials
            Material mulchMat = AssetDatabase.LoadAssetAtPath<Material>(MulchMatPath);
            Material flowerMat = AssetDatabase.LoadAssetAtPath<Material>(FlowerMatPath);
            Material foliageMat = AssetDatabase.LoadAssetAtPath<Material>(FoliageMatPath);
            Material paverMat = AssetDatabase.LoadAssetAtPath<Material>(PaverMatPath);

            if (mulchMat == null || flowerMat == null || foliageMat == null)
            {
                Debug.LogError("[FlowerBedBuilder] Missing required materials!");
                return;
            }

            // Find or recreate [GARDEN_GAME] root
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

            // Container for flower beds
            Transform existingParent = gardenRoot.transform.Find("Garden_FlowerBeds");
            if (existingParent != null)
            {
                GameObject.DestroyImmediate(existingParent.gameObject);
            }

            GameObject flowerBedsContainer = new GameObject("Garden_FlowerBeds");
            flowerBedsContainer.transform.SetParent(gardenRoot.transform, false);
            flowerBedsContainer.transform.position = Vector3.zero;

            // -------------------------------------------------------------
            // FLOWER BED 1: Curved Mixed Flower Bed (East/Southeast Edge)
            // -------------------------------------------------------------
            CreateFlowerBed01(flowerBedsContainer.transform, mulchMat, flowerMat, foliageMat, paverMat);

            // -------------------------------------------------------------
            // FLOWER BED 2: Natural Cluster Flower Bed (Open Western Meadow)
            // -------------------------------------------------------------
            CreateFlowerBed02(flowerBedsContainer.transform, mulchMat, flowerMat, foliageMat, paverMat);

            // -------------------------------------------------------------
            // FLOWER BED 3: Low Decorative Flower Bed (Southwest Lawn)
            // -------------------------------------------------------------
            CreateFlowerBed03(flowerBedsContainer.transform, mulchMat, flowerMat, foliageMat, paverMat);

            // Ensure garden root is set to active false for standard game bootstrap
            gardenRoot.SetActive(false);

            // Save Scene & Assets
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[FlowerBedBuilder] Successfully created 3 Realistic, Beautiful, Decongested Flower Beds!");
        }

        // =========================================================================
        // FLOWER BED 1 — Curved Mixed Flower Bed (East/Southeast Edge)
        // =========================================================================
        private static GameObject CreateFlowerBed01(Transform parent, Material mulchMat, Material flowerMat, Material foliageMat, Material paverMat)
        {
            GameObject bed = new GameObject("FlowerBed_01");
            bed.transform.SetParent(parent, false);

            Vector3 center = new Vector3(18.5f, 0f, -17.5f);
            center.y = SampleTerrainHeight(center.x, center.z);
            bed.transform.position = center;

            // 1. Organic curved mulch spine (14m crescent)
            List<Vector2> spine = new List<Vector2>();
            for (int i = 0; i <= 28; i++)
            {
                float t = i / 28f;
                float angle = Mathf.Lerp(-0.85f, 0.75f, t);
                float radius = 5.4f;
                float lx = Mathf.Cos(angle) * radius - 4.6f;
                float lz = Mathf.Sin(angle) * radius * 1.50f;
                spine.Add(new Vector2(lx, lz));
            }

            // High-res mulch mound with high UV tiling (32x along spine, 8x across)
            Mesh soilMesh = GenerateCurvedMoundMesh(spine, width: 2.7f, maxElevation: 0.16f, centerWorld: center, uTiling: 32f, vTiling: 8f);
            GameObject soilGo = new GameObject("Soil_Mulch_Mound");
            soilGo.transform.SetParent(bed.transform, false);
            var mf = soilGo.AddComponent<MeshFilter>();
            mf.sharedMesh = soilMesh;
            var mr = soilGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mulchMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            soilGo.AddComponent<MeshCollider>().sharedMesh = soilMesh;

            // Stone edging border outlining the curved flower bed
            if (paverMat != null)
            {
                Mesh stoneMesh = GenerateCurvedEdgingMesh(spine, width: 2.75f, stoneWidth: 0.18f, centerWorld: center);
                GameObject stoneGo = new GameObject("Stone_Edging_Border");
                stoneGo.transform.SetParent(bed.transform, false);
                var smf = stoneGo.AddComponent<MeshFilter>();
                smf.sharedMesh = stoneMesh;
                var smr = stoneGo.AddComponent<MeshRenderer>();
                smr.sharedMaterial = paverMat;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
            }

            // 2. Beautifully spaced, decongested plant drifts along the curve
            Random.InitState(401);
            List<CombineInstance> flowerCombines = new List<CombineInstance>();
            List<CombineInstance> foliageCombines = new List<CombineInstance>();

            int driftCount = 7;
            for (int d = 0; d < driftCount; d++)
            {
                float t = (d + 0.5f) / driftCount;
                int spineIdx = Mathf.Clamp((int)(t * (spine.Count - 1)), 1, spine.Count - 2);
                Vector2 sp = spine[spineIdx];
                Vector2 fwd = (spine[spineIdx + 1] - spine[spineIdx - 1]).normalized;
                Vector2 norm = new Vector2(-fwd.y, fwd.x);

                float sideBias = (d % 2 == 0) ? 0.30f : -0.30f;
                Vector2 driftCenter = sp + norm * sideBias;

                FlowerColorType cMain = (FlowerColorType)(d % 4);
                FlowerColorType cAcc = (FlowerColorType)((d + 2) % 4);

                // 3 gracefully spaced flowers in this drift (0.55m spacing - NO congestion!)
                var plantLocs = new (Vector2 offset, FlowerColorType col, float h)[]
                {
                    (Vector2.zero, cMain, Random.Range(0.48f, 0.58f)),
                    (norm * 0.48f + fwd * 0.32f, cMain, Random.Range(0.42f, 0.52f)),
                    (-norm * 0.45f - fwd * 0.30f, cAcc, Random.Range(0.40f, 0.50f))
                };

                foreach (var pl in plantLocs)
                {
                    Vector2 p2D = driftCenter + pl.offset;
                    Vector3 worldPos = center + new Vector3(p2D.x, 0f, p2D.y);
                    float groundY = SampleTerrainHeight(worldPos.x, worldPos.z);
                    float moundY = GetMoundHeight(p2D, spine, 2.7f, 0.16f);
                    float localY = (groundY - center.y) + moundY + 0.015f;

                    Vector3 plantPos = new Vector3(p2D.x, localY, p2D.y);
                    Quaternion plantRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                    float fw = pl.h * 0.72f;
                    Mesh flwMesh = CreateCrispFlowerMesh(pl.col, pl.h, fw);
                    flowerCombines.Add(new CombineInstance
                    {
                        mesh = flwMesh,
                        transform = Matrix4x4.TRS(plantPos, plantRot, Vector3.one)
                    });
                }

                // 2 Foliage accents framing the drift at comfortable distance
                FoliageType folT1 = (d % 2 == 0) ? FoliageType.Hosta : FoliageType.Fern;
                FoliageType folT2 = (d % 2 == 0) ? FoliageType.Groundcover : FoliageType.Herb;

                var folLocs = new (Vector2 offset, FoliageType fol)[]
                {
                    (norm * -0.72f, folT1),
                    (norm * 0.70f + fwd * 0.45f, folT2)
                };

                foreach (var fl in folLocs)
                {
                    Vector2 p2D = driftCenter + fl.offset;
                    Vector3 worldPos = center + new Vector3(p2D.x, 0f, p2D.y);
                    float groundY = SampleTerrainHeight(worldPos.x, worldPos.z);
                    float moundY = GetMoundHeight(p2D, spine, 2.7f, 0.16f);
                    float localY = (groundY - center.y) + moundY + 0.012f;

                    Vector3 plantPos = new Vector3(p2D.x, localY, p2D.y);
                    Quaternion plantRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                    Mesh folMesh = CreateCrispLeafMesh(fl.fol, leafSize: Random.Range(0.45f, 0.58f));
                    foliageCombines.Add(new CombineInstance
                    {
                        mesh = folMesh,
                        transform = Matrix4x4.TRS(plantPos, plantRot, Vector3.one)
                    });
                }
            }

            AttachCombinedObjects("Foliage_Layer", bed.transform, foliageCombines, foliageMat, castShadows: false);
            AttachCombinedObjects("Flowers_Layer", bed.transform, flowerCombines, flowerMat, castShadows: false);

            return bed;
        }

        // =========================================================================
        // FLOWER BED 2 — Natural Cluster Flower Bed (Open Western Meadow)
        // Balanced 360-degree organic coverage, airy, completely decongested
        // =========================================================================
        private static GameObject CreateFlowerBed02(Transform parent, Material mulchMat, Material flowerMat, Material foliageMat, Material paverMat)
        {
            GameObject bed = new GameObject("FlowerBed_02");
            bed.transform.SetParent(parent, false);

            Vector3 center = new Vector3(-12.0f, 0f, 4.0f);
            center.y = SampleTerrainHeight(center.x, center.z);
            bed.transform.position = center;

            // 1. Organic rounded mound with high UV tiling (3.5x)
            Mesh soilMesh = GenerateSmoothOrganicMoundMesh(radius: 2.8f, maxElevation: 0.17f, centerWorld: center, uvTiling: 3.5f);
            GameObject soilGo = new GameObject("Soil_Mulch_Mound");
            soilGo.transform.SetParent(bed.transform, false);
            var mf = soilGo.AddComponent<MeshFilter>();
            mf.sharedMesh = soilMesh;
            var mr = soilGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mulchMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            soilGo.AddComponent<MeshCollider>().sharedMesh = soilMesh;

            // Stone edging ring
            if (paverMat != null)
            {
                Mesh stoneMesh = GenerateRingEdgingMesh(radius: 2.85f, stoneWidth: 0.18f, centerWorld: center);
                GameObject stoneGo = new GameObject("Stone_Edging_Border");
                stoneGo.transform.SetParent(bed.transform, false);
                var smf = stoneGo.AddComponent<MeshFilter>();
                smf.sharedMesh = stoneMesh;
                var smr = stoneGo.AddComponent<MeshRenderer>();
                smr.sharedMaterial = paverMat;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
            }

            // 2. Five distinct garden lobes (Center + 4 quadrants) for full balanced coverage
            Random.InitState(502);
            List<CombineInstance> flowerCombines = new List<CombineInstance>();
            List<CombineInstance> foliageCombines = new List<CombineInstance>();

            var lobes = new (Vector2 lobeCenter, FlowerColorType c1, FlowerColorType c2, FoliageType fol, float baseH)[]
            {
                // Central crown: Tall Lavender & Marigold
                (new Vector2( 0.0f,   0.0f),  FlowerColorType.Lavender, FlowerColorType.Yellow, FoliageType.Hosta, 0.58f),
                // South-East foreground lobe: Soft Pink Roses & White Daisies
                (new Vector2( 0.95f, -0.95f), FlowerColorType.Pink,     FlowerColorType.White,    FoliageType.Groundcover, 0.44f),
                // South-West foreground lobe: Golden Marigolds & Lavender
                (new Vector2(-1.05f, -0.85f), FlowerColorType.Yellow,   FlowerColorType.Lavender, FoliageType.Herb, 0.42f),
                // North-East background lobe: Shasta White Daisies & Pink Roses
                (new Vector2( 1.10f,  0.90f), FlowerColorType.White,    FlowerColorType.Pink,     FoliageType.Fern, 0.46f),
                // North-West background lobe: Royal French Lavender & Golden Marigolds
                (new Vector2(-1.00f,  0.95f), FlowerColorType.Lavender, FlowerColorType.Yellow,   FoliageType.Hosta, 0.46f)
            };

            foreach (var lb in lobes)
            {
                // 3 well-spaced flowers per lobe (spaced 0.52m apart - NO congestion!)
                var fOffsets = new Vector2[]
                {
                    Vector2.zero,
                    new Vector2(-0.36f,  0.26f),
                    new Vector2( 0.38f, -0.24f)
                };

                for (int i = 0; i < fOffsets.Length; i++)
                {
                    Vector2 p2D = lb.lobeCenter + fOffsets[i];
                    Vector3 worldPos = center + new Vector3(p2D.x, 0f, p2D.y);
                    float groundY = SampleTerrainHeight(worldPos.x, worldPos.z);
                    float distR = p2D.magnitude;
                    float moundY = Mathf.Max(0f, 0.17f * Mathf.Exp(-1.0f * distR * distR));
                    float localY = (groundY - center.y) + moundY + 0.015f;

                    Vector3 plantPos = new Vector3(p2D.x, localY, p2D.y);
                    Quaternion plantRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                    FlowerColorType col = (i == 2) ? lb.c2 : lb.c1;
                    float fh = lb.baseH * Random.Range(0.92f, 1.08f);
                    float fw = fh * 0.72f;

                    Mesh flwMesh = CreateCrispFlowerMesh(col, fh, fw);
                    flowerCombines.Add(new CombineInstance
                    {
                        mesh = flwMesh,
                        transform = Matrix4x4.TRS(plantPos, plantRot, Vector3.one)
                    });
                }

                // 2 Foliage accents placed naturally to frame each lobe
                var folOffsets = new Vector2[] { new Vector2(-0.46f, -0.36f), new Vector2(0.44f, 0.36f) };
                foreach (var fo in folOffsets)
                {
                    Vector2 p2D = lb.lobeCenter + fo;
                    Vector3 worldPos = center + new Vector3(p2D.x, 0f, p2D.y);
                    float groundY = SampleTerrainHeight(worldPos.x, worldPos.z);
                    float distR = p2D.magnitude;
                    float moundY = Mathf.Max(0f, 0.17f * Mathf.Exp(-1.0f * distR * distR));
                    float localY = (groundY - center.y) + moundY + 0.012f;

                    Vector3 plantPos = new Vector3(p2D.x, localY, p2D.y);
                    Quaternion plantRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                    Mesh folMesh = CreateCrispLeafMesh(lb.fol, leafSize: Random.Range(0.44f, 0.58f));
                    foliageCombines.Add(new CombineInstance
                    {
                        mesh = folMesh,
                        transform = Matrix4x4.TRS(plantPos, plantRot, Vector3.one)
                    });
                }
            }

            AttachCombinedObjects("Foliage_Layer", bed.transform, foliageCombines, foliageMat, castShadows: false);
            AttachCombinedObjects("Flowers_Layer", bed.transform, flowerCombines, flowerMat, castShadows: false);

            return bed;
        }

        // =========================================================================
        // FLOWER BED 3 — Low Decorative Flower Bed (Southwest Lawn)
        // Full ribbon coverage with balanced foreground and background planting
        // =========================================================================
        private static GameObject CreateFlowerBed03(Transform parent, Material mulchMat, Material flowerMat, Material foliageMat, Material paverMat)
        {
            GameObject bed = new GameObject("FlowerBed_03");
            bed.transform.SetParent(parent, false);

            Vector3 center = new Vector3(-8.0f, 0f, -18.0f);
            center.y = SampleTerrainHeight(center.x, center.z);
            bed.transform.position = center;

            // 1. Long, gently curved low-profile bed (10m ribbon)
            List<Vector2> spine = new List<Vector2>();
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                float lz = Mathf.Lerp(-3.8f, 3.8f, t);
                float lx = Mathf.Sin(t * Mathf.PI) * 1.10f;
                spine.Add(new Vector2(lx, lz));
            }

            Mesh soilMesh = GenerateCurvedMoundMesh(spine, width: 2.3f, maxElevation: 0.14f, centerWorld: center, uTiling: 24f, vTiling: 6f);
            GameObject soilGo = new GameObject("Soil_Mulch_Mound");
            soilGo.transform.SetParent(bed.transform, false);
            var mf = soilGo.AddComponent<MeshFilter>();
            mf.sharedMesh = soilMesh;
            var mr = soilGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mulchMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            soilGo.AddComponent<MeshCollider>().sharedMesh = soilMesh;

            // Stone edging border
            if (paverMat != null)
            {
                Mesh stoneMesh = GenerateCurvedEdgingMesh(spine, width: 2.35f, stoneWidth: 0.18f, centerWorld: center);
                GameObject stoneGo = new GameObject("Stone_Edging_Border");
                stoneGo.transform.SetParent(bed.transform, false);
                var smf = stoneGo.AddComponent<MeshFilter>();
                smf.sharedMesh = stoneMesh;
                var smr = stoneGo.AddComponent<MeshRenderer>();
                smr.sharedMaterial = paverMat;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr.receiveShadows = true;
            }

            // 2. Low-profile flowers and creeping foliage in balanced 2-row layout
            Random.InitState(603);
            List<CombineInstance> flowerCombines = new List<CombineInstance>();
            List<CombineInstance> foliageCombines = new List<CombineInstance>();

            int stations = 7;
            for (int s = 0; s < stations; s++)
            {
                float t = (s + 0.5f) / stations;
                int spineIdx = Mathf.Clamp((int)(t * (spine.Count - 1)), 1, spine.Count - 2);
                Vector2 sp = spine[spineIdx];
                Vector2 dir = (spine[spineIdx + 1] - spine[spineIdx - 1]).normalized;
                Vector2 norm = new Vector2(-dir.y, dir.x);

                // Two plants per station: one forward (+norm), one back (-norm)
                // Both rows active! Foreground is filled!
                FlowerColorType c1 = (s % 3 == 0) ? FlowerColorType.White : ((s % 3 == 1) ? FlowerColorType.Pink : FlowerColorType.Yellow);
                FlowerColorType c2 = (s % 2 == 0) ? FlowerColorType.Lavender : FlowerColorType.Pink;

                var plants = new (Vector2 pos, FlowerColorType col, float h)[]
                {
                    (sp + norm * 0.40f, c1, Random.Range(0.28f, 0.36f)),
                    (sp - norm * 0.40f, c2, Random.Range(0.26f, 0.34f))
                };

                foreach (var p in plants)
                {
                    Vector3 worldPos = center + new Vector3(p.pos.x, 0f, p.pos.y);
                    float groundY = SampleTerrainHeight(worldPos.x, worldPos.z);
                    float moundY = GetMoundHeight(p.pos, spine, 2.3f, 0.14f);
                    float localY = (groundY - center.y) + moundY + 0.012f;

                    Vector3 plantPos = new Vector3(p.pos.x, localY, p.pos.y);
                    float fw = p.h * 0.75f;

                    Mesh flwMesh = CreateCrispFlowerMesh(p.col, p.h, fw);
                    flowerCombines.Add(new CombineInstance
                    {
                        mesh = flwMesh,
                        transform = Matrix4x4.TRS(plantPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one)
                    });
                }

                // Creeping groundcover tuft in the center between the rows
                Vector2 folP2D = sp + norm * Random.Range(-0.1f, 0.1f);
                Vector3 fWorldPos = center + new Vector3(folP2D.x, 0f, folP2D.y);
                float fGroundY = SampleTerrainHeight(fWorldPos.x, fWorldPos.z);
                float fMoundY = GetMoundHeight(folP2D, spine, 2.3f, 0.14f);
                float fLocalY = (fGroundY - center.y) + fMoundY + 0.010f;

                Vector3 fPlantPos = new Vector3(folP2D.x, fLocalY, folP2D.y);
                FoliageType fType = (s % 2 == 0) ? FoliageType.Groundcover : FoliageType.Herb;
                Mesh folMesh = CreateCrispLeafMesh(fType, leafSize: Random.Range(0.36f, 0.45f));
                foliageCombines.Add(new CombineInstance
                {
                    mesh = folMesh,
                    transform = Matrix4x4.TRS(fPlantPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one)
                });
            }

            AttachCombinedObjects("Foliage_Layer", bed.transform, foliageCombines, foliageMat, castShadows: false);
            AttachCombinedObjects("Flowers_Layer", bed.transform, flowerCombines, flowerMat, castShadows: false);

            return bed;
        }

        // =========================================================================
        // MESH GENERATION HELPERS
        // =========================================================================

        private static Mesh GenerateCurvedMoundMesh(List<Vector2> spine, float width, float maxElevation, Vector3 centerWorld, float uTiling, float vTiling)
        {
            Mesh mesh = new Mesh();
            mesh.name = "CurvedMound_Mesh";

            int segments = spine.Count;
            int radialSteps = 14;

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int i = 0; i < segments; i++)
            {
                Vector2 sp = spine[i];
                Vector2 fwd;
                if (i == 0) fwd = (spine[1] - spine[0]).normalized;
                else if (i == segments - 1) fwd = (spine[segments - 1] - spine[segments - 2]).normalized;
                else fwd = (spine[i + 1] - spine[i - 1]).normalized;

                Vector2 right = new Vector2(-fwd.y, fwd.x);

                // Gentle taper at tips
                float endFade = Mathf.Sin((float)i / (segments - 1) * Mathf.PI);
                float curWidth = width * Mathf.Lerp(0.40f, 1.0f, endFade);
                float curElev = maxElevation * Mathf.Lerp(0.20f, 1.0f, endFade);

                for (int r = 0; r <= radialSteps; r++)
                {
                    float fr = (float)r / radialSteps;
                    float span = (fr - 0.5f) * 2f; // -1 to +1

                    // Smooth dome elevation
                    float dome = Mathf.Max(0f, 1f - span * span);
                    float elev = curElev * dome;

                    Vector2 local2D = sp + right * (span * curWidth * 0.5f);
                    Vector3 worldPos = centerWorld + new Vector3(local2D.x, 0f, local2D.y);
                    float groundY = SampleTerrainHeight(worldPos.x, worldPos.z);

                    float localY = (groundY - centerWorld.y) + elev;
                    verts.Add(new Vector3(local2D.x, localY, local2D.y));

                    // Dense UV tiling for razor-sharp mulch chips
                    float u = ((float)i / (segments - 1)) * uTiling;
                    float v = fr * vTiling;
                    uvs.Add(new Vector2(u, v));
                }
            }

            int stride = radialSteps + 1;
            for (int i = 0; i < segments - 1; i++)
            {
                for (int r = 0; r < radialSteps; r++)
                {
                    int row1 = i * stride + r;
                    int row2 = (i + 1) * stride + r;

                    // Counter-clockwise winding for UPWARD normals
                    tris.Add(row1); tris.Add(row1 + 1); tris.Add(row2);
                    tris.Add(row1 + 1); tris.Add(row2 + 1); tris.Add(row2);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GenerateSmoothOrganicMoundMesh(float radius, float maxElevation, Vector3 centerWorld, float uvTiling)
        {
            Mesh mesh = new Mesh();
            mesh.name = "SmoothOrganicMound_Mesh";

            int radialRings = 16;
            int angularSlices = 40;

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            // Center apex
            Vector3 centerWorldPos = centerWorld;
            float cGroundY = SampleTerrainHeight(centerWorldPos.x, centerWorldPos.z);
            verts.Add(new Vector3(0f, (cGroundY - centerWorld.y) + maxElevation, 0f));
            uvs.Add(new Vector2(0f, 0f));

            for (int r = 1; r <= radialRings; r++)
            {
                float fracR = (float)r / radialRings;
                for (int a = 0; a < angularSlices; a++)
                {
                    float theta = (float)a / angularSlices * Mathf.PI * 2f;
                    float rMod = radius * (1.0f + 0.15f * Mathf.Cos(2f * theta) + 0.12f * Mathf.Sin(3f * theta));
                    float curR = rMod * fracR;

                    float lx = Mathf.Cos(theta) * curR;
                    float lz = Mathf.Sin(theta) * curR;

                    float dome = Mathf.Max(0f, 1f - fracR * fracR);
                    float elev = maxElevation * dome;

                    Vector3 worldPos = centerWorld + new Vector3(lx, 0f, lz);
                    float groundY = SampleTerrainHeight(worldPos.x, worldPos.z);
                    float localY = (groundY - centerWorld.y) + elev;

                    verts.Add(new Vector3(lx, localY, lz));
                    uvs.Add(new Vector2(lx * uvTiling, lz * uvTiling));
                }
            }

            for (int a = 0; a < angularSlices; a++)
            {
                int nextA = (a + 1) % angularSlices;
                tris.Add(0);
                tris.Add(1 + nextA);
                tris.Add(1 + a);
            }

            for (int r = 1; r < radialRings; r++)
            {
                int ring1Start = 1 + (r - 1) * angularSlices;
                int ring2Start = 1 + r * angularSlices;
                for (int a = 0; a < angularSlices; a++)
                {
                    int nextA = (a + 1) % angularSlices;

                    int r1_0 = ring1Start + a;
                    int r1_1 = ring1Start + nextA;
                    int r2_0 = ring2Start + a;
                    int r2_1 = ring2Start + nextA;

                    tris.Add(r1_0); tris.Add(r1_1); tris.Add(r2_0);
                    tris.Add(r1_1); tris.Add(r2_1); tris.Add(r2_0);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GenerateCurvedEdgingMesh(List<Vector2> spine, float width, float stoneWidth, Vector3 centerWorld)
        {
            Mesh mesh = new Mesh();
            mesh.name = "CurvedStoneEdging_Mesh";

            int segments = spine.Count;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int sideSign = -1; sideSign <= 1; sideSign += 2)
            {
                int baseVertOffset = verts.Count;
                for (int i = 0; i < segments; i++)
                {
                    Vector2 sp = spine[i];
                    Vector2 fwd;
                    if (i == 0) fwd = (spine[1] - spine[0]).normalized;
                    else if (i == segments - 1) fwd = (spine[segments - 1] - spine[segments - 2]).normalized;
                    else fwd = (spine[i + 1] - spine[i - 1]).normalized;

                    Vector2 right = new Vector2(-fwd.y, fwd.x);
                    float endFade = Mathf.Sin((float)i / (segments - 1) * Mathf.PI);
                    float curWidth = width * Mathf.Lerp(0.40f, 1.0f, endFade);

                    Vector2 inner2D = sp + right * (sideSign * curWidth * 0.5f);
                    Vector3 innerW = centerWorld + new Vector3(inner2D.x, 0f, inner2D.y);
                    float innerY = (SampleTerrainHeight(innerW.x, innerW.z) - centerWorld.y) + 0.04f;

                    Vector2 outer2D = inner2D + right * (sideSign * stoneWidth);
                    Vector3 outerW = centerWorld + new Vector3(outer2D.x, 0f, outer2D.y);
                    float outerY = (SampleTerrainHeight(outerW.x, outerW.z) - centerWorld.y) + 0.02f;

                    verts.Add(new Vector3(inner2D.x, innerY, inner2D.y));
                    verts.Add(new Vector3(outer2D.x, outerY, outer2D.y));

                    float u = ((float)i / (segments - 1)) * 16.0f;
                    uvs.Add(new Vector2(u, 0f));
                    uvs.Add(new Vector2(u, 1f));
                }

                for (int i = 0; i < segments - 1; i++)
                {
                    int r1_0 = baseVertOffset + i * 2;
                    int r1_1 = baseVertOffset + i * 2 + 1;
                    int r2_0 = baseVertOffset + (i + 1) * 2;
                    int r2_1 = baseVertOffset + (i + 1) * 2 + 1;

                    if (sideSign > 0)
                    {
                        tris.Add(r1_0); tris.Add(r1_1); tris.Add(r2_0);
                        tris.Add(r1_1); tris.Add(r2_1); tris.Add(r2_0);
                    }
                    else
                    {
                        tris.Add(r1_0); tris.Add(r2_0); tris.Add(r1_1);
                        tris.Add(r1_1); tris.Add(r2_0); tris.Add(r2_1);
                    }
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GenerateRingEdgingMesh(float radius, float stoneWidth, Vector3 centerWorld)
        {
            Mesh mesh = new Mesh();
            mesh.name = "RingStoneEdging_Mesh";

            int slices = 48;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            for (int a = 0; a <= slices; a++)
            {
                float theta = (float)a / slices * Mathf.PI * 2f;
                float rMod = radius * (1.0f + 0.15f * Mathf.Cos(2f * theta) + 0.12f * Mathf.Sin(3f * theta));

                float inX = Mathf.Cos(theta) * rMod;
                float inZ = Mathf.Sin(theta) * rMod;
                Vector3 inW = centerWorld + new Vector3(inX, 0f, inZ);
                float inY = (SampleTerrainHeight(inW.x, inW.z) - centerWorld.y) + 0.04f;

                float outX = Mathf.Cos(theta) * (rMod + stoneWidth);
                float outZ = Mathf.Sin(theta) * (rMod + stoneWidth);
                Vector3 outW = centerWorld + new Vector3(outX, 0f, outZ);
                float outY = (SampleTerrainHeight(outW.x, outW.z) - centerWorld.y) + 0.02f;

                verts.Add(new Vector3(inX, inY, inZ));
                verts.Add(new Vector3(outX, outY, outZ));

                float u = ((float)a / slices) * 20.0f;
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }

            for (int a = 0; a < slices; a++)
            {
                int r1_0 = a * 2;
                int r1_1 = a * 2 + 1;
                int r2_0 = (a + 1) * 2;
                int r2_1 = (a + 1) * 2 + 1;

                tris.Add(r1_0); tris.Add(r1_1); tris.Add(r2_0);
                tris.Add(r1_1); tris.Add(r2_1); tris.Add(r2_0);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float GetMoundHeight(Vector2 p, List<Vector2> spine, float width, float maxElevation)
        {
            float minDist = float.MaxValue;
            for (int i = 0; i < spine.Count - 1; i++)
            {
                Vector2 a = spine[i];
                Vector2 b = spine[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                float d = Vector2.Distance(p, a + t * ab);
                if (d < minDist) minDist = d;
            }
            float span = Mathf.Clamp01(minDist / (width * 0.5f));
            return maxElevation * Mathf.Max(0f, 1f - span * span);
        }

        // =========================================================================
        // CRISP BOTANICAL PROCEDURAL MESHES (Targeting 4K Atlas)
        // =========================================================================

        private static Mesh CreateCrispFlowerMesh(FlowerColorType col, float height, float width)
        {
            Mesh mesh = new Mesh();
            mesh.name = "CrispFlower_" + col;

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

            float u0 = 0.04f, u1 = 0.46f, v0 = 0.52f, v1 = 0.98f;
            switch (col)
            {
                case FlowerColorType.Lavender:
                    u0 = 0.04f; u1 = 0.46f; v0 = 0.52f; v1 = 0.98f; break;
                case FlowerColorType.White:
                    u0 = 0.54f; u1 = 0.96f; v0 = 0.52f; v1 = 0.98f; break;
                case FlowerColorType.Pink:
                    u0 = 0.04f; u1 = 0.46f; v0 = 0.02f; v1 = 0.48f; break;
                case FlowerColorType.Yellow:
                    u0 = 0.54f; u1 = 0.96f; v0 = 0.02f; v1 = 0.48f; break;
            }

            int planes = 3;
            float hw = width * 0.5f;

            for (int p = 0; p < planes; p++)
            {
                float angle = p * (Mathf.PI / planes);
                Vector3 right = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * hw;

                int baseIdx = verts.Count;

                verts.Add(-right);
                verts.Add(right);
                verts.Add(-right + Vector3.up * height);
                verts.Add(right + Vector3.up * height);

                uvs.Add(new Vector2(u0, v0));
                uvs.Add(new Vector2(u1, v0));
                uvs.Add(new Vector2(u0, v1));
                uvs.Add(new Vector2(u1, v1));

                Vector3 n = Vector3.up;
                norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);

                tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
                tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCrispLeafMesh(FoliageType fType, float leafSize)
        {
            Mesh mesh = new Mesh();
            mesh.name = "CrispLeaf_" + fType;

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

            float u0 = 0.04f, u1 = 0.46f, v0 = 0.52f, v1 = 0.98f;
            switch (fType)
            {
                case FoliageType.Hosta:
                    u0 = 0.04f; u1 = 0.46f; v0 = 0.52f; v1 = 0.98f; break;
                case FoliageType.Fern:
                    u0 = 0.54f; u1 = 0.96f; v0 = 0.52f; v1 = 0.98f; break;
                case FoliageType.Groundcover:
                    u0 = 0.04f; u1 = 0.46f; v0 = 0.02f; v1 = 0.48f; break;
                case FoliageType.Herb:
                    u0 = 0.54f; u1 = 0.96f; v0 = 0.02f; v1 = 0.48f; break;
            }

            int leafCount = (fType == FoliageType.Groundcover) ? 4 : 3;
            float hw = leafSize * 0.45f;

            for (int i = 0; i < leafCount; i++)
            {
                float angle = (i / (float)leafCount) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0.35f, Mathf.Sin(angle)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * hw;
                Vector3 tip = dir * leafSize;

                int baseIdx = verts.Count;

                verts.Add(Vector3.zero);
                verts.Add(dir * (leafSize * 0.4f) + side);
                verts.Add(dir * (leafSize * 0.4f) - side);
                verts.Add(tip);

                uvs.Add(new Vector2(Mathf.Lerp(u0, u1, 0.5f), v0));
                uvs.Add(new Vector2(u1, Mathf.Lerp(v0, v1, 0.5f)));
                uvs.Add(new Vector2(u0, Mathf.Lerp(v0, v1, 0.5f)));
                uvs.Add(new Vector2(Mathf.Lerp(u0, u1, 0.5f), v1));

                Vector3 n = Vector3.up;
                norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);

                tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
                tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 1);
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 3); tris.Add(baseIdx + 2);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AttachCombinedObjects(string name, Transform parent, List<CombineInstance> combines, Material mat, bool castShadows)
        {
            if (combines.Count == 0) return;

            Mesh combinedMesh = new Mesh();
            combinedMesh.name = name + "_Mesh";
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            combinedMesh.CombineMeshes(combines.ToArray(), true, true);
            combinedMesh.RecalculateNormals();

            Vector3[] norms = combinedMesh.normals;
            for (int i = 0; i < norms.Length; i++) norms[i] = Vector3.up;
            combinedMesh.normals = norms;
            combinedMesh.RecalculateBounds();

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = combinedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
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
