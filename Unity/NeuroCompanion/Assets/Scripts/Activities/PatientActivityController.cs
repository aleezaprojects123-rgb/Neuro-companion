// PatientActivityController.cs
// NeuroCompanion — Phase 5.2: Patient Interaction Telemetry Integration
//
// Base activity controller component that manages patient task flow and standardizes
// genuine event dispatch to the PatientTelemetryTracker and AiBrainBridge.
//
// Attach to activity anchors (e.g. OrientationRoom_Anchor, CognitiveGarden_Anchor).

using System;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// Base activity controller connecting in-game patient interactions to the telemetry pipeline.
    /// </summary>
    public class PatientActivityController : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Activity Metadata (Inspector Configurable)
        // -------------------------------------------------------------------------

        [Header("Activity Identification")]
        [Tooltip("Unique identifier for this activity.")]
        [SerializeField] private string _activityId = "OrientationRoom_ClockTask";

        [Tooltip("Therapy area classification.")]
        [SerializeField] private TherapyArea _therapyArea = TherapyArea.OrientationRoom;

        [Tooltip("Domain of cognitive interaction.")]
        [SerializeField] private string _taskDomain = "orientation";

        [Tooltip("Total number of steps in this activity.")]
        [Range(1, 20)]
        [SerializeField] private int _totalSteps = 3;

        [Header("Autonomous Evaluation Triggers")]
        [Tooltip("Automatically request AI assistance when patient presses Help.")]
        [SerializeField] private bool _autoEvaluateOnHelp = true;

        [Tooltip("Automatically request AI assistance when patient asks to repeat instructions.")]
        [SerializeField] private bool _autoEvaluateOnRepeat = true;

        [Tooltip("Automatically request AI assistance when consecutive error threshold is met (0 disables).")]
        [SerializeField] private int _consecutiveErrorTrigger = 2;

        [Tooltip("Inactivity threshold in seconds before triggering assistance check (0 disables).")]
        [SerializeField] private float _hesitationThresholdSeconds = 15.0f;

        [Header("State (Read Only)")]
        [SerializeField] private int _activeStep = 1;
        [SerializeField] private bool _isActive = false;

        public string ActivityId => _activityId;
        public TherapyArea TherapyArea => _therapyArea;
        public int ActiveStep => _activeStep;
        public int TotalSteps => _totalSteps;
        public bool IsActive => _isActive;

        // Events for UI or Scene Listeners
        public event Action<int> OnStepChanged;
        public event Action<bool> OnAttemptEvaluated;
        public event Action OnActivityFinished;

        private float _lastHesitationCheck = 0f;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Update()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            // Monitor genuine hesitation without blocking the game
            if (_hesitationThresholdSeconds > 0f)
            {
                float idleSeconds = PatientTelemetryTracker.Instance.IdleDurationMs / 1000f;
                if (idleSeconds >= _hesitationThresholdSeconds && (Time.realtimeSinceStartup - _lastHesitationCheck > _hesitationThresholdSeconds))
                {
                    _lastHesitationCheck = Time.realtimeSinceStartup;
                    Debug.Log($"[PatientActivityController] Hesitation detected ({idleSeconds:F1}s idle). Checking assistance evaluation.");
                    PatientTelemetryTracker.Instance.TriggerAssistanceEvaluation();
                }
            }
        }

        // -------------------------------------------------------------------------
        // Public Activity API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Starts the activity for a given patient and session.
        /// </summary>
        public void LaunchActivity(string patientId, string sessionId)
        {
            if (PatientTelemetryTracker.Instance == null)
            {
                Debug.LogError("[PatientActivityController] LaunchActivity failed: PatientTelemetryTracker not found.");
                return;
            }

            _activeStep = 1;
            _isActive = true;
            _lastHesitationCheck = Time.realtimeSinceStartup;

            PatientTelemetryTracker.Instance.StartActivity(
                patientId, sessionId, _activityId, _therapyArea, _taskDomain, _totalSteps);

            PatientTelemetryTracker.Instance.BeginStep(_activeStep);
            OnStepChanged?.Invoke(_activeStep);
        }

        /// <summary>
        /// Advances to the next step or completes the activity if final step was finished.
        /// </summary>
        public void AdvanceToNextStep()
        {
            if (!_isActive) return;

            if (_activeStep < _totalSteps)
            {
                _activeStep++;
                PatientTelemetryTracker.Instance.BeginStep(_activeStep);
                OnStepChanged?.Invoke(_activeStep);
            }
            else
            {
                FinishActivity(CompletionStatus.Independent);
            }
        }

        /// <summary>
        /// Records patient option selection or target attempt.
        /// </summary>
        public void SubmitAttempt(bool isCorrect, float? latencyMs = null, string substepId = null)
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            PatientTelemetryTracker.Instance.RecordAttempt(isCorrect, latencyMs, substepId);
            OnAttemptEvaluated?.Invoke(isCorrect);

            // Auto-trigger assistance evaluation if consecutive errors reach threshold
            if (!isCorrect && _consecutiveErrorTrigger > 0)
            {
                if (PatientTelemetryTracker.Instance.StepErrorCount >= _consecutiveErrorTrigger)
                {
                    Debug.Log($"[PatientActivityController] Step error count ({PatientTelemetryTracker.Instance.StepErrorCount}) met threshold ({_consecutiveErrorTrigger}). Triggering AI evaluation.");
                    PatientTelemetryTracker.Instance.TriggerAssistanceEvaluation();
                }
            }
        }

        /// <summary>
        /// Invoked when patient clicks the Help button.
        /// </summary>
        public void OnHelpButtonPressed()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            PatientTelemetryTracker.Instance.RecordPatientHelpRequest();

            if (_autoEvaluateOnHelp)
            {
                PatientTelemetryTracker.Instance.TriggerAssistanceEvaluation();
            }
        }

        /// <summary>
        /// Invoked when patient clicks the Repeat Instruction button.
        /// </summary>
        public void OnRepeatInstructionButtonPressed()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            PatientTelemetryTracker.Instance.RecordPatientRepeatRequest();

            if (_autoEvaluateOnRepeat)
            {
                PatientTelemetryTracker.Instance.TriggerAssistanceEvaluation();
            }
        }

        /// <summary>
        /// Invoked when patient clicks Take a Break.
        /// </summary>
        public void OnBreakButtonPressed()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            PatientTelemetryTracker.Instance.RecordPatientBreakRequest();
            PatientTelemetryTracker.Instance.TriggerAssistanceEvaluation();
        }

        /// <summary>
        /// Invoked when patient dismisses a prompt or hint.
        /// </summary>
        public void OnDismissPromptButtonPressed()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            PatientTelemetryTracker.Instance.RecordPatientDismissedPrompt();
        }

        /// <summary>
        /// Pauses the activity.
        /// </summary>
        public void OnPauseButtonPressed()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            PatientTelemetryTracker.Instance.PauseActivity();
        }

        /// <summary>
        /// Resumes the paused activity.
        /// </summary>
        public void OnResumeButtonPressed()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            PatientTelemetryTracker.Instance.ResumeActivity();
        }

        /// <summary>
        /// Skips the activity (patient autonomy guarantee).
        /// </summary>
        public void OnSkipButtonPressed()
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            _isActive = false;
            PatientTelemetryTracker.Instance.SkipActivity();
            OnActivityFinished?.Invoke();
        }

        /// <summary>
        /// Completes the activity.
        /// </summary>
        public void FinishActivity(string completionStatus = CompletionStatus.Independent)
        {
            if (!_isActive || PatientTelemetryTracker.Instance == null) return;

            _isActive = false;
            PatientTelemetryTracker.Instance.CompleteActivity(completionStatus);
            OnActivityFinished?.Invoke();
        }
    }
}
