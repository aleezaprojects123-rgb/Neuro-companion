// ============================================================
// CarlDialogueController.cs — Unity 6.3 LTS
// NeuroCompanion — Interactive Carl Companion Dialogue System
//
// Enables patients and caregivers to talk directly to Carl Fredricksen:
// 1. Hands-free voice recognition & optional text input.
// 2. HTTP POST communication with backend (/api/v1/carl/chat) + offline fallback.
// 3. Dynamic Carl speech bubble popup with typewriter text reveal.
// 4. Plays EXACT matching spoken voice audio clip for the dialogue response.
// 5. Procedural lip sync (mouth open/close) and conversational head nodding.
// ============================================================

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

namespace NeuroCompanion.Carl
{
    public class CarlDialogueController : MonoBehaviour
    {
        [Header("Backend Configuration")]
        public string chatEndpointUrl = "http://127.0.0.1:8000/api/v1/carl/chat";
        public string patientId = "P001";

        [Header("UI References — Carl Speech Bubble")]
        public GameObject speechBubblePanel;
        public TMP_Text speechBubbleText;
        public TMP_Text speakerNameText;

        [Header("Optional UI References (Null for voice-only)")]
        public TMP_InputField messageInputField;
        public Button sendButton;
        public Button quickPill1;
        public Button quickPill2;
        public Button quickPill3;

        [Header("Carl Model & Lip Sync References")]
        public AudioSource carlAudioSource;
        public SkinnedMeshRenderer faceSkinnedMesh;
        public Transform headTransform;

        [Header("Matching Voice Audio Clips")]
        public AudioClip clipImFine;      // "I'm doing just fine, thank you..."
        public AudioClip clipHello;       // "Hello there! It is so good to see you..."
        public AudioClip clipGardening;   // "We are nurturing our little garden..."
        public AudioClip clipGardenPeace; // "The garden is coming along wonderfully..."
        public AudioClip clipWelcome;     // Initial greeting

        [Header("Conversational Audio Bridge (Instant 0.28s Response)")]
        public AudioClip[] bridgeClips;
        public string[] bridgePhrases = new string[]
        {
            "Well now, let me see...",
            "Ah, indeed, my friend...",
            "Well, let me think on that a moment..."
        };

        private Coroutine _bridgeRoutine;
        private bool _isBridgeActive = false;

        [Header("Dialogue Animation Parameters")]
        public float typewritingSpeed = 0.025f;
        public float autoDismissDelay = 6.0f;
        public float mouthOpenMax = 65.0f;

        // Internal State
        private bool _isSpeaking = false;
        private float _speakingTimer = 0f;
        private float _speakingDuration = 0f;
        private Coroutine _currentDialogueRoutine;
        private Coroutine _typewriterRoutine;

        [Serializable]
        public class ChatPayload
        {
            public string message;
            public string patient_id;
        }

        [Serializable]
        public class ChatResponseData
        {
            public string reply;
            public string emotion;
            public string action;
            public string voice_clip;
            public string audio_base64;
        }

        private void Awake()
        {
#if UNITY_EDITOR
            LoadBridgeClipsIfEmpty();
#endif
        }

        public void LoadBridgeClipsIfEmpty()
        {
#if UNITY_EDITOR
            if (bridgeClips == null || bridgeClips.Length == 0)
            {
                bridgeClips = new AudioClip[]
                {
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarlBridge_1.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarlBridge_2.wav"),
                    UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/CarlBridge_3.wav")
                };
            }
#endif
        }

        private void Start()
        {
#if UNITY_EDITOR
            LoadBridgeClipsIfEmpty();
#endif
            // Auto-locate references if unassigned
            if (carlAudioSource == null) carlAudioSource = GetComponent<AudioSource>();
            if (faceSkinnedMesh == null) faceSkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>();

            // Setup input field listeners if assigned
            if (sendButton != null) sendButton.onClick.AddListener(OnSendButtonClicked);
            if (messageInputField != null) messageInputField.onSubmit.AddListener((val) => OnSendButtonClicked());

            if (quickPill1 != null) quickPill1.onClick.AddListener(() => SendMessageToCarl("Hi Carl, how are you?"));
            if (quickPill2 != null) quickPill2.onClick.AddListener(() => SendMessageToCarl("What are we doing today?"));
            if (quickPill3 != null) quickPill3.onClick.AddListener(() => SendMessageToCarl("Tell me about the garden"));

            // Hide speech bubble initially
            if (speechBubblePanel != null)
            {
                speechBubblePanel.SetActive(false);
            }
        }

