// ForgotPasswordPanelController.cs
// NeuroCompanion — Password Recovery Modal Panel Controller
//
// Manages the forgot password glass panel:
// - Email input & validation
// - Backend connection to /api/v1/auth/forgot-password
// - Accurate messaging without false SMTP delivery claims
// - Calm accessibility-first UI

using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeuroCompanion.Backend
{
    public class ForgotPasswordPanelController : MonoBehaviour
    {
        [Header("Input Field")]
        [SerializeField] private TMP_InputField _emailInput;

        [Header("Buttons")]
        [SerializeField] private Button _sendRequestButton;
        [SerializeField] private Button _backToLoginButton;

        [Header("Feedback")]
        [SerializeField] private TextMeshProUGUI _statusMessageText;
        [SerializeField] private GameObject _loadingIndicator;

        [Header("Parent Card")]
        [SerializeField] private GameObject _panelRoot;

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private void Start()
        {
            if (_sendRequestButton != null)
                _sendRequestButton.onClick.AddListener(OnSendRequestClicked);

            if (_backToLoginButton != null)
                _backToLoginButton.onClick.AddListener(ClosePanel);

            ClearForm();
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        public void OpenPanel(string prefilledEmail = "")
        {
            ClearForm();
            if (_emailInput != null)
            {
                _emailInput.text = prefilledEmail ?? "";
                _emailInput.Select();
            }

            if (_panelRoot != null)
                _panelRoot.SetActive(true);
        }

        public void ClosePanel()
        {
            ClearForm();
            if (_panelRoot != null)
                _panelRoot.SetActive(false);
        }

        public void OnSendRequestClicked()
        {
            string email = _emailInput != null ? _emailInput.text.Trim() : "";

            if (string.IsNullOrEmpty(email) || !EmailRegex.IsMatch(email))
            {
                ShowError("Please enter a valid email address.");
                if (_emailInput != null) _emailInput.Select();
                return;
            }

            SetLoading(true);
            ClearStatus();

            var req = new ForgotPasswordRequest { email = email };
            BackendClient.Instance.PostForgotPassword(req, result =>
            {
                SetLoading(false);

                if (result.IsSuccess && result.Data != null)
                {
                    ShowSuccess(result.Data.message);
                }
                else
                {
                    string err = "Unable to process recovery request. Please check the email address.";
                    if (result.StatusCode == 404)
                    {
                        err = "No account found associated with this email address.";
                    }
                    ShowError(err);
                }
            });
        }

        private void ClearForm()
        {
            if (_emailInput != null) _emailInput.text = "";
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

            if (_sendRequestButton != null)
                _sendRequestButton.interactable = !loading;

            if (_backToLoginButton != null)
                _backToLoginButton.interactable = !loading;
        }
    }
}
