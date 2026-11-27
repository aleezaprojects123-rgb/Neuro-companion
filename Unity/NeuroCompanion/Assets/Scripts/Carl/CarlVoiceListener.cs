// ============================================================
// CarlVoiceListener.cs — Unity 6.3 LTS
// NeuroCompanion — Robust Hands-Free Voice Activity Detection (VAD) + Backend STT
//
// 1. Balanced digital microphone gain (8.0f) — avoids fan/room noise false triggers.
// 2. Speaker Feedback Shield — completely ignores mic while Carl's audio is playing.
// 3. Unity New Input System safe (no legacy Input crashes).
// 4. Encodes spoken audio to WAV and POSTs to /api/v1/carl/voice-stt.
// 5. Keeps Unity audio alive in background (Application.runInBackground = true).
// ============================================================

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
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
        [Tooltip("Digital software microphone gain multiplier")]
        public float micGain = 8.0f;

        [Tooltip("Amplified RMS volume threshold to trigger speech detection (0.045 - 0.065 is ideal)")]
        public float voiceThreshold = 0.055f;

        [Tooltip("Seconds of silence required to consider speech finished (0.25 - 0.35s is ultra-fast)")]
        public float silenceDuration = 0.28f;

        [Tooltip("Minimum speech length in seconds to filter out brief clicks or coughs")]
        public float minSpeechLength = 0.40f;

        [Tooltip("Maximum allowed utterance length in seconds")]
        public float maxSpeechLength = 8.0f;

        [Header("Microphone Settings")]
        public bool startListeningOnAwake = false;
        public int sampleRate = 48000;
        public int circularBufferLengthSec = 10;

        // VAD Internal State
        private enum VADState { Idle, RecordingSpeech, Processing, CarlSpeaking }
        private VADState _state = VADState.Idle;
        private bool _isListeningActive = false;

        private AudioClip _micClip;
        private string _activeMicDevice;
        private int _totalBufferSamples;
        private float[] _rmsSampleBuffer = new float[512];
        private float _currentRmsVolume = 0f;
        private float _ambientNoiseFloor = 0.005f;

        private int _speechStartSample = 0;
        private float _silenceTimer = 0f;
        private float _speechDurationTimer = 0f;
        private float _carlSpeechCooldownTimer = 0f;
        private float _sustainedVoiceTimer = 0f;
        private float _actualVoicedDuration = 0f;

        [Serializable]
        public class STTResponseData
        {
            public string transcription;
            public string reply;
            public string emotion;
            public string action;
            public string voice_clip;
            public string audio_base64;
        }

        private void Awake()
        {
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
                EnableVoiceListening();
            }
            else
            {
                _isListeningActive = false;
                SetUIStatus("", Color.clear);
            }
        }

        public void EnableVoiceListening()
        {
            _isListeningActive = true;
            _state = VADState.Idle;
            StartContinuousMicrophone();
            Debug.Log("[CarlVoiceListener] Voice listening enabled for Patient Mode.");
        }

        public void DisableVoiceListening()
        {
            _isListeningActive = false;
            _state = VADState.Idle;
            if (!string.IsNullOrEmpty(_activeMicDevice) && Microphone.IsRecording(_activeMicDevice))
            {
                Microphone.End(_activeMicDevice);
            }
            SetUIStatus("", Color.clear);
            Debug.Log("[CarlVoiceListener] Voice listening disabled.");
        }

        public void StartContinuousMicrophone()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[CarlVoiceListener] No microphone detected on this computer.");
                SetUIStatus("🎙️ No microphone detected (Press Space to test)", new Color(0.85f, 0.4f, 0.4f));
                return;
            }

            _activeMicDevice = Microphone.devices[0];

            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(_activeMicDevice, out minFreq, out maxFreq);
            if (maxFreq > 0)
                sampleRate = maxFreq;
            else if (minFreq > 0)
                sampleRate = minFreq;
            else
                sampleRate = 48000;

            _totalBufferSamples = sampleRate * circularBufferLengthSec;

            try
            {
                if (Microphone.IsRecording(_activeMicDevice))
                {
                    Microphone.End(_activeMicDevice);
                }

                _micClip = Microphone.Start(_activeMicDevice, true, circularBufferLengthSec, sampleRate);
                Debug.Log($"[CarlVoiceListener] Microphone successfully started: '{_activeMicDevice}' at {sampleRate}Hz");
                SetUIStatus("🎙️ Mic Ready: Speak to Carl anytime", new Color(0.3f, 0.9f, 0.55f));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CarlVoiceListener] Error starting microphone: {ex.Message}");
                SetUIStatus("🎙️ Mic Error: " + ex.Message, new Color(0.85f, 0.4f, 0.4f));
            }
        }

        private void Update()
        {
            if (!_isListeningActive) return;

            try
            {
                // Safe hotkey fallback check for Unity New Input System
                if (CheckHotkeyTriggered())
                {
                    TriggerManualFallback("Hi Carl, how are you?");
                    return;
                }

                // 1. SPEAKER FEEDBACK SHIELD: While Carl is speaking out loud, ignore mic input completely!
                bool carlIsTalking = IsCarlCurrentlySpeaking();
                if (carlIsTalking)
                {
                    _state = VADState.CarlSpeaking;
                    _carlSpeechCooldownTimer = 2.0f; // 2s safety buffer after Carl speaks so speakers don't re-trigger mic
                    _sustainedVoiceTimer = 0f;
                    _actualVoicedDuration = 0f;
                    return;
                }

                // Cooldown countdown right after Carl finishes speaking
                if (_carlSpeechCooldownTimer > 0f)
                {
                    _carlSpeechCooldownTimer -= Time.deltaTime;
                    if (_carlSpeechCooldownTimer <= 0f && _state == VADState.CarlSpeaking)
                    {
                        _state = VADState.Idle;
                        _sustainedVoiceTimer = 0f;
                        _actualVoicedDuration = 0f;
                        SetUIStatus("🎙️ Mic Ready: Speak to Carl anytime", new Color(0.3f, 0.9f, 0.55f));
                    }
                    return;
                }

                if (_micClip == null || string.IsNullOrEmpty(_activeMicDevice) || !Microphone.IsRecording(_activeMicDevice))
                {
                    if (Time.frameCount % 180 == 0 && Microphone.devices.Length > 0)
                    {
                        StartContinuousMicrophone();
                    }
                    return;
                }

                // 2. Calculate amplified live RMS volume
                int currentWritePos = Microphone.GetPosition(_activeMicDevice);
                if (currentWritePos < 0 || currentWritePos >= _totalBufferSamples) return;

                UpdateRmsVolume(currentWritePos);

                // Dynamic trigger threshold: base threshold or 2.8x ambient room noise floor
                float effectiveThreshold = Mathf.Max(voiceThreshold, _ambientNoiseFloor * 2.8f);

                // 3. State Machine for Voice Activity Detection
                switch (_state)
                {
                    case VADState.Idle:
                        UpdateLiveVuMeter(false, effectiveThreshold);

                        // Slowly adapt to ambient room noise floor (safely bounded)
                        _ambientNoiseFloor = Mathf.Lerp(_ambientNoiseFloor, _currentRmsVolume, Time.deltaTime * 0.3f);
                        _ambientNoiseFloor = Mathf.Clamp(_ambientNoiseFloor, 0.002f, 0.04f);

                        if (_currentRmsVolume >= effectiveThreshold)
                        {
                            _sustainedVoiceTimer += Time.deltaTime;
                            // Require at least 0.12s of continuous voice sound to filter out room clicks, keyboard, fan noise
                            if (_sustainedVoiceTimer >= 0.12f)
                            {
                                // Human Voice Confirmed!
                                _state = VADState.RecordingSpeech;
                                _speechDurationTimer = _sustainedVoiceTimer;
                                _actualVoicedDuration = _sustainedVoiceTimer;
                                _silenceTimer = 0f;
                                _sustainedVoiceTimer = 0f;

                                int preBufferSamples = (int)(sampleRate * 0.30f);
                                _speechStartSample = (currentWritePos - preBufferSamples + _totalBufferSamples) % _totalBufferSamples;

                                SetUIStatus("🎙️ Hearing you... Speak naturally", new Color(0.96f, 0.72f, 0.24f));
                            }
                        }
                        else
                        {
                            _sustainedVoiceTimer = Mathf.Max(0f, _sustainedVoiceTimer - Time.deltaTime * 2.0f);
                        }
                        break;

                    case VADState.RecordingSpeech:
                        UpdateLiveVuMeter(true, effectiveThreshold);
                        _speechDurationTimer += Time.deltaTime;

                        if (_currentRmsVolume >= effectiveThreshold)
                        {
                            _actualVoicedDuration += Time.deltaTime;
                            _silenceTimer = 0f;
                        }
                        else
                        {
                            _silenceTimer += Time.deltaTime;
                        }

                        bool silenceFinished = _silenceTimer >= silenceDuration;
                        bool maxDurationReached = _speechDurationTimer >= maxSpeechLength;

                        if (silenceFinished || maxDurationReached)
                        {
                            _state = VADState.Processing;
                            SetUIStatus("🎙️ Carl is replying...", new Color(0.96f, 0.72f, 0.24f));

                            int speechEndSample = currentWritePos;
                            ExtractAndSendSpeechAudio(_speechStartSample, speechEndSample);
                        }
                        break;

                    case VADState.Processing:
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CarlVoiceListener] Exception in Update: " + ex.Message);
            }
        }

        private bool IsCarlCurrentlySpeaking()
        {
            if (dialogueController != null)
            {
                if (dialogueController.carlAudioSource != null && dialogueController.carlAudioSource.isPlaying)
                    return true;
            }
            return false;
        }

        private bool CheckHotkeyTriggered()
        {
            try
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    return Keyboard.current.spaceKey.wasPressedThisFrame ||
                           Keyboard.current.tKey.wasPressedThisFrame;
                }