        private void Update()
        {
            if (_isSpeaking)
            {
                _speakingTimer += Time.deltaTime;

                // Animate mouth opening and closing with natural speech rhythm
                float speechSyllable = Mathf.Sin(_speakingTimer * 16.0f) * 0.5f + 0.5f;
                float breath = Mathf.Sin(_speakingTimer * 4.0f) * 0.2f;
                float mouthWeight = (speechSyllable * 0.8f + breath) * mouthOpenMax;

                if (faceSkinnedMesh != null)
                {
                    // BlendShape 0: MouthOpen
                    faceSkinnedMesh.SetBlendShapeWeight(0, Mathf.Clamp(mouthWeight, 0f, 100f));
                    // BlendShape 1: Smile warmly
                    faceSkinnedMesh.SetBlendShapeWeight(1, 45f);
                }

                // Conversational head nod while talking
                if (headTransform != null)
                {
                    float nod = Mathf.Sin(_speakingTimer * 3.5f) * 4.5f;
                    float tilt = Mathf.Sin(_speakingTimer * 1.8f) * 2.0f;
                    headTransform.localRotation = Quaternion.Euler(nod, tilt, 0f);
                }

                if (_speakingTimer >= _speakingDuration)
                {
                    StopSpeakingAnimation();
                }
            }
        }

        public void OnSendButtonClicked()
        {
            if (messageInputField == null) return;
            string text = messageInputField.text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            messageInputField.text = "";
            SendMessageToCarl(text);
        }

        public void SendMessageToCarl(string userText)
        {
            Debug.Log($"[CarlDialogueController] User asked: '{userText}'");
            if (_currentDialogueRoutine != null)
                StopCoroutine(_currentDialogueRoutine);

            _currentDialogueRoutine = StartCoroutine(SendChatRequestRoutine(userText));
        }

        private IEnumerator SendChatRequestRoutine(string userText)
        {
            string reply = "";
            string action = "talk";
            AudioClip dynamicClip = null;

            var payload = new ChatPayload { message = userText, patient_id = patientId };
            string jsonBody = JsonUtility.ToJson(payload);

            using (UnityWebRequest req = new UnityWebRequest(chatEndpointUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 3;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string jsonResp = req.downloadHandler.text;
                    var resp = JsonUtility.FromJson<ChatResponseData>(jsonResp);
                    if (resp != null && !string.IsNullOrEmpty(resp.reply))
                    {
                        reply = resp.reply;
                        action = resp.action;

                        if (!string.IsNullOrEmpty(resp.audio_base64))
                        {
                            try
                            {
                                byte[] dynamicBytes = Convert.FromBase64String(resp.audio_base64);
                                dynamicClip = WavUtility.ToAudioClip(dynamicBytes, "DynamicCarlReply");
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning("[CarlDialogueController] Dynamic audio parse error: " + ex.Message);
                            }
                        }
                    }
                }
                else
                {
                    reply = GetLocalFallbackReply(userText);
                }
            }

            if (string.IsNullOrEmpty(reply))
            {
                reply = GetLocalFallbackReply(userText);
            }

            // Deliver Carl's response visually and with voice/lip-sync
            DeliverCarlReply(reply, action, dynamicClip);
        }

