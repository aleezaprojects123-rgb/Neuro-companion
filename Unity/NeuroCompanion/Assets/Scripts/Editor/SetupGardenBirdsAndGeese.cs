using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using NeuroCompanion.Environment.NewBirds;
using System.IO;
using System.Collections.Generic;

namespace NeuroCompanion.Backend.Editor
{
    public static class SetupGardenBirdsAndGeese
    {
        [MenuItem("NeuroCompanion/Setup Garden Birds and Geese")]
        public static void Setup()
        {
            Debug.Log("[SetupGardenBirdsAndGeese] Configuring Sparrows with Flapping Wings, 5-Min Intervals, and Sitting Geese...");

            // 1. Prepare Materials
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Materials/Environment/NewBirds"));
            
            // Sparrow Material
            string sparrowMatPath = "Assets/Materials/Environment/NewBirds/Mat_Sparrow_New.mat";
            var sparrowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Environment/NewBirds/Sparrow/bird_Material_BaseColor_0.jpeg");
            var matSparrow = AssetDatabase.LoadAssetAtPath<Material>(sparrowMatPath);
            if (matSparrow == null)
            {
                matSparrow = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(matSparrow, sparrowMatPath);
            }
            matSparrow.SetTexture("_BaseMap", sparrowTex);
            matSparrow.SetColor("_BaseColor", Color.white);
            matSparrow.SetFloat("_Smoothness", 0.05f);
            matSparrow.SetFloat("_SpecularHighlights", 0f);
            matSparrow.SetFloat("_EnvironmentReflections", 0f);
            matSparrow.SetFloat("_Cull", 0f); // Double-sided for feathered wings
            EditorUtility.SetDirty(matSparrow);

            // Goose Material
            string gooseMatPath = "Assets/Materials/Environment/NewBirds/Mat_Goose_New.mat";
            var gooseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Environment/NewBirds/Goose/T_Goose.png");
            var matGoose = AssetDatabase.LoadAssetAtPath<Material>(gooseMatPath);
            if (matGoose == null)
            {
                matGoose = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(matGoose, gooseMatPath);
            }
            matGoose.SetTexture("_BaseMap", gooseTex);
            matGoose.SetColor("_BaseColor", Color.white);
            matGoose.SetFloat("_Smoothness", 0.08f);
            matGoose.SetFloat("_SpecularHighlights", 0f);
            matGoose.SetFloat("_EnvironmentReflections", 0f);
            EditorUtility.SetDirty(matGoose);

            // 2. Prepare Goose Animator Controller
            string animCtrlPath = "Assets/Models/Environment/NewBirds/Goose/Goose_AnimatorController.controller";
            var animCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(animCtrlPath);
            if (animCtrl == null)
            {
                animCtrl = AnimatorController.CreateAnimatorControllerAtPath(animCtrlPath);
            }

            AnimationClip idleClip = null, grazeClip = null;
            var subAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Environment/NewBirds/Goose/GOOSE_DEMO.fbx");
            foreach (var a in subAssets)
            {
                if (a is AnimationClip c)
                {
                    if (c.name.Contains("Idle") && !c.name.Contains("__preview__")) idleClip = c;
                    if (c.name.Contains("WalkGrazing") && !c.name.Contains("__preview__")) grazeClip = c;
                }
            }

            if (animCtrl.layers.Length > 0 && idleClip != null)
            {
                var sm = animCtrl.layers[0].stateMachine;
                foreach (var childState in sm.states)
                    sm.RemoveState(childState.state);

                var stateIdle = sm.AddState("Goose_Idle");
                stateIdle.motion = idleClip;

                if (grazeClip != null)
                {
                    var stateGraze = sm.AddState("Goose_Graze");
                    stateGraze.motion = grazeClip;

                    var transToGraze = stateIdle.AddTransition(stateGraze);
                    transToGraze.hasExitTime = true;
                    transToGraze.exitTime = 1.6f;
                    transToGraze.duration = 1.0f;

                    var transToIdle = stateGraze.AddTransition(stateIdle);
                    transToIdle.hasExitTime = true;
                    transToIdle.exitTime = 1.0f;
                    transToIdle.duration = 1.0f;
                }

                sm.defaultState = stateIdle;
                EditorUtility.SetDirty(animCtrl);
            }

            // 3. Find [GARDEN_GAME] root
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                foreach (var r in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (r.name == "[GARDEN_GAME]" && r.scene.IsValid()) { gardenRoot = r; break; }
                }
            }

