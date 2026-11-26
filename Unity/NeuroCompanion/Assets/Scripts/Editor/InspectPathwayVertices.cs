using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class InspectPathwayVertices
    {
        [MenuItem("NeuroCompanion/Inspect Pathway Vertices")]
        public static void Run()
        {
            var pathGO = GameObject.Find("Garden_Pathway");
            if (pathGO == null) return;
            var mf = pathGO.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            string report = $"Pathway vertex count: {verts.Length}\n";

            // Sample vertices at different Z intervals (-20, -10, 0, 10, 20)
            float[] sampleZs = new float[] { -22f, -16f, -10f, -4f, 2f, 8f, 15f, 22f };
            foreach (float targetZ in sampleZs)
            {
                float closestDist = float.MaxValue;
                Vector3 closest = Vector3.zero;
                foreach (var v in verts)
                {
                    float d = Mathf.Abs(v.z - targetZ);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closest = v;
                    }
                }
                report += $"Near Z={targetZ:F1} -> Pathway world vertex ~ {closest.ToString("F2")}\n";
            }

            File.WriteAllText("Assets/pathway_samples.txt", report);
            Debug.Log("[InspectPathwayVertices] Done");
        }
    }
}
