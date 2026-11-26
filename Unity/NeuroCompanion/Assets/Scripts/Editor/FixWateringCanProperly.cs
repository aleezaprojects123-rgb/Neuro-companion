using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;
using NeuroCompanion.Environment;

namespace NeuroCompanion.Backend.Editor
{
    public static class FixWateringCanProperly
    {
        static string OutDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";
        private const string GreenMatPath = "Assets/Materials/Environment/WateringCan/WateringCan_Green_PBR.mat";
        private const string BrassMatPath = "Assets/Materials/Environment/WateringCan/WateringCan_Rose_Brass_PBR.mat";
        private const string WaterParticleMatPath = "Assets/Materials/Environment/WateringCan/Water_Droplets_Particle.mat";

        [MenuItem("NeuroCompanion/Fix Watering Can Upper Shelf")]
        public static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject gardenGame = null;
            bool wasActive = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "[GARDEN_GAME]") { gardenGame = root; break; }
            }

            if (gardenGame == null)
            {
                Debug.LogError("[FixCan] [GARDEN_GAME] not found!");
                return;
            }

            wasActive = gardenGame.activeSelf;
            gardenGame.SetActive(true);

            Transform station = gardenGame.transform.Find("Gardening_Planting_Station");
            if (station == null)
            {
                Debug.LogError("[FixCan] Gardening_Planting_Station not found!");
                gardenGame.SetActive(wasActive);
                return;
            }

            // Load Materials
            Material greenMat = AssetDatabase.LoadAssetAtPath<Material>(GreenMatPath);
            Material brassMat = AssetDatabase.LoadAssetAtPath<Material>(BrassMatPath);
            Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(WaterParticleMatPath);

            // Destroy existing Watering_Can
            Transform existingCan = station.Find("Watering_Can");
            if (existingCan != null)
            {
                Undo.DestroyObjectImmediate(existingCan.gameObject);
            }

            // Create new Watering_Can under station
            GameObject canRoot = new GameObject("Watering_Can");
            Undo.RegisterCreatedObjectUndo(canRoot, "Create Fixed Watering Can");
            canRoot.transform.SetParent(station, false);

            // Place on the UPPER UTILITY SHELF (Top_Utility_Shelf is at Y=1.44, Z=0.24)
            // Left side: X = -0.22, Z = 0.24. Rotated 105 deg so the spout profiles gracefully along the shelf facing right
            canRoot.transform.localPosition = new Vector3(-0.22f, 1.455f, 0.24f);
            canRoot.transform.localRotation = Quaternion.Euler(0f, 105f, 0f);
            canRoot.transform.localScale = Vector3.one;

            // 1. Body
            GameObject bodyGo = new GameObject("WateringCan_Body");
            bodyGo.transform.SetParent(canRoot.transform, false);
            bodyGo.transform.localPosition = Vector3.zero;
            bodyGo.transform.localRotation = Quaternion.identity;
            var mfBody = bodyGo.AddComponent<MeshFilter>();
            var mrBody = bodyGo.AddComponent<MeshRenderer>();
            mfBody.sharedMesh = GenerateCanBodyMesh(bottomRadius: 0.085f, topRadius: 0.075f, openingRadius: 0.055f, height: 0.22f, segments: 24);
            mrBody.sharedMaterial = greenMat;
            mrBody.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrBody.receiveShadows = true;

            // 2. Handle
            GameObject handleGo = new GameObject("WateringCan_Handle");
            handleGo.transform.SetParent(canRoot.transform, false);
            handleGo.transform.localPosition = Vector3.zero;
            handleGo.transform.localRotation = Quaternion.identity;
            var mfHandle = handleGo.AddComponent<MeshFilter>();
            var mrHandle = handleGo.AddComponent<MeshRenderer>();
            mfHandle.sharedMesh = GenerateHandleMesh(tubeRadius: 0.009f, segmentsArc: 20, segmentsTube: 8);
            mrHandle.sharedMaterial = greenMat;
            mrHandle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrHandle.receiveShadows = true;

            // 3. Spout (tilts UPWARDS and forward: -46 deg around X)
            float spoutLength = 0.26f;
            GameObject spoutGo = new GameObject("WateringCan_Spout");
            spoutGo.transform.SetParent(canRoot.transform, false);
            spoutGo.transform.localPosition = new Vector3(0f, 0.045f, 0.08f);
            spoutGo.transform.localRotation = Quaternion.Euler(-46f, 0f, 0f); // -46 deg tilts +Z UP towards +Y!
            var mfSpout = spoutGo.AddComponent<MeshFilter>();
            var mrSpout = spoutGo.AddComponent<MeshRenderer>();
            mfSpout.sharedMesh = GenerateTaperedSpoutMesh(baseRadius: 0.015f, tipRadius: 0.009f, length: spoutLength, segmentsTube: 16);
            mrSpout.sharedMaterial = greenMat;
            mrSpout.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrSpout.receiveShadows = true;

            // 4. Rose (Child of Spout at tip (0, 0, spoutLength) - GUARANTEED ATTACHED!)
            GameObject roseGo = new GameObject("WateringCan_Rose");
            roseGo.transform.SetParent(spoutGo.transform, false);
            roseGo.transform.localPosition = new Vector3(0f, 0f, spoutLength);
            roseGo.transform.localRotation = Quaternion.Euler(22f, 0f, 0f); // Tilted slightly down to face forward-downward
            var mfRose = roseGo.AddComponent<MeshFilter>();
            var mrRose = roseGo.AddComponent<MeshRenderer>();
            mfRose.sharedMesh = GenerateRoseMesh(neckRadius: 0.010f, flareRadius: 0.032f, length: 0.038f, segments: 20);
            mrRose.sharedMaterial = brassMat;
            mrRose.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mrRose.receiveShadows = true;

            // 5. Pour Point (Child of Rose at perforated face)
            GameObject pourPoint = new GameObject("Water_Pour_Point");
            pourPoint.transform.SetParent(roseGo.transform, false);
            pourPoint.transform.localPosition = new Vector3(0f, 0f, 0.038f);
            pourPoint.transform.localRotation = Quaternion.identity;

            // 6. Particle System (Controlled & Strictly Disabled on shelf!)
            ParticleSystem ps = pourPoint.AddComponent<ParticleSystem>();
            var pRenderer = pourPoint.GetComponent<ParticleSystemRenderer>();
            pRenderer.material = waterMat;
            pRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 0.55f;
            main.startSpeed = 1.4f;
            main.startSize = 0.024f;
            main.startColor = new Color(0.85f, 0.94f, 1.0f, 0.65f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = false; // OFF BY DEFAULT! NO WATER POURING!
            emission.rateOverTime = 38f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.025f;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 7. Controller Component
            var controller = canRoot.AddComponent<WateringCanController>();
            controller.InitializeReferences(ps, null);

            // 8. Collider
            var col = canRoot.AddComponent<BoxCollider>();
            col.size = new Vector3(0.24f, 0.32f, 0.44f);
            col.center = new Vector3(0f, 0.16f, 0.08f);

            // Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Take screenshots using local station coordinates transformed to world space
            // A: Direct front overview of the complete station (upper shelf + tabletop + lower shelf)
            Shoot(OutDir + @"\station_front_perfect.png",
                pos: station.TransformPoint(new Vector3(0f, 1.20f, -2.20f)),
                target: station.TransformPoint(new Vector3(0f, 0.95f, 0.05f)));

            // B: Close-up of Upper Shelf showing watering can + small pot with no water pouring
            Shoot(OutDir + @"\watering_can_upper_shelf_closeup.png",
                pos: station.TransformPoint(new Vector3(-0.05f, 1.62f, -0.95f)),
                target: station.TransformPoint(new Vector3(-0.05f, 1.52f, 0.24f)));

            // C: Tabletop workspace close-up showing seedlings, trowel, pots, and completely clear workspace
            Shoot(OutDir + @"\tabletop_seedlings_clear.png",
                pos: station.TransformPoint(new Vector3(0.0f, 1.25f, -0.95f)),
                target: station.TransformPoint(new Vector3(0.05f, 0.88f, 0.08f)));

            // D: Patient view approaching the station from the path (front side)
            Shoot(OutDir + @"\station_patient_perspective.png",
                pos: station.TransformPoint(new Vector3(0f, 1.55f, -3.20f)),
                target: station.TransformPoint(new Vector3(0f, 1.05f, 0f)));

            gardenGame.SetActive(wasActive);
            Debug.Log("[FixCan] Successfully built and placed watering can on upper shelf! Screenshots saved.");
        }

        private static Mesh GenerateCanBodyMesh(float bottomRadius, float topRadius, float openingRadius, float height, int segments)
        {
            Mesh mesh = new Mesh { name = "WateringCan_Body_Mesh" };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                verts.Add(new Vector3(cos * bottomRadius, 0f, sin * bottomRadius));
                norms.Add(new Vector3(cos, 0f, sin));
                uvs.Add(new Vector2((float)s / segments * 2f, 0f));

                verts.Add(new Vector3(cos * topRadius, height * 0.92f, sin * topRadius));
                norms.Add(new Vector3(cos, 0.1f, sin).normalized);
                uvs.Add(new Vector2((float)s / segments * 2f, 0.75f));

                verts.Add(new Vector3(cos * openingRadius, height, sin * openingRadius));
                norms.Add(Vector3.up);
                uvs.Add(new Vector2((float)s / segments * 2f, 1f));
            }

            int stride = 3;
            for (int s = 0; s < segments; s++)
            {
                int r0 = s * stride;
                int r1 = (s + 1) * stride;

                tris.Add(r0); tris.Add(r0 + 1); tris.Add(r1 + 1);
                tris.Add(r0); tris.Add(r1 + 1); tris.Add(r1);

                tris.Add(r0 + 1); tris.Add(r0 + 2); tris.Add(r1 + 2);
                tris.Add(r0 + 1); tris.Add(r1 + 2); tris.Add(r1 + 1);
            }

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

        private static Mesh GenerateHandleMesh(float tubeRadius, int segmentsArc, int segmentsTube)
        {
            Mesh mesh = new Mesh { name = "WateringCan_Handle_Mesh" };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            var pathPoints = new List<Vector3>();
            for (int i = 0; i <= segmentsArc; i++)
            {
                float t = (float)i / segmentsArc;
                float y, z;
                if (t < 0.45f)
                {
                    float t0 = t / 0.45f;
                    y = Mathf.Lerp(0.22f, 0.31f, Mathf.Sin(t0 * Mathf.PI * 0.5f));
                    z = Mathf.Lerp(0.02f, -0.06f, t0);
                }
                else
                {
                    float t1 = (t - 0.45f) / 0.55f;
                    y = Mathf.Lerp(0.31f, 0.05f, t1);
                    z = Mathf.Lerp(-0.06f, -0.10f, Mathf.Sin(t1 * Mathf.PI * 0.5f));
                }
                pathPoints.Add(new Vector3(0f, y, z));
            }

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
                    tris.Add(r0 + j); tris.Add(r1 + j); tris.Add(r1 + j + 1);
                    tris.Add(r0 + j); tris.Add(r1 + j + 1); tris.Add(r0 + j + 1);
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

        private static Mesh GenerateTaperedSpoutMesh(float baseRadius, float tipRadius, float length, int segmentsTube)
        {
            Mesh mesh = new Mesh { name = "WateringCan_Spout_Mesh" };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int s = 0; s <= segmentsTube; s++)
            {
                float a = (float)s / segmentsTube * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                verts.Add(new Vector3(cos * baseRadius, sin * baseRadius, 0f));
                norms.Add(new Vector3(cos, sin, 0f));
                uvs.Add(new Vector2((float)s / segmentsTube, 0f));

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

        private static Mesh GenerateRoseMesh(float neckRadius, float flareRadius, float length, int segments)
        {
            Mesh mesh = new Mesh { name = "WateringCan_Rose_Mesh" };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(a);
                float sin = Mathf.Sin(a);

                // Collar at 0
                verts.Add(new Vector3(cos * neckRadius, sin * neckRadius, 0f));
                norms.Add(new Vector3(cos * 0.8f, sin * 0.8f, -0.5f).normalized);
                uvs.Add(new Vector2((float)s / segments, 0f));

                // Outer flare rim
                verts.Add(new Vector3(cos * flareRadius, sin * flareRadius, length * 0.85f));
                norms.Add(new Vector3(cos, sin, 0.2f).normalized);
                uvs.Add(new Vector2((float)s / segments, 0.85f));

                // Perforated face center rim
                verts.Add(new Vector3(cos * flareRadius, sin * flareRadius, length));
                norms.Add(Vector3.forward);
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
            }

            int stride = 3;
            for (int s = 0; s < segments; s++)
            {
                int r0 = s * stride;
                int r1 = (s + 1) * stride;

                tris.Add(r0); tris.Add(r0 + 1); tris.Add(r1 + 1);
                tris.Add(r0); tris.Add(r1 + 1); tris.Add(r1);

                tris.Add(r0 + 1); tris.Add(r0 + 2); tris.Add(r1 + 2);
                tris.Add(r0 + 1); tris.Add(r1 + 2); tris.Add(r1 + 1);
            }

            // Perforated face cap
            int faceCenter = verts.Count;
            verts.Add(new Vector3(0f, 0f, length + 0.006f)); // Slightly convex dome face
            norms.Add(Vector3.forward);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s < segments; s++)
            {
                int i0 = s * stride + 2;
                int i1 = (s + 1) * stride + 2;
                tris.Add(faceCenter);
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

        static void Shoot(string path, Vector3 pos, Vector3 target)
        {
            int W = 1920, H = 1080;
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
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            byte[] b = tex.EncodeToPNG();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, b);
            Debug.Log($"[FixCan] Captured: {path} ({b.Length} bytes)");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