            if (gardenRoot == null)
            {
                Debug.LogError("[SetupGardenBirdsAndGeese] [GARDEN_GAME] root not found!");
                return;
            }

            // Clean previous fauna parent
            var prevFauna = gardenRoot.transform.Find("Garden_Fauna");
            if (prevFauna != null)
            {
                Undo.DestroyObjectImmediate(prevFauna.gameObject);
            }

            var faunaRoot = new GameObject("Garden_Fauna");
            faunaRoot.transform.SetParent(gardenRoot.transform);
            Undo.RegisterCreatedObjectUndo(faunaRoot, "Create Garden Fauna");

            var sparrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/NewBirds/Sparrow/sparrow.obj");
            if (sparrowPrefab == null)
            {
                Debug.LogError("[SetupGardenBirdsAndGeese] sparrow.obj not found!");
                return;
            }

            // Create Wing Meshes for Left and Right
            Mesh leftWingMesh = GenerateSparrowWingMesh(isLeft: true);
            Mesh rightWingMesh = GenerateSparrowWingMesh(isLeft: false);

            // ========================================================
            // 4 SPARROWS (WITH WING FLAPPING & ~5-MINUTE REST INTERVALS)
            // ========================================================
            // Default behavior: Sitting peacefully on garden perches for ~5 minutes.
            // When taking off, wings flap actively with authentic sparrow aerodynamics.

            // Sparrow 1: Perched on High Cherry Tree Branch
            Vector3[] skyPath1 = new Vector3[]
            {
                new Vector3(4.5f, 6.2f, -22.0f),
                new Vector3(11.0f, 7.5f, -16.0f),
                new Vector3(14.8f, 6.8f, -9.0f),
                new Vector3(8.5f, 5.8f, -11.0f),
                new Vector3(0.5f, 6.0f, -17.0f)
            };
            CreateSkySparrow(sparrowPrefab, matSparrow, leftWingMesh, rightWingMesh, faunaRoot.transform, "Sky_Sparrow_01",
                skyPath1,
                perchPos: new Vector3(13.8f, 2.5f, -8.5f), // High tree branch
                perchRot: Quaternion.Euler(0f, -80f, 0f),
                scale: 2.6f, speed: 3.4f, initialDelay: 45.0f);

            // Sparrow 2: Companion Branch Sparrow (flies alongside Sparrow 1)
            Vector3[] skyPath2 = new Vector3[]
            {
                new Vector3(6.0f, 6.8f, -23.2f),
                new Vector3(12.5f, 8.0f, -17.2f),
                new Vector3(16.2f, 7.4f, -10.2f),
                new Vector3(9.8f, 6.4f, -12.2f),
                new Vector3(1.8f, 6.6f, -18.2f)
            };
            CreateSkySparrow(sparrowPrefab, matSparrow, leftWingMesh, rightWingMesh, faunaRoot.transform, "Sky_Sparrow_02",
                skyPath2,
                perchPos: new Vector3(14.5f, 2.6f, -8.0f), // Nearby high branch
                perchRot: Quaternion.Euler(0f, -65f, 0f),
                scale: 2.5f, speed: 3.5f, initialDelay: 47.0f);

            // Sparrow 3: Garden Trellis Top Sparrow
            Vector3[] skyPath3 = new Vector3[]
            {
                new Vector3(-2.0f, 7.2f, -19.0f),
                new Vector3(4.0f, 8.2f, -13.0f),
                new Vector3(11.5f, 7.0f, -7.0f),
                new Vector3(5.0f, 6.0f, -9.0f),
                new Vector3(-1.0f, 5.8f, -15.0f)
            };
            CreateSkySparrow(sparrowPrefab, matSparrow, leftWingMesh, rightWingMesh, faunaRoot.transform, "Sky_Sparrow_03",
                skyPath3,
                perchPos: new Vector3(9.6f, 2.05f, 2.9f), // High trellis top
                perchRot: Quaternion.Euler(0f, 130f, 0f),
                scale: 2.7f, speed: 3.2f, initialDelay: 160.0f);

