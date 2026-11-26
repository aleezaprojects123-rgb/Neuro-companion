// AssistanceActionDispatcher.cs
// NeuroCompanion — Phase 5.4: AS01–AS10 Strategy Dispatcher
//
// Singleton MonoBehaviour that:
//  1. Subscribes to AiBrainBridge.OnAssistanceEvaluated
//  2. Runs AssistanceResponseParser to validate and normalize the raw DTO
//  3. Guards against stale sessions, duplicate requests, and concurrent dispatch
//  4. Routes canonical StrategyID to CarlAssistanceAdapter capability methods
//  5. Raises OnActionDispatched for UI/logging consumers
//
// Key Safety Rules (enforced here):
//  - AS10 → parser already sets IsActionPermitted=false → dispatcher silently no-ops
//  - AS08 → parser always marks IsOptional=true → pause is never forced
//  - Stale session guard: SessionId / PatientId must match PatientTelemetryTracker
//  - Duplicate prevention: _lastDispatchedRequestId rejects exact repeat request IDs
//  - Patient dismissal: CancelCurrentAction() stops active Carl speech immediately
//  - _isDispatching flag: blocks concurrent strategy execution
//
// Routing is based exclusively on StrategyID.* constants from BackendModels.cs.
// No raw strategy strings are compared here.

