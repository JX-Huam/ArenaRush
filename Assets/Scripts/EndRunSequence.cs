using System;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Dan.Main;

public class EndRunSequence : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject fadeOut;
    [SerializeField] private TextMeshProUGUI placeText;
    [SerializeField] private TextMeshProUGUI tapToContinueText;
    [SerializeField] private Transform spectatorUI; // Reference to spectator UI to hide it
    [SerializeField] private Transform ScoreUI;

    [Header("Podium Settings")]
    [SerializeField] private Transform podiumViewPosition; // Position where camera point to podium
    [SerializeField] private Transform firstPlacePosition; // Position on podium for 1st place player
    [SerializeField] private Transform secondPlacePosition; // Position on podium for 2nd place player
    [SerializeField] private Transform thirdPlacePosition; // Position on podium for 3rd place player
    
    [SerializeField] private Transform firstPlacePositionUI; // Position for 1st place UI
    [SerializeField] private Transform secondPlacePositionUI; // Position for 2nd place UI
    [SerializeField] private Transform thirdPlacePositionUI; // Position for 3rd place UI

    [Header("Player Stats Settings")]
    [SerializeField] private GameObject playerStatsPrefab; // Prefab for player stats display
    
    // Input System fields
    private PlayerInput playerInput;
    private bool canProceed = false;
    private Camera mainCamera;
    private string playerName;
    private int localPlayerRank = 0;
    private int playerDistance = 0;

    private void Awake()
    {
        mainCamera = Camera.main;
        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogWarning("PlayerInput component missing on EndRunSequence GameObject");
            playerInput = GetComponent<PlayerInput>() ?? gameObject.AddComponent<PlayerInput>();
        }

        if (tapToContinueText == null)
        {
            Debug.LogWarning("Tap to continue text not assigned in inspector for EndRunSequence");
        }

        // Disable the component until it's activated by the GameManager
        enabled = false;
    }

    private void OnEnable()
    {
        Debug.Log("EndRunSequence has been enabled");
        ScoreUI.gameObject.SetActive(false); // Hide score UI at the start
        
        // Get player name from local source
        GetPlayerName();
        
        // Hide spectator UI if it's active
        if (spectatorUI != null && spectatorUI.gameObject.activeSelf) 
            spectatorUI.gameObject.SetActive(false);
        
        // Start the end sequence
        StartCoroutine(EndSequence());
    }

    private IEnumerator EndSequence()
    {
        Debug.Log("Starting end sequence with podium view");
        
        // Wait a brief moment to ensure all network data is synced
        yield return new WaitForSeconds(0.5f);
        
        // Get the local player's rank
        DetermineLocalPlayerRank();

        if (PlayerMove.Instance != null && PlayerMove.Instance.IsOwner)
        {
            NetworkObject localPlayerNetObj = PlayerMove.Instance.GetComponent<NetworkObject>();
            if (localPlayerNetObj != null && GameManager.Instance != null)
            {
                playerDistance = GameManager.Instance.GetPlayerDistance(localPlayerNetObj);
                Debug.Log($"Player distance for leaderboard: {playerDistance}");
                
                // Submit to leaderboard
                SubmitToLeaderboard();
            }
        }
        
        // Update player rank UI text
        UpdatePlayerRankUI(localPlayerRank);
        
        // Move camera directly to podium view position
        SetCameraToPodiumView();
        
        // Position players on podium
        PositionPlayersOnPodium();
        
        // Create and display player stats UI
        DisplayPlayerStats();
        
        // Show tap to continue message
        yield return new WaitForSeconds(0.5f);
        canProceed = true;
        if (tapToContinueText != null)
        {
            tapToContinueText.gameObject.SetActive(true);
            StartCoroutine(PulseTapText());
        }
    }

    private void SubmitToLeaderboard()
    {
        string publicLeaderboardKey = "756e0fbdb9150586dde0292f3e4ec3345651d5af5334702e3dd9382594911ac1";
        string playerName = "Unknown Player";

        // Try to get player name from PlayerPrefs
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            playerName = PlayerPrefs.GetString("PlayerName");
        }
        
        LeaderboardCreator.UploadNewEntry(publicLeaderboardKey, playerName, playerDistance, ((msg) => {
                Leaderboards.EndlessRunnerDashboard.ResetPlayer();
        }));
    }

    private void GetPlayerName()
    {
        playerName = "Unknown Player";
        
        try
        {
            // Try to get player name from PlayerPrefs
            if (PlayerPrefs.HasKey("PlayerName"))
            {
                playerName = PlayerPrefs.GetString("PlayerName");
                Debug.Log($"Got player name: {playerName}");
            }
            else
            {
                Debug.LogWarning("PlayerName not found in PlayerPrefs, using default name");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting player name: {e.Message}");
        }
    }

    private void DetermineLocalPlayerRank()
    {
        // Get local player's NetworkObject
        if (PlayerMove.Instance != null && PlayerMove.Instance.IsOwner)
        {
            NetworkObject localPlayerNetObj = PlayerMove.Instance.GetComponent<NetworkObject>();
            if (localPlayerNetObj != null && GameManager.Instance != null)
            {
                localPlayerRank = GameManager.Instance.GetPlayerRank(localPlayerNetObj);
            }
        }
        
        // If rank is still 0, get it from GameManager's crashed count
        if (localPlayerRank == 0 && GameManager.Instance != null)
        {
            // This is a fallback method - may not be accurate
            int crashedCount = GameManager.Instance.GetPlayerCrashedNumber();
            int maxPlayers = 3; // Assuming 3 players max
            localPlayerRank = (maxPlayers - crashedCount) + 1;
        }
    }

    private void UpdatePlayerRankUI(int rank)
    {
        if (placeText == null) return;
        
        switch (rank)
        {
            case 1:
                placeText.text = $"{playerName}, YOU ARE IN 1ST PLACE!";
                break;
            case 2:
                placeText.text = $"{playerName}, YOU ARE IN 2ND PLACE!";
                break;
            case 3:
                placeText.text = $"{playerName}, YOU ARE IN 3RD PLACE!";
                break;
            default:
                placeText.text = $"{playerName}, YOU FINISHED THE RACE!";
                break;
        }
    }

    private void SetCameraToPodiumView()
    {
        if (mainCamera == null || podiumViewPosition == null)
        {
            Debug.LogError("Missing camera or podium view position reference!");
            return;
        }
        
        // Simply set camera to podium view position
        mainCamera.transform.position = podiumViewPosition.position;
        mainCamera.transform.rotation = podiumViewPosition.rotation;
        
        Debug.Log("Camera moved to podium view position");
    }

    private void PositionPlayersOnPodium()
    {
        // Make sure we have the position references
        if (firstPlacePosition == null || secondPlacePosition == null || thirdPlacePosition == null)
        {
            Debug.LogError("Missing podium position references!");
            return;
        }
        
        // Find all player objects in the scene
        PlayerMove[] allPlayers = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
        
        foreach (PlayerMove player in allPlayers)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            
            player.canMove.Value = false;

            // Get player's rank from GameManager
            int rank = GameManager.Instance.GetPlayerRank(netObj);
            Transform targetPosition = null;
            
            switch (rank)
            {
                case 1:
                    targetPosition = firstPlacePosition;
                    break;
                case 2:
                    targetPosition = secondPlacePosition;
                    break;
                case 3:
                    targetPosition = thirdPlacePosition;
                    break;
            }
            
            if (targetPosition != null)
            {
                // Position player on podium
                player.gameObject.transform.position = targetPosition.position;
                player.gameObject.transform.rotation = targetPosition.rotation;
                
                Debug.Log("Players positioned on podium");
                // Make sure they're visible
                player.gameObject.SetActive(true);
                
                // Try to play an appropriate animation if available
                Animator animator = player.GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    try
                    {
                        // First place gets a celebration animation, others get idle
                        if (rank == 1)
                            animator.Play("Victory");  // Or any celebration animation you have
                        else
                            animator.Play("Clapping");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"Failed to play animation: {e.Message}");
                    }
                }
            }

            
        }
        
        
    }

    private void DisplayPlayerStats()
    {
        if (playerStatsPrefab == null)
        {
            Debug.LogError("Missing player stats prefab!");
            return;
        }
        
        // Make sure we have UI position references
        if (firstPlacePositionUI == null || secondPlacePositionUI == null || thirdPlacePositionUI == null)
        {
            Debug.LogError("Missing UI position references!");
            return;
        }
        
        // Clean up any existing stats UIs
        CleanupExistingStatsUI(firstPlacePositionUI);
        CleanupExistingStatsUI(secondPlacePositionUI);
        CleanupExistingStatsUI(thirdPlacePositionUI);
        
        // Find all player objects
        PlayerMove[] allPlayers = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
        
        foreach (PlayerMove player in allPlayers)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj == null) continue;
            
            // Get player data from GameManager
            int rank = GameManager.Instance.GetPlayerRank(netObj);
            int coins = GameManager.Instance.GetPlayerCoinCount(player.gameObject);
            int distance = GameManager.Instance.GetPlayerDistance(netObj);
            
            string displayName;
            GameManager.PlayerData playerData = GameManager.Instance.GetPlayerData(netObj.NetworkObjectId);
            if (!string.IsNullOrEmpty(playerData.PlayerName.ToString()))
            {
                displayName = playerData.PlayerName.ToString();
            }
            else if (netObj.IsOwner && PlayerPrefs.HasKey("PlayerName"))
            {
                // Fallback for local player
                displayName = PlayerPrefs.GetString("PlayerName");
            }
            else
            {
                // Last resort fallback
                displayName = $"Player #{netObj.OwnerClientId}";
            }
            
            // Determine UI position based on rank
            Transform uiPosition = null;
            switch (rank)
            {
                case 1:
                    uiPosition = firstPlacePositionUI;
                    break;
                case 2:
                    uiPosition = secondPlacePositionUI;
                    break;
                case 3:
                    uiPosition = thirdPlacePositionUI;
                    break;
            }
            
            if (uiPosition == null) continue;
            
            // Create stats UI
            GameObject statsUI = Instantiate(playerStatsPrefab, uiPosition);
            statsUI.transform.localPosition = Vector3.zero; // Reset local position
            statsUI.transform.localRotation = Quaternion.identity; // Reset local rotation
            
            // Set up stats display
            PlayerStatsUI statsComponent = statsUI.GetComponent<PlayerStatsUI>();
            if (statsComponent != null)
            {
                statsComponent.SetPlayerStats(displayName, coins, distance);
            }
        }
        
        Debug.Log("Player stats displayed at UI positions");
    }
    
    private void CleanupExistingStatsUI(Transform parent)
    {
        // Remove any existing UI elements
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    // Input System callback for screen tap/click
    public void OnTapScreen(InputAction.CallbackContext context)
    {
        if (context.performed && canProceed)
        {
            Debug.Log("Screen tap detected, continuing to next scene");
            ContinueToNextScene();
        }
    }

    // Continue to next scene
    private void ContinueToNextScene()
    {
        if (canProceed)
        {
            // Prevent duplicate calls
            canProceed = false;
            
            // Play fade out animation
            if (fadeOut != null)
            {
                fadeOut.SetActive(true);
            }
            
            // Load main menu scene after a short delay for fade out
            StartCoroutine(LoadNextScene());
        }
    }
    
    IEnumerator PulseTapText()
    {
        if (tapToContinueText != null)
        {
            while (canProceed)
            {
                // Pulse animation for "Tap to continue" text
                for (float f = 0.3f; f <= 1.0f; f += 0.05f)
                {
                    Color currentColor = tapToContinueText.color;
                    currentColor.a = f;
                    tapToContinueText.color = currentColor;
                    yield return new WaitForSeconds(0.05f);
                }
                
                for (float f = 1.0f; f >= 0.3f; f -= 0.05f)
                {
                    Color currentColor = tapToContinueText.color;
                    currentColor.a = f;
                    tapToContinueText.color = currentColor;
                    yield return new WaitForSeconds(0.05f);
                }
            }
        }
    }
    
    // In EndRunSequence.cs, replace the LoadNextScene method:
    IEnumerator LoadNextScene()
    {
        // Wait for fade out animation
        yield return new WaitForSeconds(1.5f);
        
        // Use the new reset manager instead of directly shutting down NetworkManager
        if (NetworkResetManager.Instance != null)
        {
            NetworkResetManager.Instance.ResetNetworkState();
        }
        else
        {
            // Fallback if the reset manager isn't available
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene(0);
        }
    }
}