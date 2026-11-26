// PatientTelemetryTracker.cs
// NeuroCompanion — Phase 5.2: Patient Interaction Telemetry Integration
//
// Central manager that aggregates, validates, and standardizes patient interaction
// telemetry in real-time, bridging observable Unity activity events to the Phase 4 AI Brain.
//
// Architectural Principles:
//  - Event-driven: Receives genuine interaction events from patient activities.
//  - Decoupled: Zero dependency on Carl's character rigs, meshes, lip-sync, or audio clips.
//  - Validating: Enforces non-negative counts, reliable millisecond timers, and anti-duplication.
//  - Patient Autonomy: Pauses, skips, and breaks are respected without penalization.
//  - Dual-Path Integration:
//      1. Real-time context: Formats and dispatches AssistanceEvaluateRequest to AiBrainBridge.
//      2. Longitudinal persistence: Synchronizes events with TelemetryEmitter disk journal.

using System;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// Singleton MonoBehaviour managing in-session patient interaction telemetry.
    /// Attach to [MANAGERS]/Patient_Telemetry_Tracker.
    /// </summary>
    public class PatientTelemetryTracker : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------

        public static PatientTelemetryTracker Instance { get; private set; }

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
        // Inspector Configuration
        // -------------------------------------------------------------------------

        [Header("Telemetry Configuration")]
        [Tooltip("Minimum seconds between successive AI assistance evaluation requests (anti-spam debounce).")]
        [SerializeField] private float _minEvaluationIntervalSeconds = 2.0f;

        [Tooltip("Enable verbose telemetry logging to the Unity Console.")]
        [SerializeField] private bool _verboseLogging = true;

        [Tooltip("Client telemetry version string.")]
        [SerializeField] private string _clientVersion = "3.0.0";

        // -------------------------------------------------------------------------
        // Observable Active Session State
        // -------------------------------------------------------------------------

        [Header("Active Session State (Read Only)")]
        [SerializeField] private string _patientId = "";
        [SerializeField] private string _sessionId = "";
        [SerializeField] private string _currentActivityId = "";
        [SerializeField] private TherapyArea _currentTherapyArea = TherapyArea.OrientationRoom;
        [SerializeField] private string _taskDomain = "cognitive_selection";
        [SerializeField] private int _currentStepIndex = 1;
        [SerializeField] private int _stepTotalSteps = 1;
        [SerializeField] private bool _isActivityActive = false;
        [SerializeField] private bool _isActivityPaused = false;

        [Header("Live Step Metrics (Read Only)")]
        [SerializeField] private float _stepIdleDurationMs = 0f;
        [SerializeField] private float _lastResponseLatencyMs = 0f;
        [SerializeField] private int _targetMissCount = 0;
        [SerializeField] private int _repeatedClickCount = 0;
        [SerializeField] private int _stepErrorCount = 0;
        [SerializeField] private int _cumulativeSessionErrors = 0;
        [SerializeField] private int _consecutiveErrorCount = 0;

        [Header("Patient Intent Flags")]
        [SerializeField] private bool _patientRequestedHelp = false;
        [SerializeField] private bool _patientRequestedRepeat = false;
        [SerializeField] private bool _patientRequestedBreak = false;
        [SerializeField] private bool _patientDismissedPrompt = false;

        [Header("Caregiver Governance")]
        [SerializeField] private string _caregiverLockedStrategy = null;
        [SerializeField] private float _caregiverSpeechRateSetting = 1.0f;

        // Internal timing
        private float _sessionStartTime = 0f;
        private float _stepStartTime = 0f;
        private float _lastActionTimestamp = 0f;
        private float _lastEvaluationDispatchTime = -999f;
        private float _pauseStartTime = 0f;
        private string _candidatePromptText = null;
        private string _priorStrategyOffered = null;
        private bool _priorStrategySuccess = false;

        // -------------------------------------------------------------------------
        // Public Properties
        // -------------------------------------------------------------------------

        public string PatientId => _patientId;
        public string SessionId => _sessionId;
        public string CurrentActivityId => _currentActivityId;
        public TherapyArea CurrentTherapyArea => _currentTherapyArea;
        public int CurrentStepIndex => _currentStepIndex;
        public int StepTotalSteps => _stepTotalSteps;
        public bool IsActivityActive => _isActivityActive;
        public bool IsActivityPaused => _isActivityPaused;
        public int StepErrorCount => _stepErrorCount;
        public int CumulativeSessionErrors => _cumulativeSessionErrors;
        public float IdleDurationMs => _stepIdleDurationMs;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Raised whenever a valid interaction event is recorded.</summary>
        public event Action<string, InteractionTelemetryContextDto> OnInteractionEventRecorded;

        /// <summary>Raised when an assistance evaluation request is dispatched to AiBrainBridge.</summary>
        public event Action<AssistanceEvaluateRequest> OnAssistanceEvaluationDispatched;

        // -------------------------------------------------------------------------
        // Lifecycle & Idle Timing
        // -------------------------------------------------------------------------

        private void Update()
        {
            if (!_isActivityActive || _isActivityPaused) return;

            // Track genuine inactivity/idle duration in milliseconds
            _stepIdleDurationMs = Mathf.Max(0f, (Time.realtimeSinceStartup - _lastActionTimestamp) * 1000f);
        }

        // -------------------------------------------------------------------------
        // Session & Activity Management
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initializes a patient activity session.
        /// </summary>
        public void StartActivity(
            string patientId,
            string sessionId,
            string activityId,
            TherapyArea therapyArea,
            string taskDomain = "cognitive_selection",
            int totalSteps = 1)
        {
            if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(sessionId))
            {
                Debug.LogError("[PatientTelemetryTracker] StartActivity rejected: patientId and sessionId must be non-empty strings.");
                return;
            }

            _patientId = patientId.Trim();
            _sessionId = sessionId.Trim();
            _currentActivityId = string.IsNullOrWhiteSpace(activityId) ? "DefaultActivity" : activityId.Trim();
            _currentTherapyArea = therapyArea;
            _taskDomain = string.IsNullOrWhiteSpace(taskDomain) ? "cognitive_selection" : taskDomain.Trim();
            _stepTotalSteps = Mathf.Max(1, totalSteps);
            _currentStepIndex = 1;

            _sessionStartTime = Time.realtimeSinceStartup;
            _isActivityActive = true;
            _isActivityPaused = false;
            _cumulativeSessionErrors = 0;
            _consecutiveErrorCount = 0;

            ResetStepMetrics();

            if (_verboseLogging)
            {
                Debug.Log($"[PatientTelemetryTracker] Activity Started: {_currentActivityId} ({_currentTherapyArea}) for patient {_patientId}, total steps={_stepTotalSteps}");
            }

            // Sync with durable telemetry emitter
            if (TelemetryEmitter.Instance != null)
            {
                TelemetryEmitter.Instance.EmitActivityStarted(_patientId, _sessionId, _currentTherapyArea, _currentActivityId);
            }

            NotifyEventRecorded("activity_started");
        }

        /// <summary>
        /// Transitions to a specific step index within the active activity.
        /// </summary>
        public void BeginStep(int stepIndex, string candidatePromptText = null)
        {
            if (!_isActivityActive)
            {
                Debug.LogWarning("[PatientTelemetryTracker] BeginStep called while no activity is active.");
                return;
            }

            _currentStepIndex = Mathf.Clamp(stepIndex, 1, _stepTotalSteps);
            _candidatePromptText = candidatePromptText;
            ResetStepMetrics();

            if (_verboseLogging)
            {
                Debug.Log($"[PatientTelemetryTracker] Step {_currentStepIndex}/{_stepTotalSteps} begun for activity {_currentActivityId}");
            }

            NotifyEventRecorded("step_started");
        }

        /// <summary>
        /// Records an attempt at the current step.
        /// </summary>
        public void RecordAttempt(bool isCorrect, float? overrideLatencyMs = null, string substepId = null)
        {
            if (!_isActivityActive) return;

            float now = Time.realtimeSinceStartup;
            _lastResponseLatencyMs = overrideLatencyMs.HasValue && overrideLatencyMs.Value >= 0f
                ? overrideLatencyMs.Value
                : Mathf.Max(0f, (now - _stepStartTime) * 1000f);

            _lastActionTimestamp = now;

            if (isCorrect)
            {
                _consecutiveErrorCount = 0;
            }
            else
            {
                _stepErrorCount++;
                _cumulativeSessionErrors++;
                _consecutiveErrorCount++;
            }

            if (_verboseLogging)
            {
                Debug.Log($"[PatientTelemetryTracker] Attempt recorded: correct={isCorrect}, latency={_lastResponseLatencyMs:F0}ms, stepErrors={_stepErrorCount}");
            }

            // Sync with durable TelemetryEmitter
            if (TelemetryEmitter.Instance != null)
            {
                int attemptIndex = _stepErrorCount + (isCorrect ? 1 : 0);
                TelemetryEmitter.Instance.EmitAttemptSubmitted(
                    _patientId, _sessionId, _currentTherapyArea, _currentActivityId,
                    attemptIndex, isCorrect, _lastResponseLatencyMs / 1000f, substepId);
            }

            NotifyEventRecorded("attempt_submitted");
        }

        /// <summary>
        /// Records a target miss (e.g. click in void / wrong screen area).
        /// </summary>
        public void RecordTargetMiss()
        {
            if (!_isActivityActive) return;

            _targetMissCount++;
            _lastActionTimestamp = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log($"[PatientTelemetryTracker] Target miss recorded (count: {_targetMissCount})");

            NotifyEventRecorded("target_miss");
        }

        /// <summary>
        /// Records a rapid repeated click / double-tap.
        /// </summary>
        public void RecordRepeatedClick()
        {
            if (!_isActivityActive) return;

            _repeatedClickCount++;
            _lastActionTimestamp = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log($"[PatientTelemetryTracker] Repeated click recorded (count: {_repeatedClickCount})");

            NotifyEventRecorded("repeated_click");
        }

        /// <summary>
        /// Records an explicit patient request for help (e.g. Help button pressed).
        /// </summary>
        public void RecordPatientHelpRequest()
        {
            if (!_isActivityActive) return;

            _patientRequestedHelp = true;
            _lastActionTimestamp = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log("[PatientTelemetryTracker] Patient requested help.");

            // Sync with TelemetryEmitter
            if (TelemetryEmitter.Instance != null)
            {
                TelemetryEmitter.Instance.EmitHintRequested(
                    _patientId, _sessionId, _currentTherapyArea, _currentActivityId,
                    hintTier: 1, latencySeconds: _stepIdleDurationMs / 1000f);
            }

            NotifyEventRecorded("patient_requested_help");
        }

        /// <summary>
        /// Records an explicit patient request to repeat instructions.
        /// </summary>
        public void RecordPatientRepeatRequest()
        {
            if (!_isActivityActive) return;

            _patientRequestedRepeat = true;
            _lastActionTimestamp = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log("[PatientTelemetryTracker] Patient requested instruction repeat.");

            // Sync with TelemetryEmitter
            if (TelemetryEmitter.Instance != null)
            {
                TelemetryEmitter.Instance.EmitInstructionRepeated(
                    _patientId, _sessionId, _currentTherapyArea, _currentActivityId,
                    latencySeconds: _stepIdleDurationMs / 1000f);
            }

            NotifyEventRecorded("patient_requested_repeat");
        }

        /// <summary>
        /// Records an explicit patient request for a break / rest pause.
        /// </summary>
        public void RecordPatientBreakRequest()
        {
            if (!_isActivityActive) return;

            _patientRequestedBreak = true;
            _lastActionTimestamp = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log("[PatientTelemetryTracker] Patient requested break.");

            NotifyEventRecorded("patient_requested_break");
        }

        /// <summary>
        /// Records a patient dismissal of a proactive prompt.
        /// </summary>
        public void RecordPatientDismissedPrompt()
        {
            if (!_isActivityActive) return;

            _patientDismissedPrompt = true;
            _lastActionTimestamp = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log("[PatientTelemetryTracker] Patient dismissed active prompt.");

            NotifyEventRecorded("patient_dismissed_prompt");
        }

        /// <summary>
        /// Pauses the current activity (patient autonomy preservation).
        /// </summary>
        public void PauseActivity()
        {
            if (!_isActivityActive || _isActivityPaused) return;

            _isActivityPaused = true;
            _pauseStartTime = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log("[PatientTelemetryTracker] Activity paused.");

            if (TelemetryEmitter.Instance != null)
            {
                TelemetryEmitter.Instance.EmitActivityPaused(_patientId, _sessionId, _currentTherapyArea, _currentActivityId);
            }

            NotifyEventRecorded("activity_paused");
        }

        /// <summary>
        /// Resumes the paused activity.
        /// </summary>
        public void ResumeActivity()
        {
            if (!_isActivityActive || !_isActivityPaused) return;

            float pauseDurationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _pauseStartTime);
            _isActivityPaused = false;
            _lastActionTimestamp = Time.realtimeSinceStartup;

            if (_verboseLogging)
                Debug.Log($"[PatientTelemetryTracker] Activity resumed (paused for {pauseDurationSeconds:F1}s).");

            if (TelemetryEmitter.Instance != null)
            {
                TelemetryEmitter.Instance.EmitActivityResumed(_patientId, _sessionId, _currentTherapyArea, _currentActivityId, pauseDurationSeconds);
            }

            NotifyEventRecorded("activity_resumed");
        }

        /// <summary>
        /// Records patient skipping an activity (no penalty, preserves autonomy).
        /// </summary>
        public void SkipActivity()
        {
            if (!_isActivityActive) return;

            _isActivityActive = false;
            _isActivityPaused = false;

            if (_verboseLogging)
                Debug.Log($"[PatientTelemetryTracker] Activity {_currentActivityId} skipped by patient.");

            if (TelemetryEmitter.Instance != null)
            {
                TelemetryEmitter.Instance.EmitActivitySkipped(_patientId, _sessionId, _currentTherapyArea, _currentActivityId);
            }

            NotifyEventRecorded("activity_skipped");
        }

        /// <summary>
        /// Records successful activity completion.
        /// </summary>
        public void CompleteActivity(string completionStatus = CompletionStatus.Independent)
        {
            if (!_isActivityActive) return;

            _isActivityActive = false;
            _isActivityPaused = false;

            if (_verboseLogging)
                Debug.Log($"[PatientTelemetryTracker] Activity {_currentActivityId} completed: status={completionStatus}");

            if (TelemetryEmitter.Instance != null)
            {
                TelemetryEmitter.Instance.EmitActivityCompleted(_patientId, _sessionId, _currentTherapyArea, _currentActivityId, completionStatus);
            }

            NotifyEventRecorded("activity_completed");
        }

        // -------------------------------------------------------------------------
        // Caregiver Preset Governance
        // -------------------------------------------------------------------------

        public void SetCaregiverLockedStrategy(string strategyId)
        {
            _caregiverLockedStrategy = string.IsNullOrWhiteSpace(strategyId) ? null : strategyId.Trim();
            if (_verboseLogging)
                Debug.Log($"[PatientTelemetryTracker] Caregiver locked strategy set to: {_caregiverLockedStrategy ?? "NONE"}");
        }

        public void SetCaregiverSpeechRate(float multiplier)
        {
            _caregiverSpeechRateSetting = Mathf.Clamp(multiplier, 0.5f, 1.5f);
        }

        public void SetPriorStrategyOutcome(string strategyId, bool succeeded)
        {
            _priorStrategyOffered = strategyId;
            _priorStrategySuccess = succeeded;
        }

        // -------------------------------------------------------------------------
        // Telemetry Context Construction & AI Brain Evaluation
        // -------------------------------------------------------------------------

        /// <summary>
        /// Builds a verified, non-null InteractionTelemetryContextDto snapshot from current live state.
        /// Guaranteed valid according to Phase 4 Pydantic schema rules.
        /// </summary>
        public InteractionTelemetryContextDto BuildCurrentContextSnapshot()
        {
            float elapsedSessionMinutes = Mathf.Max(0f, (Time.realtimeSinceStartup - _sessionStartTime) / 60f);

            return new InteractionTelemetryContextDto
            {
                session_id = _sessionId,
                patient_id = _patientId,
                task_name = _currentActivityId,
                task_domain = _taskDomain,
                step_index = Mathf.Max(1, _currentStepIndex),
                step_total_steps = Mathf.Max(1, _stepTotalSteps),
                response_latency_ms = Mathf.Max(0f, _lastResponseLatencyMs),
                idle_duration_ms = Mathf.Max(0f, _stepIdleDurationMs),
                target_miss_count = Mathf.Max(0, _targetMissCount),
                repeated_click_count = Mathf.Max(0, _repeatedClickCount),
                step_error_count = Mathf.Max(0, _stepErrorCount),
                cumulative_session_errors = Mathf.Max(0, _cumulativeSessionErrors),
                consecutive_error_count = Mathf.Max(0, _consecutiveErrorCount),
                session_duration_minutes = elapsedSessionMinutes,
                patient_requested_help = _patientRequestedHelp,
                patient_requested_repeat = _patientRequestedRepeat,
                patient_requested_break = _patientRequestedBreak,
                patient_dismissed_prompt = _patientDismissedPrompt,
                caregiver_locked_strategy = _caregiverLockedStrategy,
                caregiver_speech_rate_setting = _caregiverSpeechRateSetting,
                candidate_prompt_text = _candidatePromptText,
                prior_strategy_offered = _priorStrategyOffered,
                prior_strategy_success = _priorStrategySuccess,
                client_version = _clientVersion
            };
        }

        /// <summary>
        /// Dispatches a structured evaluation request to the Phase 4 AI Brain via AiBrainBridge.
        /// Enforces anti-spam debounce, non-empty identifiers, and safe bridge validation.
        /// </summary>
        public bool TriggerAssistanceEvaluation(Action<BackendResult<AssistanceEvaluateResponse>> onComplete = null)
        {
            if (!_isActivityActive)
            {
                Debug.LogWarning("[PatientTelemetryTracker] Cannot trigger evaluation: No active activity session.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_patientId) || string.IsNullOrWhiteSpace(_sessionId))
            {
                Debug.LogError("[PatientTelemetryTracker] Cannot trigger evaluation: Missing valid patientId or sessionId.");
                return false;
            }

            // Enforce debounce
            float now = Time.realtimeSinceStartup;
            if (now - _lastEvaluationDispatchTime < _minEvaluationIntervalSeconds)
            {
                if (_verboseLogging)
                    Debug.LogWarning($"[PatientTelemetryTracker] Evaluation suppressed by debounce ({now - _lastEvaluationDispatchTime:F1}s < {_minEvaluationIntervalSeconds:F1}s).");
                return false;
            }

            if (AiBrainBridge.Instance == null)
            {
                Debug.LogError("[PatientTelemetryTracker] AiBrainBridge instance is not available in the scene.");
                return false;
            }

            if (AiBrainBridge.Instance.IsEvaluating)
            {
                if (_verboseLogging)
                    Debug.Log("[PatientTelemetryTracker] AiBrainBridge is already evaluating an in-flight request. Skipping.");
                return false;
            }

            _lastEvaluationDispatchTime = now;

            var request = new AssistanceEvaluateRequest
            {
                request_id = Guid.NewGuid().ToString(),
                seconds_since_last_prompt = 60.0f,
                context = BuildCurrentContextSnapshot()
            };

            if (_verboseLogging)
            {
                Debug.Log($"[PatientTelemetryTracker] Submitting evaluation request: step={_currentStepIndex}, idle={_stepIdleDurationMs:F0}ms, help={_patientRequestedHelp}, errors={_stepErrorCount}");
            }

            OnAssistanceEvaluationDispatched?.Invoke(request);
            AiBrainBridge.Instance.RequestAssistanceEvaluation(request, onComplete);
            return true;
        }

        // -------------------------------------------------------------------------
        // Internal Helpers
        // -------------------------------------------------------------------------

        private void ResetStepMetrics()
        {
            _stepStartTime = Time.realtimeSinceStartup;
            _lastActionTimestamp = _stepStartTime;
            _stepIdleDurationMs = 0f;
            _lastResponseLatencyMs = 0f;
            _targetMissCount = 0;
            _repeatedClickCount = 0;
            _stepErrorCount = 0;
            _patientRequestedHelp = false;
            _patientRequestedRepeat = false;
            _patientRequestedBreak = false;
            _patientDismissedPrompt = false;
        }

        private void NotifyEventRecorded(string eventName)
        {
            var snapshot = BuildCurrentContextSnapshot();
            OnInteractionEventRecorded?.Invoke(eventName, snapshot);
        }
    }
}
