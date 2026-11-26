// AuthManager.cs
// NeuroCompanion — Authentication and Session Manager
//
// Central manager for user authentication state, token persistence, and profile caching.
// Bridges the Unity UI with BackendClient.cs and ModeManager.cs.

using System;
using UnityEngine;

namespace NeuroCompanion.Backend
{
    public class AuthManager : MonoBehaviour
    {
        private const string PrefKeyToken = "nc_auth_token";
        private const string PrefKeyUserId = "nc_user_id";
        private const string PrefKeyUserRole = "nc_user_role";
        private const string PrefKeyUserEmail = "nc_user_email";
        private const string PrefKeyUserName = "nc_user_name";

        public static AuthManager Instance { get; private set; }

        public bool IsAuthenticated => !string.IsNullOrEmpty(CurrentToken);
        public string CurrentToken { get; private set; }
        public UserProfileDto CurrentProfile { get; private set; }

        public event Action<UserProfileDto> OnLoginSuccess;
        public event Action OnLogout;
        public event Action<string> OnPatientModeEntered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);
            LoadStoredSession();
        }

        private void LoadStoredSession()
        {
            string storedToken = PlayerPrefs.GetString(PrefKeyToken, string.Empty);
            if (!string.IsNullOrEmpty(storedToken))
            {
                CurrentToken = storedToken;
                CurrentProfile = new UserProfileDto
                {
                    user_id = PlayerPrefs.GetString(PrefKeyUserId, string.Empty),
                    email = PlayerPrefs.GetString(PrefKeyUserEmail, string.Empty),
                    full_name = PlayerPrefs.GetString(PrefKeyUserName, string.Empty),
                    role = PlayerPrefs.GetString(PrefKeyUserRole, "caregiver")
                };
                Debug.Log($"[AuthManager] Restored cached session for {CurrentProfile.email} ({CurrentProfile.role})");
            }
        }

        public void SetSession(string token, UserProfileDto profile)
        {
            CurrentToken = token;
            CurrentProfile = profile;

            PlayerPrefs.SetString(PrefKeyToken, token ?? string.Empty);
            if (profile != null)
            {
                PlayerPrefs.SetString(PrefKeyUserId, profile.user_id ?? string.Empty);
                PlayerPrefs.SetString(PrefKeyUserEmail, profile.email ?? string.Empty);
                PlayerPrefs.SetString(PrefKeyUserName, profile.full_name ?? string.Empty);
                PlayerPrefs.SetString(PrefKeyUserRole, profile.role ?? "caregiver");
            }
            PlayerPrefs.Save();

            Debug.Log($"[AuthManager] User logged in: {profile?.email} (Role: {profile?.role})");
            OnLoginSuccess?.Invoke(profile);
        }

        public void ClearSession()
        {
            CurrentToken = null;
            CurrentProfile = null;

            PlayerPrefs.DeleteKey(PrefKeyToken);
            PlayerPrefs.DeleteKey(PrefKeyUserId);
            PlayerPrefs.DeleteKey(PrefKeyUserEmail);
            PlayerPrefs.DeleteKey(PrefKeyUserName);
            PlayerPrefs.DeleteKey(PrefKeyUserRole);
            PlayerPrefs.Save();

            Debug.Log("[AuthManager] User logged out, session cleared.");
            OnLogout?.Invoke();
        }

        public void NotifyPatientModeEntered(string patientId)
        {
            Debug.Log($"[AuthManager] Patient Mode entered for patient '{patientId}'");
            OnPatientModeEntered?.Invoke(patientId);

            if (ModeManager.Instance != null)
            {
                ModeManager.Instance.EnterPatientMode();
            }
        }
    }
}
