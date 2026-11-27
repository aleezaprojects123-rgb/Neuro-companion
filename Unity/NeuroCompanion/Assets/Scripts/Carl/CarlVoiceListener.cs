// ============================================================
// CarlVoiceListener.cs — Unity 6.3 LTS
// NeuroCompanion — Robust Hands-Free Voice Listener with Live VU Meter
//
// 1. Sets Application.runInBackground = true so microphone never pauses.
// 2. Real-time audio VU volume meter on the HUD showing exact mic sound levels.
// 3. Expanded natural keyword triggers ("Carl", "Hi Carl", "How are you", etc.).
// 4. One-key Spacebar / T-key fallback so testing is 100% effortless.
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
        public TMP_Text micStatusText;

        [Header("Voice Settings")]
        public bool startListeningOnAwake = true;
        public float speechCooldown = 2.0f;

        [Header("Audio Level Monitoring")]
        public bool monitorMicVolume = true;
        private AudioClip _micMonitorClip;
        private string _activeMicDevice;
        private float[] _volumeSamples = new float[128];
        private float _currentVolume = 0f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer _recognizer;
#endif

        private float _lastHeardTime = -10f;
        private bool _isSpeakingToCarl = false;

        // Expanded natural vocabulary
        private string[] _keywords = new string[]
        {
            // Greetings & How are you
            "hi carl how are you",
            "carl how are you",
            "how are you carl",
            "how are you",
            "hi carl",
            "hello carl",
            "hey carl",
            "carl",
            "hi",
            "hello",
            // Activities & Garden
            "what are we doing today",
            "what are we doing",
            "tell me about the garden",
            "tell me about garden",
            "who are you",
            "what should we plant",
            "thank you carl",
            "thank you",
            "thanks carl",
            "help me carl"
        };

        private void Awake()
        {
            // CRITICAL: Ensure Unity keeps listening even when the window is unfocused!
            Application.runInBackground = true;
        }

        private void Start()
        {
            Application.runInBackground = true;

            if (dialogueController == null)
            {
                dialogueController = GetComponent<CarlDialogueController>();
                if (dialogueController == null)
                    dialogueController = FindFirstObjectByType<CarlDialogueController>();
            }

            if (startListeningOnAwake)
            {
                InitializeAndStartVoice();
                StartMicMonitoring();
            }
        }

        private void Update()
        {
            // Spacebar or T key instant trigger fallback
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.T))
            {
                TriggerSimulatedVoice("Hi Carl, how are you?");
            }

            // Real-time volume visualizer on the HUD
            if (monitorMicVolume && !_isSpeakingToCarl && _micMonitorClip != null && Microphone.IsRecording(_activeMicDevice))
            {
                UpdateLiveVolumeMeter();
            }
        }

        private void StartMicMonitoring()
        {
            if (Microphone.devices.Length == 0) return;
            _activeMicDevice = Microphone.devices[0];
            try
            {
                _micMonitorClip = Microphone.Start(_activeMicDevice, true, 2, 44100);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CarlVoiceListener] Mic monitoring exception: " + ex.Message);
            }
        }

        private void UpdateLiveVolumeMeter()
        {
            int micPos = Microphone.GetPosition(_activeMicDevice);
            if (micPos < _volumeSamples.Length) return;

            _micMonitorClip.GetData(_volumeSamples, micPos - _volumeSamples.Length);
            float sum = 0f;
            for (int i = 0; i < _volumeSamples.Length; i++)
            {
                sum += Mathf.Abs(_volumeSamples[i]);
            }
            float avg = sum / _volumeSamples.Length;
            _currentVolume = Mathf.Lerp(_currentVolume, avg, Time.deltaTime * 15f);

            // Construct volume bars
            int bars = Mathf.Clamp(Mathf.RoundToInt(_currentVolume * 120f), 0, 5);
            string meter = "";
            for (int b = 0; b < 5; b++)
            {
                meter += (b < bars) ? "▰" : "▱";
            }

            if (micStatusText != null)
            {
                if (bars > 0)
                {
                    micStatusText.text = $"🎙️ Mic: {meter} (Speaking...)";
                    micStatusText.color = new Color(0.2f, 0.9f, 0.4f);
                }
                else
                {
                    micStatusText.text = $"🎙️ Mic: {meter} Say \"Hi Carl\" (or Spacebar)";
                    micStatusText.color = new Color(0.7f, 0.85f, 0.95f);
                }
            }
        }

        public void InitializeAndStartVoice()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!PhraseRecognitionSystem.isSupported)
            {
                Debug.LogWarning("[CarlVoiceListener] Windows PhraseRecognitionSystem is not supported on this platform.");
                SetUIStatus("🎙️ Voice not supported on this OS", new Color(0.8f, 0.4f, 0.4f));
                return;
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[CarlVoiceListener] No microphone detected.");
                SetUIStatus("🎙️ No microphone connected", new Color(0.8f, 0.4f, 0.4f));
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

                Debug.Log($"[CarlVoiceListener] Recognizer started on '{Microphone.devices[0]}'. Keywords: {_keywords.Length}");
                SetUIStatus("🎙️ Mic Ready: Say \"Hi Carl\" (or press Space)", new Color(0.25f, 0.85f, 0.55f));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CarlVoiceListener] Failed to start voice recognizer: {ex.Message}");
                SetUIStatus("🎙️ Voice error: " + ex.Message, new Color(0.8f, 0.4f, 0.4f));
            }
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

            TriggerSimulatedVoice(spoken);
        }
#endif

        public void TriggerSimulatedVoice(string phrase)
        {
            _isSpeakingToCarl = true;
            SetUIStatus($"🎙️ Heard: \"{phrase}\" ➔ Carl is replying...", new Color(0.96f, 0.72f, 0.24f));

            if (dialogueController != null)
            {
                dialogueController.SendMessageToCarl(phrase);
            }

            StopAllCoroutines();
            StartCoroutine(RestoreStatusPromptRoutine());
        }

        private IEnumerator RestoreStatusPromptRoutine()
        {
            yield return new WaitForSeconds(8.0f);
            _isSpeakingToCarl = false;
            SetUIStatus("🎙️ Mic Ready: Say \"Hi Carl\" (or press Space)", new Color(0.25f, 0.85f, 0.55f));
        }

        private void SetUIStatus(string status, Color color)
        {
            if (micStatusText != null)
            {
                micStatusText.text = status;
                micStatusText.color = color;
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
            if (!string.IsNullOrEmpty(_activeMicDevice) && Microphone.IsRecording(_activeMicDevice))
            {
                Microphone.End(_activeMicDevice);
            }
        }
    }
}