using System;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// Singleton MonoBehaviour that receives AI assistance decisions and routes
    /// AS01–AS10 strategies to Carl's physical capability systems.
    /// Attach to: [MANAGERS]/Assistance_Dispatcher_Holder (or Interaction_Dispatcher_Anchor).
    /// </summary>
    public class AssistanceActionDispatcher : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------

        public static AssistanceActionDispatcher Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);
        }

        // -------------------------------------------------------------------------
        // Inspector Configuration
        // -------------------------------------------------------------------------

        [Header("Carl Adapter Reference")]
        [Tooltip("CarlAssistanceAdapter in scene. Auto-resolved at runtime if null.")]
        [SerializeField] private CarlAssistanceAdapter _carlAdapter;

        [Header("Dispatch Configuration")]
        [Tooltip("Enable verbose dispatch logging.")]
        [SerializeField] private bool _verboseLogging = true;

        [Tooltip("If true, stale-session responses are hard-rejected. Set false only for testing.")]
        [SerializeField] private bool _enforceSessionGuard = true;

        // -------------------------------------------------------------------------
        // Runtime State
        // -------------------------------------------------------------------------

        private bool _isDispatching = false;
        private string _lastDispatchedRequestId = null;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>
        /// Raised after a strategy has been successfully dispatched to Carl.
        /// Parameters: (result, capability).
        /// </summary>
        public event Action<AssistanceEvaluationResult, CarlActionCapability> OnActionDispatched;

        /// <summary>
        /// Raised when a response is received but rejected (stale, duplicate, invalid).
        /// Parameters: (result, reason).
        /// </summary>
        public event Action<AssistanceEvaluationResult, string> OnDispatchRejected;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Start()
        {
            // Resolve CarlAssistanceAdapter
            if (_carlAdapter == null)
                _carlAdapter = FindFirstObjectByType<CarlAssistanceAdapter>();

            if (_carlAdapter == null && _verboseLogging)
                Debug.LogWarning("[AssistanceActionDispatcher] CarlAssistanceAdapter not found in scene. " +
                                 "Dispatcher is registered but cannot execute Carl actions until adapter is present.");

            // Subscribe to AI Brain evaluation events
            if (AiBrainBridge.Instance != null)
            {
                AiBrainBridge.Instance.OnAssistanceEvaluated += HandleRawEvaluationResponse;
                AiBrainBridge.Instance.OnEvaluationFailed += HandleEvaluationFailure;

                if (_verboseLogging)
                    Debug.Log("[AssistanceActionDispatcher] Subscribed to AiBrainBridge events.");
            }
            else
            {
                Debug.LogWarning("[AssistanceActionDispatcher] AiBrainBridge instance not available at Start. " +
                                 "Ensure AiBrainBridge is initialized before AssistanceActionDispatcher.");
            }
        }

        private void OnDestroy()
        {
            if (AiBrainBridge.Instance != null)
            {
                AiBrainBridge.Instance.OnAssistanceEvaluated -= HandleRawEvaluationResponse;
                AiBrainBridge.Instance.OnEvaluationFailed -= HandleEvaluationFailure;
            }
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Dispatches a pre-parsed AssistanceEvaluationResult to Carl's systems.
        /// Can be called directly (e.g. from tests) bypassing the AiBrainBridge event path.
        /// </summary>
        public void DispatchResult(AssistanceEvaluationResult result)
        {
            if (result == null)
            {
                Debug.LogError("[AssistanceActionDispatcher] DispatchResult called with null result.");
                return;
            }

            // --- Guard 1: Valid result ---
            if (!result.IsValid)
            {
                string reason = $"Result is invalid: {result.ErrorMessage}";
                LogRejection(result, reason);
                OnDispatchRejected?.Invoke(result, reason);
                return;
            }

            // --- Guard 2: Action must be permitted by policy ---
            if (!result.IsActionPermitted)
            {
                string reason = $"Policy did not authorize Carl action (strategy={result.StrategyId}, status={result.DecisionStatus}).";
                if (_verboseLogging)
                    Debug.Log($"[AssistanceActionDispatcher] No action — {reason}");
                OnDispatchRejected?.Invoke(result, reason);
                return;
            }

            // --- Guard 3: Stale session check ---
            if (_enforceSessionGuard && PatientTelemetryTracker.Instance != null)
            {
                bool patientMismatch = !string.Equals(
                    result.PatientId, PatientTelemetryTracker.Instance.PatientId,
                    StringComparison.OrdinalIgnoreCase);

                bool sessionMismatch = !string.Equals(
                    result.SessionId, PatientTelemetryTracker.Instance.SessionId,
                    StringComparison.OrdinalIgnoreCase);

                if (patientMismatch || sessionMismatch)
                {
                    string reason = $"Stale response rejected: response patient={result.PatientId}/session={result.SessionId}, " +
                                    $"current patient={PatientTelemetryTracker.Instance.PatientId}/session={PatientTelemetryTracker.Instance.SessionId}.";
                    LogRejection(result, reason);
                    OnDispatchRejected?.Invoke(result, reason);
                    return;
                }
            }

            // --- Guard 4: Duplicate request_id prevention ---
            if (!string.IsNullOrEmpty(result.RequestId) &&
                string.Equals(result.RequestId, _lastDispatchedRequestId, StringComparison.Ordinal))
            {
                string reason = $"Duplicate request rejected: request_id={result.RequestId} was already dispatched.";
                LogRejection(result, reason);
                OnDispatchRejected?.Invoke(result, reason);
                return;
            }

            // --- Guard 5: Concurrency lock ---
            if (_isDispatching)
            {
                string reason = "Dispatcher is already executing a Carl action. Concurrent dispatch blocked.";
                LogRejection(result, reason);
                OnDispatchRejected?.Invoke(result, reason);
                return;
            }

            // --- Resolve CarlAssistanceAdapter (lazy) ---
            if (_carlAdapter == null)
                _carlAdapter = FindFirstObjectByType<CarlAssistanceAdapter>();

            if (_carlAdapter == null)
            {
                string reason = "CarlAssistanceAdapter not found in scene. Cannot execute Carl action.";
                Debug.LogError($"[AssistanceActionDispatcher] {reason}");
                OnDispatchRejected?.Invoke(result, reason);
                return;
            }

            // --- Route to strategy ---
            _isDispatching = true;
            _lastDispatchedRequestId = result.RequestId;

            if (_verboseLogging)
                Debug.Log($"[AssistanceActionDispatcher] Dispatching strategy={result.StrategyId}, " +
                          $"action={result.CanonicalActionType}, optional={result.IsOptional}, " +
                          $"request={result.RequestId}");

            var capability = RouteStrategy(result);

            _isDispatching = false;

            // Update prior strategy outcome in tracker
            if (PatientTelemetryTracker.Instance != null)
            {
                bool executedSuccessfully = capability == CarlActionCapability.Executed ||
                                           capability == CarlActionCapability.ExecutedWithFallback ||
                                           capability == CarlActionCapability.NoOpByDesign;
                PatientTelemetryTracker.Instance.SetPriorStrategyOutcome(result.StrategyId, executedSuccessfully);
            }

            OnActionDispatched?.Invoke(result, capability);
        }

        /// <summary>
        /// Called by PatientActivityController or UI when patient dismisses an active prompt.
        /// Immediately cancels any Carl speech or action in progress.
        /// </summary>
        public void CancelCurrentAction()
        {
            _isDispatching = false;

            if (_carlAdapter != null)
                _carlAdapter.CancelActiveAction();

            if (PatientTelemetryTracker.Instance != null)
                PatientTelemetryTracker.Instance.RecordPatientDismissedPrompt();

            if (_verboseLogging)
                Debug.Log("[AssistanceActionDispatcher] Current Carl action cancelled by patient dismissal.");
        }

        // -------------------------------------------------------------------------
        // Strategy Routing Table
        // -------------------------------------------------------------------------

        private CarlActionCapability RouteStrategy(AssistanceEvaluationResult result)
        {
            string strategy = result.StrategyId;

            // AS01 — Repeat Current Instruction
            if (strategy == StrategyID.AS01_REPEAT)
                return _carlAdapter.RepeatInstruction();

            // AS02 — Simplify Instruction
            if (strategy == StrategyID.AS02_SIMPLIFY)
                return _carlAdapter.SimplifyInstruction();

            // AS03 — Step-by-Step Guidance
            if (strategy == StrategyID.AS03_STEP_GUIDANCE)
            {
                int step = PatientTelemetryTracker.Instance?.CurrentStepIndex ?? 0;
                int total = PatientTelemetryTracker.Instance?.StepTotalSteps ?? 0;
                return _carlAdapter.AnnounceCurrentStep(step, total);
            }

            // AS04 — Visual Hint
            if (strategy == StrategyID.AS04_VISUAL_HINT)
                return _carlAdapter.ShowVisualHint();

            // AS05 — Demonstration (unavailable — no Animator on Carl)
            if (strategy == StrategyID.AS05_DEMONSTRATION)
            {
                Debug.LogWarning("[AssistanceActionDispatcher] AS05 Demonstration selected but Carl has no Animator. Logging limitation.");
                return _carlAdapter.ShowDemonstration();
            }

            // AS06 — Slower Speech
            if (strategy == StrategyID.AS06_SLOWER_SPEECH)
                return _carlAdapter.SlowSpeech();

            // AS07 — Additional Response Time (no Carl action — time extension only)
            if (strategy == StrategyID.AS07_RESPONSE_TIME)
                return _carlAdapter.AllowAdditionalTime();

            // AS08 — Optional Break (always optional per parser + dispatcher contract)
            if (strategy == StrategyID.AS08_BREAK_PAUSE)
            {
                // Guard: only offer if explicitly optional (should always be true from parser)
                if (!result.IsOptional)
                {
                    Debug.LogWarning("[AssistanceActionDispatcher] AS08 Break was not marked IsOptional — safety override applied.");
                }
                return _carlAdapter.OfferOptionalBreak();
            }

            // AS09 — Verbal Explanation
            if (strategy == StrategyID.AS09_VERBAL_EXPLANATION)
                return _carlAdapter.ExplainTask();

            // AS10 — Independent Completion
            // Parser sets IsActionPermitted=false for AS10; this branch is a safety net only.
            if (strategy == StrategyID.AS10_INDEPENDENT)
                return _carlAdapter.RemainSilent();

            // Unknown strategy — log and no-op
            Debug.LogWarning($"[AssistanceActionDispatcher] Unknown strategy ID '{strategy}' — no Carl action taken.");
            return CarlActionCapability.NotImplemented;
        }

        // -------------------------------------------------------------------------
        // Event Handlers (AiBrainBridge subscription)
        // -------------------------------------------------------------------------

        private void HandleRawEvaluationResponse(AssistanceEvaluateResponse rawDto)
        {
            if (rawDto == null)
            {
                Debug.LogWarning("[AssistanceActionDispatcher] Received null DTO from AiBrainBridge.");
                return;
            }

            // Phase 5.3: Parse and normalize via AssistanceResponseParser
            var result = AssistanceResponseParser.ValidateAndNormalize(rawDto);

            if (_verboseLogging)
            {
                Debug.Log($"[AssistanceActionDispatcher] Parsed evaluation: valid={result.IsValid}, " +
                          $"permitted={result.IsActionPermitted}, strategy={result.StrategyId ?? "none"}, " +
                          $"decision={result.DecisionStatus}");
            }

            DispatchResult(result);
        }

        private void HandleEvaluationFailure(string errorMessage)
        {
            if (_verboseLogging)
                Debug.LogWarning($"[AssistanceActionDispatcher] AI evaluation failed: {errorMessage}");
            // No Carl action on failure — system degrades gracefully.
        }

        // -------------------------------------------------------------------------
        // Internal Helpers
        // -------------------------------------------------------------------------

        private void LogRejection(AssistanceEvaluationResult result, string reason)
        {
            if (_verboseLogging)
                Debug.LogWarning($"[AssistanceActionDispatcher] Dispatch rejected: {reason} " +
                                 $"(request={result?.RequestId ?? "null"})");
        }
    }
}
