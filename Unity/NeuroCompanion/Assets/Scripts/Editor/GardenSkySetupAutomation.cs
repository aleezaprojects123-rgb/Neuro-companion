#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

namespace NeuroCompanion.Backend.Editor
{
    public static class GardenSkySetupAutomation
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PanoramicMatPath = "Assets/Materials/Environment/Garden_Panoramic_Sky.mat";
        private const string ProceduralMatPath = "Assets/Materials/Environment/Garden_Realistic_Sky.mat";
        private const string ProfilePath = "Assets/Materials/Environment/Garden_PostProcess_Profile.asset";
        private const string MeadowMatPath = "Assets/Materials/Environment/Meadow_Ground.mat";

        [MenuItem("NeuroCompanion/Setup Realistic Garden Sky & Lighting")]
        public static void SetupRealisticSky()
        {
            Debug.Log("[GardenSky] Configuring realistic sky, sun lighting & Garden Game environment...");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. ENSURE PANORAMIC REALISTIC SKY MATERIAL
            var skyMat = AssetDatabase.LoadAssetAtPath<Material>(PanoramicMatPath);
            if (skyMat == null)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Environment/GardenSky_Realistic_Equirectangular.png");
                skyMat = new Material(Shader.Find("Skybox/Panoramic"));
                if (tex != null) skyMat.mainTexture = tex;
                skyMat.SetFloat("_Exposure", 1.15f);
                AssetDatabase.CreateAsset(skyMat, PanoramicMatPath);
            }

            // 2. CONFIGURE RENDERSETTINGS
            RenderSettings.skybox = skyMat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1.0f;