#endif
            }
            catch { }
            return false;
        }

        private void UpdateRmsVolume(int currentWritePos)
        {
            int offset = (currentWritePos - _rmsSampleBuffer.Length + _totalBufferSamples) % _totalBufferSamples;
            _micClip.GetData(_rmsSampleBuffer, offset);

            float sum = 0f;
            for (int i = 0; i < _rmsSampleBuffer.Length; i++)
            {
                float amplified = _rmsSampleBuffer[i] * micGain;
                sum += amplified * amplified;
            }
            float rawRms = Mathf.Sqrt(sum / _rmsSampleBuffer.Length);
            _currentRmsVolume = Mathf.Lerp(_currentRmsVolume, rawRms, Time.deltaTime * 20f);
        }

        private void UpdateLiveVuMeter(bool isSpeaking, float effectiveThreshold)
        {
            if (micStatusText == null) return;

            int bars = Mathf.Clamp(Mathf.RoundToInt((_currentRmsVolume / Mathf.Max(effectiveThreshold, 0.001f)) * 3f), 0, 5);
            string meter = "";
            for (int b = 0; b < 5; b++)
            {
                meter += (b < bars) ? "▰" : "▱";
            }

            if (isSpeaking)
            {
                micStatusText.text = $"🎙️ Hearing: {meter} (Speaking...)";
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

            // Strict ambient filter: discard if speech was too brief OR lacked actual voiced sound (e.g. ambient fan/click)
            if (durationSeconds < minSpeechLength || _actualVoicedDuration < 0.20f)
            {
                Debug.Log($"[CarlVoiceListener] Discarded ambient noise ({durationSeconds:F2}s, voiced: {_actualVoicedDuration:F2}s). Remaining quiet.");
                _state = VADState.Idle;
                _sustainedVoiceTimer = 0f;
                _actualVoicedDuration = 0f;
                SetUIStatus("🎙️ Mic Ready: Speak to Carl anytime", new Color(0.3f, 0.9f, 0.55f));
                return;
            }

            Debug.Log($"[CarlVoiceListener] Spoken utterance detected: {durationSeconds:F2}s (voiced: {_actualVoicedDuration:F2}s). Encoding WAV...");

            // Instant 0.28s conversational bridge: Carl speaks opening words immediately
            if (dialogueController != null)
            {
                dialogueController.PlayConversationalBridge();
            }

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

            // Clean 3x software boost for transcription clarity
            for (int i = 0; i < speechSamples.Length; i++)
            {
                speechSamples[i] = Mathf.Clamp(speechSamples[i] * 3.0f, -1.0f, 1.0f);
            }

            // Downsample to 16kHz: 67% smaller payload and 2x faster STT recognition
            float[] sttSamples = WavUtility.DownsampleTo16k(speechSamples, sampleRate);
            byte[] wavBytes = WavUtility.FromAudioSamples(sttSamples, 1, 16000);
            StartCoroutine(SendWavAudioToSTT(wavBytes, durationSeconds));
        }

        private IEnumerator SendWavAudioToSTT(byte[] wavBytes, float speechDuration)
        {
            using (UnityWebRequest req = new UnityWebRequest(voiceSttEndpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(wavBytes);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "audio/wav");
                req.timeout = 30; // Generous timeout for STT + Gemini LLM + TTS synthesis

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

                        AudioClip dynamicClip = null;
                        if (!string.IsNullOrEmpty(sttData.audio_base64))
                        {
                            try
                            {
                                byte[] dynamicBytes = Convert.FromBase64String(sttData.audio_base64);
                                dynamicClip = WavUtility.ToAudioClip(dynamicBytes, "DynamicCarlReply");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning("[CarlVoiceListener] Error decoding dynamic audio: " + ex.Message);
                            }
                        }

                        if (dialogueController != null)
                        {
                            dialogueController.DeliverCarlReply(sttData.reply, sttData.action, dynamicClip);
                        }

                        _state = VADState.CarlSpeaking;
                        _carlSpeechCooldownTimer = Mathf.Clamp(sttData.reply.Length * 0.08f, 5.0f, 10.0f);
                        yield break;
                    }
                    else
                    {
                        // Speech was quiet or noise. Deliver gentle companion reassurance
                        Debug.Log("[CarlVoiceListener] Speech unintelligible or low volume. Reassuring patient.");
                        if (dialogueController != null)
                        {
                            dialogueController.DeliverCarlReply("Take all the time you need, my friend. I'm right here with you in the garden.", "talk", null);
                        }
                        _state = VADState.CarlSpeaking;
                        _carlSpeechCooldownTimer = 4.0f;
                        yield break;
                    }
                }
                else
                {
                    Debug.LogWarning($"[CarlVoiceListener] STT endpoint error ({req.error}). Delivering warm companion fallback.");
                    if (dialogueController != null)
                    {
                        dialogueController.DeliverCarlReply("I hear you, my friend. It's so peaceful enjoying our garden together today.", "talk", null);
                    }
                    _state = VADState.CarlSpeaking;
                    _carlSpeechCooldownTimer = 4.0f;
                    yield break;
                }
            }
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
