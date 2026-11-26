// DashboardController.cs  v3 — Premium Flat Healthcare UI
// NeuroCompanion Caregiver Dashboard
// Preserves ALL existing: sign-in flow, OASIS-1 prediction API, caregiver strategy,
// sign-out, chart tab switching. Improves presentation only.

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NeuroCompanion.Backend;

namespace NeuroCompanion.Backend
{
    public class DashboardController : MonoBehaviour
    {
        public static DashboardController Instance { get; private set; }

        // ── Roots ──────────────────────────────────────────────────────────────
        [Header("Roots")]
        [SerializeField] private GameObject _dashboardRoot;
        [SerializeField] private GameObject _loginCardPanel;
        [SerializeField] private GameObject _loginBackground;
        [SerializeField] private Image      _dashboardBgImage;

        // ── Header ─────────────────────────────────────────────────────────────
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _patientNameText;
        [SerializeField] private TextMeshProUGUI _patientIdText;
        [SerializeField] private TextMeshProUGUI _roleBadgeText;
        [SerializeField] private Button          _signOutButton;

        // ── OASIS Input ────────────────────────────────────────────────────────
        [Header("OASIS Assessment Input")]
        [SerializeField] private TMP_InputField _mmseInput;
        [SerializeField] private TMP_InputField _ageInput;
        [SerializeField] private TMP_Dropdown   _sexDropdown;
        [SerializeField] private TMP_InputField _educInput;
        [SerializeField] private Button         _evaluateMmseButton;
        [SerializeField] private GameObject     _evaluatingIndicator;

        // ── OASIS Output ───────────────────────────────────────────────────────
        [Header("OASIS Result Output")]
        [SerializeField] private GameObject      _resultsPanel;
        [SerializeField] private TextMeshProUGUI _predictionTitleText;
        [SerializeField] private Image           _predictionBadgeImage;
        [SerializeField] private TextMeshProUGUI _class0ProbText;
        [SerializeField] private Image           _class0ProbBar;
        [SerializeField] private TextMeshProUGUI _class1ProbText;
        [SerializeField] private Image           _class1ProbBar;
        [SerializeField] private TextMeshProUGUI _class2ProbText;
        [SerializeField] private Image           _class2ProbBar;
        [SerializeField] private TextMeshProUGUI _caregiverRecommendationText;
        [SerializeField] private TextMeshProUGUI _disclaimerText;

        // ── Chart ──────────────────────────────────────────────────────────────
        [Header("Chart")]
        [SerializeField] private Button          _weeklyTabButton;
        [SerializeField] private Button          _monthlyTabButton;
        [SerializeField] private Image           _weeklyTabBg;
        [SerializeField] private Image           _monthlyTabBg;
        [SerializeField] private GameObject      _chartBarsContainer;
        [SerializeField] private TextMeshProUGUI _chartTitleText;
        [SerializeField] private TextMeshProUGUI _chartSubtitleText;

        // ── KPI Metrics ────────────────────────────────────────────────────────
        [Header("KPI Metrics (demonstration data)")]
        [SerializeField] private TextMeshProUGUI _retentionMetricText;
        [SerializeField] private TextMeshProUGUI _hesitationMetricText;
        [SerializeField] private TextMeshProUGUI _stabilityMetricText;
        [SerializeField] private TextMeshProUGUI _sessionsMetricText;

        // ── Palette ────────────────────────────────────────────────────────────
        // Forest green #1A3D2B  Mid sage #2E5440  Warm charcoal #2C3028
        private static readonly Color Forest  = new Color(0.102f, 0.239f, 0.169f, 1f); // #1A3D2B
        private static readonly Color Sage    = new Color(0.180f, 0.330f, 0.251f, 1f); // #2E5440
        private static readonly Color Charcoal= new Color(0.172f, 0.188f, 0.157f, 1f); // #2C3028
        private static readonly Color Muted   = new Color(0.420f, 0.447f, 0.408f, 1f); // mid-gray
        private static readonly Color Cdr0Col = new Color(0.102f, 0.400f, 0.220f, 1f); // stable green
        private static readonly Color Cdr1Col = new Color(0.700f, 0.470f, 0.070f, 1f); // amber
        private static readonly Color Cdr2Col = new Color(0.650f, 0.180f, 0.140f, 1f); // terracotta

        // Tab active/inactive surface colors
        private static readonly Color TabActiveBg   = new Color(0.102f, 0.239f, 0.169f, 1f);
        private static readonly Color TabInactiveBg = new Color(0.933f, 0.925f, 0.910f, 1f);

        private bool _isWeeklyView = true;
        private readonly List<GameObject> _spawnedBars = new List<GameObject>();

