using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using NeuroCompanion.Carl;

namespace NeuroCompanion.Backend.Editor
{
    public static class SetupCarlDialogueUI
    {
        [MenuItem("NeuroCompanion/Setup Carl Dialogue System")]
        public static void Setup()
        {
            Debug.Log("[SetupCarlDialogueUI] Configuring Clean Voice-Only Communication with Matching Voice Audio...");

            // 1. Locate UI Canvas
            var canvasGo = GameObject.Find("[UI]");
            if (canvasGo == null)
            {
                Debug.LogError("[SetupCarlDialogueUI] [UI] Canvas not found!");
                return;
            }

            var hud = canvasGo.transform.Find("Patient_HUD");
            if (hud != null) hud.gameObject.SetActive(true);
            Transform parentPanel = canvasGo.transform;

            // 2. Remove bottom chat panel completely
            var prevChat = parentPanel.Find("Carl_Chat_Panel");
            if (prevChat != null) Undo.DestroyObjectImmediate(prevChat.gameObject);

            var prevChatUnderCanvas = canvasGo.transform.Find("Carl_Chat_Panel");
            if (prevChatUnderCanvas != null) Undo.DestroyObjectImmediate(prevChatUnderCanvas.gameObject);

            // 3. Locate Carl_Fredricksen
            var gardenRoot = GameObject.Find("[GARDEN_GAME]");
            if (gardenRoot == null)
            {
                foreach (var r in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (r.name == "[GARDEN_GAME]" && r.scene.IsValid()) { gardenRoot = r; break; }
                }
            }

            GameObject carlGo = null;
            if (gardenRoot != null)
            {
                var carlT = gardenRoot.transform.Find("Carl_Fredricksen");
                if (carlT != null) carlGo = carlT.gameObject;
            }

            if (carlGo == null)
            {
                Debug.LogError("[SetupCarlDialogueUI] Carl_Fredricksen not found under [GARDEN_GAME]!");
                return;
            }

            // ========================================================
            // 4. CLEAN CARL SPEECH BUBBLE (ONLY APPEARS WHEN CARL SPEAKS)
            // ========================================================
            var prevBubble = parentPanel.Find("Carl_Speech_Bubble");
            if (prevBubble != null) Undo.DestroyObjectImmediate(prevBubble.gameObject);

            var bubbleGo = new GameObject("Carl_Speech_Bubble");
            bubbleGo.transform.SetParent(parentPanel, false);
            var bubbleRt = bubbleGo.AddComponent<RectTransform>();
            bubbleRt.anchorMin = new Vector2(0.5f, 0.76f);
            bubbleRt.anchorMax = new Vector2(0.5f, 0.76f);
            bubbleRt.pivot = new Vector2(0.5f, 0.5f);
            bubbleRt.sizeDelta = new Vector2(580f, 110f);

            var bubbleBg = bubbleGo.AddComponent<Image>();
            bubbleBg.color = new Color(0.08f, 0.12f, 0.16f, 0.88f);

            // Speaker Badge
            var badgeGo = new GameObject("SpeakerBadge");
            badgeGo.transform.SetParent(bubbleGo.transform, false);
            var badgeRt = badgeGo.AddComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(0f, 1f);
            badgeRt.pivot = new Vector2(0f, 1f);
            badgeRt.anchoredPosition = new Vector2(16f, -10f);
            badgeRt.sizeDelta = new Vector2(240f, 24f);

            var badgeText = badgeGo.AddComponent<TextMeshProUGUI>();
            badgeText.text = "👴 Carl Fredricksen";
            badgeText.fontSize = 16;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.color = new Color(0.96f, 0.72f, 0.24f);

            // Dialogue Body Text
            var speechTextGo = new GameObject("SpeechText");
            speechTextGo.transform.SetParent(bubbleGo.transform, false);
            var speechRt = speechTextGo.AddComponent<RectTransform>();
            speechRt.anchorMin = new Vector2(0f, 0f);
            speechRt.anchorMax = new Vector2(1f, 1f);
            speechRt.pivot = new Vector2(0.5f, 0.5f);
            speechRt.offsetMin = new Vector2(18f, 10f);
            speechRt.offsetMax = new Vector2(-18f, -34f);

            var speechTmp = speechTextGo.AddComponent<TextMeshProUGUI>();
            speechTmp.text = "I'm doing just fine, thank you! It's a wonderful day in our garden. How are you feeling today?";
            speechTmp.fontSize = 19;
            speechTmp.color = Color.white;
            speechTmp.enableWordWrapping = true;
            speechTmp.alignment = TextAlignmentOptions.TopLeft;

            bubbleGo.SetActive(false); // Hidden by default, only appears when Carl speaks

            // ========================================================
            // 5. SUBTLE TOP-RIGHT MICROPHONE STATUS PILL
            // ========================================================
            var prevMicBadge = parentPanel.Find("Voice_Status_Badge");
            if (prevMicBadge != null) Undo.DestroyObjectImmediate(prevMicBadge.gameObject);

            var micBadgeGo = new GameObject("Voice_Status_Badge");
            micBadgeGo.transform.SetParent(parentPanel, false);
            var micBadgeRt = micBadgeGo.AddComponent<RectTransform>();
            micBadgeRt.anchorMin = new Vector2(0.5f, 1f);
            micBadgeRt.anchorMax = new Vector2(0.5f, 1f);
            micBadgeRt.pivot = new Vector2(0.5f, 1f);
            micBadgeRt.anchoredPosition = new Vector2(0f, -75f);
            micBadgeRt.sizeDelta = new Vector2(460f, 38f);

            var micBadgeBg = micBadgeGo.AddComponent<Image>();
            micBadgeBg.color = new Color(0.06f, 0.10f, 0.14f, 0.75f);

            var micTxtGo = new GameObject("Text");
            micTxtGo.transform.SetParent(micBadgeGo.transform, false);
            var micTxtRt = micTxtGo.AddComponent<RectTransform>();
            micTxtRt.anchorMin = Vector2.zero;
            micTxtRt.anchorMax = Vector2.one;
            var micStatusTmp = micTxtGo.AddComponent<TextMeshProUGUI>();
            micStatusTmp.text = "🎙️ Mic Active: Say \"Hi Carl\"";
            micStatusTmp.fontSize = 14;
            micStatusTmp.fontStyle = FontStyles.Bold;
            micStatusTmp.color = new Color(0.25f, 0.85f, 0.55f);
            micStatusTmp.alignment = TextAlignmentOptions.Center;

            // ========================================================
            // 6. CONFIGURE CarlDialogueController WITH MATCHING VOICE CLIPS
            // ========================================================
            var dialogueCtrl = carlGo.GetComponent<CarlDialogueController>();
            if (dialogueCtrl == null) dialogueCtrl = carlGo.AddComponent<CarlDialogueController>();

            dialogueCtrl.messageInputField = null;
            dialogueCtrl.sendButton = null;
            dialogueCtrl.quickPill1 = null;
            dialogueCtrl.quickPill2 = null;
            dialogueCtrl.quickPill3 = null;

            dialogueCtrl.speechBubblePanel = bubbleGo;
            dialogueCtrl.speechBubbleText = speechTmp;
            dialogueCtrl.speakerNameText = badgeText;

            var audioSrc = carlGo.GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = carlGo.AddComponent<AudioSource>();
            dialogueCtrl.carlAudioSource = audioSrc;

            // Load and assign all matching voice clips
            dialogueCtrl.clipImFine = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_im_fine.wav");
            dialogueCtrl.clipHello = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_hello.wav");
            dialogueCtrl.clipGardening = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_gardening.wav");
            dialogueCtrl.clipGardenPeace = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_garden_peace.wav");
            dialogueCtrl.clipWelcome = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_joyful_greeting.wav");

            dialogueCtrl.faceSkinnedMesh = carlGo.GetComponentInChildren<SkinnedMeshRenderer>();

            var head = carlGo.transform.Find("CarlHead") ?? carlGo.transform.Find("Head");
            if (head != null) dialogueCtrl.headTransform = head;

            // ========================================================
            // 7. CONFIGURE CarlVoiceListener (MICROPHONE LISTENER)
            // ========================================================
            var voiceListener = carlGo.GetComponent<CarlVoiceListener>();
            if (voiceListener == null) voiceListener = carlGo.AddComponent<CarlVoiceListener>();

            voiceListener.dialogueController = dialogueCtrl;
            voiceListener.micStatusText = micStatusTmp;
            voiceListener.startListeningOnAwake = true;

            // Mark Scene Dirty and Save
            EditorUtility.SetDirty(carlGo);
            EditorUtility.SetDirty(bubbleGo);
            EditorUtility.SetDirty(micBadgeGo);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[SetupCarlDialogueUI] Configured matching voice audio: carl_im_fine.wav will now play when Carl responds!");
        }
    }
}
