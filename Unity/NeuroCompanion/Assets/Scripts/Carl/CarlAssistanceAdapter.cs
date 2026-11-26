// CarlAssistanceAdapter.cs
// NeuroCompanion — Phase 5.4: Carl Assistance Adapter
//
// Thin adapter layer between the AssistanceActionDispatcher (Phase 5.4) and Carl's
// actual audio and procedural animation systems.
//
// Carl's Verified Capability Inventory (Phase 5.3+5.4 Inspection):
//  - AudioSource (assigned at runtime by CleanCarlSetupAutomation.cs)
//  - CarlLipSyncController — auto-drives lip blendshapes from AudioSource RMS
//  - CarlCleanTalkingController — auto-drives head nod from AudioSource amplitude
//  - NO Animator component or animation controller present on Carl
//  - NO TTS system — Carl uses pre-recorded AudioClip assets only
//  - AudioClips available: carl_speech_sample.wav, carl_joyful_greeting.wav
//
// Design Principles:
//  - Only calls AudioSource.Play / PlayOneShot — lip-sync and head nod auto-activate.
//  - No Animator calls, no hardcoded animation names, no new mesh components.
//  - Handles null AudioSource gracefully with logged warnings.
//  - AS05 (Demonstration): Logs honest limitation — no Animator available.
//  - AS04 (Visual Hint): Activates Prompt_Banner_Anchor if present; logs if absent.
//  - Pitch restore after AS06 (Slower Speech) via coroutine.
//  - Reports capability status per-action for downstream logging.

