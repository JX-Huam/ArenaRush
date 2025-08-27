using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class SpectatorUI : MonoBehaviour
{
    public static SpectatorUI Instance;

    [Header("UI Elements")]
    [SerializeField] private GameObject spectatorUI;
    [SerializeField] private TextMeshProUGUI spectatingPlayerNameText;
    [SerializeField] private Button nextPlayerButton;
    [SerializeField] private Button previousPlayerButton;

    [Header("Camera Settings")]
    [SerializeField] private float cameraFollowSmoothTime = 0.15f;

    private Camera mainCamera;
    [SerializeField] private List<PlayerMove> activePlayers = new List<PlayerMove>();
    private int currentPlayerIndex = 0;
    private Vector3 cameraVelocity = Vector3.zero;
    private bool isSpectating = false;

    private void Awake()
    {
        Instance = this;
        mainCamera = Camera.main;
        spectatorUI.SetActive(false);
    }

    private void Start()
    {
        // Setup button listeners
        nextPlayerButton.onClick.AddListener(SwitchToNextPlayer);
        previousPlayerButton.onClick.AddListener(SwitchToPreviousPlayer);

        // Subscribe to player elimination (not just crash)
        if (PlayerMove.Instance != null)
        {
            // Subscribe to the lives change to detect elimination
            PlayerMove.Instance.playerLives.OnValueChanged += OnPlayerLivesChanged;
            Debug.Log("SpectatorUI: Subscribed to player lives changes");
        }
        else
        {
            Debug.LogWarning("PlayerMove.Instance is null, waiting...");
            StartCoroutine(WaitForPlayerInstance());
        }
    }

    private IEnumerator WaitForPlayerInstance()
    {
        while (PlayerMove.Instance == null)
        {
            yield return null;
        }
        
        // Subscribe to lives changes instead of crash events
        PlayerMove.Instance.playerLives.OnValueChanged += OnPlayerLivesChanged;
        Debug.Log("SpectatorUI: Successfully subscribed to player lives changes");
    }

    private void OnLocalPlayerEliminated()
    {
        Debug.Log("Local player eliminated, entering spectator mode");
        StartSpectating();
    }

    private void OnPlayerLivesChanged(int previousLives, int newLives)
    {
        // Only start spectating if the local player is eliminated (0 lives)
        if (newLives <= 0 && PlayerMove.Instance != null && PlayerMove.Instance.IsOwner)
        {
            Debug.Log("Local player eliminated (0 lives), entering spectator mode");
            StartSpectating();
        }
        else if (newLives < previousLives && newLives > 0)
        {
            Debug.Log($"Local player lost a life but still has {newLives} remaining");
        }
    }

    public void StartSpectating()
    {
        // Prevent double activation
        if (isSpectating) return;

        isSpectating = true;
        FindActivePlayers();

        if (activePlayers.Count > 0)
        {
            spectatorUI.SetActive(true);
            currentPlayerIndex = 0;
            SwitchToPlayer(currentPlayerIndex);
        }
        else
        {
            Debug.Log("No active players to spectate. Going directly to end sequence.");
            SkipToEndSequence();
        }
    }

    private void FindActivePlayers()
    {
        activePlayers.Clear();
        
        // Find all PlayerMove components in the scene
        PlayerMove[] allPlayers = FindObjectsByType<PlayerMove>(FindObjectsSortMode.None);
        
        foreach (PlayerMove player in allPlayers)
        {
            // Only add players that are still alive (have lives > 0) and not the local player
            if (player != PlayerMove.Instance && !IsPlayerEliminated(player))
            {
                activePlayers.Add(player);
                Debug.Log($"Added active player to spectate: {player.gameObject.name} (Lives: {player.playerLives.Value})");
            }
        }
    }

    private void SwitchToNextPlayer()
    {
        if (activePlayers.Count == 0) return;
        
        currentPlayerIndex = (currentPlayerIndex + 1) % activePlayers.Count;
        Debug.Log($"Switching to player index: {currentPlayerIndex}");
        SwitchToPlayer(currentPlayerIndex);
    }

    private void SwitchToPreviousPlayer()
    {
        if (activePlayers.Count == 0) return;
        
        currentPlayerIndex = (currentPlayerIndex - 1 + activePlayers.Count) % activePlayers.Count;
        Debug.Log($"Switching to player index: {currentPlayerIndex}");
        SwitchToPlayer(currentPlayerIndex);
    }

    private void SwitchToPlayer(int index)
    {
        if (activePlayers.Count == 0 || index < 0 || index >= activePlayers.Count)
        {
            Debug.LogWarning("Cannot switch player: Invalid index or no active players");
            return;
        }

        // Update UI with player name and lives if available
        string playerName = "Unknown Player";
        int playerLives = 0;
        
        NetworkObject playerNetObj = activePlayers[index].GetComponent<NetworkObject>();
        if (playerNetObj != null)
        {
            ulong clientId = playerNetObj.OwnerClientId;
            playerLives = activePlayers[index].playerLives.Value;
            
            // Try to get player name from GameManager
            if (GameManager.Instance != null)
            {
                var playerData = GameManager.Instance.GetPlayerData(playerNetObj.NetworkObjectId);
                if (!string.IsNullOrEmpty(playerData.PlayerName.ToString()))
                {
                    playerName = playerData.PlayerName.ToString();
                }
                else
                {
                    playerName = $"Player #{clientId}";
                }
            }
            else
            {
                playerName = $"Player #{clientId}";
            }
        }
        
        // Show player name and lives
        spectatingPlayerNameText.text = $"{playerName}";
    }

    private void Update()
    {
        if (isSpectating && activePlayers.Count > 0)
        {
            UpdateSpectatorCamera();
            CheckForPlayerStateChanges();
        }
    }

    private void UpdateSpectatorCamera()
    {
        if (currentPlayerIndex < 0 || currentPlayerIndex >= activePlayers.Count) return;
        
        PlayerMove targetPlayer = activePlayers[currentPlayerIndex];
        if (targetPlayer != null && mainCamera != null)
        {
            // Check if the player is still alive (even if temporarily stunned)
            if (IsPlayerEliminated(targetPlayer))
            {
                // This player got eliminated, trigger a refresh
                CheckForPlayerStateChanges();
                return;
            }
            
            Vector3 newCameraPosition = new Vector3(
                targetPlayer.transform.position.x,
                mainCamera.transform.position.y,
                targetPlayer.transform.position.z - 5f);
                
            // Smoothly move camera to new position
            mainCamera.transform.position = Vector3.SmoothDamp(
                mainCamera.transform.position, 
                newCameraPosition, 
                ref cameraVelocity, 
                cameraFollowSmoothTime
            );
        }
    }

    private void CheckForPlayerStateChanges()
    {
        // Check if any players have been eliminated (0 lives) or are no longer valid
        bool needsRefresh = false;
        
        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            if (activePlayers[i] == null || IsPlayerEliminated(activePlayers[i]))
            {
                Debug.Log($"Removing eliminated player from spectator list: {activePlayers[i]?.gameObject.name}");
                activePlayers.RemoveAt(i);
                needsRefresh = true;
                
                // If we're removing the current player, adjust index
                if (i <= currentPlayerIndex)
                {
                    currentPlayerIndex = Mathf.Max(0, currentPlayerIndex - 1);
                }
            }
        }
        
        if (needsRefresh)
        {
            if (activePlayers.Count > 0)
            {
                SwitchToPlayer(currentPlayerIndex);
            }
            else
            {
                Debug.Log("No more active players to spectate");
                SkipToEndSequence();
            }
        }
    }
    
    private bool IsPlayerEliminated(PlayerMove player)
    {
        if (player == null) return true;
        
        // Player is eliminated if they have 0 lives
        return player.playerLives.Value <= 0;
    }

    public void SkipToEndSequence()
    {
        if (spectatorUI != null)
        {
            spectatorUI.SetActive(false);
        }

        isSpectating = false;

        // Ensure all players have unique ranks before showing the end screen
        if (GameManager.Instance != null && GameManager.Instance.IsServer)
        {
            GameManager.Instance.ForceUniqueRanks();
        }

        // Show the end run sequence
        EndRunSequence endRunSequence = FindFirstObjectByType<EndRunSequence>();
        if (endRunSequence != null)
        {
            endRunSequence.enabled = true;
            Debug.Log("Skipped to end sequence");
        }
        else
        {
            Debug.LogError("EndRunSequence component not found!");
        }
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions
        if (PlayerMove.Instance != null)
        {
            PlayerMove.Instance.playerLives.OnValueChanged -= OnPlayerLivesChanged;
        }
    }
}