// BackendClient.cs
// NeuroCompanion — Phase 3, Step 4: Backend Integration Layer
//
// Singleton MonoBehaviour that owns all HTTP communication with the FastAPI backend.
// API base URL: http://127.0.0.1:8000/api/v1/  (API Contract v1, P2A-30)
//
// Design rules:
//  - All network operations use UnityWebRequest inside coroutines (non-blocking).
//  - Callers receive results via callback Actions; they never block waiting.
//  - JSON serialisation uses UnityEngine.JsonUtility for simple types and
//    manual string building only where JsonUtility cannot handle the schema
//    (e.g. the "M/F" field alias, nullable arrays, top-level arrays).
//  - Sensitive data is never logged in full; only status codes and event counts.
//  - BackendClient does NOT make any AI or tier decisions.
//  - Singleton pattern: attach to Backend_Client_Holder; survives scene loads
//    but does not duplicate if already present.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// Connection status reported by BackendClient.
    /// </summary>
    public enum BackendConnectionStatus
    {
        Unknown,
        Connected,
        Unreachable,
        Error
    }

    /// <summary>
    /// Singleton MonoBehaviour. Attach to [MANAGERS]/Backend_Client_Holder.
    /// All public methods are fire-and-callback — they never block the main thread.
    /// </summary>
    public class BackendClient : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------

        public static BackendClient Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Configuration (editable in the Inspector)
        // -------------------------------------------------------------------------

        [Header("Backend Connection")]
        [Tooltip("Base URL for the FastAPI backend API v1.")]
        [SerializeField] private string _baseUrl = "http://127.0.0.1:8000/api/v1";

        [Tooltip("Request timeout in seconds.")]
        [SerializeField] private float _timeoutSeconds = 10f;

        // -------------------------------------------------------------------------
        // Connection status (observable by other systems)
        // -------------------------------------------------------------------------

        public BackendConnectionStatus ConnectionStatus { get; private set; } = BackendConnectionStatus.Unknown;

        /// <summary>Raised whenever the connection status changes.</summary>
        public event Action<BackendConnectionStatus> OnConnectionStatusChanged;

        private void SetStatus(BackendConnectionStatus status)
        {
            if (ConnectionStatus == status) return;
            ConnectionStatus = status;
            OnConnectionStatusChanged?.Invoke(status);
            Debug.Log($"[BackendClient] Connection status → {status}");
        }

        // -------------------------------------------------------------------------
        // §1  Health check — GET /api/v1/health
        // -------------------------------------------------------------------------

        /// <summary>
        /// Checks whether the FastAPI backend is running and the ML pipeline is loaded.
        /// Does not block. Callback receives (success, response).
        /// </summary>
        public void GetHealth(Action<bool, HealthResponse> callback)
        {
            StartCoroutine(GetRequest<HealthResponse>("health", callback));
        }

        // -------------------------------------------------------------------------
        // §2  Phase 1 CDR Prediction — POST /api/v1/predict
        // -------------------------------------------------------------------------

        /// <summary>
        /// Submits demographic and MMSE data for experimental CDR classification.
        /// IMPORTANT: The result must never be automatically mapped to a support tier.
        /// Callback receives (success, response).
        /// </summary>
        public void PostPredict(PredictRequest request, Action<bool, PredictResponse> callback)
        {
            // The backend field name "M/F" is not a valid JSON key for JsonUtility.
            // Build JSON manually to match the exact schema.
            string json = $"{{\"Age\":{request.Age},\"M/F\":\"{EscapeJson(request.MF)}\",\"Educ\":{request.Educ},\"MMSE\":{request.MMSE}}}";
            StartCoroutine(PostRequest<PredictResponse>("predict", json, callback));
        }

        // -------------------------------------------------------------------------
        // §3  Patient Onboarding — POST /api/v1/profiles/onboard
        // -------------------------------------------------------------------------

        /// <summary>
        /// Registers a new patient. MMSE is never required.
        /// CONTRACT GUARANTEE: active_support_tier is always null in the response.
        /// Callback receives (success, response).
        /// </summary>
        public void PostOnboard(OnboardRequest request, Action<bool, ProfileResponse> callback)
        {
            string json = BuildOnboardJson(request);
            StartCoroutine(PostRequest<ProfileResponse>("profiles/onboard", json, callback));
        }

        // -------------------------------------------------------------------------
        // §4  Support Profile — POST and GET /api/v1/profiles/support
        // -------------------------------------------------------------------------

        /// <summary>
        /// Creates or updates a support profile. active_support_tier is set by caregiver only.
        /// Callback receives (success, response).
        /// </summary>
        public void PostSupportProfile(SupportProfileRequest request, Action<bool, ProfileResponse> callback)
        {
            string json = JsonUtility.ToJson(request);
            StartCoroutine(PostRequest<ProfileResponse>("profiles/support", json, callback));
        }

        /// <summary>
        /// Retrieves the currently active support profile for a patient.
        /// Callback receives (success, response).
        /// </summary>
        public void GetSupportProfile(string patientId, Action<bool, ProfileResponse> callback)
        {
            StartCoroutine(GetRequest<ProfileResponse>($"profiles/support/{Uri.EscapeDataString(patientId)}", callback));
        }

        // -------------------------------------------------------------------------
        // §5  Telemetry — POST /api/v1/telemetry/events
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sends a batch of interaction events to the backend.
        /// Preserves detailed HTTP status code, raw response body, and Retry-After header.
        /// Idempotent — duplicate event_id values are safely skipped by the backend.
        /// </summary>
        public void PostTelemetryEvents(List<InteractionEvent> events, Action<BackendResult<TelemetryResponse>> callback)
        {
            if (events == null || events.Count == 0)
            {
                Debug.LogWarning("[BackendClient] PostTelemetryEvents called with empty list — skipped.");
                var emptyResult = new BackendResult<TelemetryResponse>
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ErrorMessage = "Empty event list."
                };
                callback?.Invoke(emptyResult);
                return;
            }

            string json = BuildEventArrayJson(events);
            StartCoroutine(PostRequestDetailed<TelemetryResponse>("telemetry/events", json, callback));
        }

        /// <summary>
        /// Convenience overload matching legacy (ok, response) signature.
        /// </summary>
        public void PostTelemetryEvents(List<InteractionEvent> events, Action<bool, TelemetryResponse> callback)
        {
            PostTelemetryEvents(events, result => callback?.Invoke(result.IsSuccess, result.Data));
        }

        // -------------------------------------------------------------------------
        // §6  Interaction Profile — GET /api/v1/profiles/interaction/{patient_id}
        // -------------------------------------------------------------------------

        /// <summary>
        /// Retrieves the aggregated longitudinal interaction summary for a patient.
        /// No cognitive inference is made — these are raw behavioral counts.
        /// Callback receives (success, response).
        /// </summary>
        public void GetInteractionProfile(string patientId, Action<bool, InteractionProfileResponse> callback)
        {
            StartCoroutine(GetRequest<InteractionProfileResponse>(
                $"profiles/interaction/{Uri.EscapeDataString(patientId)}", callback));
        }

        // -------------------------------------------------------------------------
        // §7  Recommendations — GET /api/v1/profiles/recommendations/{patient_id}
        // -------------------------------------------------------------------------

        /// <summary>
        /// Retrieves baseline support tier adaptation recommendations for caregiver review.
        /// count == 0 is a normal response (not an error).
        /// All recommendations carry is_temporary_baseline == true — they are NOT ML predictions.
        /// Callback receives (success, response).
        /// </summary>
        public void GetRecommendations(string patientId, Action<bool, RecommendationsResponse> callback, int limit = 50)
        {
            StartCoroutine(GetRequest<RecommendationsResponse>(
                $"profiles/recommendations/{Uri.EscapeDataString(patientId)}?limit={limit}", callback));
        }

        // -------------------------------------------------------------------------
        // §8  Immediate Carl Assistance — POST /api/v1/carl/assistance
        // -------------------------------------------------------------------------

        /// <summary>
        /// Requests an immediate in-session assistance action from Carl.
        /// This is a request-response action, not a persistent recommendation.
        /// Distinct from longitudinal tier recommendations (§7).
        /// Callback receives (success, response).
        /// </summary>
        public void PostCarlAssistance(CarlAssistanceRequest request, Action<bool, CarlAssistanceResponse> callback)
        {
            string json = BuildCarlAssistanceJson(request);
            StartCoroutine(PostRequest<CarlAssistanceResponse>("carl/assistance", json, callback));
        }

        // -------------------------------------------------------------------------
        // §9  Caregiver Override — POST /api/v1/profiles/override
        // -------------------------------------------------------------------------

        /// <summary>
        /// Explicit caregiver action to set or lock the support tier.
        /// Caregiver decisions always supersede automated recommendations.
        /// Callback receives (success, response).
        /// </summary>
        public void PostCaregiverOverride(CaregiverOverrideRequest request, Action<bool, CaregiverOverrideResponse> callback)
        {
            string json = JsonUtility.ToJson(request);
            StartCoroutine(PostRequest<CaregiverOverrideResponse>("profiles/override", json, callback));
        }

        // -------------------------------------------------------------------------
        // §10 Phase 4.8 / Phase 5 Assistance Evaluation — POST /api/v1/assistance/evaluate
        // -------------------------------------------------------------------------

        /// <summary>
        /// Submits real-time patient interaction telemetry to the Phase 4 AI assistance engine.
        /// Returns structured policy decision and abstract CarlAction instructions.
        /// Preserves HTTP status codes, raw JSON response, and non-blocking coroutine flow.
        /// </summary>
        public void PostEvaluateAssistance(AssistanceEvaluateRequest request, Action<BackendResult<AssistanceEvaluateResponse>> callback)
        {
            if (request == null)
            {
                Debug.LogWarning("[BackendClient] PostEvaluateAssistance called with null request — skipped.");
                var emptyResult = new BackendResult<AssistanceEvaluateResponse>
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ErrorMessage = "Null request object."
                };
                callback?.Invoke(emptyResult);
                return;
            }

            string json = BuildAssistanceEvaluateJson(request);
            StartCoroutine(PostRequestDetailed<AssistanceEvaluateResponse>("assistance/evaluate", json, callback));
        }

        /// <summary>
        /// Convenience overload matching (ok, response) signature.
        /// </summary>
        public void PostEvaluateAssistance(AssistanceEvaluateRequest request, Action<bool, AssistanceEvaluateResponse> callback)
        {
            PostEvaluateAssistance(request, result => callback?.Invoke(result.IsSuccess, result.Data));
        }

        // -------------------------------------------------------------------------
        // Authentication & User Profiles
        // -------------------------------------------------------------------------

        public void PostLogin(UserLoginRequest request, Action<BackendResult<AuthTokenResponse>> callback)
        {
            if (request == null)
            {
                callback?.Invoke(new BackendResult<AuthTokenResponse> { IsSuccess = false, ErrorMessage = "Request is null." });
                return;
            }
            string json = JsonUtility.ToJson(request);
            StartCoroutine(PostRequestDetailed<AuthTokenResponse>("auth/login", json, callback));
        }

        public void PostRegister(UserRegisterRequest request, Action<BackendResult<AuthTokenResponse>> callback)
        {
            if (request == null)
            {
                callback?.Invoke(new BackendResult<AuthTokenResponse> { IsSuccess = false, ErrorMessage = "Request is null." });
                return;
            }
            string json = JsonUtility.ToJson(request);
            StartCoroutine(PostRequestDetailed<AuthTokenResponse>("auth/register", json, callback));
        }

        public void PostForgotPassword(ForgotPasswordRequest request, Action<BackendResult<ForgotPasswordResponse>> callback)
        {
            if (request == null)
            {
                callback?.Invoke(new BackendResult<ForgotPasswordResponse> { IsSuccess = false, ErrorMessage = "Request is null." });
                return;
            }
            string json = JsonUtility.ToJson(request);
            StartCoroutine(PostRequestDetailed<ForgotPasswordResponse>("auth/forgot-password", json, callback));
        }

        public void GetMyProfile(string token, Action<BackendResult<UserProfileDto>> callback)
        {
            StartCoroutine(GetRequestWithAuth<UserProfileDto>("auth/me", token, callback));
        }

        public void PostPatientMode(PatientModeEntryRequest request, Action<BackendResult<PatientModeEntryResponse>> callback)
        {
            string json = request != null ? JsonUtility.ToJson(request) : "{\"patient_id\":\"patient_001\"}";
            StartCoroutine(PostRequestDetailed<PatientModeEntryResponse>("auth/patient-mode", json, callback));
        }

        private IEnumerator GetRequestWithAuth<TResponse>(string relativeUrl, string authToken, Action<BackendResult<TResponse>> callback)
            where TResponse : class
        {
            string url = BuildUrl(relativeUrl);
            using var request = UnityWebRequest.Get(url);
            request.timeout = Mathf.RoundToInt(_timeoutSeconds);
            request.SetRequestHeader("Accept", "application/json");
            if (!string.IsNullOrEmpty(authToken))
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");

            yield return request.SendWebRequest();

            HandleDetailedResponse(request, url, callback);
        }

        // -------------------------------------------------------------------------
        // Core HTTP helpers
        // -------------------------------------------------------------------------

        private IEnumerator GetRequest<TResponse>(string relativeUrl, Action<bool, TResponse> callback)
            where TResponse : class
        {
            string url = BuildUrl(relativeUrl);
            Debug.Log($"[BackendClient] GET {url}");

            using var request = UnityWebRequest.Get(url);
            request.timeout = Mathf.RoundToInt(_timeoutSeconds);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            HandleResponse(request, url, callback);
        }

        private IEnumerator PostRequest<TResponse>(string relativeUrl, string jsonBody,
            Action<bool, TResponse> callback)
            where TResponse : class
        {
            string url = BuildUrl(relativeUrl);
            Debug.Log($"[BackendClient] POST {url} ({jsonBody?.Length ?? 0} chars)");

            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.RoundToInt(_timeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            HandleResponse(request, url, callback);
        }

        private void HandleResponse<TResponse>(UnityWebRequest request, string url,
            Action<bool, TResponse> callback)
            where TResponse : class
        {
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogWarning($"[BackendClient] Network error for {url}: {request.error}");
                SetStatus(BackendConnectionStatus.Unreachable);
                callback?.Invoke(false, null);
                return;
            }

            long statusCode = request.responseCode;
            string body = request.downloadHandler?.text ?? "";

            if (statusCode >= 200 && statusCode < 300)
            {
                SetStatus(BackendConnectionStatus.Connected);

                TResponse parsed = null;
                try
                {
                    parsed = JsonUtility.FromJson<TResponse>(body);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BackendClient] JSON parse error for {url}: {ex.Message}\nBody: {body}");
                    callback?.Invoke(false, null);
                    return;
                }

                callback?.Invoke(true, parsed);
            }
            else
            {
                Debug.LogWarning($"[BackendClient] HTTP {statusCode} from {url}: {body}");
                SetStatus(BackendConnectionStatus.Error);
                callback?.Invoke(false, null);
            }
        }

        private IEnumerator PostRequestDetailed<TResponse>(string relativeUrl, string jsonBody,
            Action<BackendResult<TResponse>> callback)
            where TResponse : class
        {
            string url = BuildUrl(relativeUrl);
            Debug.Log($"[BackendClient] POST {url} ({jsonBody?.Length ?? 0} chars)");

            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.RoundToInt(_timeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            HandleDetailedResponse(request, url, callback);
        }

        private void HandleDetailedResponse<TResponse>(UnityWebRequest request, string url,
            Action<BackendResult<TResponse>> callback)
            where TResponse : class
        {
            var result = new BackendResult<TResponse>();

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogWarning($"[BackendClient] Network error for {url}: {request.error}");
                SetStatus(BackendConnectionStatus.Unreachable);
                result.IsSuccess = false;
                result.StatusCode = request.responseCode;
                result.ErrorMessage = request.error;
                callback?.Invoke(result);
                return;
            }

            long statusCode = request.responseCode;
            string body = request.downloadHandler?.text ?? "";
            result.StatusCode = statusCode;
            result.RawBody = body;

            string retryAfterHeader = request.GetResponseHeader("Retry-After");
            if (!string.IsNullOrEmpty(retryAfterHeader) && int.TryParse(retryAfterHeader, out int retrySec))
            {
                result.RetryAfterSeconds = retrySec;
            }

            if (statusCode >= 200 && statusCode < 300)
            {
                SetStatus(BackendConnectionStatus.Connected);
                try
                {
                    result.Data = JsonUtility.FromJson<TResponse>(body);
                    result.IsSuccess = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[BackendClient] JSON parse error for {url}: {ex.Message}\nBody: {body}");
                    result.IsSuccess = false;
                    result.ErrorMessage = ex.Message;
                }
            }
            else
            {
                Debug.LogWarning($"[BackendClient] HTTP {statusCode} from {url}: {body}");
                SetStatus(BackendConnectionStatus.Error);
                result.IsSuccess = false;
                result.ErrorMessage = $"HTTP {statusCode}: {body}";
            }

            callback?.Invoke(result);
        }

        // -------------------------------------------------------------------------
        // JSON builders for schemas that JsonUtility cannot handle directly
        // -------------------------------------------------------------------------

        private string BuildUrl(string relativeUrl)
        {
            return $"{_baseUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
        }

        /// <summary>
        /// Builds the onboard request JSON, omitting optional fields when null/empty.
        /// </summary>
        private string BuildOnboardJson(OnboardRequest request)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"patient_id\":\"{EscapeJson(request.patient_id)}\"");

            if (request.caregiver_preferences != null)
            {
                sb.Append(",\"caregiver_preferences\":");
                sb.Append(BuildCaregiverPrefsJson(request.caregiver_preferences));
            }

            if (!string.IsNullOrEmpty(request.onboarding_notes))
            {
                sb.Append($",\"onboarding_notes\":\"{EscapeJson(request.onboarding_notes)}\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string BuildCaregiverPrefsJson(CaregiverPreferences prefs)
        {
            var sb = new StringBuilder();
            sb.Append("{");

            if (prefs.preferred_therapy_areas != null && prefs.preferred_therapy_areas.Length > 0)
            {
                sb.Append("\"preferred_therapy_areas\":[");
                for (int i = 0; i < prefs.preferred_therapy_areas.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append($"\"{EscapeJson(prefs.preferred_therapy_areas[i])}\"");
                }
                sb.Append("]");
            }
            else
            {
                sb.Append("\"preferred_therapy_areas\":[]");
            }

            sb.Append($",\"max_session_duration_minutes\":{prefs.max_session_duration_minutes}");
            sb.Append($",\"allow_adaptive_recommendations\":{prefs.allow_adaptive_recommendations.ToString().ToLower()}");
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Serialises a list of InteractionEvent objects as a JSON array.
        /// Uses manual building because JsonUtility does not support top-level arrays.
        /// </summary>
        private string BuildEventArrayJson(List<InteractionEvent> events)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < events.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(BuildEventJson(events[i]));
            }
            sb.Append("]");
            return sb.ToString();
        }

        private string BuildEventJson(InteractionEvent e)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"event_id\":\"{EscapeJson(e.event_id)}\"");
            sb.Append($",\"session_id\":\"{EscapeJson(e.session_id)}\"");
            sb.Append($",\"patient_id\":\"{EscapeJson(e.patient_id)}\"");
            sb.Append($",\"timestamp_utc\":\"{EscapeJson(e.timestamp_utc)}\"");
            sb.Append($",\"therapy_area\":\"{EscapeJson(e.therapy_area.ToString())}\"");
            sb.Append($",\"activity_id\":\"{EscapeJson(e.activity_id)}\"");
            sb.Append($",\"event_type\":\"{EscapeJson(e.event_type)}\"");

            if (e.payload != null)
            {
                sb.Append(",\"payload\":");
                sb.Append(BuildPayloadJson(e.payload));
            }

            if (e.metadata != null && e.metadata.Count > 0)
            {
                sb.Append(",\"metadata\":{");
                bool first = true;
                foreach (var kv in e.metadata)
                {
                    if (!first) sb.Append(",");
                    sb.Append($"\"{EscapeJson(kv.Key)}\":\"{EscapeJson(kv.Value)}\"");
                    first = false;
                }
                sb.Append("}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string BuildPayloadJson(InteractionPayload p)
        {
            var fields = new List<string>();

            if (p.attempt_index >= 1)
                fields.Add($"\"attempt_index\":{p.attempt_index}");

            // is_correct is nullable: true, false, or omitted (null)
            if (p.is_correct.HasValue)
                fields.Add($"\"is_correct\":{(p.is_correct.Value ? "true" : "false")}");

            if (p.latency_seconds >= 0f)
                fields.Add($"\"latency_seconds\":{p.latency_seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");

            if (p.hint_tier_delivered >= 1 && p.hint_tier_delivered <= 3)
                fields.Add($"\"hint_tier_delivered\":{p.hint_tier_delivered}");

            if (!string.IsNullOrEmpty(p.assistance_source))
                fields.Add($"\"assistance_source\":\"{EscapeJson(p.assistance_source)}\"");

            if (!string.IsNullOrEmpty(p.completion_status))
                fields.Add($"\"completion_status\":\"{EscapeJson(p.completion_status)}\"");

            if (p.pause_duration_seconds >= 0f)
                fields.Add($"\"pause_duration_seconds\":{p.pause_duration_seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");

            if (!string.IsNullOrEmpty(p.substep_identifier))
                fields.Add($"\"substep_identifier\":\"{EscapeJson(p.substep_identifier)}\"");

            if (!string.IsNullOrEmpty(p.notes))
                fields.Add($"\"notes\":\"{EscapeJson(p.notes)}\"");

            return "{" + string.Join(",", fields) + "}";
        }

        private string BuildCarlAssistanceJson(CarlAssistanceRequest r)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"patient_id\":\"{EscapeJson(r.patient_id)}\"");
            sb.Append($",\"session_id\":\"{EscapeJson(r.session_id)}\"");
            sb.Append($",\"therapy_area\":\"{EscapeJson(r.therapy_area)}\"");
            sb.Append($",\"activity_id\":\"{EscapeJson(r.activity_id)}\"");
            sb.Append($",\"action_type\":\"{EscapeJson(r.action_type)}\"");

            if (r.current_step >= 0)
                sb.Append($",\"current_step\":{r.current_step}");

            if (!string.IsNullOrEmpty(r.step_instruction))
                sb.Append($",\"step_instruction\":\"{EscapeJson(r.step_instruction)}\"");

            sb.Append("}");
            return sb.ToString();
        }

        private string BuildAssistanceEvaluateJson(AssistanceEvaluateRequest req)
        {
            if (req == null) return "{}";
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"request_id\":\"{EscapeJson(req.request_id ?? Guid.NewGuid().ToString())}\"");
            sb.Append($",\"seconds_since_last_prompt\":{req.seconds_since_last_prompt.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}");

            if (req.context != null)
            {
                sb.Append(",\"context\":{");
                sb.Append($"\"session_id\":\"{EscapeJson(req.context.session_id)}\"");
                sb.Append($",\"patient_id\":\"{EscapeJson(req.context.patient_id)}\"");
                sb.Append($",\"task_name\":\"{EscapeJson(req.context.task_name ?? "DefaultTask")}\"");
                sb.Append($",\"task_domain\":\"{EscapeJson(req.context.task_domain ?? "cognitive_selection")}\"");
                sb.Append($",\"step_index\":{Math.Max(1, req.context.step_index)}");
                sb.Append($",\"step_total_steps\":{Math.Max(1, req.context.step_total_steps)}");
                sb.Append($",\"response_latency_ms\":{req.context.response_latency_ms.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($",\"idle_duration_ms\":{req.context.idle_duration_ms.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($",\"target_miss_count\":{req.context.target_miss_count}");
                sb.Append($",\"repeated_click_count\":{req.context.repeated_click_count}");
                sb.Append($",\"step_error_count\":{req.context.step_error_count}");
                sb.Append($",\"cumulative_session_errors\":{req.context.cumulative_session_errors}");
                sb.Append($",\"consecutive_error_count\":{req.context.consecutive_error_count}");
                sb.Append($",\"session_duration_minutes\":{req.context.session_duration_minutes.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
                sb.Append($",\"patient_requested_help\":{req.context.patient_requested_help.ToString().ToLower()}");
                sb.Append($",\"patient_requested_repeat\":{req.context.patient_requested_repeat.ToString().ToLower()}");
                sb.Append($",\"patient_requested_break\":{req.context.patient_requested_break.ToString().ToLower()}");
                sb.Append($",\"patient_dismissed_prompt\":{req.context.patient_dismissed_prompt.ToString().ToLower()}");

                if (!string.IsNullOrEmpty(req.context.caregiver_locked_strategy))
                {
                    sb.Append($",\"caregiver_locked_strategy\":\"{EscapeJson(req.context.caregiver_locked_strategy)}\"");
                }
                if (req.context.caregiver_speech_rate_setting > 0)
                {
                    sb.Append($",\"caregiver_speech_rate_setting\":{req.context.caregiver_speech_rate_setting.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
                }
                if (!string.IsNullOrEmpty(req.context.candidate_prompt_text))
                {
                    sb.Append($",\"candidate_prompt_text\":\"{EscapeJson(req.context.candidate_prompt_text)}\"");
                }
                if (!string.IsNullOrEmpty(req.context.prior_strategy_offered))
                {
                    sb.Append($",\"prior_strategy_offered\":\"{EscapeJson(req.context.prior_strategy_offered)}\"");
                    sb.Append($",\"prior_strategy_success\":{req.context.prior_strategy_success.ToString().ToLower()}");
                }
                sb.Append($",\"client_version\":\"{EscapeJson(req.context.client_version ?? "3.0.0")}\"");
                sb.Append("}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        // -------------------------------------------------------------------------
        // Utility
        // -------------------------------------------------------------------------

        private static string EscapeJson(string value)
        {
            if (value == null) return "";
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        // -------------------------------------------------------------------------
        // Public convenience: start a health poll at startup
        // -------------------------------------------------------------------------

        private void Start()
        {
            // Perform an initial health check to establish connection status.
            GetHealth((ok, response) =>
            {
                if (ok && response != null)
                {
                    Debug.Log($"[BackendClient] Health OK — model_loaded={response.model_loaded}, version={response.model_version}");
                    SetStatus(BackendConnectionStatus.Connected);
                }
                else
                {
                    Debug.LogWarning("[BackendClient] Health check failed — backend may be offline.");
                    SetStatus(BackendConnectionStatus.Unreachable);
                }
            });
        }
    }
}
