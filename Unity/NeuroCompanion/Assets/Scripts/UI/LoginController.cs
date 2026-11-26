// LoginController.cs
// NeuroCompanion — Login Screen Controller
//
// Manages the exact NeuroCompanion Login Form UI:
// - Email validation & input
// - Password masking & show/hide toggle
// - Sign In with real backend authentication
// - Modal opening for Registration and Password Recovery
// - Continue with Patient Mode entry
// - Calm feedback & loading states

using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroCompanion.Backend
{
    public class LoginController : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField _emailInput;
        [SerializeField] private TMP_InputField _passwordInput;

        [Header("Buttons")]
        [SerializeField] private Button _signInButton;
        [SerializeField] private Button _forgotPasswordButton;
        [SerializeField] private Button _createAccountButton;
        [SerializeField] private Button _patientModeButton;
        [SerializeField] private Button _togglePasswordButton;

        [Header("Status & Feedback")]
        [SerializeField] private TextMeshProUGUI _statusMessageText;
        [SerializeField] private GameObject _loadingIndicator;
        [SerializeField] private Image _eyeIconImage;

        [Header("Eye Sprites")]
        [SerializeField] private Sprite _eyeOpenSprite;
        [SerializeField] private Sprite _eyeClosedSprite;

        [Header("Linked Panels")]
        [SerializeField] private RegisterPanelController _registerPanel;
        [SerializeField] private ForgotPasswordPanelController _forgotPasswordPanel;
        [SerializeField] private GameObject _loginCardPanel;
        [SerializeField] private DashboardController _dashboardPanel;

        private bool _isPasswordVisible = false;
        private bool _isSubmitting = false;

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private void Start()
        {
            if (_signInButton != null)
                _signInButton.onClick.AddListener(OnSignInClicked);

            if (_forgotPasswordButton != null)
                _forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked);

            if (_createAccountButton != null)
                _createAccountButton.onClick.AddListener(OnCreateAccountClicked);

            if (_patientModeButton != null)
                _patientModeButton.onClick.AddListener(OnPatientModeClicked);

            if (_togglePasswordButton != null)
                _togglePasswordButton.onClick.AddListener(TogglePasswordVisibility);

            SetPasswordVisibility(false);
            ClearStatus();
            SetLoading(false);
        }

        private void Update()
        {
            if (!_isSubmitting)
            {
                // Tab key navigation between inputs
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    if (_emailInput != null && _emailInput.isFocused && _passwordInput != null)
                    {
                        _passwordInput.Select();
                        _passwordInput.ActivateInputField();
                    }
                    else if (_passwordInput != null && _passwordInput.isFocused && _emailInput != null)
                    {
                        _emailInput.Select();
                        _emailInput.ActivateInputField();
                    }
                }

                // Enter key submission
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    if ((_emailInput != null && _emailInput.isFocused) ||
                        (_passwordInput != null && _passwordInput.isFocused))
                    {
                        OnSignInClicked();
                    }
                }
            }
        }

        public void OnSignInClicked()
        {
            if (_isSubmitting) return;

            string email = _emailInput != null ? _emailInput.text.Trim() : "";
            string password = _passwordInput != null ? _passwordInput.text : "";

            // Validate Email
            if (string.IsNullOrEmpty(email))
            {
                ShowError("Please enter your email address.");
                if (_emailInput != null) _emailInput.Select();
                return;
            }

            if (!EmailRegex.IsMatch(email))
            {
                ShowError("Please enter a valid email address (e.g. you@example.com).");
                if (_emailInput != null) _emailInput.Select();
                return;
            }

            // Validate Password
            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter your password.");
                if (_passwordInput != null) _passwordInput.Select();
                return;
            }

            // Proceed with real authentication
            ClearStatus();
            SetLoading(true);

            var request = new UserLoginRequest
            {
                email = email,
                password = password
            };

            BackendClient.Instance.PostLogin(request, result =>
            {
                SetLoading(false);

                if (result.IsSuccess && result.Data != null && !string.IsNullOrEmpty(result.Data.token))
                {
                    ShowSuccess($"Welcome back, {result.Data.user.full_name}!");
                    
                    if (AuthManager.Instance != null)
                    {
                        AuthManager.Instance.SetSession(result.Data.token, result.Data.user);
                    }

                    // Route to appropriate mode
                    StartCoroutine(TransitionAfterLogin(result.Data.user));
                }
                else
                {
                    string errorMsg = !string.IsNullOrEmpty(result.ErrorMessage)
                        ? result.ErrorMessage
                        : "Sign-in failed. Please check your credentials.";

                    if (result.StatusCode == 401)
                    {
                        errorMsg = "Incorrect email or password. Please try again.";
                    }
                    else if (result.StatusCode == 0 || result.StatusCode == 408)
                    {
                        errorMsg = "Backend server is unreachable. Please verify the backend is running.";
                    }

                    ShowError(errorMsg);
                }
            });
        }

        private IEnumerator TransitionAfterLogin(UserProfileDto user)
        {
            yield return new WaitForSeconds(0.4f);

            // Open Dashboard via direct serialized reference or singleton
            if (_dashboardPanel != null)
            {
                _dashboardPanel.OpenDashboard(user);
            }
            else if (DashboardController.Instance != null)
            {
                DashboardController.Instance.OpenDashboard(user);
            }
            else
            {
                var dash = FindAnyObjectByType<DashboardController>(FindObjectsInactive.Include);
                if (dash != null)
                {
                    dash.OpenDashboard(user);
                }
            }

            if (ModeManager.Instance != null)
            {
                if (user.role == "caregiver")
                {
                    ModeManager.Instance.EnterCaregiverMode();
                }
                else
                {
                    ModeManager.Instance.EnterPatientMode();
                }
            }

            // Hide login form after successful login
            if (_loginCardPanel != null)
            {
                _loginCardPanel.SetActive(false);
            }
        }

        public void OnPatientModeClicked()
        {
            if (_isSubmitting) return;

            ClearStatus();
            SetLoading(true);

            var req = new PatientModeEntryRequest { patient_id = "patient_001" };
            BackendClient.Instance.PostPatientMode(req, result =>
            {
                SetLoading(false);

                if (result.IsSuccess && result.Data != null)
                {
                    ShowSuccess("Starting Patient Mode...");
                    
                    if (AuthManager.Instance != null)
                    {
                        AuthManager.Instance.NotifyPatientModeEntered(result.Data.patient_id);
                    }
                    else if (ModeManager.Instance != null)
                    {
                        ModeManager.Instance.EnterPatientMode();
                    }

                    var gardenCtrl = GardenGameController.Instance ?? FindAnyObjectByType<GardenGameController>(FindObjectsInactive.Include);
                    if (gardenCtrl != null)
                    {
                        gardenCtrl.StartGardenGame();
                    }
                    else if (_dashboardPanel != null)
                    {
                        _dashboardPanel.OpenDashboardAsPatient();
                    }
                    else if (DashboardController.Instance != null)
                    {
                        DashboardController.Instance.OpenDashboardAsPatient();
                    }
                    else
                    {
                        var dash = FindAnyObjectByType<DashboardController>(FindObjectsInactive.Include);
                        if (dash != null)
                        {
                            dash.OpenDashboardAsPatient();
                        }
                    }

                    if (_loginCardPanel != null)
                    {
                        _loginCardPanel.SetActive(false);
                    }
                }
                else
                {
                    // Fallback to local patient mode entry even if backend network hiccup occurs
                    Debug.LogWarning("[LoginController] Backend patient mode call failed, continuing locally.");
                    if (ModeManager.Instance != null)
                        ModeManager.Instance.EnterPatientMode();

                    var gardenCtrl = GardenGameController.Instance ?? FindAnyObjectByType<GardenGameController>(FindObjectsInactive.Include);
                    if (gardenCtrl != null)
                    {
                        gardenCtrl.StartGardenGame();
                    }
                    else if (_dashboardPanel != null)
                    {
                        _dashboardPanel.OpenDashboardAsPatient();
                    }
                    else if (DashboardController.Instance != null)
                    {
                        DashboardController.Instance.OpenDashboardAsPatient();
                    }
                    else
                    {
                        var dash = FindAnyObjectByType<DashboardController>(FindObjectsInactive.Include);
                        if (dash != null)
                        {
                            dash.OpenDashboardAsPatient();
                        }
                    }

                    if (_loginCardPanel != null)
                        _loginCardPanel.SetActive(false);
                }
            });
        }

        public void OnCreateAccountClicked()
        {
            if (_registerPanel != null)
            {
                ClearStatus();
                _registerPanel.OpenPanel();
            }
            else
            {
                Debug.LogWarning("[LoginController] RegisterPanel is not assigned.");
            }
        }

        public void OnForgotPasswordClicked()
        {
            if (_forgotPasswordPanel != null)
            {
                ClearStatus();
                string currentEmail = _emailInput != null ? _emailInput.text.Trim() : "";
                _forgotPasswordPanel.OpenPanel(currentEmail);
            }
            else
            {
                Debug.LogWarning("[LoginController] ForgotPasswordPanel is not assigned.");
            }
        }

        public void TogglePasswordVisibility()
        {
            SetPasswordVisibility(!_isPasswordVisible);
        }

        private void SetPasswordVisibility(bool visible)
        {
            _isPasswordVisible = visible;

            if (_passwordInput != null)
            {
                _passwordInput.contentType = visible
                    ? TMP_InputField.ContentType.Standard
                    : TMP_InputField.ContentType.Password;

                _passwordInput.ForceLabelUpdate();
            }

            if (_eyeIconImage != null)
            {
                _eyeIconImage.sprite = visible ? _eyeOpenSprite : _eyeClosedSprite;
            }
        }

        public void ShowError(string message)
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.text = message;
                _statusMessageText.color = new Color(0.78f, 0.2f, 0.2f, 1f); // Calm soft crimson
                _statusMessageText.gameObject.SetActive(true);
            }
            Debug.Log($"[LoginController] Error: {message}");
        }

        public void ShowSuccess(string message)
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.text = message;
                _statusMessageText.color = new Color(0.1f, 0.45f, 0.25f, 1f); // Gentle forest green
                _statusMessageText.gameObject.SetActive(true);
            }
            Debug.Log($"[LoginController] Success: {message}");
        }

        public void ClearStatus()
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.text = "";
                _statusMessageText.gameObject.SetActive(false);
            }
        }

        private void SetLoading(bool loading)
        {
            _isSubmitting = loading;

            if (_loadingIndicator != null)
                _loadingIndicator.SetActive(loading);

            if (_signInButton != null)
                _signInButton.interactable = !loading;

            if (_patientModeButton != null)
                _patientModeButton.interactable = !loading;

            if (_createAccountButton != null)
                _createAccountButton.interactable = !loading;

            if (_emailInput != null)
                _emailInput.interactable = !loading;

            if (_passwordInput != null)
                _passwordInput.interactable = !loading;
        }
    }
}
