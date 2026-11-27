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
            Debug.Log("[SetupCarlDialogueUI] Building Carl Dialogue HUD with Hands-Free Voice Recognition...");

            // 1. Locate UI Canvas
            var canvasGo = GameObject.Find("[UI]");
            if (canvasGo == null)
            {
                Debug.LogError("[SetupCarlDialogueUI] [UI] Canvas not found!");
                return;
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            var hud = canvasGo.transform.Find("Patient_HUD");
            Transform parentPanel = hud != null ? hud : canvasGo.transform;

            // 2. Clean previous dialogue UI if present
            var prevChat = parentPanel.Find("Carl_Chat_Panel");
            if (prevChat != null) Undo.DestroyObjectImmediate(prevChat.gameObject);

            var prevBubble = parentPanel.Find("Carl_Speech_Bubble");
            if (prevBubble != null) Undo.DestroyObjectImmediate(prevBubble.gameObject);

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
            // BUILD CARL SPEECH BUBBLE (TOP-CENTER OVER CARL)
            // ========================================================
            var bubbleGo = new GameObject("Carl_Speech_Bubble");
            bubbleGo.transform.SetParent(parentPanel, false);
            var bubbleRt = bubbleGo.AddComponent<RectTransform>();
            bubbleRt.anchorMin = new Vector2(0.5f, 0.72f);
            bubbleRt.anchorMax = new Vector2(0.5f, 0.72f);
            bubbleRt.pivot = new Vector2(0.5f, 0.5f);
            bubbleRt.sizeDelta = new Vector2(620f, 130f);

            var bubbleBg = bubbleGo.AddComponent<Image>();
            bubbleBg.color = new Color(0.08f, 0.12f, 0.16f, 0.90f); // Dark translucent soothing slate

            // Speaker Badge
            var badgeGo = new GameObject("SpeakerBadge");
            badgeGo.transform.SetParent(bubbleGo.transform, false);
            var badgeRt = badgeGo.AddComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(0f, 1f);
            badgeRt.pivot = new Vector2(0f, 1f);
            badgeRt.anchoredPosition = new Vector2(18f, -12f);
            badgeRt.sizeDelta = new Vector2(250f, 26f);

            var badgeText = badgeGo.AddComponent<TextMeshProUGUI>();
            badgeText.text = "👴 Carl Fredricksen";
            badgeText.fontSize = 17;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.color = new Color(0.96f, 0.72f, 0.24f); // Golden amber accent

            // Dialogue Body Text
            var speechTextGo = new GameObject("SpeechText");
            speechTextGo.transform.SetParent(bubbleGo.transform, false);
            var speechRt = speechTextGo.AddComponent<RectTransform>();
            speechRt.anchorMin = new Vector2(0f, 0f);
            speechRt.anchorMax = new Vector2(1f, 1f);
            speechRt.pivot = new Vector2(0.5f, 0.5f);
            speechRt.offsetMin = new Vector2(20f, 12f);
            speechRt.offsetMax = new Vector2(-20f, -38f);

            var speechTmp = speechTextGo.AddComponent<TextMeshProUGUI>();
            speechTmp.text = "I'm doing just fine, thank you! It's a wonderful day in our garden. How are you feeling today?";
            speechTmp.fontSize = 20;
            speechTmp.color = Color.white;
            speechTmp.enableWordWrapping = true;
            speechTmp.alignment = TextAlignmentOptions.TopLeft;

            bubbleGo.SetActive(false); // Initially hidden

            // ========================================================
            // BUILD CARL CHAT & VOICE PANEL (BOTTOM-CENTER OF SCREEN)
            // ========================================================
            var chatPanelGo = new GameObject("Carl_Chat_Panel");
            chatPanelGo.transform.SetParent(parentPanel, false);
            var chatRt = chatPanelGo.AddComponent<RectTransform>();
            chatRt.anchorMin = new Vector2(0.5f, 0f);
            chatRt.anchorMax = new Vector2(0.5f, 0f);
            chatRt.pivot = new Vector2(0.5f, 0f);
            chatRt.anchoredPosition = new Vector2(0f, 20f);
            chatRt.sizeDelta = new Vector2(800f, 140f);

            var chatBg = chatPanelGo.AddComponent<Image>();
            chatBg.color = new Color(0.06f, 0.09f, 0.13f, 0.88f); // Deep soothing panel

            // 1. Hands-Free Voice Status Banner (Top of Panel)
            var voiceStatusGo = new GameObject("VoiceStatusBanner");
            voiceStatusGo.transform.SetParent(chatPanelGo.transform, false);
            var voiceStatusRt = voiceStatusGo.AddComponent<RectTransform>();
            voiceStatusRt.anchorMin = new Vector2(0f, 1f);
            voiceStatusRt.anchorMax = new Vector2(1f, 1f);
            voiceStatusRt.pivot = new Vector2(0.5f, 1f);
            voiceStatusRt.anchoredPosition = new Vector2(0f, -8f);
            voiceStatusRt.sizeDelta = new Vector2(0f, 28f);

            var voiceStatusTmp = voiceStatusGo.AddComponent<TextMeshProUGUI>();
            voiceStatusTmp.text = "🎙️ Voice Active — Speak to Carl anytime! (Say: \"Hi Carl, how are you?\")";
            voiceStatusTmp.fontSize = 14;
            voiceStatusTmp.fontStyle = FontStyles.Bold;
            voiceStatusTmp.color = new Color(0.25f, 0.85f, 0.55f); // Gentle reassuring green
            voiceStatusTmp.alignment = TextAlignmentOptions.Center;

            // 2. Quick Speak Pills Row
            var pillsRowGo = new GameObject("QuickPillsRow");
            pillsRowGo.transform.SetParent(chatPanelGo.transform, false);
            var pillsRt = pillsRowGo.AddComponent<RectTransform>();
            pillsRt.anchorMin = new Vector2(0f, 1f);
            pillsRt.anchorMax = new Vector2(1f, 1f);
            pillsRt.pivot = new Vector2(0.5f, 1f);
            pillsRt.anchoredPosition = new Vector2(0f, -38f);
            pillsRt.sizeDelta = new Vector2(0f, 34f);

            var pillsLayout = pillsRowGo.AddComponent<HorizontalLayoutGroup>();
            pillsLayout.childAlignment = TextAnchor.MiddleCenter;
            pillsLayout.spacing = 10f;
            pillsLayout.childForceExpandWidth = true;
            pillsLayout.childForceExpandHeight = true;
            pillsLayout.padding = new RectOffset(16, 16, 0, 0);

            var pill1 = CreatePillButton(pillsRowGo.transform, "Pill_HiCarl", "💬 Hi Carl, how are you?");
            var pill2 = CreatePillButton(pillsRowGo.transform, "Pill_WhatDo", "🌱 What are we doing today?");
            var pill3 = CreatePillButton(pillsRowGo.transform, "Pill_Garden", "🌸 Tell me about the garden");

            // 3. Input & Send Row
            var inputRowGo = new GameObject("InputRow");
            inputRowGo.transform.SetParent(chatPanelGo.transform, false);
            var inputRowRt = inputRowGo.AddComponent<RectTransform>();
            inputRowRt.anchorMin = new Vector2(0f, 0f);
            inputRowRt.anchorMax = new Vector2(1f, 0f);
            inputRowRt.pivot = new Vector2(0.5f, 0f);
            inputRowRt.anchoredPosition = new Vector2(0f, 12f);
            inputRowRt.sizeDelta = new Vector2(-32f, 44f);

            // Input Field
            var inputGo = new GameObject("InputField");
            inputGo.transform.SetParent(inputRowGo.transform, false);
            var inputRt = inputGo.AddComponent<RectTransform>();
            inputRt.anchorMin = new Vector2(0f, 0f);
            inputRt.anchorMax = new Vector2(1f, 1f);
            inputRt.pivot = new Vector2(0f, 0.5f);
            inputRt.offsetMin = new Vector2(0f, 0f);
            inputRt.offsetMax = new Vector2(-120f, 0f);

            var inputImg = inputGo.AddComponent<Image>();
            inputImg.color = new Color(0.14f, 0.18f, 0.24f, 0.95f);

            var tmpInput = inputGo.AddComponent<TMP_InputField>();

            // Text Area
            var textAreaGo = new GameObject("Text Area");
            textAreaGo.transform.SetParent(inputGo.transform, false);
            var textAreaRt = textAreaGo.AddComponent<RectTransform>();
            textAreaRt.anchorMin = Vector2.zero;
            textAreaRt.anchorMax = Vector2.one;
            textAreaRt.offsetMin = new Vector2(14f, 4f);
            textAreaRt.offsetMax = new Vector2(-14f, -4f);
            var mask = textAreaGo.AddComponent<RectMask2D>();

            // Placeholder Text
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textAreaGo.transform, false);
            var placeholderRt = placeholderGo.AddComponent<RectTransform>();
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = Vector2.zero;
            placeholderRt.offsetMax = Vector2.zero;
            var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.text = "Speak into mic or type here (e.g. 'hi carl how are u')...";
            placeholderText.fontSize = 15;
            placeholderText.fontStyle = FontStyles.Italic;
            placeholderText.color = new Color(0.6f, 0.65f, 0.72f);
            placeholderText.alignment = TextAlignmentOptions.MidlineLeft;

            // Main Input Text
            var inputTextGo = new GameObject("Text");
            inputTextGo.transform.SetParent(textAreaGo.transform, false);
            var inputTextRt = inputTextGo.AddComponent<RectTransform>();
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = Vector2.zero;
            inputTextRt.offsetMax = Vector2.zero;
            var inputText = inputTextGo.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 17;
            inputText.color = Color.white;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;

            tmpInput.textViewport = textAreaRt;
            tmpInput.textComponent = inputText;
            tmpInput.placeholder = placeholderText;

            // Send Button
            var sendBtnGo = new GameObject("Btn_Send");
            sendBtnGo.transform.SetParent(inputRowGo.transform, false);
            var sendRt = sendBtnGo.AddComponent<RectTransform>();
            sendRt.anchorMin = new Vector2(1f, 0f);
            sendRt.anchorMax = new Vector2(1f, 1f);
            sendRt.pivot = new Vector2(1f, 0.5f);
            sendRt.anchoredPosition = Vector2.zero;
            sendRt.sizeDelta = new Vector2(110f, 44f);

            var sendImg = sendBtnGo.AddComponent<Image>();
            sendImg.color = new Color(0.18f, 0.52f, 0.38f); // Soft forest green
            var sendBtn = sendBtnGo.AddComponent<Button>();

            var sendTxtGo = new GameObject("Text");
            sendTxtGo.transform.SetParent(sendBtnGo.transform, false);
            var sendTxtRt = sendTxtGo.AddComponent<RectTransform>();
            sendTxtRt.anchorMin = Vector2.zero;
            sendTxtRt.anchorMax = Vector2.one;
            var sendTmp = sendTxtGo.AddComponent<TextMeshProUGUI>();
            sendTmp.text = "Send ➔";
            sendTmp.fontSize = 16;
            sendTmp.fontStyle = FontStyles.Bold;
            sendTmp.color = Color.white;
            sendTmp.alignment = TextAlignmentOptions.Center;

            // ========================================================
            // ATTACH & CONFIGURE CarlDialogueController ON CARL
            // ========================================================
            var dialogueCtrl = carlGo.GetComponent<CarlDialogueController>();
            if (dialogueCtrl == null) dialogueCtrl = carlGo.AddComponent<CarlDialogueController>();

            dialogueCtrl.messageInputField = tmpInput;
            dialogueCtrl.sendButton = sendBtn;
            dialogueCtrl.quickPill1 = pill1;
            dialogueCtrl.quickPill2 = pill2;
            dialogueCtrl.quickPill3 = pill3;

            dialogueCtrl.speechBubblePanel = bubbleGo;
            dialogueCtrl.speechBubbleText = speechTmp;
            dialogueCtrl.speakerNameText = badgeText;

            var audioSrc = carlGo.GetComponent<AudioSource>();
            if (audioSrc == null) audioSrc = carlGo.AddComponent<AudioSource>();
            dialogueCtrl.carlAudioSource = audioSrc;

            var voiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_joyful_greeting.wav");
            if (voiceClip == null) voiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/carl_speech_sample.wav");
            dialogueCtrl.talkingVoiceClip = voiceClip;

            dialogueCtrl.faceSkinnedMesh = carlGo.GetComponentInChildren<SkinnedMeshRenderer>();

            var head = carlGo.transform.Find("CarlHead") ?? carlGo.transform.Find("Head");
            if (head != null) dialogueCtrl.headTransform = head;

            // ========================================================
            // ATTACH & CONFIGURE CarlVoiceListener (MICROPHONE RECOGNIZER)
            // ========================================================
            var voiceListener = carlGo.GetComponent<CarlVoiceListener>();
            if (voiceListener == null) voiceListener = carlGo.AddComponent<CarlVoiceListener>();

            voiceListener.dialogueController = dialogueCtrl;
            voiceListener.micStatusText = voiceStatusTmp;
            voiceListener.startListeningOnAwake = true;

            // Mark Scene Dirty and Save
            EditorUtility.SetDirty(carlGo);
            EditorUtility.SetDirty(chatPanelGo);
            EditorUtility.SetDirty(bubbleGo);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("[SetupCarlDialogueUI] Carl Hands-Free Voice & Dialogue System configured and saved successfully!");
        }

        private static Button CreatePillButton(Transform parent, string name, string label)
        {
            var btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.16f, 0.22f, 0.30f, 0.90f); // Slate pill
            var btn = btnGo.AddComponent<Button>();

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.color = new Color(0.90f, 0.93f, 0.96f);
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }
    }
}
