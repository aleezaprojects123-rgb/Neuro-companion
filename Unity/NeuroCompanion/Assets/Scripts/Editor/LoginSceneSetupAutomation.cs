// LoginSceneSetupAutomation.cs  v9
// Complete 2D & 3D NeuroCompanion Experience:
//   1. Pristine Full-screen Garden Background (for Login only)
//   2. High-Readability Glass Login Card
//   3. Dedicated 2D/3D Clinical Dashboard (WITHOUT garden background):
//      - Sleek modern studio/clinical dark backdrop
//      - 3D tactile buttons with physical sink-down press depth (Button3D)
//      - 3D floating glass cards with diffuse ambient drop shadows
//      - 3D cylindrical specular chart bars with depth shadows
//      - OASIS-1 MMSE Cognitive Screener & Caregiver Clinical Insights
//      - Interactive Weekly (7D) & Monthly (4W) Performance Analytics

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NeuroCompanion.Backend.Editor
{
    public static class LoginSceneSetupAutomation
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string TexPath   = "Assets/Textures/Login/";
        private static TMP_FontAsset s_font;

        // Visual Palette
        private static readonly Color DarkGreen    = new Color(0.082f, 0.278f, 0.204f, 1f); // #154734
        private static readonly Color MidGreen     = new Color(0.18f,  0.33f,  0.25f,  1f);
        private static readonly Color CardCream    = new Color(0.984f, 0.957f, 0.918f, 0.98f); // #FBF4EA
        private static readonly Color CardCreamSoft= new Color(0.96f,  0.94f,  0.90f,  0.95f);
        private static readonly Color DividerCol   = new Color(0.80f,  0.87f,  0.83f,  1f);
        private static readonly Color PHColor      = new Color(0.52f,  0.58f,  0.55f,  0.85f);
        private static readonly Color InputText    = new Color(0.06f,  0.14f,  0.09f,  1f);
        private static readonly Color SagePill     = new Color(0.706f, 0.780f, 0.737f, 1f); // #B4C7BC
        private static readonly Color LightSage    = new Color(0.90f,  0.94f,  0.91f,  1f);

        [MenuItem("Tools/NeuroCompanion/Build Functional Login Scene")]
        public static void BuildLoginScene()
        {
            Debug.Log("[LoginSetup] v9 starting — building complete 2D/3D Login & Dashboard experience...");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1. CLEANUP PREVIOUS LOGIN & DASHBOARD OBJECTS
            var uiRoot = GameObject.Find("[UI]");
            if (uiRoot == null) uiRoot = new GameObject("[UI]");

            string[] toClean = {
                "Login_Background", "Login_Left_Panel", "Login_Card_Panel",
                "Register_Modal_Panel", "ForgotPassword_Modal_Panel", "Dashboard_Panel"
            };
            foreach (var name in toClean)
            {
                var existing = uiRoot.transform.Find(name);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
                var stray = GameObject.Find(name);
                if (stray != null && stray != uiRoot) Object.DestroyImmediate(stray);
            }

            // 2. LOAD DEFAULT TMP FONT
            s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (s_font == null) s_font = TMP_Settings.defaultFontAsset;
            if (s_font == null)
            {
                var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
                if (guids.Length > 0)
                    s_font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // 3. LOAD TEXTURES AND SPRITES
            var bgSpr        = LoadSpr(TexPath + "LoginBackground.jpg");
            var cardSpr      = LoadSpr(TexPath + "card_bg_cream.png");
            var inputSpr     = LoadSpr(TexPath + "input_bg.png");
            var btnGreen     = LoadSpr(TexPath + "btn_green_pill.png");
            var btnOutline   = LoadSpr(TexPath + "btn_outline_pill.png");
            var btnSage      = LoadSpr(TexPath + "btn_sage_pill.png");
            var eyeOpen      = LoadSpr(TexPath + "icon_eye_open.png");
            var eyeClosed    = LoadSpr(TexPath + "icon_eye_closed.png");
            var badgeEmail   = LoadSpr(TexPath + "badge_email.png");
            var badgePassword= LoadSpr(TexPath + "badge_password.png");
            var iconSignin   = LoadSpr(TexPath + "icon_signin.png");
            var iconUserPlus = LoadSpr(TexPath + "icon_user_plus.png");
            var iconUser     = LoadSpr(TexPath + "icon_user.png");
            var iconShield   = LoadSpr(TexPath + "icon_shield.png");

            // 3D Texture Suite
            var btn3dGreen   = LoadSpr(TexPath + "btn_3d_green.png");
            var btn3dSage    = LoadSpr(TexPath + "btn_3d_sage.png");
            var btn3dWhite   = LoadSpr(TexPath + "btn_3d_white.png");
            var btn3dAmber   = LoadSpr(TexPath + "btn_3d_amber.png");
            var bar3d        = LoadSpr(TexPath + "bar_3d_cylinder_glow.png") ?? LoadSpr(TexPath + "bar_3d_cylinder.png");
            var card3d       = LoadSpr(TexPath + "card_cyber_glass.png") ?? LoadSpr(TexPath + "card_3d_glass.png");
            var cardKpi      = LoadSpr(TexPath + "card_kpi_3d.png") ?? card3d;
            var gaugeTrack   = LoadSpr(TexPath + "gauge_track_3d.png");
            var gaugeFillGrn = LoadSpr(TexPath + "gauge_fill_green.png");
            var gaugeFillAmb = LoadSpr(TexPath + "gauge_fill_amber.png");
            var gaugeFillCor = LoadSpr(TexPath + "gauge_fill_coral.png");
            var btn3dEmerald = LoadSpr(TexPath + "btn_3d_emerald.png") ?? btn3dGreen;
            var btn3dDark    = LoadSpr(TexPath + "btn_3d_dark.png") ?? btn3dWhite;
            var studioBgSpr  = LoadSpr(TexPath + "dashboard_bg_studio.jpg");

            // Cream & Green 3D texture suite (dashboard only)
            var card3dCream      = LoadSpr(TexPath + "card_3d_cream.png");
            var cardKpiCream     = LoadSpr(TexPath + "card_kpi_cream.png");
            var gaugeTrackCream  = LoadSpr(TexPath + "gauge_track_cream.png");
            var chartCanvasCream = LoadSpr(TexPath + "chart_canvas_cream.png");

            // Flat Clean Login Texture Suite (Login Screen Only)
            var cardLoginCream   = LoadSpr(TexPath + "card_login_cream.png") ?? cardSpr;
            var inputFieldFlat   = LoadSpr(TexPath + "input_field_flat.png") ?? inputSpr;
            var btnForestFlat    = LoadSpr(TexPath + "btn_primary_forest.png") ?? btnGreen;
            var btnIvoryFlat     = LoadSpr(TexPath + "btn_secondary_ivory.png") ?? btnOutline;
            var btnSageFlat      = LoadSpr(TexPath + "btn_tertiary_sage.png") ?? btnSage;

            // 4. EVENT SYSTEM
            if (GameObject.Find("EventSystem") == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // 5. CANVAS & RESPONSIVE SCALER
            var cv = Ensure<Canvas>(uiRoot);
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 0;

            var sc = Ensure<CanvasScaler>(uiRoot);
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight  = 0.5f;

            Ensure<GraphicRaycaster>(uiRoot);
            uiRoot.transform.localScale = Vector3.one;

            // 6. FULL-SCREEN BACKGROUND (For Login Only)
            var bgGo = Child(uiRoot, "Login_Background");
            Stretch(bgGo);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.sprite = bgSpr;
            bgImg.type = Image.Type.Simple;
            bgImg.preserveAspect = false;
            bgImg.color = Color.white;
            bgImg.raycastTarget = false;
            bgGo.transform.SetAsFirstSibling();

            // 7. LOGIN CARD PANEL (Refined Minimalist Healthcare Design)
            var cardGo = Child(uiRoot, "Login_Card_Panel");
            Anchors(cardGo, new Vector2(0.535f, 0.050f), new Vector2(0.965f, 0.950f));
            var cardImg = cardGo.AddComponent<Image>();
            cardImg.sprite = cardLoginCream != null ? cardLoginCream : cardSpr;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = Color.white;
            cardGo.transform.SetSiblingIndex(1);

            var loginCtrl = cardGo.AddComponent<LoginController>();

            // Header: "Welcome back"
            var hGo = Child(cardGo, "Txt_WelcomeBack");
            Anchors(hGo, new Vector2(0.08f, 0.885f), new Vector2(0.92f, 0.945f));
            Lbl(hGo, "Welcome back", 32, DarkGreen, TextAlignmentOptions.Center, true);

            // Subtitle: "Sign in to continue"
            var sGo = Child(cardGo, "Txt_Subtitle");
            Anchors(sGo, new Vector2(0.08f, 0.840f), new Vector2(0.92f, 0.880f));
            Lbl(sGo, "Sign in to continue", 16, MidGreen, TextAlignmentOptions.Center, false);

            // ── EMAIL FIELD ──
            var eLbl = Child(cardGo, "Lbl_Email");
            Anchors(eLbl, new Vector2(0.08f, 0.775f), new Vector2(0.92f, 0.810f));
            Lbl(eLbl, "Email address", 15, DarkGreen, TextAlignmentOptions.Left, true);

            var eInputGo = Child(cardGo, "Input_Email");
            Anchors(eInputGo, new Vector2(0.08f, 0.705f), new Vector2(0.92f, 0.770f));
            var eInputImg = eInputGo.AddComponent<Image>();
            eInputImg.sprite = inputFieldFlat != null ? inputFieldFlat : inputSpr;
            eInputImg.type = Image.Type.Sliced;
            eInputImg.color = Color.white;
            var emailInput = LoginInputField(eInputGo, "you@example.com", TMP_InputField.ContentType.EmailAddress, badgeEmail);

            // ── PASSWORD FIELD ──
            var pLbl = Child(cardGo, "Lbl_Password");
            Anchors(pLbl, new Vector2(0.08f, 0.640f), new Vector2(0.92f, 0.675f));
            Lbl(pLbl, "Password", 15, DarkGreen, TextAlignmentOptions.Left, true);

            var pInputGo = Child(cardGo, "Input_Password");
            Anchors(pInputGo, new Vector2(0.08f, 0.570f), new Vector2(0.92f, 0.635f));
            var pInputImg = pInputGo.AddComponent<Image>();
            pInputImg.sprite = inputFieldFlat != null ? inputFieldFlat : inputSpr;
            pInputImg.type = Image.Type.Sliced;
            pInputImg.color = Color.white;
            var pwInput = LoginInputField(pInputGo, "••••••••", TMP_InputField.ContentType.Password, badgePassword);

            // Password Eye Toggle Button
            var eyeGo = Child(pInputGo, "Btn_ToggleEye");
            SetRect(eyeGo, new Vector2(0.90f, 0.18f), new Vector2(0.97f, 0.82f));
            var eyeImg = eyeGo.AddComponent<Image>();
            eyeImg.sprite = eyeOpen;
            eyeImg.preserveAspect = true;
            eyeImg.color = MidGreen;
            var eyeBtn = eyeGo.AddComponent<Button>();

            // ── FORGOT PASSWORD LINK ──
            var forgotGo = Child(cardGo, "Btn_ForgotPassword");
            Anchors(forgotGo, new Vector2(0.50f, 0.522f), new Vector2(0.92f, 0.555f));
            Lbl(forgotGo, "<u>Forgot password?</u>", 14, new Color(0.12f, 0.40f, 0.28f, 1f), TextAlignmentOptions.Right, true);
            var forgotBg = Child(forgotGo, "Bg");
            Stretch(forgotBg);
            var forgotBgImg = forgotBg.AddComponent<Image>();
            forgotBgImg.color = Color.clear;
            var forgotBtn = forgotGo.AddComponent<Button>();
            forgotBtn.targetGraphic = forgotBgImg;

            // ── STATUS MESSAGE & LOADING ──
            var statusGo = Child(cardGo, "Txt_StatusMessage");
            Anchors(statusGo, new Vector2(0.08f, 0.480f), new Vector2(0.92f, 0.515f));
            var statusTxt = Lbl(statusGo, "", 14, new Color(0.8f, 0.15f, 0.15f, 1f), TextAlignmentOptions.Center, false);
            statusGo.SetActive(false);

            var loadGo = Child(cardGo, "Loading_Indicator");
            Anchors(loadGo, new Vector2(0.08f, 0.480f), new Vector2(0.92f, 0.515f));
            var loadTxt = Lbl(loadGo, "Connecting to NeuroCompanion...", 14, MidGreen, TextAlignmentOptions.Center, false);
            loadTxt.fontStyle = FontStyles.Italic;
            loadGo.SetActive(false);

            // ── PRIMARY SIGN IN BUTTON ──
            var signInGo = Child(cardGo, "Btn_SignIn");
            Anchors(signInGo, new Vector2(0.08f, 0.405f), new Vector2(0.92f, 0.470f));
            var siImg = signInGo.AddComponent<Image>();
            siImg.sprite = btnForestFlat != null ? btnForestFlat : btnGreen;
            siImg.type = Image.Type.Sliced;
            siImg.color = Color.white;
            var signInBtn = signInGo.AddComponent<Button>();

            var siTxtGo = Child(signInGo, "Txt");
            Stretch(siTxtGo);
            Lbl(siTxtGo, "Sign In", 19, Color.white, TextAlignmentOptions.Center, true);

            // ── OR DIVIDER ──
            var orGo = Child(cardGo, "Or_Divider");
            Anchors(orGo, new Vector2(0.08f, 0.355f), new Vector2(0.92f, 0.385f));
            var orLayout = orGo.AddComponent<HorizontalLayoutGroup>();
            orLayout.childAlignment = TextAnchor.MiddleCenter;
            orLayout.spacing = 14f;
            orLayout.childForceExpandWidth = false;
            orLayout.childForceExpandHeight = false;

            var orLineL = Child(orGo, "LineL");
            SetSize(orLineL, 140, 1);
            orLineL.AddComponent<Image>().color = new Color(0.82f, 0.86f, 0.83f, 0.9f);

            var orTxtGo = Child(orGo, "Txt");
            SetSize(orTxtGo, 24, 20);
            Lbl(orTxtGo, "or", 14, new Color(0.48f, 0.56f, 0.52f, 1f), TextAlignmentOptions.Center, false);

            var orLineR = Child(orGo, "LineR");
            SetSize(orLineR, 140, 1);
            orLineR.AddComponent<Image>().color = new Color(0.82f, 0.86f, 0.83f, 0.9f);

            // ── SECONDARY CREATE AN ACCOUNT BUTTON ──
            var createGo = Child(cardGo, "Btn_CreateAccount");
            Anchors(createGo, new Vector2(0.08f, 0.275f), new Vector2(0.92f, 0.340f));
            var caImg = createGo.AddComponent<Image>();
            caImg.sprite = btnIvoryFlat != null ? btnIvoryFlat : btnOutline;
            caImg.type = Image.Type.Sliced;
            caImg.color = Color.white;
            var createBtn = createGo.AddComponent<Button>();

            var caTxtGo = Child(createGo, "Txt");
            Stretch(caTxtGo);
            Lbl(caTxtGo, "Create an account", 17, DarkGreen, TextAlignmentOptions.Center, true);

            // ── TERTIARY PATIENT MODE BUTTON ──
            var patGo = Child(cardGo, "Btn_PatientMode");
            Anchors(patGo, new Vector2(0.08f, 0.195f), new Vector2(0.92f, 0.260f));
            var patImg = patGo.AddComponent<Image>();
            patImg.sprite = btnSageFlat != null ? btnSageFlat : btnSage;
            patImg.type = Image.Type.Sliced;
            patImg.color = Color.white;
            var patBtn = patGo.AddComponent<Button>();

            var patTxtGo = Child(patGo, "Txt");
            Stretch(patTxtGo);
            Lbl(patTxtGo, "Continue with Patient Mode", 17, DarkGreen, TextAlignmentOptions.Center, true);

            // ── FOOTER ──
            var footerGo = Child(cardGo, "Footer");
            Anchors(footerGo, new Vector2(0.08f, 0.040f), new Vector2(0.92f, 0.080f));
            var footTxt = Lbl(footerGo, "Your information stays private and secure.", 14, new Color(0.35f, 0.46f, 0.40f, 1f), TextAlignmentOptions.Center, false);

            // ── 8. REGISTER MODAL PANEL ──
            var regGo = Child(uiRoot, "Register_Modal_Panel");
            Anchors(regGo, new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.92f));
            var regImg = regGo.AddComponent<Image>();
            regImg.sprite = cardLoginCream != null ? cardLoginCream : cardSpr;
            regImg.type = Image.Type.Sliced;
            regImg.color = Color.white;
            var regCtrl = regGo.AddComponent<RegisterPanelController>();

            var rTitle = Child(regGo, "Txt_Title");
            Anchors(rTitle, new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.96f));
            Lbl(rTitle, "Create an Account", 32, DarkGreen, TextAlignmentOptions.Center, true);

            var rName  = ModalInput(regGo, "Input_RegName",      "Full Name",              inputFieldFlat != null ? inputFieldFlat : inputSpr, new Vector2(0.1f, 0.73f), new Vector2(0.9f, 0.80f), TMP_InputField.ContentType.Standard);
            var rEmail = ModalInput(regGo, "Input_RegEmail",     "Email address",          inputFieldFlat != null ? inputFieldFlat : inputSpr, new Vector2(0.1f, 0.63f), new Vector2(0.9f, 0.70f), TMP_InputField.ContentType.EmailAddress);
            var rPw    = ModalInput(regGo, "Input_RegPassword",  "Password (min 6 chars)", inputFieldFlat != null ? inputFieldFlat : inputSpr, new Vector2(0.1f, 0.53f), new Vector2(0.9f, 0.60f), TMP_InputField.ContentType.Password);
            var rCPw   = ModalInput(regGo, "Input_RegConfirmPw", "Confirm Password",       inputFieldFlat != null ? inputFieldFlat : inputSpr, new Vector2(0.1f, 0.43f), new Vector2(0.9f, 0.50f), TMP_InputField.ContentType.Password);

            var rSubBtn = Make3DButton(regGo, "Btn_SubmitRegister", "Create Account", btnForestFlat != null ? btnForestFlat : btnGreen,
                new Vector2(0.1f, 0.26f), new Vector2(0.9f, 0.34f), Color.white, 20);
            var rBackBtn= Make3DButton(regGo, "Btn_BackToLogin",    "Cancel and Return", btnIvoryFlat != null ? btnIvoryFlat : btnOutline,
                new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.24f), DarkGreen, 18);

            var rStatGo = Child(regGo, "Txt_RegStatus");
            Anchors(rStatGo, new Vector2(0.1f, 0.36f), new Vector2(0.9f, 0.43f));
            var regStatTxt = Lbl(rStatGo, "", 16, new Color(0.8f, 0.15f, 0.15f, 1f), TextAlignmentOptions.Center, false);
            rStatGo.SetActive(false);

            Wire(regCtrl, "_fullNameInput",          rName);
            Wire(regCtrl, "_emailInput",             rEmail);
            Wire(regCtrl, "_passwordInput",          rPw);
            Wire(regCtrl, "_confirmPasswordInput",   rCPw);
            Wire(regCtrl, "_registerButton",         rSubBtn);
            Wire(regCtrl, "_backToLoginButton",      rBackBtn);
            Wire(regCtrl, "_statusMessageText",      regStatTxt);
            Wire(regCtrl, "_panelRoot",              regGo);
            regGo.SetActive(false);

            // ── 9. FORGOT PASSWORD MODAL PANEL ──
            var fpGo = Child(uiRoot, "ForgotPassword_Modal_Panel");
            Anchors(fpGo, new Vector2(0.30f, 0.15f), new Vector2(0.70f, 0.85f));
            var fpImg = fpGo.AddComponent<Image>();
            fpImg.sprite = cardLoginCream != null ? cardLoginCream : cardSpr;
            fpImg.type = Image.Type.Sliced;
            fpImg.color = Color.white;
            var fpCtrl = fpGo.AddComponent<ForgotPasswordPanelController>();

            var fpTitle = Child(fpGo, "Txt_Title");
            Anchors(fpTitle, new Vector2(0.1f, 0.88f), new Vector2(0.9f, 0.96f));
            Lbl(fpTitle, "Reset Password", 32, DarkGreen, TextAlignmentOptions.Center, true);

            var fpEmailInput = ModalInput(fpGo, "Input_ForgotEmail", "Registered email address", inputFieldFlat != null ? inputFieldFlat : inputSpr, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.65f), TMP_InputField.ContentType.EmailAddress);
            var fpSubBtn = Make3DButton(fpGo, "Btn_SubmitForgot", "Send Recovery Request", btnForestFlat != null ? btnForestFlat : btnGreen,
                new Vector2(0.1f, 0.38f), new Vector2(0.9f, 0.47f), Color.white, 20);
            var fpBackBtn= Make3DButton(fpGo, "Btn_ForgotBack",    "Return to Login", btnIvoryFlat != null ? btnIvoryFlat : btnOutline,
                new Vector2(0.1f, 0.26f), new Vector2(0.9f, 0.35f), DarkGreen, 18);

            var fpStatGo = Child(fpGo, "Txt_ForgotStatus");
            Anchors(fpStatGo, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.80f));
            var fpStatTxt = Lbl(fpStatGo, "", 16, MidGreen, TextAlignmentOptions.Center, false);
            fpStatGo.SetActive(false);

            Wire(fpCtrl, "_emailInput",          fpEmailInput);
            Wire(fpCtrl, "_sendRequestButton",   fpSubBtn);
            Wire(fpCtrl, "_backToLoginButton",   fpBackBtn);
            Wire(fpCtrl, "_statusMessageText",   fpStatTxt);
            Wire(fpCtrl, "_panelRoot",           fpGo);
            fpGo.SetActive(false);

            // ══════════════════════════════════════════════════════════════════
            // 10. PREMIUM FLAT HEALTHCARE DASHBOARD
            //     Warm ivory background · Forest green actions · Flat surfaces
            //     No 3D buttons, no glossy effects, no heavy shadows
            // ══════════════════════════════════════════════════════════════════

            // Palette constants (local)
            Color Forest   = new Color(0.102f, 0.239f, 0.169f, 1f); // #1A3D2B — primary action
            Color Sage     = new Color(0.180f, 0.330f, 0.251f, 1f); // #2E5440 — accent
            Color Charcoal = new Color(0.172f, 0.188f, 0.157f, 1f); // #2C3028 — body text
            Color Muted    = new Color(0.420f, 0.447f, 0.408f, 1f); // mid-gray  — secondary text
            Color IvoryBg  = new Color(0.961f, 0.953f, 0.937f, 1f); // #F5F3EF  — main background
            Color SurfaceA = new Color(1.000f, 0.998f, 0.992f, 1f); // #FFFFFB  — card surface
            Color SurfaceB = new Color(0.957f, 0.953f, 0.945f, 1f); // #F4F3F1  — input/tile surface
            Color BorderCol= new Color(0.839f, 0.831f, 0.816f, 1f); // #D6D4D0  — subtle border
            Color SageLight= new Color(0.878f, 0.922f, 0.898f, 1f); // #E0EBE5  — badge fill

            var dashGo = Child(uiRoot, "Dashboard_Panel");
            Stretch(dashGo);
            var dashCtrl = dashGo.AddComponent<DashboardController>();

            // ── Full-screen warm ivory background ──────────────────────────────
            var dashBg = Child(dashGo, "Dashboard_Studio_BG");
            Stretch(dashBg);
            var dashBgImg = dashBg.AddComponent<Image>();
            dashBgImg.color        = IvoryBg;
            dashBgImg.raycastTarget= true;

            // ══════════════════════════════════════════════════════════════════
            // A. HEADER BAR — white surface, 1px border bottom
            // ══════════════════════════════════════════════════════════════════
            var navGo = Child(dashGo, "Nav_Header");
            Anchors(navGo, new Vector2(0f, 0.926f), new Vector2(1f, 1f));
            var navImg = navGo.AddComponent<Image>();
            navImg.color = SurfaceA;

            // Thin border line under header
            var navBorder = Child(navGo, "BottomBorder");
            Anchors(navBorder, new Vector2(0f, 0f), new Vector2(1f, 0f));
            var nbImg = navBorder.AddComponent<Image>(); nbImg.color = BorderCol;
            RectOf(navBorder).sizeDelta = new Vector2(0f, 1f);

            // Brand — left-aligned
            var navTitleGo = Child(navGo, "BrandTitle");
            Anchors(navTitleGo, new Vector2(0.018f, 0.15f), new Vector2(0.32f, 0.85f));
            Lbl(navTitleGo, "<b>NeuroCompanion</b>  <color=#2E5440>· Caregiver Dashboard</color>", 24, Charcoal, TextAlignmentOptions.MidlineLeft, false);

            // Center: welcome name (top half)
            var navUserGo = Child(navGo, "UserInfo");
            Anchors(navUserGo, new Vector2(0.33f, 0.50f), new Vector2(0.70f, 0.92f));
            var navUserTxt = Lbl(navUserGo, "Welcome, <b>Caregiver</b>", 22, Charcoal, TextAlignmentOptions.MidlineLeft, false);

            // Patient ID (bottom half)
            var navIdGo = Child(navGo, "UserId");
            Anchors(navIdGo, new Vector2(0.33f, 0.10f), new Vector2(0.70f, 0.50f));
            var navIdTxt = Lbl(navIdGo, "Patient: <b>patient_001</b>  ·  OASIS-1 Connected", 16, Muted, TextAlignmentOptions.MidlineLeft, false);

            // Role badge — forest green flat pill, right of center
            var navRoleGo = Child(navGo, "RoleBadge");
            Anchors(navRoleGo, new Vector2(0.715f, 0.18f), new Vector2(0.845f, 0.82f));
            var navRoleImg = navRoleGo.AddComponent<Image>();
            navRoleImg.color  = SageLight;
            navRoleImg.sprite = null;
            var navRoleTxtGo = Child(navRoleGo, "Txt"); Stretch(navRoleTxtGo);
            var navRoleTxt = Lbl(navRoleTxtGo, "Caregiver Mode", 16, Sage, TextAlignmentOptions.Center, true);

            // Sign Out — flat bordered button
            var navSignOutGo = Child(navGo, "Btn_SignOut");
            Anchors(navSignOutGo, new Vector2(0.856f, 0.18f), new Vector2(0.978f, 0.82f));
            var soImg = navSignOutGo.AddComponent<Image>(); soImg.color = SurfaceA;
            var soOutline = navSignOutGo.AddComponent<Outline>();
            soOutline.effectColor    = BorderCol;
            soOutline.effectDistance = new Vector2(1f, -1f);
            var navSignOutBtn = navSignOutGo.AddComponent<Button>();
            navSignOutGo.AddComponent<Button3D>();
            var soTxtGo = Child(navSignOutGo, "Txt"); Stretch(soTxtGo);
            Lbl(soTxtGo, "Sign Out", 17, Charcoal, TextAlignmentOptions.Center, false);

            // ══════════════════════════════════════════════════════════════════
            // B. LEFT CARD — OASIS Assessment  (41% width)
            //    White surface · 1px border · generous padding
            // ══════════════════════════════════════════════════════════════════
            var leftCol = Child(dashGo, "Card_OasisAssessment");
            Anchors(leftCol, new Vector2(0.012f, 0.014f), new Vector2(0.405f, 0.912f));
            var leftColImg = leftCol.AddComponent<Image>();
            leftColImg.color = SurfaceA;
            leftColImg.sprite = null;
            var leftOutline = leftCol.AddComponent<Outline>();
            leftOutline.effectColor = BorderCol; leftOutline.effectDistance = new Vector2(1f, -1f);

            // ── Section header ──────────────────────────────────────────
            var oasisTitle = Child(leftCol, "Title");
            Anchors(oasisTitle, new Vector2(0.06f, 0.930f), new Vector2(0.94f, 0.984f));
            Lbl(oasisTitle, "OASIS-1 Cognitive Assessment", 24, Charcoal, TextAlignmentOptions.MidlineLeft, true);

            var oasisSub = Child(leftCol, "Sub");
            Anchors(oasisSub, new Vector2(0.06f, 0.885f), new Vector2(0.94f, 0.928f));
            Lbl(oasisSub, "Enter the Mini-Mental State Exam score (0 – 30) to evaluate a clinical pattern", 16, Muted, TextAlignmentOptions.MidlineLeft, false);

            // Thin divider
            var oasisDiv = Child(leftCol, "Div");
            Anchors(oasisDiv, new Vector2(0.06f, 0.879f), new Vector2(0.94f, 0.880f));
            oasisDiv.AddComponent<Image>().color = BorderCol;

            // ── Patient info row ────────────────────────────────────────
            var patRow = Child(leftCol, "PatientInfoRow");
            Anchors(patRow, new Vector2(0.06f, 0.825f), new Vector2(0.94f, 0.872f));
            Lbl(patRow, "Age: <b>74</b>    Sex: <b>Male</b>    Education: <b>Level 3</b>    <i>(OASIS Cohort Defaults)</i>", 16, Muted, TextAlignmentOptions.MidlineLeft, false);

            // ── MMSE input ──────────────────────────────────────────────
            var mmseLblGo = Child(leftCol, "Lbl_MMSE");
            Anchors(mmseLblGo, new Vector2(0.06f, 0.774f), new Vector2(0.56f, 0.816f));
            Lbl(mmseLblGo, "MMSE Score (0 – 30)", 19, Charcoal, TextAlignmentOptions.MidlineLeft, true);

            var mmseInputGo = Child(leftCol, "Input_MMSE");
            Anchors(mmseInputGo, new Vector2(0.60f, 0.760f), new Vector2(0.94f, 0.820f));
            var mmseBoxImg = mmseInputGo.AddComponent<Image>(); mmseBoxImg.color = SurfaceB;
            var mmseBoxOutline = mmseInputGo.AddComponent<Outline>();
            mmseBoxOutline.effectColor = BorderCol; mmseBoxOutline.effectDistance = new Vector2(1f, -1f);
            var mmseInput = InputField(mmseInputGo, "0 – 30", TMP_InputField.ContentType.IntegerNumber);
            mmseInput.text = "24";
            var mmseTx = mmseInputGo.GetComponentInChildren<TextMeshProUGUI>();
            if (mmseTx != null) { mmseTx.fontSize = 26; mmseTx.color = Charcoal; mmseTx.alignment = TextAlignmentOptions.Center; }

            // ── Evaluate button — forest green flat ─────────────────────
            var evalBtnGo = Child(leftCol, "Btn_Evaluate");
            Anchors(evalBtnGo, new Vector2(0.06f, 0.690f), new Vector2(0.94f, 0.748f));
            var evalBtnImg = evalBtnGo.AddComponent<Image>(); evalBtnImg.color = Forest;
            var evalBtn = evalBtnGo.AddComponent<Button>();
            evalBtnGo.AddComponent<Button3D>();
            var evalTxtGo = Child(evalBtnGo, "Txt"); Stretch(evalTxtGo);
            Lbl(evalTxtGo, "Evaluate with OASIS-1 AI", 20, Color.white, TextAlignmentOptions.Center, true);

            // Evaluating spinner text
            var evalLoadGo = Child(leftCol, "EvaluatingIndicator");
            Anchors(evalLoadGo, new Vector2(0.06f, 0.654f), new Vector2(0.94f, 0.684f));
            var evalLoadTxt = Lbl(evalLoadGo, "Analysing — please wait…", 16, Muted, TextAlignmentOptions.Center, false);
            evalLoadTxt.fontStyle = FontStyles.Italic;
            evalLoadGo.SetActive(false);

            // ── Results panel ───────────────────────────────────────────
            var resGo = Child(leftCol, "ResultsPanel");
            Anchors(resGo, new Vector2(0.06f, 0.016f), new Vector2(0.94f, 0.646f));

            // Result title bar
            var resBadgeGo = Child(resGo, "PredictionBadge");
            Anchors(resBadgeGo, new Vector2(0f, 0.882f), new Vector2(1f, 1f));
            var resBadgeImg = resBadgeGo.AddComponent<Image>(); resBadgeImg.color = SageLight;
            var resTitleGo = Child(resBadgeGo, "Txt"); Stretch(resTitleGo);
            var resTitleTxt = Lbl(resTitleGo, "Awaiting assessment…", 19, Sage, TextAlignmentOptions.MidlineLeft, true);
            RectOf(resTitleGo).offsetMin = new Vector2(14f, 0f);

            // Probabilities — 3 clean rows
            var probGo = Child(resGo, "ProbabilitiesRow");
            Anchors(probGo, new Vector2(0f, 0.520f), new Vector2(1f, 0.872f));

            // CDR 0 row
            var c0RowGo = Child(probGo, "C0_Row");
            Anchors(c0RowGo, new Vector2(0f, 0.70f), new Vector2(1f, 1f));
            var c0Txt = Lbl(c0RowGo, "No dementia (CDR 0)   0%", 18, Charcoal, TextAlignmentOptions.MidlineLeft, false);
            var c0TrackGo = Child(probGo, "C0_Track");
            Anchors(c0TrackGo, new Vector2(0f, 0.63f), new Vector2(1f, 0.69f));
            var c0TrackBg = c0TrackGo.AddComponent<Image>(); c0TrackBg.color = SurfaceB;
            var c0FillGo = Child(c0TrackGo, "Fill"); Anchors(c0FillGo, Vector2.zero, new Vector2(0.2f, 1f));
            var c0ProbBar = c0FillGo.AddComponent<Image>(); c0ProbBar.color = new Color(0.102f,0.400f,0.220f,1f);

            var c1RowGo = Child(probGo, "C1_Row");
            Anchors(c1RowGo, new Vector2(0f, 0.37f), new Vector2(1f, 0.62f));
            var c1Txt = Lbl(c1RowGo, "Very mild (CDR 0.5)   0%", 18, Charcoal, TextAlignmentOptions.MidlineLeft, false);
            var c1TrackGo = Child(probGo, "C1_Track");
            Anchors(c1TrackGo, new Vector2(0f, 0.30f), new Vector2(1f, 0.36f));
            var c1TrackBg = c1TrackGo.AddComponent<Image>(); c1TrackBg.color = SurfaceB;
            var c1FillGo = Child(c1TrackGo, "Fill"); Anchors(c1FillGo, Vector2.zero, new Vector2(0.0f, 1f));
            var c1ProbBar = c1FillGo.AddComponent<Image>(); c1ProbBar.color = new Color(0.700f,0.470f,0.070f,1f);

            var c2RowGo = Child(probGo, "C2_Row");
            Anchors(c2RowGo, new Vector2(0f, 0.04f), new Vector2(1f, 0.29f));
            var c2Txt = Lbl(c2RowGo, "Mild–moderate (CDR 1+)   0%", 18, Charcoal, TextAlignmentOptions.MidlineLeft, false);
            var c2TrackGo = Child(probGo, "C2_Track");
            Anchors(c2TrackGo, new Vector2(0f, -0.04f), new Vector2(1f, 0.03f));
            var c2TrackBg = c2TrackGo.AddComponent<Image>(); c2TrackBg.color = SurfaceB;
            var c2FillGo = Child(c2TrackGo, "Fill"); Anchors(c2FillGo, Vector2.zero, new Vector2(0.0f, 1f));
            var c2ProbBar = c2FillGo.AddComponent<Image>(); c2ProbBar.color = new Color(0.650f,0.180f,0.140f,1f);

            // ── Caregiver strategy card ─────────────────────────────────
            var stratGo = Child(resGo, "StrategyBox");
            Anchors(stratGo, new Vector2(0f, 0.055f), new Vector2(1f, 0.512f));
            var stratImg = stratGo.AddComponent<Image>(); stratImg.color = SurfaceB;
            var stratOutline = stratGo.AddComponent<Outline>();
            stratOutline.effectColor = BorderCol; stratOutline.effectDistance = new Vector2(1f, -1f);

            var stratLblGo = Child(stratGo, "Header");
            Anchors(stratLblGo, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.98f));
            Lbl(stratLblGo, "Caregiver Recommendation", 17, Sage, TextAlignmentOptions.MidlineLeft, true);

            var stratTxtGo = Child(stratGo, "Txt");
            Anchors(stratTxtGo, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.80f));
            var stratTxt = Lbl(stratTxtGo, "Complete the assessment above to view the caregiver strategy.", 17, Charcoal, TextAlignmentOptions.TopLeft, false);
            var stratTxtComp = stratTxtGo.GetComponent<TextMeshProUGUI>();
            if (stratTxtComp != null) stratTxtComp.lineSpacing = 10f;

            // Disclaimer
            var discGo = Child(resGo, "Disclaimer");
            Anchors(discGo, new Vector2(0f, 0f), new Vector2(1f, 0.050f));
            var discTxt = Lbl(discGo, "For clinical reference only — not a medical diagnosis.", 14, Muted, TextAlignmentOptions.Center, false);

            // ══════════════════════════════════════════════════════════════════
            // C. RIGHT CARD — Analytics & Chart  (57% width)
            //    White surface · 1px border · generous padding
            // ══════════════════════════════════════════════════════════════════
            var rightCol = Child(dashGo, "Card_Analytics");
            Anchors(rightCol, new Vector2(0.415f, 0.014f), new Vector2(0.988f, 0.912f));
            var rightColImg = rightCol.AddComponent<Image>(); rightColImg.color = SurfaceA;
            var rightOutline = rightCol.AddComponent<Outline>();
            rightOutline.effectColor = BorderCol; rightOutline.effectDistance = new Vector2(1f, -1f);

            // ── KPI row — 4 flat tiles ──────────────────────────────────
            var metricsGo = Child(rightCol, "MetricsContainer");
            Anchors(metricsGo, new Vector2(0.03f, 0.800f), new Vector2(0.97f, 0.972f));

            var m1 = MakeFlatKpiTile(metricsGo, "Tile1", "Memory Retention", "No data yet",
                new Vector2(0f,0f), new Vector2(0.238f,1f), SurfaceB, BorderCol, Charcoal, Muted);
            var m2 = MakeFlatKpiTile(metricsGo, "Tile2", "Response Speed",   "No data yet",
                new Vector2(0.254f,0f), new Vector2(0.492f,1f), SurfaceB, BorderCol, Charcoal, Muted);
            var m3 = MakeFlatKpiTile(metricsGo, "Tile3", "Stability",        "No data yet",
                new Vector2(0.508f,0f), new Vector2(0.746f,1f), SurfaceB, BorderCol, Charcoal, Muted);
            var m4 = MakeFlatKpiTile(metricsGo, "Tile4", "Sessions",         "0 recorded",
                new Vector2(0.762f,0f), new Vector2(1f,1f),     SurfaceB, BorderCol, Charcoal, Muted);

            // ── Chart header ────────────────────────────────────────────
            var chartArea = Child(rightCol, "ChartArea");
            Anchors(chartArea, new Vector2(0.03f, 0.018f), new Vector2(0.97f, 0.782f));

            var chartTitle = Child(chartArea, "ChartTitle");
            Anchors(chartTitle, new Vector2(0f, 0.930f), new Vector2(0.60f, 1f));
            var chartTitleTxt = Lbl(chartTitle, "Interaction Trend", 19, Charcoal, TextAlignmentOptions.MidlineLeft, true);

            var chartSub = Child(chartArea, "ChartSub");
            Anchors(chartSub, new Vector2(0f, 0.870f), new Vector2(1f, 0.924f));
            var chartSubTxt = Lbl(chartSub, "Demonstration data — no session scores recorded yet for this patient", 14, Muted, TextAlignmentOptions.MidlineLeft, false);

            // ── Tab pills ───────────────────────────────────────────────
            var tabsGo = Child(chartArea, "Tabs");
            Anchors(tabsGo, new Vector2(0.63f, 0.930f), new Vector2(1f, 1f));

            // Weekly tab (active)
            var wTabGo = Child(tabsGo, "Tab_Weekly");
            Anchors(wTabGo, new Vector2(0f,0f), new Vector2(0.47f,1f));
            var wTabImg = wTabGo.AddComponent<Image>(); wTabImg.color = Forest;
            var wTabBtn = wTabGo.AddComponent<Button>(); wTabGo.AddComponent<Button3D>();
            var wTabTxtGo = Child(wTabGo,"Txt"); Stretch(wTabTxtGo);
            Lbl(wTabTxtGo, "Weekly", 15, Color.white, TextAlignmentOptions.Center, true);

            // Monthly tab (inactive)
            var mTabGo = Child(tabsGo, "Tab_Monthly");
            Anchors(mTabGo, new Vector2(0.53f,0f), new Vector2(1f,1f));
            var mTabImg = mTabGo.AddComponent<Image>(); mTabImg.color = SurfaceB;
            var mTabBtn = mTabGo.AddComponent<Button>(); mTabGo.AddComponent<Button3D>();
            var mTabTxtGo = Child(mTabGo,"Txt"); Stretch(mTabTxtGo);
            Lbl(mTabTxtGo, "Monthly", 15, Sage, TextAlignmentOptions.Center, false);

            // ── Chart canvas — ivory inset ──────────────────────────────
            var chartCanvas = Child(chartArea, "ChartCanvas");
            Anchors(chartCanvas, new Vector2(0f, 0f), new Vector2(1f, 0.845f));
            var chartCanvasImg = chartCanvas.AddComponent<Image>();
            chartCanvasImg.color = SurfaceB;

            // Gridlines — subtle warm neutral
            float[] gridYs  = { 0.25f, 0.50f, 0.75f };
            string[] gridTags = { "25", "50", "75" };
            for (int g = 0; g < 3; g++)
            {
                var gl = Child(chartCanvas, $"Grid_{g}");
                Anchors(gl, new Vector2(0.05f, gridYs[g]), new Vector2(0.99f, gridYs[g]));
                gl.AddComponent<Image>().color = BorderCol;
                RectOf(gl).sizeDelta = new Vector2(0f, 1f);

                var gTag = Child(gl, "Tag");
                Anchors(gTag, new Vector2(-0.048f, -0.5f), new Vector2(-0.008f, 1.5f));
                Lbl(gTag, gridTags[g], 11, Muted, TextAlignmentOptions.MidlineRight, false);
            }

            var chartBarsContainer = Child(chartCanvas, "BarsContainer");
            Anchors(chartBarsContainer, new Vector2(0.045f, 0f), new Vector2(0.985f, 1f));

            // ══════════════════════════════════════════════════════════════════
            // Wire DashboardController
            // ══════════════════════════════════════════════════════════════════
            Wire(dashCtrl, "_dashboardRoot",               dashGo);
            Wire(dashCtrl, "_loginCardPanel",              cardGo);
            Wire(dashCtrl, "_loginBackground",             bgGo);
            Wire(dashCtrl, "_dashboardBgImage",            dashBgImg);
            Wire(dashCtrl, "_patientNameText",             navUserTxt);
            Wire(dashCtrl, "_patientIdText",               navIdTxt);
            Wire(dashCtrl, "_roleBadgeText",               navRoleTxt);
            Wire(dashCtrl, "_signOutButton",               navSignOutBtn);
            Wire(dashCtrl, "_mmseInput",                   mmseInput);
            Wire(dashCtrl, "_evaluateMmseButton",          evalBtn);
            Wire(dashCtrl, "_evaluatingIndicator",         evalLoadGo);
            Wire(dashCtrl, "_resultsPanel",                resGo);
            Wire(dashCtrl, "_predictionTitleText",         resTitleTxt);
            Wire(dashCtrl, "_predictionBadgeImage",        resBadgeImg);
            Wire(dashCtrl, "_class0ProbText",              c0Txt);
            Wire(dashCtrl, "_class1ProbText",              c1Txt);
            Wire(dashCtrl, "_class2ProbText",              c2Txt);
            Wire(dashCtrl, "_class0ProbBar",               c0ProbBar);
            Wire(dashCtrl, "_class1ProbBar",               c1ProbBar);
            Wire(dashCtrl, "_class2ProbBar",               c2ProbBar);
            Wire(dashCtrl, "_caregiverRecommendationText", stratTxt);
            Wire(dashCtrl, "_disclaimerText",              discTxt);
            Wire(dashCtrl, "_weeklyTabButton",             wTabBtn);
            Wire(dashCtrl, "_monthlyTabButton",            mTabBtn);
            Wire(dashCtrl, "_weeklyTabBg",                 wTabImg);
            Wire(dashCtrl, "_monthlyTabBg",                mTabImg);
            Wire(dashCtrl, "_chartBarsContainer",          chartBarsContainer);
            Wire(dashCtrl, "_chartTitleText",              chartTitleTxt);
            Wire(dashCtrl, "_chartSubtitleText",           chartSubTxt);
            Wire(dashCtrl, "_retentionMetricText",         m1);
            Wire(dashCtrl, "_hesitationMetricText",        m2);
            Wire(dashCtrl, "_stabilityMetricText",         m3);
            Wire(dashCtrl, "_sessionsMetricText",          m4);

            dashGo.SetActive(false);
            Wire(regCtrl, "_dashboardPanel", dashCtrl);

            // ── 11. WIRE LOGIN CONTROLLER ──
            Wire(loginCtrl, "_emailInput",           emailInput);
            Wire(loginCtrl, "_passwordInput",        pwInput);
            Wire(loginCtrl, "_signInButton",         signInBtn);
            Wire(loginCtrl, "_forgotPasswordButton", forgotBtn);
            Wire(loginCtrl, "_createAccountButton",  createBtn);
            Wire(loginCtrl, "_patientModeButton",    patBtn);
            Wire(loginCtrl, "_togglePasswordButton", eyeBtn);
            Wire(loginCtrl, "_statusMessageText",    statusTxt);
            Wire(loginCtrl, "_loadingIndicator",     loadGo);
            Wire(loginCtrl, "_eyeIconImage",         eyeImg);
            Wire(loginCtrl, "_eyeOpenSprite",        eyeOpen);
            Wire(loginCtrl, "_eyeClosedSprite",      eyeClosed);
            Wire(loginCtrl, "_registerPanel",        regCtrl);
            Wire(loginCtrl, "_forgotPasswordPanel",  fpCtrl);
            Wire(loginCtrl, "_loginCardPanel",       cardGo);
            Wire(loginCtrl, "_dashboardPanel",       dashCtrl);

            // ── 12. AUTH MANAGER SINGLETON ──
            var mgr = GameObject.Find("[MANAGERS]") ?? new GameObject("[MANAGERS]");
            if (mgr.transform.Find("Auth_Manager_Holder") == null)
            {
                var ah = new GameObject("Auth_Manager_Holder");
                ah.transform.SetParent(mgr.transform, false);
                ah.AddComponent<RectTransform>();
                ah.AddComponent<AuthManager>();
            }

            // ── 13. SAVE SCENE ──
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LoginSetup] ✅ v9 Complete 2D/3D Login & Dashboard experience built and saved!");
        }

        // ════ HELPERS ═════════════════════════════════════════════════════════

        private static Button Make3DButton(GameObject parent, string name, string label,
            Sprite bg, Vector2 min, Vector2 max, Color txtColor, float sz)
        {
            var go = Child(parent, name);
            Anchors(go, min, max);
            var img = go.AddComponent<Image>();
            img.sprite = bg;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var btn = go.AddComponent<Button>();
            go.AddComponent<Button3D>();

            var tGo = Child(go, "Txt");
            Stretch(tGo);
            Lbl(tGo, label, sz, txtColor, TextAlignmentOptions.Center, true);
            return btn;
        }

        private static TextMeshProUGUI CreateKpiTile(GameObject parent, string name, string label, string defaultVal,
            Vector2 min, Vector2 max, Sprite bg)
        {
            var tile = Child(parent, name);
            Anchors(tile, min, max);
            var img = tile.AddComponent<Image>();
            img.sprite = bg; img.type = Image.Type.Sliced; img.color = Color.white;

            var lGo = Child(tile, "Lbl");
            Anchors(lGo, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.94f));
            Lbl(lGo, label, 14, MidGreen, TextAlignmentOptions.MidlineLeft, true);

            var vGo = Child(tile, "Val");
            Anchors(vGo, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.58f));
            return Lbl(vGo, defaultVal, 20, DarkGreen, TextAlignmentOptions.MidlineLeft, true);
        }

        private static TextMeshProUGUI MakeFlatKpiTile(GameObject parent, string name, string label, string defaultVal,
            Vector2 min, Vector2 max, Color bgCol, Color borderCol, Color valCol, Color lblCol)
        {
            var tile = Child(parent, name);
            Anchors(tile, min, max);
            var img = tile.AddComponent<Image>();
            img.color = bgCol;
            var outline = tile.AddComponent<Outline>();
            outline.effectColor = borderCol;
            outline.effectDistance = new Vector2(1f, -1f);

            var lGo = Child(tile, "Lbl");
            Anchors(lGo, new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.94f));
            Lbl(lGo, label, 13, lblCol, TextAlignmentOptions.MidlineLeft, true);

            var vGo = Child(tile, "Val");
            Anchors(vGo, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.58f));
            return Lbl(vGo, defaultVal, 18, valCol, TextAlignmentOptions.MidlineLeft, true);
        }

        private static TMP_InputField LoginInputField(GameObject go, string ph, TMP_InputField.ContentType ctype, Sprite iconSpr = null)
        {
            var ifield = go.AddComponent<TMP_InputField>();
            ifield.contentType = ctype;
            ifield.lineType = TMP_InputField.LineType.SingleLine;

            float xStart = 0.04f;
            if (iconSpr != null)
            {
                var icoGo = Child(go, "Ico");
                SetRect(icoGo, new Vector2(0.030f, 0.22f), new Vector2(0.080f, 0.78f));
                var icoImg = icoGo.AddComponent<Image>();
                icoImg.sprite = iconSpr;
                icoImg.preserveAspect = true;
                icoImg.color = new Color(0.28f, 0.40f, 0.34f, 0.85f);
                icoImg.raycastTarget = false;
                xStart = 0.095f;
            }

            var txGo = Child(go, "Txt");
            SetRect(txGo, new Vector2(xStart, 0f), new Vector2(0.88f, 1f));
            var tx = TMP(txGo);
            tx.fontSize = 17;
            tx.color = DarkGreen;
            tx.alignment = TextAlignmentOptions.MidlineLeft;

            var phGo = Child(go, "PH");
            SetRect(phGo, new Vector2(xStart, 0f), new Vector2(0.88f, 1f));
            var phComp = TMP(phGo);
            phComp.text = ph;
            phComp.fontSize = 17;
            phComp.fontStyle = FontStyles.Italic;
            phComp.color = new Color(0.48f, 0.56f, 0.52f, 0.85f);
            phComp.alignment = TextAlignmentOptions.MidlineLeft;

            ifield.textComponent = tx;
            ifield.placeholder   = phComp;
            return ifield;
        }

        private static TMP_InputField InputField(GameObject go, string ph, TMP_InputField.ContentType ctype)
        {
            var ifield = go.AddComponent<TMP_InputField>();
            ifield.contentType = ctype;
            ifield.lineType = TMP_InputField.LineType.SingleLine;

            var txGo = Child(go, "Txt");
            SetRect(txGo, new Vector2(0.04f, 0f), new Vector2(0.88f, 1f));
            var tx = TMP(txGo);
            tx.fontSize = 22;
            tx.color = InputText;
            tx.alignment = TextAlignmentOptions.MidlineLeft;

            var phGo = Child(go, "PH");
            SetRect(phGo, new Vector2(0.04f, 0f), new Vector2(0.88f, 1f));
            var phComp = TMP(phGo);
            phComp.text = ph;
            phComp.fontSize = 22;
            phComp.fontStyle = FontStyles.Italic;
            phComp.color = PHColor;
            phComp.alignment = TextAlignmentOptions.MidlineLeft;

            ifield.textComponent = tx;
            ifield.placeholder   = phComp;
            return ifield;
        }

        private static TMP_InputField ModalInput(GameObject parent, string name, string ph, Sprite bg,
            Vector2 min, Vector2 max, TMP_InputField.ContentType ctype)
        {
            var go = Child(parent, name);
            Anchors(go, min, max);
            var img = go.AddComponent<Image>();
            img.sprite = bg;
            img.type = Image.Type.Sliced;
            return InputField(go, ph, ctype);
        }

        private static Sprite LoadSpr(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) return s;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Sprite sp) return sp;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return Sprite.Create(tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f,0.5f));
            Debug.LogWarning($"[LoginSetup] ⚠ Sprite not loaded: {path}");
            return null;
        }

        private static TextMeshProUGUI TMP(GameObject go)
        {
            var t = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            if (s_font != null) t.font = s_font;
            return t;
        }

        private static TextMeshProUGUI Lbl(GameObject go, string text, float sz, Color col,
            TextAlignmentOptions align, bool bold)
        {
            var t = TMP(go);
            t.text = text;
            t.fontSize = sz;
            t.color = col;
            t.alignment = align;
            if (bold) t.fontStyle = FontStyles.Bold;
            return t;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
            => go.GetComponent<T>() ?? go.AddComponent<T>();

        private static RectTransform RectOf(GameObject go)
            => go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();

        private static void Stretch(GameObject go)
        {
            var r = RectOf(go);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void Anchors(GameObject go, Vector2 min, Vector2 max)
        {
            var r = RectOf(go);
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void SetRect(GameObject go, Vector2 min, Vector2 max)
        {
            var r = RectOf(go);
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void SetSize(GameObject go, float width, float height)
        {
            var r = RectOf(go);
            r.sizeDelta = new Vector2(width, height);
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
        }

        private static void Wire(Component target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[LoginSetup] SerializedField '{field}' not found on {target.GetType().Name}");
                return;
            }
            if (value is Object uObj) prop.objectReferenceValue = uObj;
            else if (value is GameObject go) prop.objectReferenceValue = go;
            so.ApplyModifiedProperties();
        }
    }
}