        // ══════════════════════════════════════════════════════════════════════
        // Unity lifecycle
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            _evaluateMmseButton?.onClick.AddListener(OnEvaluateMmseClicked);
            _weeklyTabButton?.onClick.AddListener(() => SwitchChartView(true));
            _monthlyTabButton?.onClick.AddListener(() => SwitchChartView(false));
            _signOutButton?.onClick.AddListener(OnSignOutClicked);
        }

        private void Start()
        {
            if (_mmseInput  != null && string.IsNullOrEmpty(_mmseInput.text))  _mmseInput.text  = "24";
            if (_ageInput   != null && string.IsNullOrEmpty(_ageInput.text))   _ageInput.text   = "74";
            if (_educInput  != null && string.IsNullOrEmpty(_educInput.text))  _educInput.text  = "3";
            SwitchChartView(true);
        }

        // ══════════════════════════════════════════════════════════════════════
        // Public API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Opens the dashboard for a signed-in caregiver/patient.</summary>
        public void OpenDashboard(UserProfileDto user)
        {
            Instance = this;
            gameObject.SetActive(true);
            _dashboardRoot?.SetActive(true);

            // Hide login panels; show cream dashboard background
            HideLoginPanels();

            string name = !string.IsNullOrEmpty(user?.full_name) ? user.full_name : "Caregiver";
            string role = !string.IsNullOrEmpty(user?.role)
                ? char.ToUpper(user.role[0]) + user.role.Substring(1) + " Mode"
                : "Caregiver Mode";

            if (_patientNameText != null) _patientNameText.text = $"Welcome, <b>{name}</b>";
            if (_patientIdText   != null) _patientIdText.text   = $"Patient: <b>{user?.patient_id ?? "patient_001"}</b>  ·  OASIS-1 Connected";
            if (_roleBadgeText   != null) _roleBadgeText.text   = role;

            SwitchChartView(true);
            OnEvaluateMmseClicked();
        }

        /// <summary>Opens dashboard in Patient (read-only) mode.</summary>
        public void OpenDashboardAsPatient()
        {
            OpenDashboard(new UserProfileDto
            {
                full_name           = "Patient View",
                role                = "patient",
                patient_association = "patient_001",
                patient_id          = "patient_001"
            });
        }

        /// <summary>Signs out and returns to login screen.</summary>
        public void OnSignOutClicked()
        {
            _dashboardRoot?.SetActive(false);

            var card = _loginCardPanel ?? GameObject.Find("Login_Card_Panel");
            card?.SetActive(true);

            var bg = _loginBackground ?? GameObject.Find("Login_Background") as GameObject;
            if (_loginBackground != null) _loginBackground.SetActive(true);
            else { var b = GameObject.Find("Login_Background"); if (b != null) b.SetActive(true); }

            AuthManager.Instance?.ClearSession();
        }

        // ══════════════════════════════════════════════════════════════════════
        // OASIS-1 Evaluation  — preserved exactly
        // ══════════════════════════════════════════════════════════════════════

