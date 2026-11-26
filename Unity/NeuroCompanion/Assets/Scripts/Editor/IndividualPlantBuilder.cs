#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeuroCompanion.Backend.Editor
{
    public static class IndividualPlantBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FlowerMatPath = "Assets/Materials/Environment/FlowerBeds/Flower_Blossom.mat";
        private const string FoliageMatPath = "Assets/Materials/Environment/FlowerBeds/Foliage_Base.mat";

        public enum PlantType
        {
            Lavender,
            WhiteDaisy,
            BlushRose,
            GoldenMarigold,
            Hosta,
            Fern,
            CreepingThyme,
            GardenHerb
        }

        [MenuItem("NeuroCompanion/Build Phase 3.4 Individual Plants")]
        public static void BuildIndividualPlants()
        {
            Debug.Log("[IndividualPlantBuilder] Building Phase 3.4 Individual Flowers & Plants...");

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Load Materials
            Material flowerMat = AssetDatabase.LoadAssetAtPath<Material>(FlowerMatPath);
            Material foliageMat = AssetDatabase.LoadAssetAtPath<Material>(FoliageMatPath);

            if (flowerMat == null || foliageMat == null)
            {
                Debug.LogError("[IndividualPlantBuilder] Required botanical materials not found!");
                return;
            }

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

            // Clean up any existing Individual_Plants container
            Transform existingParent = gardenRoot.transform.Find("Individual_Plants");
            if (existingParent != null)
            {
                GameObject.DestroyImmediate(existingParent.gameObject);
            }

            // Create single organized parent
            GameObject individualPlantsRoot = new GameObject("Individual_Plants");
            individualPlantsRoot.transform.SetParent(gardenRoot.transform, false);
            individualPlantsRoot.transform.position = Vector3.zero;

            Random.InitState(777);

            // Subgroup 1: TreeBase_Accents
            GameObject treeBaseGroup = new GameObject("TreeBase_Accents");
            treeBaseGroup.transform.SetParent(individualPlantsRoot.transform, false);
            BuildTreeBaseAccents(treeBaseGroup.transform, flowerMat, foliageMat);

            // Subgroup 2: Meadow_Wildflowers
            GameObject meadowGroup = new GameObject("Meadow_Wildflowers");
            meadowGroup.transform.SetParent(individualPlantsRoot.transform, false);
            BuildMeadowWildflowers(meadowGroup.transform, flowerMat, foliageMat);

            // Subgroup 3: Pathway_Accents
            GameObject pathwayGroup = new GameObject("Pathway_Accents");
            pathwayGroup.transform.SetParent(individualPlantsRoot.transform, false);
            BuildPathwayAccents(pathwayGroup.transform, flowerMat, foliageMat);

            // Subgroup 4: FenceLine_Accents
            GameObject fenceGroup = new GameObject("FenceLine_Accents");
            fenceGroup.transform.SetParent(individualPlantsRoot.transform, false);
            BuildFenceLineAccents(fenceGroup.transform, flowerMat, foliageMat);

            // Keep [GARDEN_GAME] inactive for standard game bootstrap
            gardenRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[IndividualPlantBuilder] Phase 3.4 Individual Plants successfully created!");
        }

        // -------------------------------------------------------------
        // Subgroup 1: TreeBase_Accents
        // -------------------------------------------------------------
        private static void BuildTreeBaseAccents(Transform parent, Material flwMat, Material folMat)
        {
            var treeAccents = new (string name, Vector3 basePos, (PlantType type, Vector2 offset, float scale)[] items)[]
            {
                ("CherryTree_1_RootAccent", new Vector3(17.5f, 0f, -10.0f), new []
                {
                    (PlantType.BlushRose, new Vector2(1.5f, 0.8f), 0.44f),
                    (PlantType.Hosta, new Vector2(1.2f, -1.2f), 0.48f),
                    (PlantType.Lavender, new Vector2(-1.4f, 1.2f), 0.46f)
                }),
                ("CherryTree_2_RootAccent", new Vector3(19.0f, 0f, 12.0f), new []
                {
                    (PlantType.GoldenMarigold, new Vector2(1.2f, -0.8f), 0.42f),
                    (PlantType.Fern, new Vector2(-1.2f, 0.9f), 0.46f),
                    (PlantType.WhiteDaisy, new Vector2(0.6f, 1.3f), 0.42f)
                }),
                ("CherryTree_3_RootAccent", new Vector3(-9.0f, 0f, 12.0f), new []
                {
                    (PlantType.Lavender, new Vector2(-1.2f, -0.7f), 0.46f),
                    (PlantType.Hosta, new Vector2(1.2f, 0.8f), 0.48f),
                    (PlantType.WhiteDaisy, new Vector2(-0.6f, 1.3f), 0.42f)
                }),
                ("CherryTree_4_RootAccent", new Vector3(-12.0f, 0f, -10.0f), new []
                {
                    (PlantType.BlushRose, new Vector2(1.2f, -0.9f), 0.44f),
                    (PlantType.CreepingThyme, new Vector2(-1.1f, -0.8f), 0.38f),
                    (PlantType.WhiteDaisy, new Vector2(0.8f, 1.1f), 0.42f)
                }),
                ("RealisticTree_1_BaseAccent", new Vector3(24.0f, 0f, -16.0f), new []
                {
                    (PlantType.Fern, new Vector2(-1.2f, 0.6f), 0.50f),
                    (PlantType.GardenHerb, new Vector2(0.8f, -0.9f), 0.44f)
                }),
                ("RealisticTree_6_BaseAccent", new Vector3(-24.0f, 0f, 2.0f), new []
                {
                    (PlantType.Lavender, new Vector2(1.0f, -0.8f), 0.44f),
                    (PlantType.Hosta, new Vector2(-0.8f, 1.0f), 0.46f)
                })
            };

            foreach (var ta in treeAccents)
            {
                GameObject group = new GameObject(ta.name);
                group.transform.SetParent(parent, false);

                for (int i = 0; i < ta.items.Length; i++)
                {
                    var item = ta.items[i];
                    Vector3 worldPos = ta.basePos + new Vector3(item.offset.x, 0f, item.offset.y);
                    worldPos.y = SampleTerrainHeight(worldPos.x, worldPos.z) + 0.01f;

                    CreatePlantInstance(group.transform, item.type.ToString() + "_" + (i + 1), item.type, worldPos, item.scale, flwMat, folMat);
                }
            }
        }

        // -------------------------------------------------------------
        // Subgroup 2: Meadow_Wildflowers
        // -------------------------------------------------------------
        private static void BuildMeadowWildflowers(Transform parent, Material flwMat, Material folMat)
        {
            var meadowClusters = new (string name, Vector3 basePos, (PlantType type, Vector2 offset, float scale)[] items)[]
            {
                ("Meadow_Wildflower_Cluster_01", new Vector3(-16.0f, 0f, 10.0f), new []
                {
                    (PlantType.WhiteDaisy, new Vector2(0f, 0f), 0.40f),
                    (PlantType.Lavender, new Vector2(0.55f, 0.35f), 0.44f),
                    (PlantType.CreepingThyme, new Vector2(-0.45f, -0.30f), 0.36f)
                }),
                ("Meadow_Wildflower_Cluster_02", new Vector3(-18.0f, 0f, -4.0f), new []
                {
                    (PlantType.BlushRose, new Vector2(0f, 0f), 0.42f),
                    (PlantType.GoldenMarigold, new Vector2(-0.50f, 0.40f), 0.38f),
                    (PlantType.GardenHerb, new Vector2(0.48f, -0.35f), 0.40f)
                }),
                ("Meadow_Wildflower_Cluster_03", new Vector3(-5.0f, 0f, 20.0f), new []
                {
                    (PlantType.WhiteDaisy, new Vector2(0f, 0f), 0.42f),
                    (PlantType.Lavender, new Vector2(-0.52f, -0.35f), 0.46f),
                    (PlantType.Fern, new Vector2(0.48f, 0.40f), 0.48f)
                })
            };

            foreach (var mc in meadowClusters)
            {
                GameObject group = new GameObject(mc.name);
                group.transform.SetParent(parent, false);

                for (int i = 0; i < mc.items.Length; i++)
                {
                    var item = mc.items[i];
                    Vector3 worldPos = mc.basePos + new Vector3(item.offset.x, 0f, item.offset.y);
                    worldPos.y = SampleTerrainHeight(worldPos.x, worldPos.z) + 0.01f;

                    CreatePlantInstance(group.transform, item.type.ToString() + "_" + (i + 1), item.type, worldPos, item.scale, flwMat, folMat);
                }
            }
        }

        // -------------------------------------------------------------
        // Subgroup 3: Pathway_Accents (Guaranteed distance >= 4.5m from path)
        // -------------------------------------------------------------
        private static void BuildPathwayAccents(Transform parent, Material flwMat, Material folMat)
        {
            var pathAccents = new (string name, Vector3 basePos, (PlantType type, Vector2 offset, float scale)[] items)[]
            {
                ("Pathway_GentleAccent_01", new Vector3(3.5f, 0f, -14.0f), new []
                {
                    (PlantType.BlushRose, new Vector2(0f, 0f), 0.38f),
                    (PlantType.CreepingThyme, new Vector2(0.45f, 0.25f), 0.34f)
                }),
                ("Pathway_GentleAccent_02", new Vector3(0.0f, 0f, 8.0f), new []
                {
                    (PlantType.WhiteDaisy, new Vector2(0f, 0f), 0.38f),
                    (PlantType.CreepingThyme, new Vector2(-0.40f, -0.30f), 0.34f)
                })
            };

            foreach (var pa in pathAccents)
            {
                GameObject group = new GameObject(pa.name);
                group.transform.SetParent(parent, false);

                for (int i = 0; i < pa.items.Length; i++)
                {
                    var item = pa.items[i];
                    Vector3 worldPos = pa.basePos + new Vector3(item.offset.x, 0f, item.offset.y);
                    worldPos.y = SampleTerrainHeight(worldPos.x, worldPos.z) + 0.01f;

                    CreatePlantInstance(group.transform, item.type.ToString() + "_" + (i + 1), item.type, worldPos, item.scale, flwMat, folMat);
                }
            }
        }

        // -------------------------------------------------------------
        // Subgroup 4: FenceLine_Accents
        // -------------------------------------------------------------
        private static void BuildFenceLineAccents(Transform parent, Material flwMat, Material folMat)
        {
            var fenceAccents = new (string name, Vector3 basePos, (PlantType type, Vector2 offset, float scale)[] items)[]
            {
                ("North_Fence_Pocket_01", new Vector3(2.0f, 0f, 26.5f), new []
                {
                    (PlantType.Lavender, new Vector2(0f, 0f), 0.44f),
                    (PlantType.Hosta, new Vector2(0.48f, -0.32f), 0.48f)
                }),
                ("North_Fence_Pocket_02", new Vector3(-14.0f, 0f, 25.5f), new []
                {
                    (PlantType.GoldenMarigold, new Vector2(0f, 0f), 0.40f),
                    (PlantType.Fern, new Vector2(-0.45f, 0.30f), 0.46f)
                }),
                ("East_Fence_Pocket_01", new Vector3(25.5f, 0f, -8.0f), new []
                {
                    (PlantType.WhiteDaisy, new Vector2(0f, 0f), 0.42f),
                    (PlantType.CreepingThyme, new Vector2(-0.40f, -0.28f), 0.36f)
                })
            };

            foreach (var fa in fenceAccents)
            {
                GameObject group = new GameObject(fa.name);
                group.transform.SetParent(parent, false);

                for (int i = 0; i < fa.items.Length; i++)
                {
                    var item = fa.items[i];
                    Vector3 worldPos = fa.basePos + new Vector3(item.offset.x, 0f, item.offset.y);
                    worldPos.y = SampleTerrainHeight(worldPos.x, worldPos.z) + 0.01f;

                    CreatePlantInstance(group.transform, item.type.ToString() + "_" + (i + 1), item.type, worldPos, item.scale, flwMat, folMat);
                }
            }
        }

        // -------------------------------------------------------------
        // Plant Mesh Generator Helpers
        // -------------------------------------------------------------
        private static void CreatePlantInstance(Transform parent, string name, PlantType type, Vector3 worldPos, float scale, Material flwMat, Material folMat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            bool isFlower = (type == PlantType.Lavender || type == PlantType.WhiteDaisy || type == PlantType.BlushRose || type == PlantType.GoldenMarigold);
            Material mat = isFlower ? flwMat : folMat;

            Mesh mesh;
            if (isFlower)
            {
                float u0 = 0.04f, u1 = 0.46f, v0 = 0.52f, v1 = 0.98f;
                switch (type)
                {
                    case PlantType.Lavender:
                        u0 = 0.04f; u1 = 0.46f; v0 = 0.52f; v1 = 0.98f; break;
                    case PlantType.WhiteDaisy:
                        u0 = 0.54f; u1 = 0.96f; v0 = 0.52f; v1 = 0.98f; break;
                    case PlantType.BlushRose:
                        u0 = 0.04f; u1 = 0.46f; v0 = 0.02f; v1 = 0.48f; break;
                    case PlantType.GoldenMarigold:
                        u0 = 0.54f; u1 = 0.96f; v0 = 0.02f; v1 = 0.48f; break;
                }
                mesh = CreateFlowerQuadMesh(scale, scale * 0.75f, u0, u1, v0, v1);
            }
            else
            {
                float u0 = 0.04f, u1 = 0.46f, v0 = 0.52f, v1 = 0.98f;
                switch (type)
                {
                    case PlantType.Hosta:
                        u0 = 0.04f; u1 = 0.46f; v0 = 0.52f; v1 = 0.98f; break;
                    case PlantType.Fern:
                        u0 = 0.54f; u1 = 0.96f; v0 = 0.52f; v1 = 0.98f; break;
                    case PlantType.CreepingThyme:
                        u0 = 0.04f; u1 = 0.46f; v0 = 0.02f; v1 = 0.48f; break;
                    case PlantType.GardenHerb:
                        u0 = 0.54f; u1 = 0.96f; v0 = 0.02f; v1 = 0.48f; break;
                }
                mesh = CreateLeafTuftMesh(type, scale, u0, u1, v0, v1);
            }

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        private static Mesh CreateFlowerQuadMesh(float height, float width, float u0, float u1, float v0, float v1)
        {
            Mesh mesh = new Mesh();
            mesh.name = "IndividualFlower_Mesh";

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

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

        private static Mesh CreateLeafTuftMesh(PlantType type, float leafSize, float u0, float u1, float v0, float v1)
        {
            Mesh mesh = new Mesh();
            mesh.name = "IndividualLeaf_Mesh";

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

            int leafCount = (type == PlantType.CreepingThyme) ? 4 : 3;
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
                tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
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