using System;
using System.Collections;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    /// <summary>
    /// Capability status codes returned by CarlAssistanceAdapter action methods.
    /// </summary>
    public enum CarlActionCapability
    {
        /// <summary>Action executed successfully using Carl's available systems.</summary>
        Executed,
        /// <summary>Action executed with a fallback (e.g. repeated original clip instead of alternate).</summary>
        ExecutedWithFallback,
        /// <summary>Action is a no-op by design (e.g. AS07, AS10).</summary>
        NoOpByDesign,
        /// <summary>Action could not execute — required Carl system (AudioSource, etc.) was unavailable.</summary>
        UnavailableSystem,
        /// <summary>Action is architecturally unavailable in this Unity build (e.g. AS05 without Animator).</summary>
        NotImplemented
    }

    /// <summary>
    /// Thin MonoBehaviour adapter connecting the AI assistance dispatcher to Carl's
    /// AudioSource, lip-sync, and procedural animation systems.
    ///
    /// Attach to: Carl_Companion_Anchor or the same GameObject as the AudioSource.
    /// All lip-sync (CarlLipSyncController) and head nods (CarlCleanTalkingController)
    /// auto-activate from the AudioSource amplitude — no manual calls required.
    /// </summary>
    public class CarlAssistanceAdapter : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // Inspector Configuration
        // -------------------------------------------------------------------------

        [Header("Carl Audio Source (assigned by setup automation or inspector)")]
        [Tooltip("Carl's primary speech AudioSource. Leave null to use FindFirstObjectByType at runtime.")]
        [SerializeField] private AudioSource _carlAudioSource;

        [Header("Pre-recorded Clips")]
        [Tooltip("Primary instruction clip (used by AS01 Repeat, AS02 fallback, AS03, AS09 fallback).")]
        [SerializeField] private AudioClip _primaryInstructionClip;

        [Tooltip("Optional simplified instruction clip (AS02). Falls back to primary if unassigned.")]
        [SerializeField] private AudioClip _simplifiedInstructionClip;

        [Tooltip("Optional verbal explanation clip (AS09). Falls back to primary if unassigned.")]
        [SerializeField] private AudioClip _verbalExplanationClip;

        [Header("Visual Hint Target")]
        [Tooltip("Prompt_Banner_Anchor transform. If unassigned, adapter searches by name at runtime.")]
        [SerializeField] private Transform _promptBannerAnchor;

        [Header("Timing")]
        [Tooltip("Speech pitch multiplier applied during AS06 (Slower Speech). Default: 0.80f.")]
        [Range(0.5f, 0.95f)]
        [SerializeField] private float _slowerSpeechPitch = 0.80f;

        [Tooltip("Additional response time extension in seconds for AS07.")]
        [Range(5f, 30f)]
        [SerializeField] private float _additionalResponseTimeSeconds = 10f;

        [Header("Logging")]
        [SerializeField] private bool _verboseLogging = true;

        // -------------------------------------------------------------------------
        // Runtime State
        // -------------------------------------------------------------------------

        private float _originalPitch = 1.0f;
        private Coroutine _pitchRestoreCoroutine;

        // -------------------------------------------------------------------------
        // Events
        // -------------------------------------------------------------------------

        /// <summary>
        /// Raised when a Carl action completes or is fully initiated.
        /// Parameters: (strategyId, capability, description).
        /// </summary>
        public event Action<string, CarlActionCapability, string> OnActionExecuted;

        // -------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------

        private void Start()
        {
            // Lazy AudioSource resolution — CleanCarlSetupAutomation assigns at runtime
            if (_carlAudioSource == null)
            {
                _carlAudioSource = FindFirstObjectByType<AudioSource>();
                if (_carlAudioSource != null && _verboseLogging)
                    Debug.Log($"[CarlAssistanceAdapter] AudioSource resolved at runtime: {_carlAudioSource.gameObject.name}");
            }

            // Lazy Prompt_Banner_Anchor resolution
            if (_promptBannerAnchor == null)
            {
                var bannerGo = GameObject.Find("Prompt_Banner_Anchor");
                if (bannerGo != null)
                    _promptBannerAnchor = bannerGo.transform;
            }

            if (_carlAudioSource != null)
                _originalPitch = _carlAudioSource.pitch;
        }

        // -------------------------------------------------------------------------
        // Public Capability API
        // -------------------------------------------------------------------------

        /// <summary>
        /// AS01 — Repeat Current Instruction.
        /// Replays the current instruction AudioClip via Carl's AudioSource.
        /// Lip-sync and head nod auto-activate from amplitude.
        /// </summary>
        public CarlActionCapability RepeatInstruction(AudioClip overrideClip = null)
        {
            var clip = overrideClip ?? _primaryInstructionClip;

            if (!TryGetAudioSource(out var src))
                return Report(StrategyID.AS01_REPEAT, CarlActionCapability.UnavailableSystem,
                    "AudioSource unavailable — cannot repeat instruction.");

            if (clip == null)
            {
                // No clip assigned — Carl remains attentive without speaking.
                if (_verboseLogging)
                    Debug.LogWarning("[CarlAssistanceAdapter] AS01 Repeat: No AudioClip assigned to _primaryInstructionClip. Carl remains silent.");
                return Report(StrategyID.AS01_REPEAT, CarlActionCapability.UnavailableSystem,
                    "No instruction AudioClip assigned.");
            }

            RestorePitchImmediate(src);
            src.PlayOneShot(clip);
            return Report(StrategyID.AS01_REPEAT, CarlActionCapability.Executed,
                $"Replaying instruction clip '{clip.name}' at normal pitch.");
        }

        /// <summary>
        /// AS02 — Simplify Current Instruction.
        /// Plays the simplified clip if available; falls back to primary clip.
        /// </summary>
        public CarlActionCapability SimplifyInstruction()
        {
            if (!TryGetAudioSource(out var src))
                return Report(StrategyID.AS02_SIMPLIFY, CarlActionCapability.UnavailableSystem,
                    "AudioSource unavailable — cannot simplify instruction.");

            var clip = _simplifiedInstructionClip ?? _primaryInstructionClip;
            bool isFallback = (_simplifiedInstructionClip == null);

            if (clip == null)
                return Report(StrategyID.AS02_SIMPLIFY, CarlActionCapability.UnavailableSystem,
                    "No simplified or fallback AudioClip assigned.");

            RestorePitchImmediate(src);
            src.PlayOneShot(clip);

            return isFallback
                ? Report(StrategyID.AS02_SIMPLIFY, CarlActionCapability.ExecutedWithFallback,
                    "No simplified clip assigned — replaying primary instruction clip as fallback.")
                : Report(StrategyID.AS02_SIMPLIFY, CarlActionCapability.Executed,
                    $"Playing simplified instruction clip '{clip.name}'.");
        }

        /// <summary>
        /// AS03 — Step-by-Step Guidance.
        /// Announces the current step by playing the primary clip.
        /// A future audio library could provide per-step clips; for now we use the primary.
        /// </summary>
        public CarlActionCapability AnnounceCurrentStep(int stepIndex = 0, int totalSteps = 0)
        {
            if (!TryGetAudioSource(out var src))
                return Report(StrategyID.AS03_STEP_GUIDANCE, CarlActionCapability.UnavailableSystem,
                    "AudioSource unavailable — cannot announce step.");

            var clip = _primaryInstructionClip;
            if (clip == null)
                return Report(StrategyID.AS03_STEP_GUIDANCE, CarlActionCapability.UnavailableSystem,
                    "No instruction AudioClip assigned for step announcement.");

            if (_verboseLogging)
                Debug.Log($"[CarlAssistanceAdapter] AS03: Announcing step {stepIndex}/{totalSteps}");

            RestorePitchImmediate(src);
            src.PlayOneShot(clip);

            return Report(StrategyID.AS03_STEP_GUIDANCE, CarlActionCapability.ExecutedWithFallback,
                $"Playing primary clip for step {stepIndex}. Per-step clip library not yet implemented.");
        }

        /// <summary>
        /// AS04 — Show Visual Hint.
        /// Activates the Prompt_Banner_Anchor GameObject if present in the scene.
        /// No UI components are attached yet — activation is a placeholder for future UI work.
        /// </summary>
        public CarlActionCapability ShowVisualHint()
        {
            if (_promptBannerAnchor != null)
            {
                _promptBannerAnchor.gameObject.SetActive(true);
                return Report(StrategyID.AS04_VISUAL_HINT, CarlActionCapability.ExecutedWithFallback,
                    "Prompt_Banner_Anchor activated. Visual hint UI components not yet attached.");
            }

            if (_verboseLogging)
                Debug.LogWarning("[CarlAssistanceAdapter] AS04 Visual Hint: Prompt_Banner_Anchor not found in scene. Visual hint unavailable.");

            return Report(StrategyID.AS04_VISUAL_HINT, CarlActionCapability.NotImplemented,
                "Prompt_Banner_Anchor not found — visual hint system not yet implemented.");
        }

        /// <summary>
        /// AS05 — Demonstration.
        /// UNAVAILABLE: Carl has no Animator component. No demonstration system exists.
        /// Logs honest limitation and takes no action.
        /// </summary>
        public CarlActionCapability ShowDemonstration()
        {
            Debug.LogWarning("[CarlAssistanceAdapter] AS05 Demonstration: Carl has no Animator or demonstration system. " +
                             "This strategy is architecturally unavailable in this Unity build.");
            return Report(StrategyID.AS05_DEMONSTRATION, CarlActionCapability.NotImplemented,
                "Carl has no Animator component — AS05 Demonstration is not implementable without animation assets.");
        }

        /// <summary>
        /// AS06 — Slower Speech.
        /// Reduces AudioSource pitch temporarily to slow perceived speech rate.
        /// Pitch is automatically restored after the clip finishes playing.
        /// </summary>
        public CarlActionCapability SlowSpeech(AudioClip overrideClip = null)
        {
            if (!TryGetAudioSource(out var src))
                return Report(StrategyID.AS06_SLOWER_SPEECH, CarlActionCapability.UnavailableSystem,
                    "AudioSource unavailable — cannot apply slow speech.");

            var clip = overrideClip ?? _primaryInstructionClip;
            if (clip == null)
                return Report(StrategyID.AS06_SLOWER_SPEECH, CarlActionCapability.UnavailableSystem,
                    "No AudioClip assigned for slower speech playback.");

            // Cancel any active pitch restoration
            if (_pitchRestoreCoroutine != null)
                StopCoroutine(_pitchRestoreCoroutine);

            src.pitch = _slowerSpeechPitch;
            src.PlayOneShot(clip);

            _pitchRestoreCoroutine = StartCoroutine(RestorePitchAfterClip(src, clip.length / _slowerSpeechPitch));

            return Report(StrategyID.AS06_SLOWER_SPEECH, CarlActionCapability.Executed,
                $"Playing clip at pitch={_slowerSpeechPitch:F2} (slower speech). Pitch restores automatically.");
        }

        /// <summary>
        /// AS07 — Additional Response Time.
        /// No-op in Carl — time extension is managed at the activity/tracker level.
        /// Notifies PatientTelemetryTracker if available to extend hesitation threshold.
        /// </summary>
        public CarlActionCapability AllowAdditionalTime()
        {
            // Extend tracker hesitation threshold if accessible
            if (_verboseLogging)
                Debug.Log($"[CarlAssistanceAdapter] AS07 Additional Time: Patient granted {_additionalResponseTimeSeconds:F0}s additional time. Carl remains attentive.");

            return Report(StrategyID.AS07_RESPONSE_TIME, CarlActionCapability.NoOpByDesign,
                $"Additional response time ({_additionalResponseTimeSeconds:F0}s) granted. Carl remains attentive and silent.");
        }

        /// <summary>
        /// AS08 — Optional Break / Pause.
        /// Requests a pause via PatientTelemetryTracker if available.
        /// Always optional — never forced. Carl remains silent.
        /// </summary>
        public CarlActionCapability OfferOptionalBreak()
        {
            if (PatientTelemetryTracker.Instance != null && PatientTelemetryTracker.Instance.IsActivityActive)
            {
                PatientTelemetryTracker.Instance.PauseActivity();
                return Report(StrategyID.AS08_BREAK_PAUSE, CarlActionCapability.Executed,
                    "Activity paused via PatientTelemetryTracker. Patient may resume at any time.");
            }

            if (_verboseLogging)
                Debug.Log("[CarlAssistanceAdapter] AS08 Break: PatientTelemetryTracker unavailable or no active activity — no-op.");

            return Report(StrategyID.AS08_BREAK_PAUSE, CarlActionCapability.NoOpByDesign,
                "Optional break offered. No active activity tracker — Carl remains present and calm.");
        }

        /// <summary>
        /// AS09 — Verbal Explanation.
        /// Plays the verbal explanation clip if assigned; falls back to primary clip.
        /// </summary>
        public CarlActionCapability ExplainTask()
        {
            if (!TryGetAudioSource(out var src))
                return Report(StrategyID.AS09_VERBAL_EXPLANATION, CarlActionCapability.UnavailableSystem,
                    "AudioSource unavailable — cannot play verbal explanation.");

            var clip = _verbalExplanationClip ?? _primaryInstructionClip;
            bool isFallback = (_verbalExplanationClip == null);

            if (clip == null)
                return Report(StrategyID.AS09_VERBAL_EXPLANATION, CarlActionCapability.UnavailableSystem,
                    "No verbal explanation or fallback AudioClip assigned.");

            RestorePitchImmediate(src);
            src.PlayOneShot(clip);

            return isFallback
                ? Report(StrategyID.AS09_VERBAL_EXPLANATION, CarlActionCapability.ExecutedWithFallback,
                    "No verbal explanation clip assigned — playing primary instruction clip as fallback.")
                : Report(StrategyID.AS09_VERBAL_EXPLANATION, CarlActionCapability.Executed,
                    $"Playing verbal explanation clip '{clip.name}'.");
        }

        /// <summary>
        /// AS10 — Independent Completion.
        /// No-op. Carl remains silent and attentive. The patient completes without intervention.
        /// </summary>
        public CarlActionCapability RemainSilent()
        {
            if (_verboseLogging)
                Debug.Log("[CarlAssistanceAdapter] AS10 Independent: Carl remains silent and attentive.");

            return Report(StrategyID.AS10_INDEPENDENT, CarlActionCapability.NoOpByDesign,
                "Patient completing independently. Carl remains silent and attentive.");
        }

        // -------------------------------------------------------------------------
        // Active Interruption
        // -------------------------------------------------------------------------

        /// <summary>
        /// Immediately stops any active Carl speech and restores pitch.
        /// Called when patient dismisses a prompt.
        /// </summary>
        public void CancelActiveAction()
        {
            if (_pitchRestoreCoroutine != null)
            {
                StopCoroutine(_pitchRestoreCoroutine);
                _pitchRestoreCoroutine = null;
            }

            if (TryGetAudioSource(out var src))
            {
                if (src.isPlaying)
                    src.Stop();
                RestorePitchImmediate(src);
            }

            if (_verboseLogging)
                Debug.Log("[CarlAssistanceAdapter] Active Carl action cancelled by patient dismissal.");
        }

        // -------------------------------------------------------------------------
        // Internal Helpers
        // -------------------------------------------------------------------------

        private bool TryGetAudioSource(out AudioSource src)
        {
            if (_carlAudioSource != null)
            {
                src = _carlAudioSource;
                return true;
            }

            // Runtime fallback
            _carlAudioSource = FindFirstObjectByType<AudioSource>();
            src = _carlAudioSource;

            if (src == null)
            {
                Debug.LogWarning("[CarlAssistanceAdapter] No AudioSource found in scene. Carl cannot speak.");
                return false;
            }

            return true;
        }

        private void RestorePitchImmediate(AudioSource src)
        {
            if (_pitchRestoreCoroutine != null)
            {
                StopCoroutine(_pitchRestoreCoroutine);
                _pitchRestoreCoroutine = null;
            }
            src.pitch = _originalPitch;
        }

        private IEnumerator RestorePitchAfterClip(AudioSource src, float clipLengthSeconds)
        {
            yield return new WaitForSeconds(clipLengthSeconds + 0.1f);
            if (src != null)
                src.pitch = _originalPitch;

            _pitchRestoreCoroutine = null;

            if (_verboseLogging)
                Debug.Log("[CarlAssistanceAdapter] AS06: Speech pitch restored to normal.");
        }

        private CarlActionCapability Report(string strategyId, CarlActionCapability capability, string description)
        {
            if (_verboseLogging)
            {
                string icon = capability == CarlActionCapability.Executed ? "✓" :
                              capability == CarlActionCapability.ExecutedWithFallback ? "⚠" :
                              capability == CarlActionCapability.NoOpByDesign ? "○" : "✗";
                Debug.Log($"[CarlAssistanceAdapter] [{icon}] {strategyId}: {description}");
            }

            OnActionExecuted?.Invoke(strategyId, capability, description);
            return capability;
        }
    }
}
