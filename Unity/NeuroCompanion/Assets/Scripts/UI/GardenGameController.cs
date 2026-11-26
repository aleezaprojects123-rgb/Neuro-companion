using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// Controls the Cognitive Garden Game state and Patient Mode environment.
    /// Features a smooth cinematic flight descent from the realistic sky down to eye-level.
    /// </summary>
    public class GardenGameController : MonoBehaviour
    {
        public static GardenGameController Instance { get; private set; }

        [Header("Scene Roots")]
        [SerializeField] private GameObject _gardenGameRoot;
        [SerializeField] private GameObject _gardenHudRoot;
        [SerializeField] private GameObject _loginCardPanel;
        [SerializeField] private GameObject _loginBackground;
        [SerializeField] private GameObject _dashboardPanel;

        [Header("Sky & Environment")]
        [SerializeField] private Material _realisticSkyMaterial;
        [SerializeField] private Light _sunLight;

        [Header("HUD Elements")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Button _exitButton;

        [Header("Flight Animation Settings")]
        [SerializeField] private float _flightDuration = 5.0f; // Cinematic drone glide

        [Header("Ambient Sound")]
        [SerializeField] private AudioClip _windAudioClip;
        [SerializeField] private float _maxWindVolume = 1.0f;
        private AudioSource _windAudioSource;

        private Coroutine _flightCoroutine;
        private CanvasGroup _hudCanvasGroup;

        // Ground landing target (directly on the curved pathway entrance, eye-level)
        private static readonly Vector3 GroundPos = new Vector3(2.6f, 1.32f, -28.5f);
        private static readonly Quaternion GroundRot = Quaternion.Euler(-6.5f, 45f, 0f);

        // High soaring drone camera perspective overlooking the flowers and trees
        private static readonly Vector3 SkyStartPos = new Vector3(-6.5f, 32f, -46f);
        private static readonly Quaternion SkyStartRot = Quaternion.Euler(32f, 35f, 0f);

        // Smooth curved drone flight path control points
        private static readonly Vector3 CurveControlPoint1 = new Vector3(-2f, 22f, -40f);
        private static readonly Vector3 CurveControlPoint2 = new Vector3(1.2f, 7.5f, -34.0f);

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (_exitButton != null)
                _exitButton.onClick.AddListener(ExitGardenGame);

            if (_gardenHudRoot != null)
            {
                _hudCanvasGroup = _gardenHudRoot.GetComponent<CanvasGroup>();
                if (_hudCanvasGroup == null)
                    _hudCanvasGroup = _gardenHudRoot.AddComponent<CanvasGroup>();
            }

            // Initialize audio source for calm wind sound
            if (_windAudioSource == null)
            {
                _windAudioSource = gameObject.GetComponent<AudioSource>();
                if (_windAudioSource == null)
                    _windAudioSource = gameObject.AddComponent<AudioSource>();
                _windAudioSource.playOnAwake = false;
                _windAudioSource.loop = true;
                _windAudioSource.spatialBlend = 0f; // 2D ambient breeze
            }

#if UNITY_EDITOR
            if (_windAudioClip == null)
            {
                _windAudioClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/soft_calm_wind.wav");
            }
#endif
        }

        /// <summary>
        /// Starts the Cognitive Garden Game when Patient Mode is entered,
        /// initiating a graceful bird-like flight descent from the sky to the ground.
        /// </summary>
        public void StartGardenGame()
        {
            Debug.Log("[GardenGame] Starting Cognitive Garden Game with soaring sky descent...");

            if (_loginCardPanel != null) _loginCardPanel.SetActive(false);
            if (_loginBackground != null) _loginBackground.SetActive(false);
            if (_dashboardPanel != null) _dashboardPanel.SetActive(false);

            if (_gardenGameRoot != null) _gardenGameRoot.SetActive(true);

            // Apply realistic skybox and lighting
            if (_realisticSkyMaterial != null)
            {
                RenderSettings.skybox = _realisticSkyMaterial;
                DynamicGI.UpdateEnvironment();
            }

            RenderSettings.fog = true;

            if (_flightCoroutine != null)
                StopCoroutine(_flightCoroutine);

            _flightCoroutine = StartCoroutine(FlyDownFromSkyRoutine());
        }

        /// <summary>
        /// Smoothly glides the camera down from high in the clouds into the garden.
        /// </summary>
        private IEnumerator FlyDownFromSkyRoutine()
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            cam.clearFlags = CameraClearFlags.Skybox;

            // Start up high in the sky (drone perspective)
            cam.transform.position = SkyStartPos;
            cam.transform.rotation = SkyStartRot;

            // Start soft calm wind audio
            if (_windAudioSource != null && _windAudioClip != null)
            {
                _windAudioSource.clip = _windAudioClip;
                _windAudioSource.volume = 0f;
                _windAudioSource.Play();
            }

            // Hide HUD during the soaring flight for pure visual immersion
            if (_gardenHudRoot != null)
            {
                _gardenHudRoot.SetActive(true);
                if (_hudCanvasGroup != null) _hudCanvasGroup.alpha = 0f;
            }

            float elapsed = 0f;

            while (elapsed < _flightDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _flightDuration);

                // Smooth S-curve easing (smoothstep + gentle ease-out)
                float ease = Mathf.SmoothStep(0f, 1f, t);
                float smoothT = Mathf.Sin(ease * Mathf.PI * 0.5f); // Soft, gentle touchdown

                // Cubic Bezier curve for natural curved flight descent
                Vector3 currentPos = CalculateCubicBezier(SkyStartPos, CurveControlPoint1, CurveControlPoint2, GroundPos, smoothT);
                Quaternion currentRot = Quaternion.Slerp(SkyStartRot, GroundRot, smoothT);

                cam.transform.position = currentPos;
                cam.transform.rotation = currentRot;

                // Modulate soft calm wind: gentle fade in, steady glide, smooth fade out to ground
                if (_windAudioSource != null)
                {
                    float targetVol = _maxWindVolume;
                    if (t < 0.20f)
                        targetVol *= (t / 0.20f);
                    else if (t > 0.75f)
                        targetVol *= Mathf.Clamp01(1f - (t - 0.75f) / 0.25f);
                    _windAudioSource.volume = targetVol;
                }

                // Fade in HUD during the final 25% of descent
                if (_hudCanvasGroup != null && t >= 0.75f)
                {
                    _hudCanvasGroup.alpha = Mathf.Clamp01((t - 0.75f) / 0.25f);
                }

                yield return null;
            }

            // Ensure exact final ground landing transform
            cam.transform.position = GroundPos;
            cam.transform.rotation = GroundRot;
            if (_hudCanvasGroup != null) _hudCanvasGroup.alpha = 1f;

            // Stop wind voice completely when it reaches the ground
            if (_windAudioSource != null)
            {
                _windAudioSource.Stop();
                _windAudioSource.volume = 0f;
            }

            _flightCoroutine = null;
            Debug.Log("[GardenGame] Touchdown complete — patient standing in garden sanctuary.");
        }

        private static Vector3 CalculateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return uuu * p0 + 3f * uu * t * p1 + 3f * u * tt * p2 + ttt * p3;
        }

        /// <summary>
        /// Returns to the Login screen.
        /// </summary>
        public void ExitGardenGame()
        {
            Debug.Log("[GardenGame] Exiting to Login screen...");

            if (_flightCoroutine != null)
            {
                StopCoroutine(_flightCoroutine);
                _flightCoroutine = null;
            }

            if (_windAudioSource != null)
            {
                _windAudioSource.Stop();
                _windAudioSource.volume = 0f;
            }

            if (_gardenGameRoot != null) _gardenGameRoot.SetActive(false);
            if (_gardenHudRoot != null) _gardenHudRoot.SetActive(false);

            if (_loginBackground != null) _loginBackground.SetActive(true);
            if (_loginCardPanel != null) _loginCardPanel.SetActive(true);

            if (ModeManager.Instance != null)
                ModeManager.Instance.SwitchTo(ApplicationMode.None);
        }
    }
}
