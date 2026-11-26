// BackendModels.cs
// NeuroCompanion — Phase 3, Step 4: Backend Integration Layer
//
// C# data-transfer objects that mirror the API Contract v1 (P2A-30).
// Source of truth: Backend/docs/API_CONTRACT_v1.md
//
// Rules:
//  - Only fields that actually exist in the backend schema are present here.
//  - active_support_tier is nullable — it is ALWAYS null after onboarding.
//  - No CDR-to-tier mapping, no hard-coded tier selection, no fake AI.
//  - JsonUtility requires [Serializable] on every class.
//  - Nullable reference types use wrapper classes (JsonUtility limitation).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    // =========================================================================
    // §1  Health
    // =========================================================================

    /// <summary>Response from GET /api/v1/health</summary>
    [Serializable]
    public class HealthResponse
    {
        /// <summary>Always "ok" on success.</summary>
        public string status;

        /// <summary>True when pipeline.joblib is loaded in memory.</summary>
        public bool model_loaded;

        /// <summary>Model artifact version identifier (e.g. "oasis-practical-v1").</summary>
        public string model_version;
    }

    // =========================================================================
    // §2  Phase 1 CDR Prediction
    // =========================================================================

    /// <summary>Request body for POST /api/v1/predict</summary>
    [Serializable]
    public class PredictRequest
    {
        /// <summary>Participant age in years (30–105).</summary>
        public float Age;

        /// <summary>Biological sex: "M" or "F".</summary>
        [SerializeField] private string _mf;

        /// <summary>Education level code (1–5).</summary>
        public float Educ;

        /// <summary>Mini-Mental State Examination score (0–30).</summary>
        public float MMSE;

        // The backend field name is "M/F" which is not a valid C# identifier.
        // We build the JSON manually in BackendClient to handle this alias.
        public string MF { get => _mf; set => _mf = value; }
    }

    /// <summary>Probability distribution across three CDR classes.</summary>
    [Serializable]
    public class ClassProbabilities
    {
        public float class_0;
        public float class_1;
        public float class_2;
    }

    /// <summary>Response from POST /api/v1/predict</summary>
    [Serializable]
    public class PredictResponse
    {
        public string model_version;

        /// <summary>0, 1, or 2.</summary>
        public int predicted_class;

        /// <summary>Human-readable CDR category label.</summary>
        public string predicted_label;

        public ClassProbabilities class_probabilities;

        /// <summary>Mandatory educational disclaimer — always present.</summary>
        public string disclaimer;
    }

    // =========================================================================
    // §3  Patient Onboarding
    // =========================================================================

    /// <summary>Caregiver-provided initial preferences. All fields optional.</summary>
    [Serializable]
    public class CaregiverPreferences
    {
        /// <summary>Therapy areas to prioritise. Valid: "OrientationRoom", "ReminiscenceCorner", "CognitiveGarden".</summary>
        public string[] preferred_therapy_areas;

        /// <summary>Maximum session duration in minutes (5–60). Default: 15.</summary>
        public int max_session_duration_minutes = 15;

        /// <summary>Whether the system may propose tier adjustments. Default: true.</summary>
        public bool allow_adaptive_recommendations = true;
    }

    /// <summary>Request body for POST /api/v1/profiles/onboard</summary>
    [Serializable]
    public class OnboardRequest
    {
        /// <summary>Required. Unique anonymised patient identifier.</summary>
        public string patient_id;

        /// <summary>Optional. Caregiver-provided initial preferences.</summary>
        public CaregiverPreferences caregiver_preferences;

        /// <summary>Optional. Free-text accessibility notes.</summary>
        public string onboarding_notes;
    }

    /// <summary>Carl companion configuration returned from onboarding/support profile.</summary>
    [Serializable]
    public class CarlConfig
    {
        public string instruction_style;
        public bool allow_auto_hints;
        public float hesitation_threshold_seconds;
        public bool demonstrations_enabled;
        public float speech_rate_multiplier;
    }

    /// <summary>
    /// Patient support profile returned from onboarding, GET/POST support profile.
    /// CONTRACT GUARANTEE: active_support_tier is always null immediately after onboarding.
    /// </summary>
    [Serializable]
    public class SupportProfile
    {
        public string patient_id;
        public string created_at;

        /// <summary>
        /// Null immediately after onboarding. Never assign based on CDR result.
        /// Valid non-null values: "Standard", "Guided", "HighSupport".
        /// </summary>
        public string active_support_tier;   // null-safe: JsonUtility returns null for missing string fields

        public CarlConfig carl_config;
        public CaregiverPreferences caregiver_preferences;
        public string onboarding_notes;
    }

    /// <summary>Wrapper response for onboarding and support profile endpoints.</summary>
    [Serializable]
    public class ProfileResponse
    {
        public string status;
        public SupportProfile profile;
    }

    // =========================================================================
    // §4  Support Profile Management
    // =========================================================================

    /// <summary>Request body for POST /api/v1/profiles/support</summary>
    [Serializable]
    public class SupportProfileRequest
    {
        public string patient_id;

        /// <summary>null, "Standard", "Guided", or "HighSupport". Set by caregiver only.</summary>
        public string active_support_tier;

        public CarlConfig carl_config;
        public CaregiverPreferences caregiver_preferences;
    }

    // =========================================================================
    // §5  Telemetry Event Ingestion
    // =========================================================================

    /// <summary>
    /// Optional contextual measurements for a specific interaction event.
    /// All fields are optional — omit those that do not apply.
    /// </summary>
    [Serializable]
    public class InteractionPayload
    {
        /// <summary>Sequential attempt count (1-indexed). -1 = not set.</summary>
        public int attempt_index = -1;

        /// <summary>Backing store for JsonUtility persistence: -1 = null, 0 = false, 1 = true.</summary>
        [SerializeField] private int correctness_flag = -1;

        /// <summary>Whether the attempt was correct. null = unscored/unspecified.</summary>
        public bool? is_correct
        {
            get => correctness_flag == 1 ? true : (correctness_flag == 0 ? false : (bool?)null);
            set => correctness_flag = value.HasValue ? (value.Value ? 1 : 0) : -1;
        }

        /// <summary>Response time preceding this event in seconds. -1 = not set.</summary>
        public float latency_seconds = -1f;

        /// <summary>Graduated hint level: 1=verbal cue, 2=highlight, 3=walkthrough. -1 = not set.</summary>
        public int hint_tier_delivered = -1;

        /// <summary>"user_requested", "carl_auto_prompted", or "caregiver_initiated".</summary>
        public string assistance_source;

        /// <summary>"independent", "with_gentle_hints", "with_step_guidance", "skipped_by_user", "timed_out".</summary>
        public string completion_status;

        /// <summary>Pause duration if event_type is activity_paused. -1 = not set.</summary>
        public float pause_duration_seconds = -1f;

        /// <summary>Sub-task or item identifier.</summary>
        public string substep_identifier;

        /// <summary>Safe telemetry annotation.</summary>
        public string notes;
    }

    /// <summary>
    /// Atomic interaction record emitted by the Unity client.
    /// Sent as a JSON array to POST /api/v1/telemetry/events.
    /// </summary>
    [Serializable]
    public class InteractionEvent
    {
        /// <summary>Required. Unique event ID — use System.Guid.NewGuid().ToString().</summary>
        public string event_id;

        /// <summary>Required. Active session identifier.</summary>
        public string session_id;

        /// <summary>Required. Anonymised patient identifier.</summary>
        public string patient_id;

        /// <summary>Required. UTC timestamp in ISO-8601 format (e.g. "2026-11-11T17:00:00Z").</summary>
        public string timestamp_utc;

        /// <summary>
        /// Required. One of: OrientationRoom, ReminiscenceCorner, CognitiveGarden.
        /// </summary>
        public TherapyArea therapy_area;

        /// <summary>Required. Unique activity identifier within the therapy area.</summary>
        public string activity_id;

        /// <summary>
        /// Required. Valid values:
        ///   "activity_started", "instruction_requested", "instruction_repeated",
        ///   "hint_requested", "attempt_submitted", "assistance_triggered",
        ///   "demonstration_requested", "activity_paused", "activity_resumed",
        ///   "activity_skipped", "activity_completed"
        /// </summary>
        public string event_type;

        /// <summary>Optional. Contextual measurements for this event.</summary>
        public InteractionPayload payload;

        /// <summary>Optional. Extensible key-value metadata dictionary (not natively serialisable by JsonUtility — handled manually).</summary>
        [NonSerialized]
        public Dictionary<string, string> metadata;
    }

    /// <summary>Response from POST /api/v1/telemetry/events</summary>
    [Serializable]
    public class TelemetryResponse
    {
        public string status;
        public int received_count;
        public int inserted_count;
        public int skipped_duplicates_count;
    }

    // =========================================================================
    // §6  Interaction Profile
    // =========================================================================

    /// <summary>Aggregated behavioral summary for one therapy area.</summary>
    [Serializable]
    public class AreaSummary
    {
        public int activities_attempted;
        public int activities_completed;
        public int hints_requested_total;
        public int instructions_repeated_total;
        public int demonstrations_requested_total;
        public int skips_total;
        public float average_response_latency_seconds; // 0 if null from backend
    }

    /// <summary>
    /// Longitudinal interaction summary returned from GET /api/v1/profiles/interaction/{patient_id}.
    /// No cognitive inference is made by the backend — these are raw behavioral counts.
    /// </summary>
    [Serializable]
    public class InteractionProfileData
    {
        public string patient_id;
        public string last_updated;
        public int total_sessions_recorded;
        public float overall_completion_rate;

        // area_summaries is a dictionary keyed by therapy area string.
        // Deserialized manually in BackendClient due to JsonUtility limitations.
        [NonSerialized] public Dictionary<string, AreaSummary> area_summaries;

        // observed_assistance_frequency is also a dictionary.
        [NonSerialized] public Dictionary<string, int> observed_assistance_frequency;

        public string recent_trend_note;
    }

    /// <summary>Wrapper for GET /api/v1/profiles/interaction/{patient_id}</summary>
    [Serializable]
    public class InteractionProfileResponse
    {
        public string status;
        public InteractionProfileData profile;
    }

    // =========================================================================
    // §7  Support Adaptation Recommendations
    // =========================================================================

    /// <summary>
    /// A single support tier recommendation. Always requires caregiver review.
    /// is_temporary_baseline == true means this is NOT an ML prediction.
    /// </summary>
    [Serializable]
    public class SupportRecommendation
    {
        public string recommendation_id;
        public string patient_id;
        public string timestamp_utc;
        public string current_tier;        // null-safe
        public string recommended_tier;
        public string rationale;
        public string triggering_area;
        public bool is_temporary_baseline;
    }

    /// <summary>Response from GET /api/v1/profiles/recommendations/{patient_id}</summary>
    [Serializable]
    public class RecommendationsResponse
    {
        public string status;
        public string patient_id;
        public int count;
        public SupportRecommendation[] recommendations;
    }

    // =========================================================================
    // §8  Immediate Carl Assistance
    // =========================================================================

    /// <summary>Request body for POST /api/v1/carl/assistance</summary>
    [Serializable]
    public class CarlAssistanceRequest
    {
        /// <summary>Required. Anonymised patient identifier.</summary>
        public string patient_id;

        /// <summary>Required. Active session identifier.</summary>
        public string session_id;

        /// <summary>Required. "OrientationRoom", "ReminiscenceCorner", or "CognitiveGarden".</summary>
        public string therapy_area;

        /// <summary>Required. Current activity identifier.</summary>
        public string activity_id;

        /// <summary>
        /// Required. Valid values:
        ///   "repeat_instruction", "step_by_step_breakdown", "visual_demonstration",
        ///   "show_hint", "slow_pacing", "pause_or_skip"
        /// </summary>
        public string action_type;

        /// <summary>Optional. Current step number within the activity.</summary>
        public int current_step = -1;

        /// <summary>Optional. Instruction text for the current step (used in repeat_instruction response).</summary>
        public string step_instruction;
    }

    /// <summary>
    /// Response from POST /api/v1/carl/assistance.
    /// carl_state is the Animator state identifier for Carl in Unity.
    /// visual_cue is a symbolic string — Unity must dispatch it to scene behaviour.
    /// </summary>
    [Serializable]
    public class CarlAssistanceResponse
    {
        public string status;
        public string action_type;

        /// <summary>Text for Carl to speak via Unity TTS.</summary>
        public string spoken_prompt;

        /// <summary>
        /// Symbolic Unity UI directive. Possible values:
        ///   "highlight_current_target", "isolate_single_element", "play_ghost_animation",
        ///   "pulse_recommended_item", "show_navigation_options", "gentle_companion_wave", null
        /// </summary>
        public string visual_cue;

        /// <summary>Speed factor (less than 1.0 = slower, 1.0 = normal).</summary>
        public float applied_pacing_multiplier;

        /// <summary>Always true — patient may always skip.</summary>
        public bool allow_skip;

        /// <summary>Animator state identifier for Carl.</summary>
        public string carl_state;
    }

    // =========================================================================
    // §9  Caregiver Override and Lock
    // =========================================================================

    /// <summary>Request body for POST /api/v1/profiles/override</summary>
    [Serializable]
    public class CaregiverOverrideRequest
    {
        /// <summary>Required. Unique transaction ID — use System.Guid.NewGuid().ToString().</summary>
        public string override_id;

        /// <summary>Required. Anonymised patient identifier.</summary>
        public string patient_id;

        /// <summary>Required. "Standard", "Guided", or "HighSupport".</summary>
        public string selected_tier;

        /// <summary>Required. If true, automated recommendations are suppressed.</summary>
        public bool lock_tier;

        /// <summary>Optional. UTC timestamp for the override.</summary>
        public string timestamp_utc;

        /// <summary>Optional. Reason recorded in audit log.</summary>
        public string caregiver_note;
    }

    /// <summary>Response from POST /api/v1/profiles/override</summary>
    [Serializable]
    public class CaregiverOverrideResponse
    {
        public string status;
        public string message;
        public string active_tier;
        public bool lock_tier;
    }

    // =========================================================================
    // Error response
    // =========================================================================

    /// <summary>FastAPI default error format for 4xx/5xx responses.</summary>
    [Serializable]
    public class ErrorResponse
    {
        public string detail;
    }

    // =========================================================================
    // Support Tier constants (application-level only — NOT clinical staging)
    // =========================================================================

    /// <summary>
    /// Application-level assistance tier string constants.
    /// IMPORTANT: These are software assistance preferences, not clinical labels.
    /// Never present to patients or caregivers as diagnostic labels.
    /// </summary>
    public static class SupportTier
    {
        public const string Standard    = "Standard";
        public const string Guided      = "Guided";
        public const string HighSupport = "HighSupport";
    }

    // =========================================================================
    // Therapy area and event type constants (matches backend enums exactly)
    // =========================================================================

    [Serializable]
    public enum TherapyArea
    {
        OrientationRoom,
        ReminiscenceCorner,
        CognitiveGarden
    }

    /// <summary>
    /// Encapsulates the HTTP response from the backend, preserving status codes,
    /// headers, raw body, and strongly typed parsed payload.
    /// </summary>
    public struct BackendResult<T> where T : class
    {
        public bool IsSuccess;
        public long StatusCode;
        public T Data;
        public string RawBody;
        public string ErrorMessage;
        public int? RetryAfterSeconds;
    }

    public static class EventType
    {
        public const string ActivityStarted       = "activity_started";
        public const string InstructionRequested  = "instruction_requested";
        public const string InstructionRepeated   = "instruction_repeated";
        public const string HintRequested         = "hint_requested";
        public const string AttemptSubmitted      = "attempt_submitted";
        public const string AssistanceTriggered   = "assistance_triggered";
        public const string DemonstrationRequested = "demonstration_requested";
        public const string ActivityPaused        = "activity_paused";
        public const string ActivityResumed       = "activity_resumed";
        public const string ActivitySkipped       = "activity_skipped";
        public const string ActivityCompleted     = "activity_completed";
    }

    public static class AssistanceSource
    {
        public const string UserRequested      = "user_requested";
        public const string CarlAutoPrompted   = "carl_auto_prompted";
        public const string CaregiverInitiated = "caregiver_initiated";
    }

    public static class CompletionStatus
    {
        public const string Independent       = "independent";
        public const string WithGentleHints   = "with_gentle_hints";
        public const string WithStepGuidance  = "with_step_guidance";
        public const string SkippedByUser     = "skipped_by_user";
        public const string TimedOut          = "timed_out";
    }

    public static class CarlActionType
    {
        public const string RepeatInstruction    = "repeat_instruction";
        public const string StepByStepBreakdown  = "step_by_step_breakdown";
        public const string VisualDemonstration  = "visual_demonstration";
        public const string ShowHint             = "show_hint";
        public const string SlowPacing           = "slow_pacing";
        public const string PauseOrSkip          = "pause_or_skip";

        // Phase 4.7 Canonical CarlActionType identifiers returned by the Assistance Runtime
        public const string REPEAT_CURRENT_INSTRUCTION     = "REPEAT_CURRENT_INSTRUCTION";
        public const string SIMPLIFY_CURRENT_INSTRUCTION   = "SIMPLIFY_CURRENT_INSTRUCTION";
        public const string PROVIDE_STEP_BY_STEP_GUIDANCE  = "PROVIDE_STEP_BY_STEP_GUIDANCE";
        public const string SHOW_VISUAL_HINT               = "SHOW_VISUAL_HINT";
        public const string SHOW_DEMONSTRATION             = "SHOW_DEMONSTRATION";
        public const string SLOW_SPEECH                    = "SLOW_SPEECH";
        public const string ALLOW_ADDITIONAL_RESPONSE_TIME = "ALLOW_ADDITIONAL_RESPONSE_TIME";
        public const string OFFER_OPTIONAL_BREAK           = "OFFER_OPTIONAL_BREAK";
        public const string PROVIDE_VERBAL_EXPLANATION     = "PROVIDE_VERBAL_EXPLANATION";
        public const string ALLOW_INDEPENDENT_COMPLETION   = "ALLOW_INDEPENDENT_COMPLETION";
    }

    public static class StrategyID
    {
        public const string AS01_REPEAT             = "AS01_REPEAT";
        public const string AS02_SIMPLIFY           = "AS02_SIMPLIFY";
        public const string AS03_STEP_GUIDANCE      = "AS03_STEP_GUIDANCE";
        public const string AS04_VISUAL_HINT        = "AS04_VISUAL_HINT";
        public const string AS05_DEMONSTRATION      = "AS05_DEMONSTRATION";
        public const string AS06_SLOWER_SPEECH      = "AS06_SLOWER_SPEECH";
        public const string AS07_RESPONSE_TIME      = "AS07_RESPONSE_TIME";
        public const string AS08_BREAK_PAUSE        = "AS08_BREAK_PAUSE";
        public const string AS09_VERBAL_EXPLANATION = "AS09_VERBAL_EXPLANATION";
        public const string AS10_INDEPENDENT        = "AS10_INDEPENDENT";
    }

    // =========================================================================
    // §10  Phase 4.8 / Phase 5 Assistance Evaluation — POST /api/v1/assistance/evaluate
    // =========================================================================

    /// <summary>
    /// Interaction telemetry context payload matching InteractionTelemetrySchema.
    /// </summary>
    [Serializable]
    public class InteractionTelemetryContextDto
    {
        public string session_id;
        public string patient_id;
        public string task_name = "DefaultTask";
        public string task_domain = "cognitive_selection";
        public int step_index = 1;
        public int step_total_steps = 1;
        public float response_latency_ms;
        public float idle_duration_ms;
        public int target_miss_count;
        public int repeated_click_count;
        public int step_error_count;
        public int cumulative_session_errors;
        public int consecutive_error_count;
        public float session_duration_minutes;
        public bool patient_requested_help;
        public bool patient_requested_repeat;
        public bool patient_requested_break;
        public bool patient_dismissed_prompt;
        public string caregiver_locked_strategy;
        public float caregiver_speech_rate_setting = 1.0f;
        public string candidate_prompt_text;
        public string prior_strategy_offered;
        public bool prior_strategy_success;
        public string client_version = "3.0.0";
    }

    /// <summary>
    /// Request body for POST /api/v1/assistance/evaluate.
    /// Supports nested context (recommended) or flat parameters.
    /// </summary>
    [Serializable]
    public class AssistanceEvaluateRequest
    {
        public InteractionTelemetryContextDto context;
        public float seconds_since_last_prompt = 60.0f;
        public string request_id;

        public static AssistanceEvaluateRequest Create(
            string patientId,
            string sessionId,
            string taskName = "DefaultTask",
            int stepIndex = 1,
            int stepTotalSteps = 1,
            float idleDurationMs = 0f,
            int stepErrorCount = 0,
            bool patientRequestedHelp = false,
            string caregiverLockedStrategy = null)
        {
            var req = new AssistanceEvaluateRequest();
            req.request_id = Guid.NewGuid().ToString();
            req.context = new InteractionTelemetryContextDto
            {
                patient_id = patientId,
                session_id = sessionId,
                task_name = taskName,
                step_index = Mathf.Max(1, stepIndex),
                step_total_steps = Mathf.Max(1, stepTotalSteps),
                idle_duration_ms = idleDurationMs,
                step_error_count = stepErrorCount,
                patient_requested_help = patientRequestedHelp,
                caregiver_locked_strategy = caregiverLockedStrategy,
                client_version = "3.0.0"
            };
            return req;
        }
    }

    /// <summary>
    /// Abstract companion action specification returned within AssistanceEvaluateResponse.
    /// </summary>
    [Serializable]
    public class CarlActionDto
    {
        public string action_type;
        public string strategy_id;
        public bool is_optional;
        public string description;
    }

    /// <summary>
    /// Structured response from POST /api/v1/assistance/evaluate.
    /// </summary>
    [Serializable]
    public class AssistanceEvaluateResponse
    {
        public string request_id;
        public string patient_id;
        public string session_id;
        public string runtime_status;
        public string decision_id;
        public string decision_status;
        public string selected_strategy_id;
        public string decision_reason;
        public string[] policy_rules_applied;
        public bool caregiver_control_applied;
        public bool patient_choice_required;
        public string patient_choice_status;
        public bool requires_carl_action;
        public CarlActionDto carl_action;
        public string created_at;
    }

    public static class VisualCue
    {
        public const string HighlightCurrentTarget  = "highlight_current_target";
        public const string IsolateSingleElement    = "isolate_single_element";
        public const string PlayGhostAnimation      = "play_ghost_animation";
        public const string PulseRecommendedItem    = "pulse_recommended_item";
        public const string ShowNavigationOptions   = "show_navigation_options";
        public const string GentleCompanionWave     = "gentle_companion_wave";
        // null means no visual cue required
    }

    // =========================================================================
    // §11 Authentication & User Profiles
    // =========================================================================

    [Serializable]
    public class UserRegisterRequest
    {
        public string full_name;
        public string email;
        public string password;
        public string role = "caregiver"; // "caregiver" or "patient"
    }

    [Serializable]
    public class UserLoginRequest
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class ForgotPasswordRequest
    {
        public string email;
    }

    [Serializable]
    public class ForgotPasswordResponse
    {
        public string status;
        public string email;
        public string message;
        public bool recovery_initiated;
        public string support_contact;
    }

    [Serializable]
    public class UserProfileDto
    {
        public string user_id;
        public string email;
        public string full_name;
        public string role;
        public string profile_picture_url;
        public string patient_association;
        public string patient_id;
        public string created_at;
        public string last_login;
    }

    [Serializable]
    public class AuthTokenResponse
    {
        public string status;
        public string token;
        public string token_type;
        public int expires_in_seconds;
        public UserProfileDto user;
    }

    [Serializable]
    public class PatientModeEntryRequest
    {
        public string patient_id = "patient_001";
    }

    [Serializable]
    public class PatientModeEntryResponse
    {
        public string status;
        public string mode;
        public string patient_id;
        public string active_support_tier;
        public string carl_instruction_style;
        public bool allow_auto_hints;
        public float hesitation_threshold_seconds;
        public float speech_rate_multiplier;
        public string message;
    }
}
