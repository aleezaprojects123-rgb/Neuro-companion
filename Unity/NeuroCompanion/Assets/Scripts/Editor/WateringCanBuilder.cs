#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NeuroCompanion.Environment;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanBuilder
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string GreenAlbedoPath = "Assets/Textures/Environment/WateringCan/WateringCan_GreenMetal_Albedo.png";
        private const string GreenNormalPath = "Assets/Textures/Environment/WateringCan/WateringCan_GreenMetal_Normal.png";
        private const string DropletPath = "Assets/Textures/Environment/WateringCan/Water_Droplet.png";

        private const string GreenMatPath = "Assets/Materials/Environment/WateringCan/WateringCan_Green_PBR.mat";
        private const string BrassMatPath = "Assets/Materials/Environment/WateringCan/WateringCan_Rose_Brass_PBR.mat";
        private const string WaterParticleMatPath = "Assets/Materials/Environment/WateringCan/Water_Droplets_Particle.mat";

        [MenuItem("NeuroCompanion/Build Phase 4.4 Watering Can")]
        public static void BuildWateringCan()
        {
            Debug.Log("[WateringCanBuilder] Building Phase 4.4 Watering Can with Controlled Water-Pouring Effect...");

            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. Configure Textures
            ConfigureTexture(GreenAlbedoPath, isNormal: false, sRGB: true);
            ConfigureTexture(GreenNormalPath, isNormal: true, sRGB: false);
            ConfigureTexture(DropletPath, isNormal: false, sRGB: true, isSprite: true);

            // 2. Setup Materials
            Material greenMat = SetupGreenMaterial();
            Material brassMat = SetupBrassMaterial();
            Material waterMat = SetupWaterParticleMaterial();

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
                Debug.LogError("[WateringCanBuilder] Gardening_Planting_Station not found! Please build Phase 4.1 first.");
                return;
            }

            // Remove existing Watering_Can if present
            Transform existingCan = station.transform.Find("Watering_Can");
            if (existingCan != null)
            {
                GameObject.DestroyImmediate(existingCan.gameObject);
            }

            // 4. Create Parent GameObject Watering_Can under Gardening_Planting_Station
            // Placed naturally on the Lower Storage Shelf (Y = 0.242m) on the right side (X = 0.35m, Z = -0.04m)
            // Sturdy resting place that keeps tabletop 100% open
            GameObject canRoot = new GameObject("Watering_Can");
            canRoot.transform.SetParent(station.transform, false);
            canRoot.transform.localPosition = new Vector3(0.35f, 0.242f, -0.04f);
            canRoot.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);

            // 5. Build Model Components
            // A. Main Body
            BuildCanBody(canRoot.transform, greenMat);

            // B. Dual-Grip Arched Handle
            BuildCanHandle(canRoot.transform, greenMat);

            // C. Long Angled Spout
            BuildCanSpout(canRoot.transform, greenMat);

            // D. Sprinkler Rose & Water Pour Point
            var roseAndPoint = BuildCanRose(canRoot.transform, brassMat, greenMat);
            Transform pourPoint = roseAndPoint.pourPoint;

            // E. Controlled Water Pour Effect (Particle System)
            ParticleSystem ps = BuildWaterParticleSystem(pourPoint, waterMat);

            // F. Controller Component (isolated and strictly OFF by default)
            var controller = canRoot.AddComponent<WateringCanController>();
            controller.InitializeReferences(ps, null);

            // G. Base Interaction Collider
            var col = canRoot.AddComponent<BoxCollider>();
            col.size = new Vector3(0.24f, 0.32f, 0.44f);
            col.center = new Vector3(0f, 0.16f, 0.08f);

            // Save Scene
            GameObject gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot != null) gardenRoot.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[WateringCanBuilder] Phase 4.4 Watering Can & Controlled Pouring Effect successfully built!");
        }

        private static void ConfigureTexture(string path, bool isNormal, bool sRGB, bool isSprite = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            var targetType = isNormal ? TextureImporterType.NormalMap : (isSprite ? TextureImporterType.Sprite : TextureImporterType.Default);
            if (importer.textureType != targetType)
            {
                importer.textureType = targetType;
                dirty = true;
            }
            if (importer.sRGBTexture != sRGB)
            {
                importer.sRGBTexture = sRGB;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }

        private static Material SetupGreenMaterial()
        {
            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GreenAlbedoPath);
            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(GreenNormalPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GreenMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, GreenMatPath);
            }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetColor("_BaseColor", new Color(0.95f, 1.0f, 0.95f, 1f));

            if (normalTex != null)
            {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normalTex);
                mat.SetFloat("_BumpScale", 0.9f);
            }

            mat.SetFloat("_Metallic", 0.45f); // Powder-coated sheet metal
            mat.SetFloat("_Smoothness", 0.36f);
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
            mat.SetColor("_BaseColor", new Color(0.86f, 0.73f, 0.38f, 1f)); // Polished brass sprinkler face
            mat.SetFloat("_Metallic", 0.85f);
            mat.SetFloat("_Smoothness", 0.65f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SetupWaterParticleMaterial()
        {
            var dropletTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DropletPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterParticleMatPath);
            if (mat == null)
            {
                // Particles/Unlit or Universal Render Pipeline/Particles/Unlit
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
                if (sh == null) sh = Shader.Find("Mobile/Particles/Alpha Blended");
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, WaterParticleMatPath);
            }
            mat.SetTexture("_BaseMap", dropletTex);
            mat.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1.0f, 0.75f));

            // Set surface type to Transparent
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0); // Alpha blend
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------------
        // 1. Watering Can Body
        // ---------------------------------------------------------------------
        private static void BuildCanBody(Transform parent, Material mat)
        {
            GameObject bodyGo = new GameObject("WateringCan_Body");
            bodyGo.transform.SetParent(parent, false);
            bodyGo.transform.localPosition = Vector3.zero;
            bodyGo.transform.localRotation = Quaternion.identity;

            var mf = bodyGo.AddComponent<MeshFilter>();
            var mr = bodyGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateCanBodyMesh(bottomRadius: 0.088f, topRadius: 0.076f, height: 0.22f, openingRadius: 0.052f, segments: 24);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private static Mesh GenerateCanBodyMesh(float bottomRadius, float topRadius, float height, float openingRadius, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "WateringCan_Body_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Ring 0: Bottom base
            // Ring 1: Mid-body taper
            // Ring 2: Top shoulder
            // Ring 3: Top opening collar rim
            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                // Base
                verts.Add(new Vector3(cos * bottomRadius, 0f, sin * bottomRadius));
                norms.Add(new Vector3(cos, -0.1f, sin).normalized);
                uvs.Add(new Vector2((float)s / segments * 2f, 0f));

                // Shoulder
                verts.Add(new Vector3(cos * topRadius, height * 0.92f, sin * topRadius));
                norms.Add(new Vector3(cos, 0.1f, sin).normalized);
                uvs.Add(new Vector2((float)s / segments * 2f, 0.75f));

                // Dome rim
                verts.Add(new Vector3(cos * openingRadius, height, sin * openingRadius));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2((float)s / segments * 2f, 1f));
            }

            int stride = 3;
            for (int s = 0; s < segments; s++)
            {
                int r0 = s * stride;
                int r1 = (s + 1) * stride;

                // Cylinder wall (base to shoulder)
                tris.Add(r0); tris.Add(r0 + 1); tris.Add(r1 + 1);
                tris.Add(r0); tris.Add(r1 + 1); tris.Add(r1);

                // Dome collar (shoulder to opening)
                tris.Add(r0 + 1); tris.Add(r0 + 2); tris.Add(r1 + 2);
                tris.Add(r0 + 1); tris.Add(r1 + 2); tris.Add(r1 + 1);
            }

            // Bottom disk
            int baseCenter = verts.Count;
            verts.Add(Vector3.zero);
            norms.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s < segments; s++)
            {
                int i0 = s * stride;
                int i1 = (s + 1) * stride;
                tris.Add(baseCenter);
                tris.Add(i1);
                tris.Add(i0);
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
        // 2. Dual-Grip Arched Handle
        // ---------------------------------------------------------------------
        private static void BuildCanHandle(Transform parent, Material mat)
        {
            GameObject handleGo = new GameObject("WateringCan_Handle");
            handleGo.transform.SetParent(parent, false);
            handleGo.transform.localPosition = Vector3.zero;
            handleGo.transform.localRotation = Quaternion.identity;

            var mf = handleGo.AddComponent<MeshFilter>();
            var mr = handleGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateArchedHandleMesh(tubeRadius: 0.009f, segmentsArc: 20, segmentsTube: 8);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private static Mesh GenerateArchedHandleMesh(float tubeRadius, int segmentsArc, int segmentsTube)
        {
            Mesh mesh = new Mesh();
            mesh.name = "WateringCan_Handle_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Spline path of classic English garden watering can handle:
            // Starts at top center (0, 0.22, 0.02)
            // Loops up to (0, 0.31, -0.04) for top carry grip
            // Curves over to (0, 0.26, -0.16)
            // Extends down to rear base (0, 0.05, -0.10) for pouring grip
            var pathPoints = new List<Vector3>();
            for (int i = 0; i <= segmentsArc; i++)
            {
                float t = (float)i / segmentsArc;
                float y, z;
                if (t < 0.45f)
                {
                    // Top arch
                    float t0 = t / 0.45f;
                    y = Mathf.Lerp(0.22f, 0.31f, Mathf.Sin(t0 * Mathf.PI * 0.5f));
                    z = Mathf.Lerp(0.02f, -0.06f, t0);
                }
                else
                {
                    // Rear curve down to lower body
                    float t1 = (t - 0.45f) / 0.55f;
                    y = Mathf.Lerp(0.31f, 0.05f, t1);
                    z = Mathf.Lerp(-0.06f, -0.10f, Mathf.Sin(t1 * Mathf.PI * 0.5f));
                }
                pathPoints.Add(new Vector3(0f, y, z));
            }

            // Extrude circle along path
            for (int i = 0; i <= segmentsArc; i++)
            {
                Vector3 center = pathPoints[i];
                Vector3 forward = (i < segmentsArc ? (pathPoints[i + 1] - center) : (center - pathPoints[i - 1])).normalized;
                Vector3 right = Vector3.right;
                Vector3 up = Vector3.Cross(forward, right).normalized;

                for (int j = 0; j <= segmentsTube; j++)
                {
                    float a = (float)j / segmentsTube * Mathf.PI * 2f;
                    Vector3 normal = (right * Mathf.Cos(a) + up * Mathf.Sin(a)).normalized;
                    verts.Add(center + normal * tubeRadius);
                    norms.Add(normal);
                    uvs.Add(new Vector2((float)j / segmentsTube, (float)i / segmentsArc * 3f));
                }
            }

            int ringStride = segmentsTube + 1;
            for (int i = 0; i < segmentsArc; i++)
            {
                int r0 = i * ringStride;
                int r1 = (i + 1) * ringStride;
                for (int j = 0; j < segmentsTube; j++)
                {
                    int a = r0 + j;
                    int b = r1 + j;
                    int c = r1 + j + 1;
                    int d = r0 + j + 1;

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

        // ---------------------------------------------------------------------
        // 3. Long Angled Spout
        // ---------------------------------------------------------------------
        private static void BuildCanSpout(Transform parent, Material mat)
        {
            GameObject spoutGo = new GameObject("WateringCan_Spout");
            spoutGo.transform.SetParent(parent, false);
            // Spout originates at front-lower body (0, 0.045, 0.08)
            spoutGo.transform.localPosition = new Vector3(0f, 0.045f, 0.08f);
            spoutGo.transform.localRotation = Quaternion.Euler(46f, 0f, 0f);

            var mf = spoutGo.AddComponent<MeshFilter>();
            var mr = spoutGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateTaperedTubeMesh(baseRadius: 0.015f, tipRadius: 0.009f, length: 0.25f, segmentsTube: 16);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        private static Mesh GenerateTaperedTubeMesh(float baseRadius, float tipRadius, float length, int segmentsTube)
        {
            Mesh mesh = new Mesh();
            mesh.name = "WateringCan_Spout_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Extends along +Z in local space
            for (int s = 0; s <= segmentsTube; s++)
            {
                float a = (float)s / segmentsTube * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                // Base
                verts.Add(new Vector3(cos * baseRadius, sin * baseRadius, 0f));
                norms.Add(new Vector3(cos, sin, 0f));
                uvs.Add(new Vector2((float)s / segmentsTube, 0f));

                // Tip
                verts.Add(new Vector3(cos * tipRadius, sin * tipRadius, length));
                norms.Add(new Vector3(cos, sin, 0f));
                uvs.Add(new Vector2((float)s / segmentsTube, 1f));
            }

            for (int s = 0; s < segmentsTube; s++)
            {
                int r0 = s * 2;
                int r1 = (s + 1) * 2;

                tris.Add(r0); tris.Add(r1); tris.Add(r1 + 1);
                tris.Add(r0); tris.Add(r1 + 1); tris.Add(r0 + 1);
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
        // 4. Sprinkler Rose & Water Pour Point
        // ---------------------------------------------------------------------
        private static (GameObject roseGo, Transform pourPoint) BuildCanRose(Transform parent, Material brassMat, Material greenMat)
        {
            // Positioned at the tip of the spout:
            // Spout pos: (0, 0.045, 0.08), rot: 46 deg around X
            // Tip in local space = pos + rot * (0, 0, 0.25)
            // = (0, 0.045, 0.08) + (0, 0.25 * sin(46), 0.25 * cos(46))
            // = (0, 0.045 + 0.1798, 0.08 + 0.1736) = (0, 0.225, 0.254)
            Vector3 rosePos = new Vector3(0f, 0.225f, 0.254f);
            Quaternion roseRot = Quaternion.Euler(72f, 0f, 0f); // Tilted forward/downward so water showers toward ground/soil

            GameObject roseGo = new GameObject("WateringCan_Rose");
            roseGo.transform.SetParent(parent, false);
            roseGo.transform.localPosition = rosePos;
            roseGo.transform.localRotation = roseRot;

            var mf = roseGo.AddComponent<MeshFilter>();
            var mr = roseGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = GenerateSprinklerRoseMesh(neckRadius: 0.010f, flareRadius: 0.032f, length: 0.038f, segments: 20);
            mr.sharedMaterial = brassMat; // Classic brass perforated rose
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            // Water Pour Point (located exactly at the perforated face center, pointing forward)
            GameObject pourPointGo = new GameObject("Water_Pour_Point");
            pourPointGo.transform.SetParent(roseGo.transform, false);
            pourPointGo.transform.localPosition = new Vector3(0f, 0f, 0.038f); // Center of rose face
            pourPointGo.transform.localRotation = Quaternion.identity;

            return (roseGo, pourPointGo.transform);
        }

        private static Mesh GenerateSprinklerRoseMesh(float neckRadius, float flareRadius, float length, int segments)
        {
            Mesh mesh = new Mesh();
            mesh.name = "WateringCan_Rose_Mesh";

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Conical flare from neck to wide perforated shower face
            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                // Collar
                verts.Add(new Vector3(cos * neckRadius, sin * neckRadius, 0f));
                norms.Add(new Vector3(cos * 0.8f, sin * 0.8f, -0.5f).normalized);
                uvs.Add(new Vector2((float)s / segments, 0f));

                // Outer flare rim
                verts.Add(new Vector3(cos * flareRadius, sin * flareRadius, length * 0.85f));
                norms.Add(new Vector3(cos, sin, 0.2f).normalized);
                uvs.Add(new Vector2((float)s / segments, 0.85f));

                // Face rim
                verts.Add(new Vector3(cos * flareRadius, sin * flareRadius, length));
                norms.Add(Vector3.forward);
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            int stride = 3;
            for (int s = 0; s < segments; s++)
            {
                int r0 = s * stride;
                int r1 = (s + 1) * stride;

                // Conical body
                tris.Add(r0); tris.Add(r1 + 1); tris.Add(r0 + 1);
                tris.Add(r0); tris.Add(r1); tris.Add(r1 + 1);

                // Rim collar
                tris.Add(r0 + 1); tris.Add(r1 + 2); tris.Add(r0 + 2);
                tris.Add(r0 + 1); tris.Add(r1 + 1); tris.Add(r1 + 2);
            }

            // Perforated face cap (slightly domed forward)
            int centerIdx = verts.Count;
            verts.Add(new Vector3(0f, 0f, length + 0.005f));
            norms.Add(Vector3.forward);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s < segments; s++)
            {
                int i0 = s * stride + 2;
                int i1 = (s + 1) * stride + 2;
                tris.Add(centerIdx);
                tris.Add(i0);
                tris.Add(i1);
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
        // 5. Water Particle System (Controlled & strictly OFF by default)
        // ---------------------------------------------------------------------
        private static ParticleSystem BuildWaterParticleSystem(Transform parent, Material waterMat)
        {
            GameObject psGo = new GameObject("Water_Pour_Effect");
            psGo.transform.SetParent(parent, false);
            psGo.transform.localPosition = Vector3.zero;
            psGo.transform.localRotation = Quaternion.identity;

            var ps = psGo.AddComponent<ParticleSystem>();

            // MAIN MODULE - MUST BE OFF BY DEFAULT
            var main = ps.main;
            main.playOnAwake = false; // GUARANTEED OFF ON AWAKE
            main.loop = true;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.024f);
            main.gravityModifier = 1.35f; // Natural gravitational arc downward toward target soil
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;

            // EMISSION MODULE - DISABLED BY DEFAULT
            var emission = ps.emission;
            emission.enabled = false; // GUARANTEED DISABLED BY DEFAULT
            emission.rateOverTime = 120f; // Multi-stream droplet shower

            // SHAPE MODULE (Conical spray from sprinkler rose face)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9.5f; // Gentle focused garden rose spray cone
            shape.radius = 0.026f;
            shape.radiusThickness = 1.0f;

            // COLOR OVER LIFETIME (Translucent water fading naturally)
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.96f, 1f), 0f), new GradientColorKey(new Color(0.85f, 0.94f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0.0f, 1f) }
            );
            col.color = grad;

            // SIZE OVER LIFETIME
            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.8f);
            curve.AddKey(0.5f, 1.0f);
            curve.AddKey(1f, 0.5f);
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            // RENDERER MODULE
            var psr = psGo.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = waterMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.alignment = ParticleSystemRenderSpace.View;
            psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            psr.receiveShadows = false;

            // Stop emission immediately to guarantee clean initial state
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            return ps;
        }
    }
}
#endif