            // Sparrow 4: Wooden Feeder Roof Sparrow
            Vector3[] skyPath4 = new Vector3[]
            {
                new Vector3(7.5f, 5.5f, -21.0f),
                new Vector3(13.0f, 6.6f, -15.0f),
                new Vector3(9.5f, 7.2f, -10.0f),
                new Vector3(3.5f, 5.9f, -15.0f)
            };
            CreateSkySparrow(sparrowPrefab, matSparrow, leftWingMesh, rightWingMesh, faunaRoot.transform, "Sky_Sparrow_04",
                skyPath4,
                perchPos: new Vector3(16.2f, 1.95f, -14.2f), // Feeder roof top
                perchRot: Quaternion.Euler(0f, -45f, 0f),
                scale: 2.5f, speed: 3.3f, initialDelay: 240.0f);

            // ========================================================
            // PERMANENT SITTING FAUNA (Feeder Sparrow & 2 Geese on Lawn)
            // ========================================================

            // Sparrow sitting permanently on bird feeder tray platform
            CreateSittingSparrow(sparrowPrefab, matSparrow, faunaRoot.transform, "Sparrow_Sitting_Feeder",
                pos: new Vector3(16.32f, 1.15f, -14.15f),
                rot: Quaternion.Euler(0f, 320f, 0f),
                scale: 1.0f);

            // 2 Geese sitting peacefully on the lawn grass
            var goosePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Environment/NewBirds/Goose/GOOSE_DEMO.fbx");
            if (goosePrefab != null)
            {
                CreateGoose(goosePrefab, matGoose, animCtrl, faunaRoot.transform, "Goose_Sitting_01",
                    pos: new Vector3(9.20f, 0.02f, -19.20f),
                    rot: Quaternion.Euler(0f, -65f, 0f),
                    scale: 0.85f);

                CreateGoose(goosePrefab, matGoose, animCtrl, faunaRoot.transform, "Goose_Sitting_02",
                    pos: new Vector3(10.60f, 0.02f, -20.50f),
                    rot: Quaternion.Euler(0f, -40f, 0f),
                    scale: 0.80f);
            }

            // Save Scene
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("[SetupGardenBirdsAndGeese] Setup complete: 4 sky sparrows with flapping wings and 5-min rest intervals, 1 permanent feeder sparrow, and 2 sitting geese!");
        }

        static void CreateSkySparrow(GameObject prefab, Material mat, Mesh leftWingMesh, Mesh rightWingMesh,
            Transform parent, string name, Vector3[] skyWaypoints, Vector3 perchPos, Quaternion perchRot, float scale, float speed, float initialDelay)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = perchPos;
            go.transform.rotation = perchRot;

            var model = Object.Instantiate(prefab, go.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;

            var r = model.GetComponentInChildren<Renderer>();
            if (r != null) r.sharedMaterial = mat;

            // Create Left Wing attached to bird shoulder
            var wingL = new GameObject("Wing_Left");
            wingL.transform.SetParent(model.transform, false);
            wingL.transform.localPosition = new Vector3(-0.024f, 0.052f, -0.015f);
            wingL.transform.localRotation = Quaternion.identity;
            var mfL = wingL.AddComponent<MeshFilter>();
            var mrL = wingL.AddComponent<MeshRenderer>();
            mfL.sharedMesh = leftWingMesh;
            mrL.sharedMaterial = mat;
            mrL.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Create Right Wing attached to bird shoulder
            var wingR = new GameObject("Wing_Right");
            wingR.transform.SetParent(model.transform, false);
            wingR.transform.localPosition = new Vector3(0.024f, 0.052f, -0.015f);
            wingR.transform.localRotation = Quaternion.identity;
            var mfR = wingR.AddComponent<MeshFilter>();
            var mrR = wingR.AddComponent<MeshRenderer>();
            mfR.sharedMesh = rightWingMesh;
            mrR.sharedMaterial = mat;
            mrR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            // Initially folded while perched
            wingL.SetActive(false);
            wingR.SetActive(false);

            var skyAgent = go.AddComponent<SkySparrowAgent>();
            skyAgent.skyFlightWaypoints = skyWaypoints;
            skyAgent.perchPosition = perchPos;
            skyAgent.perchRotation = perchRot;
            skyAgent.restBreakDuration = 300.0f; // 5 minutes between flights
            skyAgent.initialPerchDelay = initialDelay;
            skyAgent.flySpeed = speed;
            skyAgent.modelTransform = model.transform;
            skyAgent.leftWing = wingL.transform;
            skyAgent.rightWing = wingR.transform;
            skyAgent.currentState = SkySparrowAgent.SkyState.RestingOnPerch;
        }