        public void OnEvaluateMmseClicked()
        {
            float mmse = 24f;
            if (_mmseInput != null && float.TryParse(_mmseInput.text, out float pm))
                mmse = Mathf.Clamp(pm, 0f, 30f);

            float age = 74f;
            if (_ageInput != null && float.TryParse(_ageInput.text, out float pa))
                age = Mathf.Clamp(pa, 30f, 105f);

            float educ = 3f;
            if (_educInput != null && float.TryParse(_educInput.text, out float pe))
                educ = Mathf.Clamp(pe, 1f, 5f);

            string sex = (_sexDropdown != null && _sexDropdown.value == 1) ? "F" : "M";

            SetEvaluating(true);

            var req = new PredictRequest { Age = age, MF = sex, Educ = educ, MMSE = mmse };

            if (BackendClient.Instance != null)
            {
                BackendClient.Instance.PostPredict(req, (ok, resp) =>
                {
                    SetEvaluating(false);
                    if (ok && resp != null) DisplayOasisResults(resp, mmse);
                    else                   DisplayOfflineFallback(mmse);
                });
            }
            else
            {
                SetEvaluating(false);
                DisplayOfflineFallback(mmse);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // Result display — REDESIGNED presentation, same data
        // ══════════════════════════════════════════════════════════════════════

        private void DisplayOasisResults(PredictResponse resp, float mmse)
        {
            _resultsPanel?.SetActive(true);

            string title; Color accent; string strategy;

            if (resp.predicted_class == 0)
            {
                title    = "CDR 0 — No Dementia Pattern Detected";
                accent   = Cdr0Col;
                strategy = "The assessment indicates stable baseline cognitive functioning. Continue daily interactive reminiscence sessions with Carl. Engage with gentle problem-solving activities. Monitor weekly.";
            }
            else if (resp.predicted_class == 1)
            {
                title    = "CDR 0.5 — Very Mild Memory Variation";
                accent   = Cdr1Col;
                strategy = "Subtle cognitive hesitation noted. Recommend structured daily orientation prompts, a measured Carl speech rate, gentle memory cues, and a calm predictable daily routine.";
            }
            else
            {
                title    = "CDR 1+ — Mild to Moderate Support Indicated";
                accent   = Cdr2Col;
                strategy = "Noticeable memory variance detected. Enable Step-by-Step Guidance in Carl, simplify tasks to single choices, activate visual assistance, and limit cognitive fatigue with shorter sessions.";
            }

            if (_predictionTitleText != null)
            {
                _predictionTitleText.text  = title;
                _predictionTitleText.color = accent;
            }
            if (_predictionBadgeImage != null)
                _predictionBadgeImage.color = new Color(accent.r, accent.g, accent.b, 0.12f);

            // Probabilities — real values from backend
            float p0 = resp.class_probabilities?.class_0 ?? 0f;
            float p1 = resp.class_probabilities?.class_1 ?? 0f;
            float p2 = resp.class_probabilities?.class_2 ?? 0f;

            SetProbRow(_class0ProbText, _class0ProbBar, "No dementia (CDR 0)",       p0, Cdr0Col);
            SetProbRow(_class1ProbText, _class1ProbBar, "Very mild (CDR 0.5)",       p1, Cdr1Col);
            SetProbRow(_class2ProbText, _class2ProbBar, "Mild–moderate (CDR 1+)",   p2, Cdr2Col);

            if (_caregiverRecommendationText != null)
                _caregiverRecommendationText.text = strategy;

            if (_disclaimerText != null)
                _disclaimerText.text = $"Model: {resp.model_version}  ·  MMSE {mmse:F0}/30  ·  For clinical reference only — not a medical diagnosis.";
        }

        private static void SetProbRow(TextMeshProUGUI lbl, Image bar, string category, float prob, Color col)
        {
            if (lbl != null)
            {
                lbl.text  = $"{category}   <b>{prob * 100f:F0}%</b>";
                lbl.color = Charcoal;
            }
            if (bar != null)
            {
                bar.color = col;
                var rt = bar.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(Mathf.Clamp01(prob), 1f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
            }
        }

        private void DisplayOfflineFallback(float mmse)
        {
            DisplayOasisResults(new PredictResponse
            {
                model_version     = "oasis-practical-v1 (offline)",
                predicted_class   = mmse >= 27 ? 0 : (mmse >= 21 ? 1 : 2),
                class_probabilities = new ClassProbabilities
                {
                    class_0 = mmse >= 27 ? 0.75f : 0.10f,
                    class_1 = (mmse >= 21 && mmse < 27) ? 0.65f : 0.20f,
                    class_2 = mmse < 21 ? 0.70f : 0.10f
                }
            }, mmse);
        }

        private void SetEvaluating(bool on)
        {
            _evaluatingIndicator?.SetActive(on);
            if (_evaluateMmseButton != null) _evaluateMmseButton.interactable = !on;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Chart — demonstration data, clearly labelled
        // ══════════════════════════════════════════════════════════════════════

        public void SwitchChartView(bool weekly)
        {
            _isWeeklyView = weekly;

            // Tab pill styling — forest green active, warm neutral inactive
            ApplyTabStyle(_weeklyTabBg,  _weeklyTabButton,  weekly);
            ApplyTabStyle(_monthlyTabBg, _monthlyTabButton, !weekly);

            RenderChart(weekly);
        }

        private static void ApplyTabStyle(Image bg, Button btn, bool active)
        {
            if (bg != null)
            {
                bg.color  = active ? TabActiveBg : TabInactiveBg;
                bg.sprite = null;
                bg.type   = Image.Type.Simple;
            }
            var t = btn?.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.color = active ? Color.white : Sage;
        }

        private void RenderChart(bool weekly)
        {
            // Titles
            if (_chartTitleText != null)
            {
                _chartTitleText.color = Charcoal;
                _chartTitleText.text  = weekly
                    ? "Weekly MMSE Interaction Trend"
                    : "Monthly MMSE Interaction Trend";
            }
            if (_chartSubtitleText != null)
            {
                _chartSubtitleText.color = Muted;
                _chartSubtitleText.text  = "Demonstration data — session scores not yet recorded for this patient";
            }

            // Clear previous bars
            ClearBars();
            if (_chartBarsContainer == null) return;

            // Demonstration data — clearly labelled as such above
            string[] labels = weekly
                ? new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }
                : new[] { "Wk 1", "Wk 2", "Wk 3", "Wk 4" };

            float[] scores = weekly
                ? new[] { 76f, 79f, 82f, 80f, 85f, 88f, 91f }
                : new[] { 74f, 79f, 84f, 89f };

            int   count  = labels.Length;
            var   cRect  = _chartBarsContainer.GetComponent<RectTransform>();
            float cW     = (cRect != null && cRect.rect.width  > 100f) ? cRect.rect.width  : 760f;
            float cH     = (cRect != null && cRect.rect.height > 100f) ? cRect.rect.height : 260f;

            float barW   = weekly ? Mathf.Min(52f, (cW - 60f) / count - 12f)
                                  : Mathf.Min(90f, (cW - 60f) / count - 16f);
            float gap    = (cW - count * barW) / (count + 1);

            for (int i = 0; i < count; i++)
            {
                float score   = scores[i];
                float barH    = (score / 100f) * (cH - 44f);
                float xCenter = gap + i * (barW + gap) + barW * 0.5f;

                var col = new GameObject($"Bar_{labels[i]}");
                col.transform.SetParent(_chartBarsContainer.transform, false);
                var colRT = col.AddComponent<RectTransform>();
                colRT.anchorMin        = Vector2.zero;
                colRT.anchorMax        = Vector2.zero;
                colRT.pivot            = new Vector2(0.5f, 0f);
                colRT.anchoredPosition = new Vector2(xCenter, 28f);
                colRT.sizeDelta        = new Vector2(barW, cH - 28f);

                // Bar fill — flat rect, forest-green family
                var fill = MakeChild(col, "Fill");
                var fRT  = fill.GetComponent<RectTransform>();
                fRT.anchorMin        = new Vector2(0f, 0f);
                fRT.anchorMax        = new Vector2(1f, 0f);
                fRT.pivot            = new Vector2(0.5f, 0f);
                fRT.anchoredPosition = Vector2.zero;
                fRT.sizeDelta        = new Vector2(0f, barH);
                var fImg  = fill.AddComponent<Image>();
                fImg.color = new Color(0.102f + (score - 70f) / 30f * 0.07f,
                                       0.329f + (score - 70f) / 30f * 0.09f,
                                       0.220f + (score - 70f) / 30f * 0.04f, 1f);

                // Score label above bar
                var scoreLbl = MakeChild(col, "Score");
                var sRT  = scoreLbl.GetComponent<RectTransform>();
                sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 0f);
                sRT.pivot = new Vector2(0.5f, 0f);
                sRT.anchoredPosition = new Vector2(0f, barH + 2f);
                sRT.sizeDelta        = new Vector2(0f, 22f);
                var sTxt = AddTMP(scoreLbl, $"{score:F0}%", 13f, Forest, TextAlignmentOptions.Center);
                sTxt.fontStyle = FontStyles.Bold;

                // Day label below bar
                var dayLbl = MakeChild(col, "Day");
                var dRT = dayLbl.GetComponent<RectTransform>();
                dRT.anchorMin = new Vector2(0f, 0f); dRT.anchorMax = new Vector2(1f, 0f);
                dRT.pivot = new Vector2(0.5f, 1f);
                dRT.anchoredPosition = new Vector2(0f, -2f);
                dRT.sizeDelta        = new Vector2(0f, 20f);
                AddTMP(dayLbl, labels[i], 12f, Muted, TextAlignmentOptions.Center);

                _spawnedBars.Add(col);
            }

            // KPI tiles — honest empty-state labels
            SetKpiEmpty(_retentionMetricText, "Memory Retention", "No data yet");
            SetKpiEmpty(_hesitationMetricText, "Response Speed", "No data yet");
            SetKpiEmpty(_stabilityMetricText, "Stability", "No data yet");
            SetKpiEmpty(_sessionsMetricText, "Sessions", "0 recorded");
        }

        private static void SetKpiEmpty(TextMeshProUGUI t, string label, string val)
        {
            if (t == null) return;
            t.text  = val;
            t.color = Muted;
        }

        // ══════════════════════════════════════════════════════════════════════
        // Helpers
        // ══════════════════════════════════════════════════════════════════════

        private void HideLoginPanels()
        {
            if (_loginBackground != null) _loginBackground.SetActive(false);
            else { var b = GameObject.Find("Login_Background"); if (b != null) b.SetActive(false); }

            if (_loginCardPanel != null) _loginCardPanel.SetActive(false);
            else { var c = GameObject.Find("Login_Card_Panel"); if (c != null) c.SetActive(false); }
        }

        private void ClearBars()
        {
            if (_chartBarsContainer == null) return;
            for (int i = _chartBarsContainer.transform.childCount - 1; i >= 0; i--)
            {
                var c = _chartBarsContainer.transform.GetChild(i).gameObject;
                c.transform.SetParent(null);
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            _spawnedBars.Clear();
        }

        private static GameObject MakeChild(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static TextMeshProUGUI AddTMP(GameObject go, string text, float size,
            Color col, TextAlignmentOptions align)
        {
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text      = text;
            t.fontSize  = size;
            t.color     = col;
            t.alignment = align;
            return t;
        }
    }
}
