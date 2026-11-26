// ModeManager.cs
// NeuroCompanion — Phase 3, Step 4: Backend Integration Layer
//
// Foundation for Patient Mode and Caregiver Mode switching.
// This component:
//   - Maintains the currently active application mode.
//   - Raises events when the mode changes so other systems can react.
//   - Does NOT implement the full Patient or Caregiver UI (future steps).
//   - Does NOT make support-tier or AI decisions.
//
// Attach to [MANAGERS]/Mode_Manager_Holder.

using System;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// The two operational modes of the NeuroCompanion application.
    /// </summary>
    public enum ApplicationMode
    {
        /// <summary>
        /// No mode has been selected yet (initial state at application start).
        /// </summary>
        None,

        /// <summary>
        /// Patient-facing mode: therapy activities, Carl companion, accessibility-first UI.
        /// </summary>
        PatientMode,

        /// <summary>
        /// Caregiver-facing mode: profile management, tier review, session oversight.
        /// Requires caregiver authentication (planned future feature).
        /// </summary>
        CaregiverMode
    }

    /// <summary>
    /// MonoBehaviour. Attach to [MANAGERS]/Mode_Manager_Holder.
    /// Provides the application-mode foundation for all future Patient and Caregiver systems.
    /// </summary>
    public class ModeManager : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Singleton
        // -------------------------------------------------------------------------

        public static ModeManager Instance { get; private set; }

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
        // State
        // -------------------------------------------------------------------------

        [Header("Mode Manager")]
        [Tooltip("Mode active at startup. Leave as None for the launcher to decide.")]
        [SerializeField] private ApplicationMode _initialMode = ApplicationMode.None;

        /// <summary>The currently active application mode.</summary>
        public ApplicationMode CurrentMode { get; private set; } = ApplicationMode.None;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>
        /// Raised when the application mode changes.
        /// Parameters: (previousMode, newMode)
        /// </summary>
        public event Action<ApplicationMode, ApplicationMode> OnModeChanged;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Start()
        {
            if (_initialMode != ApplicationMode.None)
                SwitchTo(_initialMode);
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>
        /// Switch to the specified application mode.
        /// No-op if the mode is already active.
        /// </summary>
        public void SwitchTo(ApplicationMode newMode)
        {
            if (newMode == CurrentMode)
            {
                Debug.Log($"[ModeManager] Already in {newMode} — no change.");
                return;
            }

            ApplicationMode previous = CurrentMode;
            CurrentMode = newMode;

            Debug.Log($"[ModeManager] Mode: {previous} → {newMode}");
            OnModeChanged?.Invoke(previous, newMode);
        }

        /// <summary>Switch to Patient Mode.</summary>
        public void EnterPatientMode()  => SwitchTo(ApplicationMode.PatientMode);

        /// <summary>Switch to Caregiver Mode.</summary>
        public void EnterCaregiverMode() => SwitchTo(ApplicationMode.CaregiverMode);

        // -------------------------------------------------------------------------
        // Convenience queries
        // -------------------------------------------------------------------------

        public bool IsPatientMode   => CurrentMode == ApplicationMode.PatientMode;
        public bool IsCaregiverMode => CurrentMode == ApplicationMode.CaregiverMode;
        public bool IsInitialized   => CurrentMode != ApplicationMode.None;
    }
}