            // 3. ATMOSPHERIC HORIZON FOG
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.83f, 0.88f, 1f); // Seamless horizon blend
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 220f;

            // 4. CONFIGURE DIRECTIONAL SUN LIGHT
            var sunGo = GameObject.Find("Garden_SunLight") ?? GameObject.Find("Directional Light");
            if (sunGo == null) sunGo = new GameObject("Garden_SunLight");
            sunGo.name = "Garden_SunLight";
            var sun = sunGo.GetComponent<Light>() ?? sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.color = new Color(1.0f, 0.97f, 0.92f, 1f); // Warm natural sunlight
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            // Align sun angle to match the visual sun in the sky panorama (azimuth 115 deg, elevation 38 deg)
            sunGo.transform.rotation = Quaternion.Euler(38f, -65f, 0f);
            RenderSettings.sun = sun;

            // 5. URP GLOBAL VOLUME
            var volumeGo = GameObject.Find("Global_Volume_Garden") ?? new GameObject("Global_Volume_Garden");
            var vol = volumeGo.GetComponent<Volume>() ?? volumeGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            vol.sharedProfile = profile;

            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true; bloom.threshold.value = 1.1f;
            bloom.intensity.overrideState = true; bloom.intensity.value = 0.30f;
            bloom.scatter.overrideState = true; bloom.scatter.value = 0.70f;

            if (!profile.TryGet<Tonemapping>(out var tone)) tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true; tone.mode.value = TonemappingMode.ACES;

            if (!profile.TryGet<ColorAdjustments>(out var colorAdj)) colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.overrideState = true; colorAdj.postExposure.value = 0.12f;
            colorAdj.contrast.overrideState = true; colorAdj.contrast.value = 8f;
            colorAdj.saturation.overrideState = true; colorAdj.saturation.value = 6f;

            // 6. BUILD [GARDEN_GAME] ENVIRONMENT ROOT
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null) gardenRoot = new GameObject("[GARDEN_GAME]");

            // Meadow Ground Surface
            var ground = gardenRoot.transform.Find("Meadow_Ground");
            GameObject groundGo;
            if (ground == null)
            {
                groundGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                groundGo.name = "Meadow_Ground";
                groundGo.transform.SetParent(gardenRoot.transform, false);
                groundGo.transform.position = new Vector3(0f, -0.05f, 0f);
                groundGo.transform.localScale = new Vector3(80f, 0.1f, 80f); // Wide circular meadow
            }
            else
            {
                groundGo = ground.gameObject;
            }

            var meadowMat = AssetDatabase.LoadAssetAtPath<Material>(MeadowMatPath);
            if (meadowMat == null)
            {
                meadowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                meadowMat.color = new Color(0.38f, 0.54f, 0.32f, 1f); // Lush natural green meadow
                meadowMat.SetFloat("_Smoothness", 0.1f);
                AssetDatabase.CreateAsset(meadowMat, MeadowMatPath);
            }
            var mr = groundGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = meadowMat;

            // 7. BUILD PATIENT MODE HUD
            var uiRoot = GameObject.Find("[UI]");
            var hudGo = uiRoot != null ? uiRoot.transform.Find("Patient_HUD")?.gameObject : null;
            if (hudGo == null && uiRoot != null)
            {
                hudGo = new GameObject("Patient_HUD");
                hudGo.transform.SetParent(uiRoot.transform, false);
                var hudRect = hudGo.AddComponent<RectTransform>();
                hudRect.anchorMin = Vector2.zero;
                hudRect.anchorMax = Vector2.one;
                hudRect.offsetMin = Vector2.zero;
                hudRect.offsetMax = Vector2.zero;

                // Top Banner Pill
                var bannerGo = new GameObject("Top_Banner");
                bannerGo.transform.SetParent(hudGo.transform, false);
                var bRect = bannerGo.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.30f, 0.91f);
                bRect.anchorMax = new Vector2(0.70f, 0.97f);
                bRect.offsetMin = Vector2.zero;
                bRect.offsetMax = Vector2.zero;
                var bImg = bannerGo.AddComponent<Image>();
                bImg.color = new Color(0.98f, 0.97f, 0.95f, 0.92f); // Ivory card

                var bOutline = bannerGo.AddComponent<Outline>();
                bOutline.effectColor = new Color(0.20f, 0.38f, 0.28f, 0.3f);
                bOutline.effectDistance = new Vector2(1, -1);

                var titleGo = new GameObject("Title");
                titleGo.transform.SetParent(bannerGo.transform, false);
                var tRect = titleGo.AddComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
                tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
                var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
                titleTxt.text = "Cognitive Garden Sanctuary  •  Patient Mode";
                titleTxt.fontSize = 20;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.color = new Color(0.09f, 0.25f, 0.18f, 1f); // Dark forest green
                titleTxt.alignment = TextAlignmentOptions.Center;

                // Exit / Return Button (top right)
                var exitGo = new GameObject("Btn_ExitToLogin");
                exitGo.transform.SetParent(hudGo.transform, false);
                var exitRect = exitGo.AddComponent<RectTransform>();
                exitRect.anchorMin = new Vector2(0.86f, 0.91f);
                exitRect.anchorMax = new Vector2(0.98f, 0.97f);
                exitRect.offsetMin = Vector2.zero;
                exitRect.offsetMax = Vector2.zero;
                var exitImg = exitGo.AddComponent<Image>();
                exitImg.color = new Color(0.98f, 0.97f, 0.95f, 0.95f);
                var exitOutline = exitGo.AddComponent<Outline>();
                exitOutline.effectColor = new Color(0.80f, 0.83f, 0.80f, 1f);
                exitOutline.effectDistance = new Vector2(1, -1);
                var exitBtn = exitGo.AddComponent<Button>();

                var exitTxtGo = new GameObject("Txt");
                exitTxtGo.transform.SetParent(exitGo.transform, false);
                var etRect = exitTxtGo.AddComponent<RectTransform>();
                etRect.anchorMin = Vector2.zero; etRect.anchorMax = Vector2.one;
                etRect.offsetMin = Vector2.zero; etRect.offsetMax = Vector2.zero;
                var exitTxt = exitTxtGo.AddComponent<TextMeshProUGUI>();
                exitTxt.text = "Exit to Menu";
                exitTxt.fontSize = 16;
                exitTxt.fontStyle = FontStyles.Bold;
                exitTxt.color = new Color(0.15f, 0.35f, 0.25f, 1f);
                exitTxt.alignment = TextAlignmentOptions.Center;
            }

            if (hudGo != null) hudGo.SetActive(false); // Initially hidden on login screen

            // 8. ATTACH & CONFIGURE GARDENGAMECONTROLLER
            var gameCtrl = gardenRoot.GetComponent<GardenGameController>() ?? gardenRoot.AddComponent<GardenGameController>();
            var cardGo = GameObject.Find("Login_Card_Panel");
            var bgGo = GameObject.Find("Login_Background");
            var dashGo = GameObject.Find("Dashboard_Panel");

            Wire(gameCtrl, "_gardenGameRoot", gardenRoot);
            Wire(gameCtrl, "_gardenHudRoot", hudGo);
            Wire(gameCtrl, "_loginCardPanel", cardGo);
            Wire(gameCtrl, "_loginBackground", bgGo);
            Wire(gameCtrl, "_dashboardPanel", dashGo);
            Wire(gameCtrl, "_realisticSkyMaterial", skyMat);
            Wire(gameCtrl, "_sunLight", sun);

            if (hudGo != null)
            {
                var exitBtn = hudGo.transform.Find("Btn_ExitToLogin")?.GetComponent<Button>();
                Wire(gameCtrl, "_exitButton", exitBtn);
            }

            // Garden root initially hidden on login screen
            gardenRoot.SetActive(false);

            // 9. CONFIGURE MAIN CAMERA
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.transform.position = new Vector3(0f, 1.55f, -8f);
                cam.transform.rotation = Quaternion.Euler(-2f, 85f, 0f);

                var camData = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camData.renderPostProcessing = true;
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("[GardenSky] Setup completed successfully!");
        }

        private static void Wire(object target, string fieldName, object value)
        {
            if (target == null) return;
            var f = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
#endif