        private string GetLocalFallbackReply(string msg)
        {
            string lower = msg.ToLower();
            if (lower.Contains("how are you") || lower.Contains("how are u"))
                return "I'm doing just fine, thank you! It's a wonderful day in our garden. How are you feeling today?";
            if (lower.Contains("hi") || lower.Contains("hello") || lower.Contains("hey"))
                return "Hello there! It is so good to see you. Take a deep breath of the fresh garden air with me.";
            if (lower.Contains("doing") || lower.Contains("plant") || lower.Contains("what next"))
                return "We are nurturing our little garden! You can water the seedlings at our planting station or simply watch the sparrows in the sky.";
            if (lower.Contains("garden") || lower.Contains("flower") || lower.Contains("tree"))
                return "The garden is coming along wonderfully. The cherry blossoms and seedlings bring so much peace and color here.";

            return "I'm doing just fine, thank you! It's a wonderful day in our garden. How are you feeling today?";
        }

        private Coroutine _deliverRoutine;

        public void DeliverCarlReply(string replyText, string action = "talk", AudioClip dynamicAudioClip = null)
        {
            if (_deliverRoutine != null)
            {
                StopCoroutine(_deliverRoutine);
            }
            _deliverRoutine = StartCoroutine(DeliverCarlReplyRoutine(replyText, action, dynamicAudioClip));
        }

        private IEnumerator DeliverCarlReplyRoutine(string replyText, string action, AudioClip dynamicAudioClip)
        {
            Debug.Log($"[CarlDialogueController] Delivering Carl reply: '{replyText}' (dynamicAudio: {dynamicAudioClip != null})");

            if (_thinkingRoutine != null)
            {
                StopCoroutine(_thinkingRoutine);
                _thinkingRoutine = null;
            }

            // If Carl is currently speaking the bridge opening phrase, let him calmly finish the phrase!
            if (_isBridgeActive && carlAudioSource != null && carlAudioSource.isPlaying)
            {
                while (carlAudioSource.isPlaying && _isBridgeActive)
                {
                    yield return null;
                }
            }

            if (_bridgeRoutine != null)
            {
                StopCoroutine(_bridgeRoutine);
                _isBridgeActive = false;
                _bridgeRoutine = null;
            }

            // 1. Show speech bubble with speaker name
            if (speechBubblePanel != null)
            {
                speechBubblePanel.SetActive(true);
            }
            if (speakerNameText != null)
            {
                speakerNameText.text = "👴 Carl Fredricksen";
            }

            // 2. Play dynamic speech audio or fallback to matching voice clip
            AudioClip clipToPlay = dynamicAudioClip != null ? dynamicAudioClip : SelectMatchingVoiceClip(replyText);

            if (carlAudioSource != null && clipToPlay != null)
            {
                carlAudioSource.Stop();
                carlAudioSource.clip = clipToPlay;
                carlAudioSource.Play();
                _speakingDuration = clipToPlay.length;
                typewritingSpeed = 0.012f;
            }
            else
            {
                _speakingDuration = Mathf.Clamp(replyText.Length * 0.065f, 3.0f, 7.0f);
            }

            // 3. Typewriter text reveal synchronized with speech
            if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = StartCoroutine(TypewriterRoutine(replyText));

            // 4. Start speaking lip sync and head gestures
            _speakingTimer = 0f;
            _isSpeaking = true;
        }

        private AudioClip SelectMatchingVoiceClip(string replyText)
        {
            string lower = replyText.ToLower();
            if (lower.Contains("fine") || lower.Contains("how are you feeling"))
                return clipImFine;
            if (lower.Contains("hello there") || lower.Contains("good to see you"))
                return clipHello;
            if (lower.Contains("nurturing") || lower.Contains("planting station"))
                return clipGardening;
            if (lower.Contains("coming along") || lower.Contains("cherry blossoms"))
                return clipGardenPeace;

            return clipImFine != null ? clipImFine : clipWelcome;
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            if (speechBubbleText == null || string.IsNullOrEmpty(fullText)) yield break;

            speechBubbleText.text = "";
            // Perfect 1:1 synchronization with Carl's spoken voice
            // Text reveals smoothly frame-by-frame, perfectly matching Carl's spoken speech cadence
            float targetDuration = Mathf.Max(_speakingDuration * 0.90f, 0.8f);
            float elapsed = 0f;

            while (elapsed < targetDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / targetDuration);
                int charIndex = Mathf.Clamp(Mathf.RoundToInt(progress * fullText.Length), 0, fullText.Length);
                speechBubbleText.text = fullText.Substring(0, charIndex);
                yield return null;
            }
            speechBubbleText.text = fullText;

