// AiBrainBridge.cs
// NeuroCompanion — Phase 5.1: Unity <-> AI Communication Bridge
//
// High-level communication bridge connecting Unity game and activity systems to
// the Phase 4 FastAPI Backend (Assistance Runtime at POST /api/v1/assistance/evaluate).
//
// Architectural Principles:
//  - Sits cleanly atop BackendClient.cs without creating duplicate networking stacks.
//  - Fully asynchronous and non-blocking using Unity coroutines (zero main-thread stalls).
//  - Completely decoupled from Carl's visual presentation, audio playback, and lip-sync.
//  - Manages request timeouts, safe cancellation, error propagation, and concurrency guards.
//  - Dispatches strongly typed events for downstream strategy interpretation (Phases 5.3–5.4).

using System;
using System.Collections;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// High-level communication bridge connecting Unity to the Phase 4 AI assistance engine.
    /// Attach to [MANAGERS]/Ai_Brain_Bridge_Holder in the Unity scene.
    /// </summary>
    public class AiBrainBridge : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------

        public static AiBrainBridge Instance { get; private set; }

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
        // Configuration (Inspector-editable)
        // -------------------------------------------------------------------------

        [Header("Bridge Configuration")]
        [Tooltip("Maximum timeout in seconds for an assistance evaluation call.")]
        [SerializeField] private float _evaluationTimeoutSeconds = 8.0f;

        [Tooltip("Enable detailed bridge diagnostics in the Unity Console.")]
        [SerializeField] private bool _verboseLogging = true;

        // -------------------------------------------------------------------------
        // Observable State
        // -------------------------------------------------------------------------

        [Header("Live State (Read Only)")]
        [SerializeField] private bool _isEvaluating = false;
        [SerializeField] private string _lastDecisionStatus = "None";
        [SerializeField] private string _lastStrategyId = "None";

        /// <summary>True while an evaluation request is actively in-flight.</summary>
        public bool IsEvaluating => _isEvaluating;

        /// <summary>Last received policy decision status (e.g. APPROVED, SUPPRESSED, NEUTRAL).</summary>
        public string LastDecisionStatus => _lastDecisionStatus;

        /// <summary>Last received strategy ID (e.g. AS04_VISUAL_HINT, AS01_REPEAT).</summary>
        public string LastStrategyId => _lastStrategyId;

        /// <summary>Current backend connection status from BackendClient.</summary>
        public BackendConnectionStatus ConnectionStatus =>
            BackendClient.Instance != null ? BackendClient.Instance.ConnectionStatus : BackendConnectionStatus.Unknown;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>Raised when a valid assistance evaluation response is received from the AI Brain.</summary>
        public event Action<AssistanceEvaluateResponse> OnAssistanceEvaluated;

        /// <summary>Raised when an evaluation request fails (network error, timeout, HTTP error).</summary>
        public event Action<string> OnEvaluationFailed;

        /// <summary>Raised when the in-flight evaluation state transitions between idle and active.</summary>
        public event Action<bool> OnEvaluatingStateChanged;

        // -------------------------------------------------------------------------
        // Coroutine Management & Safe Cancellation
        // -------------------------------------------------------------------------

        private Coroutine _activeEvaluationCoroutine;

        private void OnDisable()
        {
            CancelPendingEvaluation();
        }

        private void OnDestroy()
        {
            CancelPendingEvaluation();
        }

        /// <summary>
        /// Safely cancels any active in-flight assistance evaluation request.
        /// </summary>
        public void CancelPendingEvaluation()
        {
            if (_activeEvaluationCoroutine != null)
            {
                StopCoroutine(_activeEvaluationCoroutine);
                _activeEvaluationCoroutine = null;
            }

            if (_isEvaluating)
            {
                SetEvaluating(false);
                if (_verboseLogging)
                    Debug.Log("[AiBrainBridge] In-flight assistance evaluation safely cancelled.");
            }
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Asynchronously verifies backend availability and ML pipeline health.
        /// Non-blocking callback: (isHealthy, response).
        /// </summary>
        public void CheckBackendHealth(Action<bool, HealthResponse> callback)
        {
            if (BackendClient.Instance == null)
            {
                Debug.LogWarning("[AiBrainBridge] BackendClient instance is not available.");
                callback?.Invoke(false, null);
                return;
            }

            BackendClient.Instance.GetHealth((success, response) =>
            {
                if (_verboseLogging)
                {
                    if (success && response != null)
                        Debug.Log($"[AiBrainBridge] Health check OK: status={response.status}, model_loaded={response.model_loaded}");
                    else
                        Debug.LogWarning("[AiBrainBridge] Health check failed: Backend unreachable or offline.");
                }
                callback?.Invoke(success, response);
            });
        }

        /// <summary>
        /// Submits real-time patient interaction telemetry to the Phase 4 AI assistance engine.
        /// Guaranteed non-blocking: executes via Unity coroutines, preserving Carl's speech and lip-sync.
        /// </summary>
        public void RequestAssistanceEvaluation(
            AssistanceEvaluateRequest request,
            Action<BackendResult<AssistanceEvaluateResponse>> onComplete = null)
        {
            if (_isEvaluating)
            {
                if (_verboseLogging)
                    Debug.LogWarning("[AiBrainBridge] Assistance evaluation already in progress. Rejecting concurrent duplicate request.");
                return;
            }

            if (BackendClient.Instance == null)
            {
                string error = "BackendClient instance not found in scene. Ensure BackendClient is attached to a GameObject.";
                Debug.LogError($"[AiBrainBridge] {error}");
                OnEvaluationFailed?.Invoke(error);
                onComplete?.Invoke(new BackendResult<AssistanceEvaluateResponse>
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ErrorMessage = error
                });
                return;
            }

            _activeEvaluationCoroutine = StartCoroutine(EvaluationCoroutine(request, onComplete));
        }

        // -------------------------------------------------------------------------
        // Internal Coroutine Execution
        // -------------------------------------------------------------------------

        private IEnumerator EvaluationCoroutine(
            AssistanceEvaluateRequest request,
            Action<BackendResult<AssistanceEvaluateResponse>> onComplete)
        {
            SetEvaluating(true);

            bool completed = false;
            BackendResult<AssistanceEvaluateResponse> finalResult = default;

            if (_verboseLogging)
            {
                string pId = request?.context?.patient_id ?? "unknown";
                string sId = request?.context?.session_id ?? "unknown";
                int step = request?.context?.step_index ?? 0;
                Debug.Log($"[AiBrainBridge] Dispatching evaluation request: patient={pId}, session={sId}, step={step}");
            }

            // Invoke through BackendClient non-blocking coroutine
            BackendClient.Instance.PostEvaluateAssistance(request, result =>
            {
                finalResult = result;
                completed = true;
            });

            // Await response while enforcing timeout protection
            float elapsed = 0f;
            while (!completed && elapsed < _evaluationTimeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!completed)
            {
                string timeoutMsg = $"Assistance evaluation timed out after {_evaluationTimeoutSeconds:F1}s.";
                Debug.LogWarning($"[AiBrainBridge] {timeoutMsg}");
                finalResult = new BackendResult<AssistanceEvaluateResponse>
                {
                    IsSuccess = false,
                    StatusCode = 408,
                    ErrorMessage = timeoutMsg
                };
            }

            SetEvaluating(false);
            _activeEvaluationCoroutine = null;

            // Handle and dispatch outcomes
            if (finalResult.IsSuccess && finalResult.Data != null)
            {
                _lastDecisionStatus = finalResult.Data.decision_status ?? "UNKNOWN";
                _lastStrategyId = finalResult.Data.selected_strategy_id ?? "NONE";

                if (_verboseLogging)
                {
                    Debug.Log($"[AiBrainBridge] Evaluation success: status={_lastDecisionStatus}, strategy={_lastStrategyId}, requires_carl_action={finalResult.Data.requires_carl_action}");
                }

                OnAssistanceEvaluated?.Invoke(finalResult.Data);
            }
            else
            {
                string err = finalResult.ErrorMessage ?? "Unknown evaluation failure.";
                if (_verboseLogging)
                {
                    Debug.LogWarning($"[AiBrainBridge] Evaluation failed: {err} (HTTP {finalResult.StatusCode})");
                }
                OnEvaluationFailed?.Invoke(err);
            }

            onComplete?.Invoke(finalResult);
        }

        private void SetEvaluating(bool evaluating)
        {
            if (_isEvaluating == evaluating) return;
            _isEvaluating = evaluating;
            OnEvaluatingStateChanged?.Invoke(_isEvaluating);
        }
    }
}
