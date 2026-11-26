using UnityEngine;
using UnityEditor;

namespace NeuroCompanion.Backend.Editor
{
    public static class WateringCanRepositioner
    {
        [MenuItem("NeuroCompanion/Reposition Watering Can")]
        public static void RepositionWateringCan()
        {
            // Activate garden root so we can find objects
            GameObject gardenRoot = GameObject.Find("[GARDEN_GAME]");
            bool wasActive = false;
            if (gardenRoot != null)
            {
                wasActive = gardenRoot.activeSelf;
                gardenRoot.SetActive(true);
            }

            // Find the watering can
            GameObject can = GameObject.Find("Watering_Can");
            if (can == null)
            {
                // Search all objects including inactive
                foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (obj.name == "Watering_Can" && obj.scene.IsValid())
                    {
                        can = obj;
                        break;
                    }
                }
            }

            // Find the station to get its transform reference
            GameObject station = null;
            foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj.name == "Gardening_Planting_Station" && obj.scene.IsValid())
                {
                    station = obj;
                    break;
                }
            }

            if (can == null)
            {
                Debug.LogError("[WateringCanRepositioner] Could not find Watering_Can!");
                return;
            }

            if (station == null)
            {
                Debug.LogError("[WateringCanRepositioner] Could not find Gardening_Planting_Station!");
                return;
            }

            // Log current state
            Debug.Log($"[WateringCanRepositioner] Found: {can.name}, parent={can.transform.parent?.name}, localPos={can.transform.localPosition}");

            // The watering can is currently on the lower shelf next to/conflicting with pots.
            // Move it to the RIGHT SIDE of the station on the GROUND, leaning against the right leg.
            // Station world position is roughly (9.33, 0, 3.34).
            // Place the can on the ground to the RIGHT of the station, slightly in front,
            // so it looks naturally set down and not crowding the shelf.

            // Reparent to station for clean local coordinates, then place on floor beside right leg
            // Station right side: local X = +0.55 (outside the shelf), Y ground-level, Z = 0
            // The station sits at ground, tabletop is ~0.75m high.
            // Can is ~0.46m tall, so bottom at Y=0 means center at Y=0.23.

            Undo.RecordObject(can.transform, "Reposition Watering Can");

            // Keep can parented to station (or its parent), but move to right of station on GROUND
            // Local pos relative to station: right side (+X), ground level, slightly forward
            can.transform.localPosition = new Vector3(0.72f, 0.0f, 0.05f);
            can.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);

            Debug.Log($"[WateringCanRepositioner] Repositioned to localPos={can.transform.localPosition}, localRot={can.transform.localEulerAngles}");

            // Take screenshots to verify
            string artifactDir = @"C:\Users\Admin\.gemini\antigravity\brain\5333acc0-237c-438b-b597-66228ebbe771";

            // Screenshot 1: Side angle showing can beside station
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.LookAt(
                    station.transform.position + new Vector3(0.5f, 0f, 0f),
                    Quaternion.Euler(20f, -100f, 0f),
                    1.8f
                );
                sceneView.Repaint();
            }

            // Save the scene
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            // Restore garden root state
            if (gardenRoot != null)
                gardenRoot.SetActive(wasActive);

            // Capture game view screenshots via utility
            EditorApplication.delayCall += () =>
            {
                // Reactivate to screenshot
                if (gardenRoot != null) gardenRoot.SetActive(true);

                // Screenshot: watering can new position - station right side view
                TakeGameViewScreenshot(artifactDir + @"\watering_can_repositioned_right_side.png",
                    station.transform.position + new Vector3(1.2f, 0.9f, -1.5f),
                    Quaternion.Euler(18f, 220f, 0f), 2.5f);

                // Screenshot: full station with can in new spot
                TakeGameViewScreenshot(artifactDir + @"\watering_can_repositioned_full_station.png",
                    station.transform.position + new Vector3(0f, 0.9f, -2.0f),
                    Quaternion.Euler(20f, 180f, 0f), 3.5f);

                if (gardenRoot != null) gardenRoot.SetActive(wasActive);

                Debug.Log("[WateringCanRepositioner] Done! Watering can moved to right side of station on ground.");
                EditorUtility.DisplayDialog("Reposition Complete",
                    "Watering can moved to right side of gardening station (on ground).\nNo longer conflicting with plant pots on shelf.", "OK");
            };
        }

        private static void TakeGameViewScreenshot(string path, Vector3 camPos, Quaternion camRot, float camSize)
        {
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView == null) return;
            sceneView.LookAt(camPos, camRot, camSize);
            sceneView.Repaint();

            // Use ScreenCapture via game view
            var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType != null)
            {
                var gameView = EditorWindow.GetWindow(gameViewType);
                if (gameView != null)
                {
                    gameView.Repaint();
                }
            }

            // Capture scene view
            int width = 1280; int height = 720;
            RenderTexture rt = new RenderTexture(width, height, 24);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            // Use scene view camera
            Camera sceneCam = sceneView.camera;
            if (sceneCam != null)
            {
                var oldRT = sceneCam.targetTexture;
                sceneCam.targetTexture = rt;
                sceneCam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                sceneCam.targetTexture = oldRT;
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[WateringCanRepositioner] Screenshot saved: {path}");
            }

            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