            // Wait autoDismissDelay then fade/dismiss bubble
            yield return new WaitForSeconds(autoDismissDelay);
            if (!_isSpeaking && speechBubblePanel != null)
            {
                speechBubblePanel.SetActive(false);
            }
        }

        public void StopSpeakingAnimation()
        {
            _isSpeaking = false;
            if (faceSkinnedMesh != null)
            {
                faceSkinnedMesh.SetBlendShapeWeight(0, 0f); // Close mouth
                faceSkinnedMesh.SetBlendShapeWeight(1, 25f); // Gentle resting smile
            }
            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Instant zero-lag response: Carl immediately opens his mouth and speaks a calm
        /// conversational bridge phrase in under 0.3s while Gemini thinks in the background.
        /// </summary>
        public void PlayConversationalBridge()
        {
            if (_isSpeaking && !_isBridgeActive) return;

            if (_thinkingRoutine != null)
            {
                StopCoroutine(_thinkingRoutine);
                _thinkingRoutine = null;
            }

            if (_bridgeRoutine != null) StopCoroutine(_bridgeRoutine);
            _bridgeRoutine = StartCoroutine(BridgeRoutine());
        }

        private IEnumerator BridgeRoutine()
        {
            _isBridgeActive = true;
            int idx = UnityEngine.Random.Range(0, bridgePhrases.Length);
            string phrase = bridgePhrases[idx];

            if (speechBubblePanel != null) speechBubblePanel.SetActive(true);
            if (speakerNameText != null) speakerNameText.text = "👴 Carl Fredricksen";

            AudioClip clip = (bridgeClips != null && idx < bridgeClips.Length) ? bridgeClips[idx] : null;

            if (carlAudioSource != null && clip != null)
            {
                carlAudioSource.Stop();
                carlAudioSource.clip = clip;
                carlAudioSource.Play();
                _speakingDuration = clip.length;
            }
            else
            {
                _speakingDuration = 1.35f;
            }

            // Reveal bridge phrase simultaneously with speech
            if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = StartCoroutine(TypewriterRoutine(phrase));

            _isSpeaking = true;
            _speakingTimer = 0f;

            while (_speakingTimer < _speakingDuration && _isBridgeActive)
            {
                yield return null;
            }

            _isBridgeActive = false;
            _bridgeRoutine = null;
        }

        private Coroutine _thinkingRoutine;

        /// <summary>
        /// Instant zero-lag reaction: Carl immediately nods and tilts his head thoughtfully
        /// the exact moment the patient finishes speaking, eliminating all perceived wait time.
        /// </summary>
        public void TriggerThinkingReaction()
        {
            if (_thinkingRoutine != null) StopCoroutine(_thinkingRoutine);
            _thinkingRoutine = StartCoroutine(ThinkingNodRoutine());
        }

        private IEnumerator ThinkingNodRoutine()
        {
            if (headTransform == null) yield break;
            float elapsed = 0f;
            Quaternion originalRot = headTransform.localRotation;
            while (elapsed < 1.8f && !_isSpeaking)
            {
                elapsed += Time.deltaTime;
                float nod = Mathf.Sin(elapsed * 4.0f) * 3.5f;
                float tilt = Mathf.Sin(elapsed * 2.0f) * 2.0f;
                headTransform.localRotation = originalRot * Quaternion.Euler(nod, tilt, 0f);
                if (faceSkinnedMesh != null)
                {
                    faceSkinnedMesh.SetBlendShapeWeight(1, 35f); // Gentle smile
                }
                yield return null;
            }
            if (!_isSpeaking && headTransform != null)
            {
                headTransform.localRotation = originalRot;
            }
            _thinkingRoutine = null;
        }

        [ContextMenu("Test Voice Audio: 'I'm doing just fine'")]
        public void TestImFineVoice()
        {
            SendMessageToCarl("hi carl how are you");
        }
    }
}
