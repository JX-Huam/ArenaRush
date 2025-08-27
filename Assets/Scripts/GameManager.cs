using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.SceneManagement;
using Unity.Netcode.Components;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    // Network variable for crashed players count
    public NetworkVariable<int> playerCrashedNumber = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private NetworkVariable<int> avatarsLoadedCount = new NetworkVariable<int>(0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public event EventHandler OnAllAvatarsLoaded;

    private Dictionary<ulong, bool> playerAvatarsLoadedLocal = new Dictionary<ulong, bool>();
    // Player data structure
    [Serializable]
    public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
    {
        public ulong NetworkObjectId;
        public int Rank;
        public int Coins;
        public int Distance;
        public int Lives;
        public FixedString64Bytes PlayerName;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref NetworkObjectId);
            serializer.SerializeValue(ref Rank);
            serializer.SerializeValue(ref Coins);
            serializer.SerializeValue(ref Distance);
            serializer.SerializeValue(ref Lives);
            serializer.SerializeValue(ref PlayerName);
        }

        public bool Equals(PlayerData other)
        {
            return NetworkObjectId == other.NetworkObjectId &&
                Rank == other.Rank &&
                Coins == other.Coins &&
                Distance == other.Distance &&
                Lives == other.Lives &&
                PlayerName == other.PlayerName;
        }
    }

    // NetworkList to track all player data
    private NetworkList<PlayerData> playerDataList;

    // UI elements
    [SerializeField] private GameObject coinCountDisplay;
    [SerializeField] private GameObject disDisplay;

    // Race settings
    [SerializeField] private GameObject[] sections;
    private float zPos = 20;
    public bool createSection = false;
    public int secNum;

    // Player management
    [SerializeField] private int maxPlayer;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;
    private List<ulong> spawnedClients = new List<ulong>();
    private Dictionary<ulong, GameObject> playerInstances = new Dictionary<ulong, GameObject>();
    private Dictionary<ulong, string> playerAvatarUrls = new Dictionary<ulong, string>();

    public event EventHandler OnAllPlayerJoined;

    [SerializeField] private RuntimeAnimatorController animatorController;

    // Add these fields to GameManager class
    [Header("Section Generation Optimization")]
    [SerializeField] private int maxActiveSections = 10; // Maximum sections at once
    [SerializeField] private float sectionGenerationDistance = 60f; // Distance ahead to generate
    [SerializeField] private float generationCheckInterval = 0.5f; // How often to check if new sections needed
    private int currentActiveSections = 0;
    private float lastGenerationCheck = 0f;
    private float furthestSectionZ = 20f; // Track the furthest generated section

    private void Awake()
    {
        Instance = this;

        maxPlayer = int.Parse(PlayerPrefs.GetString("MaxPlayers"));

        playerDataList = new NetworkList<PlayerData>();
        RPMAvatarManager.Instance.OnAvatarLoaded += GameManager_OnAvatarLoaded;

        // Initialize the empty dictionary
        playerAvatarsLoadedLocal = new Dictionary<ulong, bool>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Reset avatar loading count
            avatarsLoadedCount.Value = 0;
            playerAvatarsLoadedLocal.Clear();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;

            if (IsHost)
            {
                Debug.Log("Host is spawning itself.");
                OnClientConnected(NetworkManager.Singleton.LocalClientId);
            }
        }

        // Set up event handler for player data list changes
        playerDataList.OnListChanged += PlayerDataList_OnListChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        Debug.Log("GameManager OnNetworkDespawn - cleaning up references");

        // Clean up references to avoid accessing destroyed objects
        spawnedClients.Clear();
        playerInstances.Clear();
        playerAvatarUrls.Clear();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void PlayerDataList_OnListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        // This will be called on all clients when the list changes
        // We can use this to update UI or other client-side elements
        if (PlayerMove.Instance != null && PlayerMove.Instance.IsOwner)
        {
            UpdateLocalPlayerUI();
        }
    }

    private void GameManager_OnAvatarLoaded(object sender, RPMAvatarManager.AvatarLoadedEventArgs e)
    {
        // REMOVE the section that calls NotifyAvatarLoadedServerRpc
        // We're now handling this in LoadAvatarLocallyCoroutine instead

        if (CheckIfAllPlayerJoined())
        {
            Debug.Log("All players joined, waiting for avatars to load");
        }
    }

    // track avatar loading
    [Rpc(SendTo.Server)]
    public void NotifyAvatarLoadedServerRpc(ulong clientId)
    {
        if (!IsServer)
        {
            Debug.LogError("NotifyAvatarLoadedServerRpc called on non-server!");
            return;
        }

        Debug.Log($"SERVER: Avatar loaded notification received for client {clientId}");

        // Update local tracking
        playerAvatarsLoadedLocal[clientId] = true;

        // Update the network variable that counts loaded avatars
        avatarsLoadedCount.Value++;

        // Check if all are loaded
        CheckAllAvatarsLoaded();
    }

    // Add this method to check if all avatars are loaded
    private void CheckAllAvatarsLoaded()
    {
        if (!IsServer) return;
        Debug.Log(avatarsLoadedCount.Value + " avatars loaded out of " + maxPlayer + " players");
        // We've loaded all avatars if the count matches the number of spawned clients
        if (avatarsLoadedCount.Value >= spawnedClients.Count && spawnedClients.Count == maxPlayer)
        {
            OnAllAvatarsLoaded?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"clientId joined {clientId}");

        if (spawnedClients.Contains(clientId))
        {
            Debug.LogWarning($"Client {clientId} connection triggered twice, ignoring duplicate");
            return;
        }

        // Clean up any null references in playerInstances before using them
        CleanupInvalidPlayerReferences();

        // DEBUG: Log all currently used positions
        Debug.Log("Currently spawned positions:");
        foreach (var player in playerInstances.Values)
        {
            if (player != null && player.transform != null)
            {
                Debug.Log($"- {player.transform.position}");
            }
        }

        // Find an available spawn point
        Transform selectedSpawnPoint = null;

        // Try to find a spawn point that isn't already occupied
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            bool isUsed = false;
            Vector3 pointPosition = spawnPoints[i].position;

            foreach (var player in playerInstances.Values)
            {
                if (player != null && player.transform != null)
                {
                    if (Vector3.Distance(player.transform.position, pointPosition) < 1.0f)
                    {
                        isUsed = true;
                        Debug.Log($"Spawn point {i} at {pointPosition} is already in use");
                        break;
                    }
                }
            }

            if (!isUsed)
            {
                selectedSpawnPoint = spawnPoints[i];
                Debug.Log($"Selected spawn point {i} at {selectedSpawnPoint.position} for client {clientId}");
                break;
            }
        }

        // If all points are used, fall back to index-based selection
        if (selectedSpawnPoint == null)
        {
            int index = spawnedClients.Count % spawnPoints.Length;
            selectedSpawnPoint = spawnPoints[index];
            Debug.Log($"All spawn points in use, falling back to point {index} at {selectedSpawnPoint.position}");
        }

        // Create the player instance at the selected position
        Vector3 spawnPos = selectedSpawnPoint.position;

        GameObject playerInstance = Instantiate(playerPrefab, spawnPos, selectedSpawnPoint.rotation);
        Debug.Log($"Instantiated player for client {clientId} at {spawnPos}");

        // Setup player avatar which will handle setting up animator components
        StartCoroutine(SetupPlayerAvatar(clientId, playerInstance, selectedSpawnPoint));
    }

    // Add this new method to clean up invalid references
    private void CleanupInvalidPlayerReferences()
    {
        List<ulong> keysToRemove = new List<ulong>();

        foreach (var entry in playerInstances)
        {
            if (entry.Value == null)
            {
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            playerInstances.Remove(key);
            Debug.Log($"Removed invalid player reference for client {key}");
        }
    }

    private IEnumerator SetupPlayerAvatar(ulong clientId, GameObject playerInstance, Transform spawnPoint)
    {
        playerInstances[clientId] = playerInstance;
        spawnedClients.Add(clientId);

        Debug.Log($"Added client {clientId} to tracking. Total count: {spawnedClients.Count}/{maxPlayer}");

        if (IsServer)
        {
            playerAvatarsLoadedLocal[clientId] = false;
        }

        NetworkObject networkObj = playerInstance.GetComponent<NetworkObject>();
        networkObj.SpawnAsPlayerObject(clientId);

        // First, try to get the player's own avatar URL
        PlayerMove playerMove = playerInstance.GetComponent<PlayerMove>();
        string avatarUrl = "https://models.readyplayer.me/67b417f62600df572bf39f64.glb"; // Default URL

        // Wait briefly for network variables to sync
        yield return new WaitForSeconds(0.5f);

        // Get the player's avatar URL if available
        if (playerMove != null && !string.IsNullOrEmpty(playerMove.avatarUrl.Value.ToString()))
        {
            avatarUrl = playerMove.avatarUrl.Value.ToString();
            Debug.Log($"Using player {clientId}'s provided avatar URL: {avatarUrl}");
        }

        // Store the URL for this player
        playerAvatarUrls[clientId] = avatarUrl;

        string playerNameStr = "Player_" + clientId;
        if (playerMove != null && !string.IsNullOrEmpty(playerMove.playerName.Value.ToString()))
        {
            playerNameStr = playerMove.playerName.Value.ToString();
            Debug.Log($"Using player {clientId}'s provided name: {playerNameStr}");
        }

        // Initialize player data after NetworkObject is spawned
        if (IsServer)
        {
            // Add player to the network list
            AddOrUpdatePlayerData(networkObj.NetworkObjectId,
                                 0, // Initial rank
                                 0, // Initial coins
                                 0, // Initial distance
                                 playerNameStr);
        }

        LoadAvatarOnAllClientsRpc(networkObj.NetworkObjectId, avatarUrl);

        if (IsServer && clientId != NetworkManager.Singleton.LocalClientId)
        {
            GameObject host = playerInstances[NetworkManager.Singleton.LocalClientId];
            NetworkObject hostNetObj = host.GetComponent<NetworkObject>();
            string hostUrl = playerAvatarUrls.ContainsKey(NetworkManager.Singleton.LocalClientId) ?
                             playerAvatarUrls[NetworkManager.Singleton.LocalClientId] :
                             "https://models.readyplayer.me/67b417f62600df572bf39f64.glb";

            LoadAvatarForSpecificClientRpc(hostNetObj.NetworkObjectId, hostUrl, clientId);
        }

        if (IsServer && spawnedClients.Count == maxPlayer)
        {
            Debug.Log("All players have joined!");
            CheckIfAllPlayerJoined();
        }
    }

    // Helper method to add or update player data in the network list
    private void AddOrUpdatePlayerData(ulong networkObjectId, int rank, int coins, int distance, string playerName)
    {
        if (!IsServer) return;

        // Look for existing player data
        for (int i = 0; i < playerDataList.Count; i++)
        {
            PlayerData existingData = playerDataList[i];
            if (existingData.NetworkObjectId == networkObjectId)
            {
                // Update existing player data
                PlayerData updatedData = new PlayerData
                {
                    NetworkObjectId = networkObjectId,
                    Rank = rank,
                    Coins = coins,
                    Distance = distance,
                    Lives = existingData.Lives, // Keep existing lives
                    PlayerName = playerName
                };

                playerDataList[i] = updatedData;
                return;
            }
        }

        // Player not found, add new entry
        PlayerData newPlayerData = new PlayerData
        {
            NetworkObjectId = networkObjectId,
            Rank = rank,
            Coins = coins,
            Distance = distance,
            Lives = 3, // 🆕 Start with 3 lives
            PlayerName = playerName
        };

        playerDataList.Add(newPlayerData);
        Debug.Log($"Added player data for NetworkObjectId {networkObjectId}, Name: {playerName}, Lives: 3");
    }

    // Helper method to update a specific field for a player
    private void UpdatePlayerField(ulong networkObjectId, string fieldName, int value)
    {
        if (!IsServer) return;

        for (int i = 0; i < playerDataList.Count; i++)
        {
            PlayerData existingData = playerDataList[i];
            if (existingData.NetworkObjectId == networkObjectId)
            {
                PlayerData updatedData = existingData;

                // Update the specified field
                switch (fieldName)
                {
                    case "Rank":
                        updatedData.Rank = value;
                        break;
                    case "Coins":
                        updatedData.Coins = value;
                        break;
                    case "Distance":
                        updatedData.Distance = value;
                        break;
                }

                playerDataList[i] = updatedData;
                return;
            }
        }
    }

    // Helper method to increment a player's field value
    private void IncrementPlayerField(ulong networkObjectId, string fieldName, int increment = 1)
    {
        if (!IsServer) return;

        for (int i = 0; i < playerDataList.Count; i++)
        {
            PlayerData existingData = playerDataList[i];
            if (existingData.NetworkObjectId == networkObjectId)
            {
                PlayerData updatedData = existingData;

                // Increment the specified field
                switch (fieldName)
                {
                    case "Rank":
                        updatedData.Rank += increment;
                        break;
                    case "Coins":
                        updatedData.Coins += increment;
                        break;
                    case "Distance":
                        updatedData.Distance += increment;
                        break;
                }

                playerDataList[i] = updatedData;
                return;
            }
        }
    }

    // Helper method to get player data
    public PlayerData GetPlayerData(ulong networkObjectId)
    {
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i].NetworkObjectId == networkObjectId)
            {
                return playerDataList[i];
            }
        }

        // Return empty data if not found
        return new PlayerData
        {
            NetworkObjectId = 0,
            Rank = 0,
            Coins = 0,
            Distance = 0,
            PlayerName = "Unknown"
        };
    }

    private void UpdatePlayerLives(ulong networkObjectId, int newLives)
    {
        if (!IsServer) return;

        for (int i = 0; i < playerDataList.Count; i++)
        {
            PlayerData existingData = playerDataList[i];
            if (existingData.NetworkObjectId == networkObjectId)
            {
                PlayerData updatedData = existingData;
                updatedData.Lives = newLives;
                playerDataList[i] = updatedData;
                Debug.Log($"Updated lives for player {networkObjectId}: {newLives}");
                return;
            }
        }
    }

    public int GetPlayerLives(NetworkObject playerNetObject)
    {
        if (playerNetObject == null) return 0;

        return GetPlayerData(playerNetObject.NetworkObjectId).Lives;
    }



    [Rpc(SendTo.ClientsAndHost)]
    private void LoadAvatarOnAllClientsRpc(ulong networkObjectId, string avatarUrl)
    {
        // Find the player object with matching NetworkObjectId
        NetworkObject targetObj = null;
        foreach (NetworkObject netObj in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
        {
            if (netObj.NetworkObjectId == networkObjectId)
            {
                targetObj = netObj;
                break;
            }
        }

        if (targetObj != null)
        {
            StartCoroutine(LoadAvatarLocallyCoroutine(targetObj.gameObject, avatarUrl));
        }
        else
        {
            Debug.LogError($"Failed to find NetworkObject with ID {networkObjectId}");
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void LoadAvatarForSpecificClientRpc(ulong networkObjectId, string avatarUrl, ulong targetClientId)
    {
        // Only process this if we are the target client
        if (NetworkManager.Singleton.LocalClientId != targetClientId)
            return;

        // Find the player object with matching NetworkObjectId
        NetworkObject targetObj = null;
        foreach (NetworkObject netObj in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
        {
            if (netObj.NetworkObjectId == networkObjectId)
            {
                targetObj = netObj;
                break;
            }
        }

        if (targetObj != null)
        {
            Debug.Log($"Late joiner loading avatar for existing player: {targetObj.OwnerClientId}");
            StartCoroutine(LoadAvatarLocallyCoroutine(targetObj.gameObject, avatarUrl));
        }
    }

    private IEnumerator LoadAvatarLocallyCoroutine(GameObject playerObject, string avatarUrl)
    {
        Debug.Log($"Loading avatar locally for player object {playerObject.name} with URL {avatarUrl}");

        // Use RPMAvatarManager to load the avatar into this player object
        yield return StartCoroutine(RPMAvatarManager.Instance.LoadAvatarWithURL(playerObject.transform, avatarUrl));

        // set up the animation components locally
        Transform avatarTransform = null;

        for (int i = 0; i < playerObject.transform.childCount; i++)
        {
            Transform child = playerObject.transform.GetChild(i);
            if (child.GetComponentInChildren<Animator>() != null)
            {
                avatarTransform = child;
                break;
            }
        }

        if (avatarTransform != null)
        {
            // Get or add animator component
            Animator animator = avatarTransform.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                // Set animator controller
                if (GameManager.Instance.animatorController != null)
                {
                    animator.runtimeAnimatorController = GameManager.Instance.animatorController;
                }

                // Add NetworkAnimator component to same GameObject as Animator
                if (animator.gameObject.GetComponent<NetworkAnimator>() == null)
                {
                    NetworkAnimator networkAnim = animator.gameObject.AddComponent<NetworkAnimator>();
                    networkAnim.Animator = animator;
                    Debug.Log($"Added NetworkAnimator to avatar for synchronized animations");
                }
            }
            else
            {
                Debug.LogWarning($"No Animator component found on avatar for {playerObject.name}");
            }

            NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                ulong clientId = netObj.OwnerClientId;
                Debug.Log($"Avatar loaded for client {clientId}, notifying server");

                NotifyAvatarLoadedServerRpc(clientId);
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TriggerOnAllPlayerJoinedRpc()
    {
        OnAllPlayerJoined?.Invoke(this, EventArgs.Empty);
    }

    private void Update()
    {
        if (IsServer)
        {
            // Update distance and lives for all active players
            foreach (var playerEntry in playerInstances)
            {
                GameObject playerObj = playerEntry.Value;
                if (playerObj != null)
                {
                    PlayerMove playerMove = playerObj.GetComponent<PlayerMove>();
                    NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

                    if (playerMove != null && netObj != null)
                    {
                        // Calculate distance based on Z position
                        int newDistance = Mathf.FloorToInt(playerObj.transform.position.z);

                        // Get current player data
                        PlayerData playerData = GetPlayerData(netObj.NetworkObjectId);

                        // Update distance if increased
                        if (playerMove.canMove.Value && newDistance > playerData.Distance)
                        {
                            UpdatePlayerField(netObj.NetworkObjectId, "Distance", newDistance);
                        }

                        // 🆕 Sync lives from PlayerMove to GameManager data
                        if (playerMove.playerLives.Value != playerData.Lives)
                        {
                            UpdatePlayerLives(netObj.NetworkObjectId, playerMove.playerLives.Value);
                        }
                    }
                }
            }
        }

        // Update local player's UI
        if (PlayerMove.Instance != null && PlayerMove.Instance.IsOwner)
        {
            UpdateLocalPlayerUI();
        }
    }

    private bool IsPlayerEliminated(PlayerMove playerMove)
    {
        return playerMove.playerLives.Value <= 0;
    }

    // Add this to the existing ResetGameState method in GameManager
    public void ResetGameState()
    {
        Debug.Log("Resetting game state");
        
        // 🆕 Reset section generation tracking
        ResetSectionGeneration();
        
        // 🆕 Reset all player lives before other cleanup
        ResetAllPlayerLives();
        
        // Reset player data
        if (playerDataList != null)
        {
            playerDataList.Clear();
        }
        
        // Reset avatar loading tracking
        if (IsServer)
        {
            avatarsLoadedCount.Value = 0;
        }
        playerAvatarsLoadedLocal.Clear();
        
        // Properly clean up player instances
        foreach (var playerObj in playerInstances.Values)
        {
            if (playerObj != null)
            {
                // 🆕 Reset player lives and states before despawning
                PlayerMove playerMove = playerObj.GetComponent<PlayerMove>();
                if (playerMove != null)
                {
                    playerMove.playerLives.Value = 3;
                    playerMove.canMove.Value = false; // Reset movement
                }
                
                // Make sure to despawn networked objects properly
                NetworkObject netObj = playerObj.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned && IsServer)
                {
                    netObj.Despawn(true);
                }
                else if (playerObj != null)
                {
                    Destroy(playerObj);
                }
            }
        }
        
        // Clear tracking dictionaries
        spawnedClients.Clear();
        playerInstances.Clear();
        playerAvatarUrls.Clear();
        
        // Reset crashed player count
        if (IsServer)
        {
            playerCrashedNumber.Value = 0;
        }
        
        // 🔧 Updated: Don't manually reset zPos and createSection - handled by ResetSectionGeneration()
        Debug.Log("Game state reset complete - optimized section generation ready");
    }

    private void UpdateLocalPlayerUI()
    {
        NetworkObject localPlayerNetObj = PlayerMove.Instance.GetComponent<NetworkObject>();
        if (localPlayerNetObj != null && disDisplay != null && coinCountDisplay != null)
        {
            // Get local player data
            PlayerData localPlayerData = GetPlayerData(localPlayerNetObj.NetworkObjectId);

            // Update distance display
            disDisplay.GetComponent<TextMeshProUGUI>().text = localPlayerData.Distance.ToString();

            // Update coin display
            coinCountDisplay.GetComponent<TextMeshProUGUI>().text = localPlayerData.Coins.ToString();

            // Update lives display if it exists
            GameObject livesDisplay = GameObject.Find("LivesDisplay");
            if (livesDisplay != null)
            {
                livesDisplay.GetComponent<TextMeshProUGUI>().text = $"❤️ {localPlayerData.Lives}";
            }
        }
    }

    public void OnPlayerCrashed(object sender, EventArgs e)
    {
        Debug.Log("GameManager: Player crashed event received");

        PlayerMove playerMove = sender as PlayerMove;

        if (IsServer && playerMove != null)
        {
            // Only increment crashed count if player has no lives left
            if (IsPlayerEliminated(playerMove))
            {
                playerCrashedNumber.Value++;

                NetworkObject netObj = playerMove.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    Debug.Log($"Player with NetworkObjectId {netObj.NetworkObjectId} eliminated at distance {GetPlayerDistance(netObj)}");

                    // Check if all players are eliminated
                    if (playerCrashedNumber.Value == maxPlayer)
                    {
                        Debug.Log($"playercrashedNumber.Value: {playerCrashedNumber.Value}, maxPlayer: {maxPlayer}");
                        CalculateFinalRankings();
                        TriggerStopGenerateSectionRpc();
                        Debug.Log("GameManager: All players eliminated - stopping section generation");
                        StopAllCoroutines();
                    }
                    // If all-but-one players are eliminated, wait then calculate rankings
                    else if (playerCrashedNumber.Value == maxPlayer - 1)
                    {
                        StartCoroutine(DelayedFinalRankings());
                    }
                }
            }
            else
            {
                Debug.Log($"Player {playerMove.OwnerClientId} lost a life but is still in the game (Lives: {playerMove.playerLives.Value})");
            }
        }

        // Only show end sequence for local player if they're truly eliminated (0 lives)
        if (playerMove != null && playerMove.IsOwner && IsPlayerEliminated(playerMove))
        {
            // Stop any flashing coroutines and ensure avatar is visible
            playerMove.StopAllCoroutines();
            Transform avatarRoot = playerMove.GetAvatarRoot();
            if (avatarRoot != null)
            {
                avatarRoot.gameObject.SetActive(true);
            }

            Debug.Log("GameManager: Local player eliminated - activating spectator/end sequence");
            ShowEndRunSequenceForLocalPlayer();
        }
        else if (playerMove != null && playerMove.IsOwner)
        {
            Debug.Log($"GameManager: Local player lost a life but still has {playerMove.playerLives.Value} lives remaining");
        }
    }

    public void ResetAllPlayerLives()
    {
        if (!IsServer) return;

        foreach (var playerEntry in playerInstances)
        {
            GameObject playerObj = playerEntry.Value;
            if (playerObj != null)
            {
                PlayerMove playerMove = playerObj.GetComponent<PlayerMove>();
                if (playerMove != null)
                {
                    playerMove.playerLives.Value = 3;
                    Debug.Log($"Reset lives to 3 for player {playerMove.OwnerClientId}");
                }
            }
        }
    }

    private IEnumerator DelayedFinalRankings()
    {
        // Wait a moment to ensure the last player's distance is updated
        yield return new WaitForSeconds(1.0f);

        // Now calculate rankings
        CalculateFinalRankings();
    }

    public void ForceUniqueRanks()
    {
        CalculateFinalRankings();
    }

    private void CalculateFinalRankings()
    {
        if (!IsServer) return;

        Debug.Log("🏁 Calculating final rankings based on distance traveled");

        // Create a list to sort players by distance
        List<(ulong networkObjectId, int distance)> playerDistances = new List<(ulong, int)>();

        // Collect all players with their distances
        for (int i = 0; i < playerDataList.Count; i++)
        {
            PlayerData playerData = playerDataList[i];
            playerDistances.Add((playerData.NetworkObjectId, playerData.Distance));
            Debug.Log($"Player {playerData.NetworkObjectId} traveled distance: {playerData.Distance}");
        }

        // Sort by distance in descending order (highest distance first)
        playerDistances.Sort((a, b) => b.distance.CompareTo(a.distance));

        // Assign ranks based on distance (1st place = furthest distance)
        for (int i = 0; i < playerDistances.Count; i++)
        {
            int rank = i + 1; // 1st, 2nd, 3rd...
            UpdatePlayerField(playerDistances[i].networkObjectId, "Rank", rank);
            Debug.Log($"✅ Assigned rank {rank} to player {playerDistances[i].networkObjectId} with distance {playerDistances[i].distance}");
        }

        // Force sync the rankings to all clients
        SyncFinalRankingsClientRpc();
    }

    [ClientRpc]
    private void SyncFinalRankingsClientRpc()
    {
        Debug.Log("📊 Final rankings synced from server");
        // This forces the network list to update on all clients
    }

    // Helper method to find the surviving player and assign 1st place
    private void AssignFirstPlaceToSurvivor()
    {
        if (!IsServer) return;

        foreach (var playerObj in playerInstances.Values)
        {
            if (playerObj == null) continue;

            PlayerMove playerMove = playerObj.GetComponent<PlayerMove>();
            NetworkObject netObj = playerObj.GetComponent<NetworkObject>();

            if (playerMove != null && netObj != null && playerMove.canMove.Value)
            {
                // This player is still active, they must be the survivor
                UpdatePlayerField(netObj.NetworkObjectId, "Rank", 1); // Assign 1st place
                Debug.Log($"Assigned 1st place to survivor with NetworkObjectId {netObj.NetworkObjectId}");
                break;
            }
        }
    }

    private void ShowEndRunSequenceForLocalPlayer()
    {
        // If we're the server, make sure all players have unique ranks before showing the end sequence
        if (IsServer)
        {
            ForceUniqueRanks();
        }

        // Check if SpectatorUI exists
        SpectatorUI spectatorUI = FindFirstObjectByType<SpectatorUI>();

        if (spectatorUI != null)
        {
            // Start spectating instead of showing end sequence directly
            spectatorUI.StartSpectating();
            Debug.Log("GameManager: Started spectator mode");
        }
        else
        {
            // Fall back to original behavior if spectator UI doesn't exist
            Debug.LogWarning("GameManager: SpectatorUI not found, falling back to EndRunSequence");
            EndRunSequence endRunSequence = FindFirstObjectByType<EndRunSequence>();

            if (endRunSequence != null)
            {
                endRunSequence.enabled = true;
                Debug.Log("GameManager: EndRunSequence enabled directly");
            }
            else
            {
                Debug.LogError("GameManager: EndRunSequence component not found!");
            }
        }
    }

    public void OnCoinCollected(object sender, EventArgs e)
    {
        if (e is CollectCoin.PlayerCoinEventArgs coinArgs)
        {
            GameObject playerObject = coinArgs.PlayerObject;

            if (IsServer)
            {
                // Get player's NetworkObject for the key
                NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    // Increment the player's coin count
                    IncrementPlayerField(netObj.NetworkObjectId, "Coins");
                }
            }
        }
    }

    public void StartRunSequence_OnCountDownStart(object sender, EventArgs e)
    {
        TriggerGenerateSectionRpc();
    }

    [Rpc(SendTo.Server)]
    private void TriggerGenerateSectionRpc()
    {
        createSection = true;
        StartCoroutine(GenerateSection());
    }

    [Rpc(SendTo.Server)]
    private void TriggerStopGenerateSectionRpc()
    {
        createSection = false;
    }

    private bool CheckIfAllPlayerJoined()
    {
        bool result = false;
        if (spawnedClients.Count == maxPlayer)
        {
            result = true;
            // Trigger the OnAllPlayerJoined event when all players have joined
            // but BEFORE avatar loading is complete
            TriggerOnAllPlayerJoinedRpc();
        }
        Debug.Log("spawned client number: " + spawnedClients.Count + "/" + maxPlayer);
        return result;
    }

    // Get player's rank by NetworkObject
    public int GetPlayerRank(NetworkObject playerNetObject)
    {
        if (playerNetObject == null) return 0;

        return GetPlayerData(playerNetObject.NetworkObjectId).Rank;
    }

    // Get player's coins by GameObject (kept for backward compatibility)
    public int GetPlayerCoinCount(GameObject playerObject)
    {
        if (playerObject == null) return 0;

        NetworkObject netObj = playerObject.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            return GetPlayerData(netObj.NetworkObjectId).Coins;
        }

        return 0;
    }

    // Get player's distance by NetworkObject
    public int GetPlayerDistance(NetworkObject playerNetObject)
    {
        if (playerNetObject == null) return 0;

        return GetPlayerData(playerNetObject.NetworkObjectId).Distance;
    }

    // Get crashed player count
    public int GetPlayerCrashedNumber()
    {
        return playerCrashedNumber.Value;
    }

    // Get all player data for display
    public List<PlayerData> GetAllPlayerData()
    {
        List<PlayerData> allPlayerData = new List<PlayerData>();

        // Copy all player data from the NetworkList
        for (int i = 0; i < playerDataList.Count; i++)
        {
            allPlayerData.Add(playerDataList[i]);
        }

        return allPlayerData;
    }

    IEnumerator GenerateSection()
    {
        while (createSection)
        {
            // Check if we need new sections less frequently
            if (Time.time - lastGenerationCheck < generationCheckInterval)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            lastGenerationCheck = Time.time;

            // Get furthest player position
            float furthestPlayerZ = GetFurthestPlayerPosition();

            // Only generate if Players are getting close to the last generated section
            bool shouldGenerate = ShouldGenerateNewSection(furthestPlayerZ);

            if (shouldGenerate)
            {
                GenerateNewSection();
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                yield return new WaitForSeconds(generationCheckInterval);
            }
        }
    }

    private bool ShouldGenerateNewSection(float furthestPlayerZ)
    {
        // Don't generate if we have too many sections
        if (currentActiveSections >= maxActiveSections)
        {
            Debug.Log($"⚠️ Max sections reached ({currentActiveSections}/{maxActiveSections})");
            return false;
        }

        // Don't generate if players are still far from the last section
        float distanceToLastSection = furthestSectionZ - furthestPlayerZ;
        if (distanceToLastSection > sectionGenerationDistance)
        {
            return false;
        }

        Debug.Log($"✅ Need new section - Player at {furthestPlayerZ}, last section at {furthestSectionZ}");
        return true;
    }

    private void GenerateNewSection()
    {
        secNum = UnityEngine.Random.Range(0, sections.Length);
        Vector3 spawnPosition = new Vector3(0, 0, furthestSectionZ);

        GameObject section = Instantiate(sections[secNum], spawnPosition, Quaternion.identity);

        // Add section tracking component
        SectionTracker tracker = section.AddComponent<SectionTracker>();
        tracker.Initialize(this);

        NetworkObject netObj = section.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }

        furthestSectionZ += 20;
        currentActiveSections++;

        Debug.Log($"🏗️ Generated section at Z:{spawnPosition.z} | Active: {currentActiveSections}/{maxActiveSections}");
    }

    private float GetFurthestPlayerPosition()
    {
        float furthestZ = 0f;

        foreach (var playerEntry in playerInstances)
        {
            GameObject playerObj = playerEntry.Value;
            if (playerObj != null)
            {
                PlayerMove playerMove = playerObj.GetComponent<PlayerMove>();
                if (playerMove != null && playerMove.canMove.Value)
                {
                    if (playerObj.transform.position.z > furthestZ)
                    {
                        furthestZ = playerObj.transform.position.z;
                    }
                }
            }
        }

        return furthestZ;
    }

    
    // Method called by SectionTracker when section is destroyed
    public void OnSectionDestroyed()
    {
        currentActiveSections--;
        Debug.Log($"🗑️ Section destroyed | Active: {currentActiveSections}/{maxActiveSections}");
    }

    // Reset method to call when game restarts
    public void ResetSectionGeneration()
    {
        furthestSectionZ = 20f;
        currentActiveSections = 0;
        lastGenerationCheck = 0f;
        Debug.Log("🔄 Section generation reset");
    }
}