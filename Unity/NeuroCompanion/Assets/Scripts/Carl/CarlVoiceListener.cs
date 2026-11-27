// ============================================================
// CarlVoiceListener.cs — Unity 6.3 LTS
// NeuroCompanion — Hands-Free Voice Activity Detection (VAD) + Backend STT
//
// 1. Continuous Microphone Voice Activity Detection (VAD) with RMS volume threshold.
// 2. Automatically starts recording when you speak, stops after 0.9s pause.
// 3. Encodes spoken audio into 16-bit PCM WAV and POSTs to /api/v1/carl/voice-stt.
// 4. Understands ANY open-ended English sentence without fixed keyword restrictions.
// 5. Keeps Unity audio alive in background (Application.runInBackground = true).
// 6. Live VU volume meter on the HUD showing exact mic level in real-time.
// 7. Spacebar / T key instant fallback available anytime for keyboard testing.
// ============================================================

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
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

        [Header("Backend STT Configuration")]
        public string voiceSttEndpoint = "http://127.0.0.1:8000/api/v1/carl/voice-stt";

        [Header("UI Voice Status Indicator")]
        public GameObject micIndicatorPanel;
        public TMP_Text micStatusText;

        [Header("Voice Activity Detection (VAD)")]
        [Tooltip("RMS volume threshold to trigger speech detection (0.015 - 0.04 is typical for laptop mics)")]
        public float voiceThreshold = 0.022f;

        [Tooltip("Seconds of silence required to consider speech finished")]
        public float silenceDuration = 0.95f;

        [Tooltip("Minimum speech length in seconds to filter out brief clicks or coughs")]
        public float minSpeechLength = 0.45f;

        [Tooltip("Maximum allowed utterance length in seconds")]
        public float maxSpeechLength = 8.0f;

        [Header("Microphone Settings")]
        public bool startListeningOnAwake = true;
        public int sampleRate = 16000;
        public int circularBufferLengthSec = 10;

        // VAD Internal State
        private enum VADState { Idle, RecordingSpeech, Processing, CarlSpeaking }
        private VADState _state = VADState.Idle;

        private AudioClip _micClip;
        private string _activeMicDevice;
        private int _totalBufferSamples;
        private float[] _rmsSampleBuffer = new float[256];
        private float _currentRmsVolume = 0f;

        private int _speechStartSample = 0;
        private float _silenceTimer = 0f;
        private float _speechDurationTimer = 0f;
        private float _carlSpeechCooldownTimer = 0f;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer _backupKeywordRecognizer;
