// RegisterPanelController.cs
// NeuroCompanion — Registration Modal Panel Controller
//
// Manages the account registration glass panel:
// - Full Name, Email, Password, Confirm Password
// - Role Selection (Caregiver or Patient)
// - Client-side validation (matching passwords, minimum length, email syntax)
// - Backend integration with PBKDF2 hashing
// - Smooth transitions back to Login

using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroCompanion.Backend
{
    public class RegisterPanelController : MonoBehaviour
    {
        [Header("Input Fields")]
        [SerializeField] private TMP_InputField _fullNameInput;
        [SerializeField] private TMP_InputField _emailInput;
        [SerializeField] private TMP_InputField _passwordInput;
        [SerializeField] private TMP_InputField _confirmPasswordInput;

        [Header("Role Selection")]
        [SerializeField] private TMP_Dropdown _roleDropdown; // Option 0: Caregiver, Option 1: Patient

        [Header("Buttons")]
        [SerializeField] private Button _registerButton;
        [SerializeField] private Button _backToLoginButton;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI _statusMessageText;
        [SerializeField] private GameObject _loadingIndicator;

        [Header("Parent Card")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private DashboardController _dashboardPanel;

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private void Start()
        {
            if (_registerButton != null)
                _registerButton.onClick.AddListener(OnRegisterClicked);

            if (_backToLoginButton != null)
                _backToLoginButton.onClick.AddListener(ClosePanel);

            ClearForm();
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        public void OpenPanel()
        {
            ClearForm();
            if (_panelRoot != null)
                _panelRoot.SetActive(true);

            if (_fullNameInput != null)
                _fullNameInput.Select();
        }

        public void ClosePanel()
        {
            ClearForm();
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        public void OnRegisterClicked()
        {
            string fullName = _fullNameInput != null ? _fullNameInput.text.Trim() : "";
            string email = _emailInput != null ? _emailInput.text.Trim() : "";
            string password = _passwordInput != null ? _passwordInput.text : "";
            string confirmPassword = _confirmPasswordInput != null ? _confirmPasswordInput.text : "";

            // 1. Validate Full Name
            if (string.IsNullOrEmpty(fullName) || fullName.Length < 2)
            {
                ShowError("Please enter your full name (minimum 2 characters).");
                if (_fullNameInput != null) _fullNameInput.Select();
                return;
            }

            // 2. Validate Email
            if (string.IsNullOrEmpty(email) || !EmailRegex.IsMatch(email))
            {
                ShowError("Please enter a valid email address (e.g. name@example.com).");
                if (_emailInput != null) _emailInput.Select();
                return;
            }

            // 3. Validate Password
            if (string.IsNullOrEmpty(password) || password.Length < 6)
            {
                ShowError("Password must be at least 6 characters long.");
                if (_passwordInput != null) _passwordInput.Select();
                return;
            }

            // 4. Confirm Password Match
            if (password != confirmPassword)
            {
                ShowError("Passwords do not match. Please verify both fields.");
                if (_confirmPasswordInput != null) _confirmPasswordInput.Select();
                return;
            }

            // 5. Determine Role
            string role = "caregiver";
            if (_roleDropdown != null && _roleDropdown.value == 1)
            {
                role = "patient";
            }

            SetLoading(true);
            ClearStatus();

            var req = new UserRegisterRequest
            {
                full_name = fullName,
                email = email,
                password = password,
                role = role
            };

            BackendClient.Instance.PostRegister(req, result =>
            {
                SetLoading(false);

                if (result.IsSuccess && result.Data != null)
                {
                    ShowSuccess("Account created successfully! Signing you in...");

                    if (AuthManager.Instance != null)
                    {
                        AuthManager.Instance.SetSession(result.Data.token, result.Data.user);
                    }

                    // Auto-open dashboard after account creation
                    if (_dashboardPanel != null)
                    {
                        _dashboardPanel.OpenDashboard(result.Data.user);
                    }
                    else if (DashboardController.Instance != null)
                    {
                        DashboardController.Instance.OpenDashboard(result.Data.user);
                    }
                    else
                    {
                        var dash = FindAnyObjectByType<DashboardController>(FindObjectsInactive.Include);
                        if (dash != null)
                        {
                            dash.OpenDashboard(result.Data.user);
                        }
                    }

                    // Auto-close modal after brief feedback
                    Invoke(nameof(ClosePanel), 0.8f);
                }
                else
                {
                    string err = !string.IsNullOrEmpty(result.ErrorMessage)
                        ? result.ErrorMessage
                        : "Registration failed. Please try again.";

                    if (result.StatusCode == 409)
                    {
                        err = "This email is already registered. Please sign in or use a different email.";
                    }

                    ShowError(err);
                }
            });
        }

        private void ClearForm()
        {
            if (_fullNameInput != null) _fullNameInput.text = "";
            if (_emailInput != null) _emailInput.text = "";
            if (_passwordInput != null) _passwordInput.text = "";
            if (_confirmPasswordInput != null) _confirmPasswordInput.text = "";
            if (_roleDropdown != null) _roleDropdown.value = 0;
            ClearStatus();
            SetLoading(false);
        }

        private void ShowError(string msg)
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.text = msg;
                _statusMessageText.color = new Color(0.78f, 0.2f, 0.2f, 1f);
                _statusMessageText.gameObject.SetActive(true);
            }
        }

        private void ShowSuccess(string msg)
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.text = msg;
                _statusMessageText.color = new Color(0.1f, 0.45f, 0.25f, 1f);
                _statusMessageText.gameObject.SetActive(true);
            }
        }

        private void ClearStatus()
        {
            if (_statusMessageText != null)
            {
                _statusMessageText.text = "";
                _statusMessageText.gameObject.SetActive(false);
            }
        }

        private void SetLoading(bool loading)
        {
            if (_loadingIndicator != null)
                _loadingIndicator.SetActive(loading);

            if (_registerButton != null)
                _registerButton.interactable = !loading;

            if (_backToLoginButton != null)
                _backToLoginButton.interactable = !loading;
        }
    }
}