        static void CreateSittingSparrow(GameObject prefab, Material mat, Transform parent, string name, Vector3 pos, Quaternion rot, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.transform.rotation = rot;

            var model = Object.Instantiate(prefab, go.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;

            var r = model.GetComponentInChildren<Renderer>();
            if (r != null) r.sharedMaterial = mat;

            var sittingAgent = go.AddComponent<GardenSparrowAgent>();
            sittingAgent.visualBody = model.transform;
        }

        static void CreateGoose(GameObject prefab, Material mat, AnimatorController animCtrl, Transform parent, string name, Vector3 pos, Quaternion rot, float scale)
        {
            var go = Object.Instantiate(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = Vector3.one * scale;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
            }

            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = animCtrl;

            go.AddComponent<GardenGooseAgent>();
        }

        // ============================================================
        // PROCEDURAL FEATHERED FLIGHT WING GENERATOR
        // ============================================================
        public static Mesh GenerateSparrowWingMesh(bool isLeft)
        {
            Mesh mesh = new Mesh();
            mesh.name = isLeft ? "Sparrow_FlightWing_L" : "Sparrow_FlightWing_R";

            float s = isLeft ? -1f : 1f;

            // Wing feather anatomy vertices (root, elbow, wrist, primary tip, trailing secondaries)
            Vector3 v0 = Vector3.zero; // Shoulder attachment root
            Vector3 v1 = new Vector3(s * 0.038f, 0.006f, -0.008f);  // Elbow joint
            Vector3 v2 = new Vector3(s * 0.076f, 0.003f, -0.022f);  // Wrist joint
            Vector3 v3 = new Vector3(s * 0.122f, -0.005f, -0.052f); // Outer primary feather tip
            Vector3 v4 = new Vector3(s * 0.092f, -0.008f, -0.068f); // Outer secondary feather
            Vector3 v5 = new Vector3(s * 0.052f, -0.009f, -0.058f); // Mid secondary feather
            Vector3 v6 = new Vector3(s * 0.016f, -0.007f, -0.040f); // Inner covert feather

            // Thickness offset for aerodynamic camber
            Vector3 thick = new Vector3(0f, 0.0022f, 0f);

            List<Vector3> verts = new List<Vector3>
            {
                v0, v1, v2, v3, v4, v5, v6,
                v0 + thick, v1 + thick, v2 + thick, v3 + thick, v4 + thick, v5 + thick, v6 + thick
            };

            // UVs mapped to sparrow wing feather texture region
            Vector2 uvRoot = new Vector2(0.48f, 0.58f);
            Vector2 uvElbow = new Vector2(0.42f, 0.52f);
            Vector2 uvWrist = new Vector2(0.35f, 0.44f);
            Vector2 uvTip = new Vector2(0.22f, 0.28f);
            Vector2 uvSecOut = new Vector2(0.28f, 0.22f);
            Vector2 uvSecMid = new Vector2(0.40f, 0.24f);
            Vector2 uvSecIn = new Vector2(0.46f, 0.32f);

            List<Vector2> uvs = new List<Vector2>
            {
                uvRoot, uvElbow, uvWrist, uvTip, uvSecOut, uvSecMid, uvSecIn,
                uvRoot, uvElbow, uvWrist, uvTip, uvSecOut, uvSecMid, uvSecIn
            };

            List<int> tris = new List<int>();

            // Upper camber triangles
            AddTri(tris, isLeft, 0, 1, 6);
            AddTri(tris, isLeft, 1, 5, 6);
            AddTri(tris, isLeft, 1, 2, 5);
            AddTri(tris, isLeft, 2, 4, 5);
            AddTri(tris, isLeft, 2, 3, 4);

            // Lower camber triangles (reverse winding)
            AddTri(tris, !isLeft, 7, 8, 13);
            AddTri(tris, !isLeft, 8, 12, 13);
            AddTri(tris, !isLeft, 8, 9, 12);
            AddTri(tris, !isLeft, 9, 11, 12);
            AddTri(tris, !isLeft, 9, 10, 11);

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void AddTri(List<int> tris, bool reverse, int a, int b, int c)
        {
            if (reverse)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
        }
    }
}
