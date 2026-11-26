using UnityEngine;
using UnityEditor;
using System.IO;

namespace NeuroCompanion.Backend.Editor
{
    public static class PinpointGlowDirect
    {
        [MenuItem("NeuroCompanion/Pinpoint Station Child Direct")]
        public static void PinpointDirect()
        {
            var station = GameObject.Find("Gardening_Planting_Station");
            if (station == null)
            {
                foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    var t = r.transform.Find("Gardening_Planting_Station");
                    if (t != null) { station = t.gameObject; break; }
                }
            }
            if (station == null) return;
            station.SetActive(true);

            var cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null) return;

            // Save original active state of children
            int childCount = station.transform.childCount;
            bool[] origState = new bool[childCount];
            for (int i = 0; i < childCount; i++)
            {
                origState[i] = station.transform.GetChild(i).gameObject.activeSelf;
            }

            int W = 1280, H = 720;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var oldRT = cam.targetTexture;

            string resultLog = "=== STATION CHILDREN BRIGHT PIXEL TEST ===\n";

            // Test 1: All ON
            for (int i = 0; i < childCount; i++) station.transform.GetChild(i).gameObject.SetActive(true);
            int allCount = CountBrightPixels(cam, rt, tex, W, H);
            resultLog += $"ALL ON: {allCount} bright pixels\n";

            // Test 2: Disable each child one by one
            for (int i = 0; i < childCount; i++)
            {
                var targetChild = station.transform.GetChild(i);
                targetChild.gameObject.SetActive(false);
                int countWithout = CountBrightPixels(cam, rt, tex, W, H);
                targetChild.gameObject.SetActive(true);

                resultLog += $"Without '{targetChild.name}': {countWithout} bright pixels (diff: {allCount - countWithout})\n";
            }

            // Restore
            for (int i = 0; i < childCount; i++)
            {
                station.transform.GetChild(i).gameObject.SetActive(origState[i]);
            }

            cam.targetTexture = oldRT;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);

            File.WriteAllText(@"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771\scratch\child_test_results.txt", resultLog);
            Debug.Log("[PinpointGlowDirect] Done! Wrote child_test_results.txt");
        }

        private static int CountBrightPixels(Camera cam, RenderTexture rt, Texture2D tex, int W, int H)
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();

            var pixels = tex.GetPixels32();
            int count = 0;
            // Spot is roughly in x in [150..500], y in [200..550] (from bottom in Unity tex coordinates)
            for (int y = 200; y < 550; y++)
            {
                for (int x = 150; x < 500; x++)
                {
                    var p = pixels[y * W + x];
                    if (p.r > 240 && p.g > 240 && p.b > 240)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
