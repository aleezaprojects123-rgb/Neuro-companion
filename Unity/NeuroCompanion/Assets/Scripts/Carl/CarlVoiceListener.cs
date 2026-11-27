// ============================================================
// CarlVoiceListener.cs — Unity 6.3 LTS
// NeuroCompanion — Hands-Free Voice Recognition for Carl Fredricksen
//
// Allows patients to speak directly into their microphone (e.g. "hi carl how are you")
// without clicking or typing. Uses UnityEngine.Windows.Speech.KeywordRecognizer.
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace NeuroCompanion.Carl
{
    public class CarlVoiceListener : MonoBehaviour
    {
        [Header("Target Dialogue Controller")]
        public CarlDialogueController dialogueController;

        [Header("UI Voice Status Indicator")]
        public GameObject micIndicatorPanel;
        public Image micIconImage;
        public TMP_Text micStatusText;

        [Header("Voice Settings")]
        public bool startListeningOnAwake = true;
        [Tooltip("Minimum time in seconds between voice trigger processing to prevent echo.")]
        public float speechCooldown = 2.0f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer _recognizer;
#endif

        private float _lastHeardTime = -10f;
        private string[] _keywords = new string[]
        {
            "hi carl how are you",
            "carl how are you",
            "how are you carl",
            "how are you",
            "hi carl",
            "hello carl",
            "hey carl",
            "carl",
            "what are we doing today",
            "tell me about the garden",
            "who are you",
            "what should we plant",
            "thank you carl",
            "thanks carl",
            "help me carl"
        };

        private void Start()
        {
            if (dialogueController == null)
            {
                dialogueController = GetComponent<CarlDialogueController>();
                if (dialogueController == null)
                    dialogueController = FindFirstObjectByType<CarlDialogueController>();
            }

            if (startListeningOnAwake)
            {
                InitializeAndStartVoice();
            }
        }

        public void InitializeAndStartVoice()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!PhraseRecognitionSystem.isSupported)
            {
                Debug.LogWarning("[CarlVoiceListener] Windows PhraseRecognitionSystem is not supported on this platform.");
                UpdateUIStatus("🎙️ Voice not supported on this OS", new Color(0.8f, 0.4f, 0.4f));
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[CarlVoiceListener] No microphone detected.");
                UpdateUIStatus("🎙️ No microphone connected", new Color(0.8f, 0.4f, 0.4f));
                return;
            }

            try
            {
                if (_recognizer != null && _recognizer.IsRunning)
                {
                    _recognizer.Stop();
                    _recognizer.Dispose();
                }

                _recognizer = new KeywordRecognizer(_keywords, ConfidenceLevel.Low);
                _recognizer.OnPhraseRecognized += OnPhraseRecognized;
                _recognizer.Start();

                Debug.Log($"[CarlVoiceListener] Microphone active: '{Microphone.devices[0]}'. Listening for: {string.Join(", ", _keywords)}");
                UpdateUIStatus("🎙️ Listening... Say \"Hi Carl, how are you?\"", new Color(0.2f, 0.8f, 0.5f));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CarlVoiceListener] Failed to start voice recognizer: {ex.Message}");
                UpdateUIStatus("🎙️ Voice error: " + ex.Message, new Color(0.8f, 0.4f, 0.4f));
            }
#else
            Debug.LogWarning("[CarlVoiceListener] Windows Speech is only supported on Windows.");
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            if (Time.time - _lastHeardTime < speechCooldown)
                return;

            _lastHeardTime = Time.time;
            string spoken = args.text;
            Debug.Log($"[CarlVoiceListener] Recognized voice phrase: '{spoken}' (Confidence: {args.confidence})");

            // Update UI to show what was heard
            UpdateUIStatus($"🎙️ Heard: \"{spoken}\"", new Color(0.96f, 0.72f, 0.24f));

            // Deliver to Carl
            if (dialogueController != null)
            {
                dialogueController.SendMessageToCarl(spoken);
            }

            // Restore listening status prompt after 4 seconds
            StopAllCoroutines();
            StartCoroutine(RestoreStatusPromptRoutine());
        }
#endif

        private IEnumerator RestoreStatusPromptRoutine()
        {
            yield return new WaitForSeconds(4.0f);
            UpdateUIStatus("🎙️ Listening... Say \"Hi Carl, how are you?\"", new Color(0.2f, 0.8f, 0.5f));
        }

        private void UpdateUIStatus(string status, Color color)
        {
            if (micStatusText != null)
            {
                micStatusText.text = status;
                micStatusText.color = color;
            }
            if (micIconImage != null)
            {
                micIconImage.color = color;
            }
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (_recognizer != null)
            {
                if (_recognizer.IsRunning) _recognizer.Stop();
                _recognizer.Dispose();
                _recognizer = null;
            }
#endif
        }

        // Context menu for testing voice input directly in Editor without speaking
        [ContextMenu("Simulate Voice: 'Hi Carl how are you'")]
        public void SimulateVoiceGreeting()
        {
            Debug.Log("[CarlVoiceListener] Simulating voice input: 'hi carl how are you'");
            UpdateUIStatus("🎙️ Heard: \"hi carl how are you\"", new Color(0.96f, 0.72f, 0.24f));
            if (dialogueController != null)
            {
                dialogueController.SendMessageToCarl("hi carl how are you");
            }
        }
    }
}