#endif

        [Serializable]
        public class STTResponseData
        {
            public string transcription;
            public string reply;
            public string emotion;
            public string action;
            public string voice_clip;
        }

        private void Awake()
        {
            // CRITICAL: Ensure Unity microphone continues running even when window is not focused!
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

            StartContinuousMicrophone();
        }

        private void StartContinuousMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[CarlVoiceListener] No microphone detected on this computer.");
                SetUIStatus("🎙️ No microphone detected (Press Space to test)", new Color(0.85f, 0.4f, 0.4f));
                return;
            }

            _activeMicDevice = Microphone.devices[0];
            _totalBufferSamples = sampleRate * circularBufferLengthSec;

            try
            {
                _micClip = Microphone.Start(_activeMicDevice, true, circularBufferLengthSec, sampleRate);
                Debug.Log($"[CarlVoiceListener] Continuous mic started on '{_activeMicDevice}' ({sampleRate}Hz, {circularBufferLengthSec}s buffer)");
                SetUIStatus("🎙️ Mic Ready: Speak to Carl anytime (or press Space)", new Color(0.3f, 0.9f, 0.55f));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CarlVoiceListener] Error starting microphone: {ex.Message}");
                SetUIStatus("🎙️ Mic Error: " + ex.Message, new Color(0.85f, 0.4f, 0.4f));
            }
        }

        private void Update()
        {
            // Manual hotkey fallback (Spacebar or T key) for instant keyboard testing
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.T))
            {
                TriggerManualFallback("Hi Carl, how are you?");
                return;
            }

            // Cooldown while Carl is speaking to avoid acoustic echo feedback
            if (_carlSpeechCooldownTimer > 0f)
            {
                _carlSpeechCooldownTimer -= Time.deltaTime;
                if (_carlSpeechCooldownTimer <= 0f && _state == VADState.CarlSpeaking)
                {
                    _state = VADState.Idle;
                    SetUIStatus("🎙️ Mic Ready: Speak to Carl anytime (or press Space)", new Color(0.3f, 0.9f, 0.55f));
                }
                return;
            }

            if (_micClip == null || string.IsNullOrEmpty(_activeMicDevice) || !Microphone.IsRecording(_activeMicDevice))
                return;

            // 1. Calculate live RMS volume from microphone write head
            int currentWritePos = Microphone.GetPosition(_activeMicDevice);
            if (currentWritePos < 0 || currentWritePos >= _totalBufferSamples) return;

            UpdateRmsVolume(currentWritePos);

            // 2. State Machine for Voice Activity Detection
            switch (_state)
            {
                case VADState.Idle:
                    UpdateLiveVuMeter(false);

                    if (_currentRmsVolume >= voiceThreshold)
                    {
                        // Speech Detected!
                        _state = VADState.RecordingSpeech;
                        _speechDurationTimer = 0f;
                        _silenceTimer = 0f;

                        // Include 0.25 seconds of pre-speech buffer so leading consonants are preserved
                        int preBufferSamples = (int)(sampleRate * 0.25f);
                        _speechStartSample = (currentWritePos - preBufferSamples + _totalBufferSamples) % _totalBufferSamples;

                        SetUIStatus("🎙️ Hearing you... Speak naturally", new Color(0.96f, 0.72f, 0.24f));
                    }
                    break;

                case VADState.RecordingSpeech:
                    UpdateLiveVuMeter(true);
                    _speechDurationTimer += Time.deltaTime;

                    if (_currentRmsVolume >= voiceThreshold)
                    {
                        // Active voice sustained: reset silence counter
                        _silenceTimer = 0f;
                    }
                    else
                    {
                        // Volume dropped below threshold: count silence
                        _silenceTimer += Time.deltaTime;
                    }

                    // End conditions: User stopped speaking (pause > silenceDuration) or max speech length hit
                    bool silenceFinished = _silenceTimer >= silenceDuration;
                    bool maxDurationReached = _speechDurationTimer >= maxSpeechLength;

                    if (silenceFinished || maxDurationReached)
                    {
                        _state = VADState.Processing;
                        SetUIStatus("🎙️ Processing speech... Carl is thinking", new Color(0.45f, 0.8f, 0.95f));

                        int speechEndSample = currentWritePos;
                        ExtractAndSendSpeechAudio(_speechStartSample, speechEndSample);
                    }
                    break;

                case VADState.Processing:
                    // Waiting for HTTP backend response
                    break;

                case VADState.CarlSpeaking:
                    // Waiting for Carl to finish speaking
                    break;
            }
        }

        private void UpdateRmsVolume(int currentWritePos)
        {
            int offset = (currentWritePos - _rmsSampleBuffer.Length + _totalBufferSamples) % _totalBufferSamples;
            _micClip.GetData(_rmsSampleBuffer, offset);

            float sum = 0f;
            for (int i = 0; i < _rmsSampleBuffer.Length; i++)
            {
                sum += _rmsSampleBuffer[i] * _rmsSampleBuffer[i];
            }
            float rms = Mathf.Sqrt(sum / _rmsSampleBuffer.Length);
            _currentRmsVolume = Mathf.Lerp(_currentRmsVolume, rms, Time.deltaTime * 18f);
        }

        private void UpdateLiveVuMeter(bool isSpeaking)
        {
            if (micStatusText == null) return;

            int bars = Mathf.Clamp(Mathf.RoundToInt((_currentRmsVolume / (voiceThreshold * 2.2f)) * 5f), 0, 5);
            string meter = "";
            for (int b = 0; b < 5; b++)
            {
                meter += (b < bars) ? "▰" : "▱";
            }

            if (isSpeaking)
            {
                micStatusText.text = $"🎙️ Hearing: {meter} (Listening...)";
                micStatusText.color = new Color(0.96f, 0.72f, 0.24f);
            }
            else
            {
                if (bars > 0)
                {
                    micStatusText.text = $"🎙️ Mic: {meter} Speak to Carl";
                    micStatusText.color = new Color(0.3f, 0.9f, 0.55f);
                }
                else
                {
                    micStatusText.text = $"🎙️ Mic: {meter} Say anything to Carl (or Spacebar)";
                    micStatusText.color = new Color(0.7f, 0.85f, 0.95f);
                }
            }
        }

        private void ExtractAndSendSpeechAudio(int startSample, int endSample)
        {
            int totalSamplesRecorded;
            if (endSample >= startSample)
            {
                totalSamplesRecorded = endSample - startSample;
            }
            else
            {
                totalSamplesRecorded = (_totalBufferSamples - startSample) + endSample;
            }

            float durationSeconds = (float)totalSamplesRecorded / sampleRate;

            // Filter out accidental quick noise/clicks
            if (durationSeconds < minSpeechLength)
            {
                Debug.Log($"[CarlVoiceListener] Discarded brief noise ({durationSeconds:F2}s < {minSpeechLength}s)");
                _state = VADState.Idle;
                SetUIStatus("🎙️ Mic Ready: Speak to Carl anytime (or press Space)", new Color(0.3f, 0.9f, 0.55f));
                return;
            }

            Debug.Log($"[CarlVoiceListener] Spoken utterance detected: {durationSeconds:F2}s ({totalSamplesRecorded} samples). Encoding WAV...");

            float[] speechSamples = new float[totalSamplesRecorded];
            if (endSample >= startSample)
            {
                _micClip.GetData(speechSamples, startSample);
            }
            else
            {
                int part1Count = _totalBufferSamples - startSample;
                float[] part1 = new float[part1Count];
                _micClip.GetData(part1, startSample);
                Array.Copy(part1, 0, speechSamples, 0, part1Count);

                int part2Count = endSample;
                if (part2Count > 0)
                {
                    float[] part2 = new float[part2Count];
                    _micClip.GetData(part2, 0);
                    Array.Copy(part2, 0, speechSamples, part1Count, part2Count);
                }
            }

            // Convert to 16-bit PCM WAV
            byte[] wavBytes = WavUtility.FromAudioSamples(speechSamples, 1, sampleRate);

            StartCoroutine(SendWavAudioToSTT(wavBytes, durationSeconds));
        }

        private IEnumerator SendWavAudioToSTT(byte[] wavBytes, float speechDuration)
        {
            using (UnityWebRequest req = new UnityWebRequest(voiceSttEndpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(wavBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "audio/wav");
                req.timeout = 7;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = req.downloadHandler.text;
                    Debug.Log($"[CarlVoiceListener] STT Backend response: {jsonResponse}");

                    var sttData = JsonUtility.FromJson<STTResponseData>(jsonResponse);
                    if (sttData != null && !string.IsNullOrEmpty(sttData.reply))
                    {
                        string userTranscript = !string.IsNullOrEmpty(sttData.transcription) ? sttData.transcription : "(Voice detected)";
                        SetUIStatus($"🎙️ Heard: \"{userTranscript}\"", new Color(0.25f, 0.95f, 0.55f));

                        // Deliver reply through Carl's speech bubble and animated voice
                        if (dialogueController != null)
                        {
                            dialogueController.DeliverCarlReply(sttData.reply, sttData.action);
                        }

                        // Pause VAD while Carl is speaking (+ 1.5s margin)
                        _state = VADState.CarlSpeaking;
                        _carlSpeechCooldownTimer = Mathf.Clamp(sttData.reply.Length * 0.08f, 5.0f, 10.0f);
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning($"[CarlVoiceListener] STT endpoint failed: {req.error}. Using local companion reply.");
                }
            }

            // Fallback if backend was unreachable or error occurred
            if (dialogueController != null)
            {
                dialogueController.SendMessageToCarl("Hi Carl, how are you?");
            }

            _state = VADState.CarlSpeaking;
            _carlSpeechCooldownTimer = 6.0f;
        }

        public void TriggerManualFallback(string phrase)
        {
            _state = VADState.CarlSpeaking;
            _carlSpeechCooldownTimer = 7.0f;

            SetUIStatus($"🎙️ Input: \"{phrase}\" ➔ Carl is replying...", new Color(0.96f, 0.72f, 0.24f));

            if (dialogueController != null)
            {
                dialogueController.SendMessageToCarl(phrase);
            }
        }

        private void SetUIStatus(string text, Color color)
        {
            if (micStatusText != null)
            {
                micStatusText.text = text;
                micStatusText.color = color;
            }
        }

        private void OnDestroy()
        {
            if (!string.IsNullOrEmpty(_activeMicDevice) && Microphone.IsRecording(_activeMicDevice))
            {
                Microphone.End(_activeMicDevice);
            }
        }
    }
}
