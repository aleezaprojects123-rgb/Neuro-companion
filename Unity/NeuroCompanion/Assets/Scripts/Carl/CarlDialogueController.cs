// ============================================================
// CarlDialogueController.cs — Unity 6.3 LTS
// NeuroCompanion — Interactive Carl Companion Dialogue System
//
// Enables patients and caregivers to talk directly to Carl Fredricksen:
// 1. Text chat input ("hi carl how are u") & 1-click quick-speak pills.
// 2. HTTP POST communication with backend (/api/v1/carl/chat) + offline fallback.
// 3. Dynamic Carl speech bubble popup with typewriter text reveal.
// 4. Procedural lip sync (mouth open/close) and conversational head nodding.
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

        [Header("UI References — Chat Bar")]
        public TMP_InputField messageInputField;
        public Button sendButton;
        public Button quickPill1; // "Hi Carl, how are you?"
        public Button quickPill2; // "What are we doing today?"
        public Button quickPill3; // "Tell me about the garden"

        [Header("UI References — Carl Speech Bubble")]
        public GameObject speechBubblePanel;
        public TMP_Text speechBubbleText;
        public TMP_Text speakerNameText;

        [Header("Carl Model & Lip Sync References")]
        public AudioSource carlAudioSource;
        public AudioClip talkingVoiceClip;
        public SkinnedMeshRenderer faceSkinnedMesh;
        public Transform headTransform;

        [Header("Dialogue Animation Parameters")]
        public float typewritingSpeed = 0.025f;
        public float autoDismissDelay = 8.0f;
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
        }

        private void Start()
        {
            // Auto-locate references if unassigned
            if (carlAudioSource == null) carlAudioSource = GetComponent<AudioSource>();
            if (faceSkinnedMesh == null) faceSkinnedMesh = GetComponentInChildren<SkinnedMeshRenderer>();

            // Setup input field listeners
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendButtonClicked);
            }

            if (messageInputField != null)
            {
                messageInputField.onSubmit.AddListener((val) => OnSendButtonClicked());
            }

            // Setup Quick Pill Listeners
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

            var payload = new ChatPayload { message = userText, patient_id = patientId };
            string jsonBody = JsonUtility.ToJson(payload);

            using (UnityWebRequest req = new UnityWebRequest(chatEndpointUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 3; // 3 sec timeout

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string jsonResp = req.downloadHandler.text;
                    var resp = JsonUtility.FromJson<ChatResponseData>(jsonResp);
                    if (resp != null && !string.IsNullOrEmpty(resp.reply))
                    {
                        reply = resp.reply;
                        action = resp.action;
                    }
                }
                else
                {
                    Debug.LogWarning($"[CarlDialogueController] Backend chat returned {req.error}. Using intelligent local fallback.");
                    reply = GetLocalFallbackReply(userText);
                }
            }

            if (string.IsNullOrEmpty(reply))
            {
                reply = GetLocalFallbackReply(userText);
            }

            // Deliver Carl's response visually and with voice/lip-sync
            DeliverCarlReply(reply, action);
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

            return "I'm right here with you, enjoying every moment in our peaceful garden.";
        }

        public void DeliverCarlReply(string replyText, string action = "talk")
        {
            Debug.Log($"[CarlDialogueController] Carl replies: '{replyText}'");

            // 1. Show speech bubble with speaker name
            if (speechBubblePanel != null)
            {
                speechBubblePanel.SetActive(true);
            }
            if (speakerNameText != null)
            {
                speakerNameText.text = "Carl Fredricksen";
            }

            // 2. Typewriter text reveal
            if (_typewriterRoutine != null) StopCoroutine(_typewriterRoutine);
            _typewriterRoutine = StartCoroutine(TypewriterRoutine(replyText));

            // 3. Trigger voice audio
            if (carlAudioSource != null && talkingVoiceClip != null)
            {
                carlAudioSource.Stop();
                carlAudioSource.clip = talkingVoiceClip;
                carlAudioSource.Play();
            }

            // 4. Start speaking lip sync and head gestures
            // Calculate speaking duration based on text length (~14-16 chars per second)
            _speakingDuration = Mathf.Clamp(replyText.Length * 0.065f, 2.5f, 7.0f);
            _speakingTimer = 0f;
            _isSpeaking = true;
        }

        private IEnumerator TypewriterRoutine(string fullText)
        {
            if (speechBubbleText == null) yield break;

            speechBubbleText.text = "";
            for (int i = 0; i <= fullText.Length; i++)
            {
                speechBubbleText.text = fullText.Substring(0, i);
                yield return new WaitForSeconds(typewritingSpeed);
            }

            // Wait autoDismissDelay then fade/dismiss bubble
            yield return new WaitForSeconds(autoDismissDelay);
            if (!_isSpeaking && speechBubblePanel != null)
            {
                speechBubblePanel.SetActive(false);
            }
        }

        private void StopSpeakingAnimation()
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

        [ContextMenu("Test 'Hi Carl how are u'")]
        public void TestGreeting()
        {
            SendMessageToCarl("hi carl how are u");
        }
    }
}
