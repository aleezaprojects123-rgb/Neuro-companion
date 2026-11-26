// ============================================================
// CarlJawBoneCreator.cs  — Unity 6.3 LTS Editor Tool
// NeuroCompanion — Carl Fredricksen Jaw Bone Feasibility Prototype
//
// PURPOSE
//   Editor-only tool that:
//   1. Duplicates the imported Carl mesh (never touches the original).
//   2. Analyses vertex positions to identify the lower-jaw region
//      using a configurable Y-threshold (adjustable in the Inspector).
//   3. Creates a minimal 2-bone skeleton: Root bone + JawBone.
//   4. Builds a SkinnedMeshRenderer with per-vertex bone weights:
//      - Vertices BELOW the jaw threshold → weighted 100% to JawBone.
//      - Vertices ABOVE the jaw threshold → weighted 100% to Root bone.
//   5. Places the jaw pivot at the centroid of the jaw-zone vertices.
//
// LIMITATIONS (see feasibility report)
//   - Threshold-based separation is a GEOMETRIC ESTIMATE.
//     The boundary between jaw and upper face is NOT clearly defined in
//     the mesh — it is a continuous merged surface. The threshold can be
//     tuned but cannot guarantee clean separation without topology editing.
//   - Triangulated mesh (all tris) will deform with crease artefacts
//     at the jaw boundary unless the threshold is placed conservatively.
//   - This prototype does NOT create blend shapes or visemes.
//   - This is INSPECTION/PROTOTYPE ONLY. Do not use in production scenes.
//
// USAGE
//   1. Import Carl Fredricksen.obj into Unity (Assets/Characters/CarlFredricksen/).
//   2. Drag the imported mesh prefab into the scene.
//   3. Select it.
//   4. Open: Tools → NeuroCompanion → Carl Jaw Bone Creator
//   5. Adjust thresholds and click "Create Jaw Bone (Duplicate)".
// ============================================================

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CarlJawBoneCreator : EditorWindow
{
    // --- Configuration ---
    // These Y values are in WORLD SPACE of the imported OBJ.
    // Carl's model: total height ~9.78 units, head starts ~Y 7.0
    // Lower jaw candidates: Y 7.0 – 8.1 AND Z > 0.5 (forward-facing)
    
    [Header("Jaw Detection Thresholds (World Space Y)")]
    private float jawYUpperLimit    = 8.1f;    // Vertices below this → jaw
    private float jawYLowerLimit    = 7.0f;    // Vertices above this (and below upper) → jaw
    private float jawZMinForward    = 0.5f;    // Vertices with Z > this are considered forward-facing jaw/teeth
    private bool  useZFilter        = true;    // Apply Z forward filter to jaw selection
    private float jawPivotYOffset   = 0.0f;   // Fine-tune the pivot position relative to centroid
    
    private GameObject selectedObject;
    private bool showDebug = true;

    [MenuItem("Tools/NeuroCompanion/Carl Jaw Bone Creator")]
    public static void ShowWindow()
    {
        GetWindow<CarlJawBoneCreator>("Carl Jaw Creator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Carl Fredricksen — Jaw Bone Creator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "PROTOTYPE ONLY. Works on a DUPLICATE of the selected mesh. " +
            "Original is never modified. Adjust thresholds to tune jaw vertex selection.",
            MessageType.Warning);

        EditorGUILayout.Space(8);
        GUILayout.Label("Configuration", EditorStyles.boldLabel);
        jawYUpperLimit = EditorGUILayout.FloatField("Jaw Y Upper Limit (world)", jawYUpperLimit);
        jawYLowerLimit = EditorGUILayout.FloatField("Jaw Y Lower Limit (world)", jawYLowerLimit);
        useZFilter     = EditorGUILayout.Toggle("Apply Z Forward Filter", useZFilter);
        if (useZFilter)
            jawZMinForward = EditorGUILayout.FloatField("  Jaw Z Min (forward)", jawZMinForward);
        jawPivotYOffset = EditorGUILayout.FloatField("Jaw Pivot Y Offset", jawPivotYOffset);
        showDebug       = EditorGUILayout.Toggle("Show Debug Info", showDebug);

        EditorGUILayout.Space(8);
        selectedObject = (GameObject)EditorGUILayout.ObjectField(
            "Carl Mesh (Scene Object)", selectedObject, typeof(GameObject), true);

        if (selectedObject == null && Selection.activeGameObject != null)
            selectedObject = Selection.activeGameObject;

        EditorGUILayout.Space(8);

        if (GUILayout.Button("Analyse Vertices (No Changes)", GUILayout.Height(28)))
            AnalyseVertices();

        EditorGUILayout.Space(4);
        EditorGUI.BeginDisabledGroup(selectedObject == null);
        if (GUILayout.Button("Create Jaw Bone (Duplicate Only)", GUILayout.Height(36)))
            CreateJawBone();
        EditorGUI.EndDisabledGroup();
    }

    // ----------------------------------------------------------------
    // ANALYSE — count jaw candidates without creating anything
    // ----------------------------------------------------------------
    private void AnalyseVertices()
    {
        if (selectedObject == null)
        {
            Debug.LogWarning("[CarlJaw] No object selected.");
            return;
        }

        MeshFilter mf = selectedObject.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = selectedObject.GetComponent<SkinnedMeshRenderer>();
        Mesh mesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);

        if (mesh == null)
        {
            Debug.LogError("[CarlJaw] No Mesh found on selected object.");
            return;
        }

        Vector3[] verts = mesh.vertices;
        Transform t = selectedObject.transform;
        int jawCount = 0, rootCount = 0;

        foreach (var v in verts)
        {
            Vector3 world = t.TransformPoint(v);
            if (IsJawVertex(world))
                jawCount++;
            else
                rootCount++;
        }

        Debug.Log($"[CarlJaw] ANALYSIS — Total verts: {verts.Length} | " +
                  $"Jaw candidates: {jawCount} ({100f*jawCount/verts.Length:F1}%) | " +
                  $"Root (upper body): {rootCount} ({100f*rootCount/verts.Length:F1}%)");

        EditorUtility.DisplayDialog("Carl Jaw Analysis",
            $"Total vertices: {verts.Length}\n" +
            $"Jaw candidates (Y={jawYLowerLimit}–{jawYUpperLimit}" + (useZFilter ? $", Z>{jawZMinForward}" : "") + $"): {jawCount} ({100f*jawCount/verts.Length:F1}%)\n" +
            $"Upper-body / Root bone: {rootCount} ({100f*rootCount/verts.Length:F1}%)\n\n" +
            "Adjust thresholds and re-analyse before creating bones.", "OK");
    }

    // ----------------------------------------------------------------
    // CREATE — duplicate + build SkinnedMeshRenderer with jaw bone
    // ----------------------------------------------------------------
    private void CreateJawBone()
    {
        if (selectedObject == null)
        {
            EditorUtility.DisplayDialog("Error", "Select a Carl mesh object in the scene first.", "OK");
            return;
        }

        // --- 1. Get the source mesh ---
        MeshFilter mf = selectedObject.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = selectedObject.GetComponent<SkinnedMeshRenderer>();
        Mesh srcMesh = mf != null ? mf.sharedMesh : (smr != null ? smr.sharedMesh : null);

        if (srcMesh == null)
        {
            Debug.LogError("[CarlJaw] No Mesh on selected object.");
            return;
        }

        // --- 2. Duplicate the mesh (never touch the original) ---
        Mesh workMesh = Object.Instantiate(srcMesh);
        workMesh.name = srcMesh.name + "_JawRig";

        // --- 3. Create the duplicate GameObject ---
        GameObject duplicate = new GameObject("Carl_JawRig_PROTOTYPE");
        duplicate.transform.SetPositionAndRotation(
            selectedObject.transform.position,
            selectedObject.transform.rotation);
        duplicate.transform.localScale = selectedObject.transform.localScale;
        Undo.RegisterCreatedObjectUndo(duplicate, "Create Carl Jaw Rig");

        // --- 4. Build bone hierarchy ---
        //   RootBone (at character base)
        //   └── JawBone (at jaw pivot)
        Transform rootBone = new GameObject("Root_Bone").transform;
        rootBone.SetParent(duplicate.transform, false);
        rootBone.localPosition = Vector3.zero;

        // Compute jaw pivot centroid from jaw-zone vertices
        Vector3 jawCentroid = ComputeJawCentroid(workMesh, selectedObject.transform);
        // Convert to local space of duplicate
        Vector3 jawLocalPivot = duplicate.transform.InverseTransformPoint(jawCentroid);
        jawLocalPivot.y += jawPivotYOffset;

        Transform jawBone = new GameObject("Jaw_Bone").transform;
        jawBone.SetParent(rootBone, false);
        jawBone.localPosition = jawLocalPivot;

        // --- 5. Set bind poses ---
        Matrix4x4[] bindPoses = new Matrix4x4[2];
        bindPoses[0] = rootBone.worldToLocalMatrix * duplicate.transform.localToWorldMatrix;
        bindPoses[1] = jawBone.worldToLocalMatrix  * duplicate.transform.localToWorldMatrix;
        workMesh.bindposes = bindPoses;

        // --- 6. Compute per-vertex bone weights ---
        Vector3[] verts = workMesh.vertices;
        BoneWeight[] boneWeights = new BoneWeight[verts.Length];
        int jawCount = 0;

        for (int i = 0; i < verts.Length; i++)
        {
            // Convert local mesh vertex to world space
            Vector3 world = selectedObject.transform.TransformPoint(verts[i]);
            bool isJaw = IsJawVertex(world);

            boneWeights[i] = new BoneWeight
            {
                boneIndex0 = isJaw ? 1 : 0,   // 1 = JawBone, 0 = RootBone
                weight0    = 1.0f,
                boneIndex1 = 0,
                weight1    = 0.0f,
                boneIndex2 = 0,
                weight2    = 0.0f,
                boneIndex3 = 0,
                weight3    = 0.0f
            };
            if (isJaw) jawCount++;
        }

        workMesh.boneWeights = boneWeights;

        // --- 7. Add SkinnedMeshRenderer ---
        SkinnedMeshRenderer rigSMR = duplicate.AddComponent<SkinnedMeshRenderer>();
        rigSMR.sharedMesh      = workMesh;
        rigSMR.bones           = new Transform[] { rootBone, jawBone };
        rigSMR.rootBone        = rootBone;

        // Copy materials from original
        if (mf != null)
        {
            MeshRenderer origMR = selectedObject.GetComponent<MeshRenderer>();
            if (origMR != null) rigSMR.sharedMaterials = origMR.sharedMaterials;
        }
        else if (smr != null)
        {
            rigSMR.sharedMaterials = smr.sharedMaterials;
        }

        // --- 8. Save the mesh as an asset ---
        string meshPath = $"Assets/Characters/CarlFredricksen/{workMesh.name}.asset";
        AssetDatabase.CreateAsset(workMesh, meshPath);
        AssetDatabase.SaveAssets();

        // --- 9. Add the jaw test controller ---
        duplicate.AddComponent<CarlJawTestController>();

        // Select the new object
        Selection.activeGameObject = duplicate;

        if (showDebug)
        {
            Debug.Log($"[CarlJaw] Created Carl_JawRig_PROTOTYPE\n" +
                      $"  Total verts: {verts.Length}\n" +
                      $"  Jaw bone verts: {jawCount} ({100f*jawCount/verts.Length:F1}%)\n" +
                      $"  Root bone verts: {verts.Length - jawCount}\n" +
                      $"  Jaw pivot (local): {jawLocalPivot}\n" +
                      $"  Mesh saved: {meshPath}");
        }

        EditorUtility.DisplayDialog("Done",
            $"Created Carl_JawRig_PROTOTYPE\n\n" +
            $"Jaw bone vertices: {jawCount} of {verts.Length}\n" +
            $"Jaw pivot: Y={jawLocalPivot.y:F3}, Z={jawLocalPivot.z:F3}\n\n" +
            "The CarlJawTestController component will animate the jaw in Play Mode.\n\n" +
            "IMPORTANT: Inspect the jaw boundary carefully. If you see deformation\n" +
            "artefacts at the jaw line, adjust the Y threshold and re-run.",
            "OK");
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private bool IsJawVertex(Vector3 worldPos)
    {
        bool inYRange = worldPos.y >= jawYLowerLimit && worldPos.y < jawYUpperLimit;
        bool inZRange = !useZFilter || worldPos.z > jawZMinForward;
        return inYRange && inZRange;
    }

    private Vector3 ComputeJawCentroid(Mesh mesh, Transform originalTransform)
    {
        Vector3[] verts = mesh.vertices;
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (var v in verts)
        {
            Vector3 world = originalTransform.TransformPoint(v);
            if (IsJawVertex(world))
            {
                sum += world;
                count++;
            }
        }
        return count > 0 ? sum / count : originalTransform.position;
    }
}
