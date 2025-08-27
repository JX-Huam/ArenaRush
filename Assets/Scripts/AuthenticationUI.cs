using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Authentication;
using LeaderboardCreatorDemo;

public class AuthenticateUI : MonoBehaviour {

    [SerializeField] private Button authenticateButton;
    [SerializeField] private Button leaderboardButton;

    private void Awake() {
        authenticateButton.onClick.AddListener(() => {
            LobbyManager.Instance.Authenticate(EditPlayerName.Instance.GetPlayerName());
            LobbyListUI.Instance.Show();
            EditPlayerName.Instance.Hide();
            AvatarCreationUI.Instance.Hide();
            Hide();
        });

        // Check authentication status on start
        CheckAuthenticationStatus();
    }

    private void Start() {
        // Subscribe to authentication state change events
        LobbyManager.Instance.OnAuthenticationStateChanged += LobbyManager_OnAuthenticationStateChanged;
    }

    private void OnDestroy() {
        if (LobbyManager.Instance != null) {
            LobbyManager.Instance.OnAuthenticationStateChanged -= LobbyManager_OnAuthenticationStateChanged;
        }
    }

    private void LobbyManager_OnAuthenticationStateChanged(object sender, LobbyManager.AuthenticationStateEventArgs e) {
        if (e.IsAuthenticated) {
            Hide();
            LobbyListUI.Instance.Show();
            EditPlayerName.Instance.Hide();
            AvatarCreationUI.Instance.Hide();
            LeaderboardManager.Instance.Hide();
            Debug.Log("Authentication UI hidden - User authenticated");
        } else {
            Show();
            Debug.Log("Authentication UI shown - User not authenticated");
        }
    }

    // Check current authentication status
    private void CheckAuthenticationStatus() {
        try {
            // Check if Unity Services has been initialized
            if (Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized 
                && AuthenticationService.Instance != null) {
                bool isAuthenticated = AuthenticationService.Instance.IsSignedIn;
                if (isAuthenticated) {
                    Hide();
                    LobbyListUI.Instance.Show();
                    EditPlayerName.Instance.Hide();
                    AvatarCreationUI.Instance.Hide();
                    LeaderboardManager.Instance.Hide();
                    Debug.Log("User is already authenticated, hiding Authentication UI");
                } else {
                    Show();
                    Debug.Log("User is not authenticated, showing Authentication UI");
                }
            } else {
                // Services not initialized yet, show authentication UI
                Show();
                Debug.Log("Unity Services not initialized, showing Authentication UI");
            }
        } catch (System.Exception e) {
            Debug.LogWarning($"Error checking authentication status: {e.Message}");
            Show(); // Default to showing the UI if there's an error
        }
    }

    public void Hide() {
        gameObject.SetActive(false);
    }

    public void Show() {
        gameObject.SetActive(true);
    }
}