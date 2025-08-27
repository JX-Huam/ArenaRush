using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance {get; private set;}


    public const string KEY_PLAYER_NAME = "PlayerName";
    public const string KEY_START_GAME = "0";
    
    public event EventHandler OnLeftLobby;
    public event EventHandler OnGameStarted;
    public event EventHandler<LobbyEventArgs> OnJoinedLobby;
    public event EventHandler<LobbyEventArgs> OnJoinedLobbyUpdate;
    public event EventHandler<LobbyEventArgs> OnKickedFromLobby;
    public class LobbyEventArgs : EventArgs {
        public Lobby lobby;
    }
    public event EventHandler<OnLobbyListChangedEventArgs> OnLobbyListChanged;
    public class OnLobbyListChangedEventArgs : EventArgs {
        public List<Lobby> lobbyList;
    }

    // Add this event to the existing events section in LobbyManager.cs
    public event EventHandler<AuthenticationStateEventArgs> OnAuthenticationStateChanged;
    public class AuthenticationStateEventArgs : EventArgs {
        public bool IsAuthenticated;
    }

    private Lobby joinedLobby;
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private float lobbyPollTimer;
    private string playerName;

    // Add a timer for checking authentication status
    private float authCheckTimer = 0f;
    private float authCheckInterval = 5f;
    private bool lastAuthState = false;


    private void Awake() {
        Instance = this;

        if (PlayerPrefs.HasKey("PlayerName")) {
            this.playerName = PlayerPrefs.GetString("PlayerName");
            Debug.Log($"Loaded player name from PlayerPrefs: {playerName}");
        }
    }

    private void Update()
    {
        try
        {
            HandleLobbyHeartbeat();
            HandleLobbyPolling();

            CheckAuthenticationStatus();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            throw;
        }


    }


    //This function is to send heartbeat to the lobby server to tell this lobby is still alive (If not lobby will become inactive in 30 seconds)
    private async void HandleLobbyHeartbeat()
    {
        if (IsLobbyHost()) {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer < 0f) {
                float heartbeatTimerMax = 15f;
                heartbeatTimer = heartbeatTimerMax;

                Debug.Log("Heartbeat");
                await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            }
        }
    }

    private async void HandleLobbyPolling()
    {
        try
        {
            if (joinedLobby != null) {
                lobbyPollTimer -= Time.deltaTime;
                if (lobbyPollTimer < 0f) {

                    float lobbyPollTimerMax = 1.1f;
                    lobbyPollTimer = lobbyPollTimerMax;

                    if (!IsPlayerInLobby()) {
                        // Player was kicked out of this lobby
                        Debug.Log("Kicked from Lobby!");

                        OnKickedFromLobby?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });

                        joinedLobby = null;
                        
                        return;
                    }

                    joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);

                    OnJoinedLobbyUpdate?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });

                    if(joinedLobby == null) return;

                    if (joinedLobby.Data[KEY_START_GAME].Value != "0" && joinedLobby != null)
                    {
                        if(!IsLobbyHost())
                        {
                            Relay.instance.JoinRelay(joinedLobby.Data[KEY_START_GAME].Value);

                            joinedLobby = null;

                            OnGameStarted?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            throw;
        }
    }

    private void CheckAuthenticationStatus()
    {
        authCheckTimer -= Time.deltaTime;
        if (authCheckTimer <= 0f)
        {
            authCheckTimer = authCheckInterval;
            
            bool isCurrentlyAuthenticated = false;
            
            try {
                // Check if Unity Services has been initialized
                if (Unity.Services.Core.UnityServices.State == Unity.Services.Core.ServicesInitializationState.Initialized 
                    && AuthenticationService.Instance != null) {
                    isCurrentlyAuthenticated = AuthenticationService.Instance.IsSignedIn;
                }
            } catch (System.Exception e) {
                Debug.LogWarning($"Error checking authentication status: {e.Message}");
                isCurrentlyAuthenticated = false;
            }
            
            // If authentication state changed, trigger the event
            if (isCurrentlyAuthenticated != lastAuthState)
            {
                lastAuthState = isCurrentlyAuthenticated;
                OnAuthenticationStateChanged?.Invoke(this, new AuthenticationStateEventArgs { 
                    IsAuthenticated = isCurrentlyAuthenticated 
                });
                
                Debug.Log($"Authentication state changed: {(isCurrentlyAuthenticated ? "Authenticated" : "Not Authenticated")}");
            }
        }
    }

    // public async void Authenticate(string playerName)
    // {
    //     try
    //     {
    //         this.playerName = playerName;
    //         InitializationOptions initializationOptions = new InitializationOptions();
    //         initializationOptions.SetProfile(playerName);

    //         await UnityServices.InitializeAsync(initializationOptions);

    //         AuthenticationService.Instance.SignedIn += () => {
    //             // do nothing
    //             Debug.Log("Signed in! " + AuthenticationService.Instance.PlayerId);

    //             RefreshLobbyList();
    //         };

    //         await AuthenticationService.Instance.SignInAnonymouslyAsync();
    //     }
    //     catch(AuthenticationException e)
    //     {
    //         Debug.Log(e);
    //         throw;
    //     }
        
    // }

    public async void Authenticate(string playerName)
    {
        try
        {
            this.playerName = playerName;
            InitializationOptions initializationOptions = new InitializationOptions();
            initializationOptions.SetProfile(playerName);

            await UnityServices.InitializeAsync(initializationOptions);

            AuthenticationService.Instance.SignedIn += () => {
                // Trigger authentication state change event
                Debug.Log("Signed in! " + AuthenticationService.Instance.PlayerId);
                OnAuthenticationStateChanged?.Invoke(this, new AuthenticationStateEventArgs { 
                    IsAuthenticated = true 
                });
                
                RefreshLobbyList();
            };

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        catch(AuthenticationException e)
        {
            Debug.Log(e);
            OnAuthenticationStateChanged?.Invoke(this, new AuthenticationStateEventArgs { 
                IsAuthenticated = false 
            });
            throw;
        }
    }

    public async void CreateLobby(string lobbyName, int maxPlayers, bool isPrivate)
    {
        try
        {
            Player player = GetPlayer();

            CreateLobbyOptions options = new CreateLobbyOptions {
                Player = player,
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject> {
                    {KEY_START_GAME, new DataObject(DataObject.VisibilityOptions.Member, "0")}
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            joinedLobby = lobby;

            OnJoinedLobby?.Invoke(this, new LobbyEventArgs { lobby = lobby });

            Debug.Log("Created Lobby " + lobby.Name);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            throw;
        }
    }

    private async void ListLobbies()
    {
        try
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions{
                Count = 25,
                Filters = new List<QueryFilter>{
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>{
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            Debug.Log($"Lobbies found: {queryResponse.Results.Count}"); 
            foreach(Lobby lobby in queryResponse.Results)
            {
                Debug.Log($"{lobby.Name} {lobby.MaxPlayers}");
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            throw;
        }
    }

    private async void JoinLobbyByCode(string lobbyCode)
    {
        Player player = GetPlayer();

        Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, new JoinLobbyByCodeOptions {
            Player = player
        });

        joinedLobby = lobby;

        OnJoinedLobby?.Invoke(this, new LobbyEventArgs { lobby = lobby });
    }

    public async void JoinLobby(Lobby lobby) {
        try
        {
            Player player = GetPlayer();

            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, new JoinLobbyByIdOptions {
                Player = player
            });

            OnJoinedLobby?.Invoke(this, new LobbyEventArgs { lobby = lobby });
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            throw;
        }
        
    }

    // Add to LobbyManager.cs
    public async Task<bool> TryLeaveLobby()
    {
        try
        {
            if (joinedLobby != null)
            {
                try
                {
                    await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogWarning($"Error leaving lobby: {e.Message}");
                }
                
                joinedLobby = null;
                OnLeftLobby?.Invoke(this, EventArgs.Empty);
                Debug.Log("Player left lobby successfully");
                return true;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in TryLeaveLobby: {e.Message}");
        }
        
        return false;
    }

    public async void UpdatePlayerName(string newPlayerName)
    {
        this.playerName = newPlayerName;

        if(joinedLobby != null)
        {
            try
            {
                UpdatePlayerOptions options = new UpdatePlayerOptions();

                options.Data = new Dictionary<string, PlayerDataObject>(){
                    {
                        KEY_PLAYER_NAME, new PlayerDataObject(
                            visibility: PlayerDataObject.VisibilityOptions.Public,
                            value: newPlayerName
                        )
                    }
                };
                
                string playerID = AuthenticationService.Instance.PlayerId;

                Lobby lobby = await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, playerID, options);
                joinedLobby = lobby;

                OnJoinedLobbyUpdate?.Invoke(this, new LobbyEventArgs { lobby = joinedLobby });
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
                throw;
            }
        }
        
    }

    public async void QuickJoinLobby() {
        try {
            QuickJoinLobbyOptions options = new QuickJoinLobbyOptions();

            Lobby lobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
            joinedLobby = lobby;

            OnJoinedLobby?.Invoke(this, new LobbyEventArgs { lobby = lobby });
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }
    }

    public async void LeaveLobby()
    {
        if (joinedLobby != null) {
            try {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);

                joinedLobby = null;

                OnLeftLobby?.Invoke(this, EventArgs.Empty);
                RefreshLobbyList();
            } catch (LobbyServiceException e) {
                Debug.Log(e);
            }
        }
    }

    public async void KickPlayer(string playerId)
    {
        if (IsLobbyHost()) {
            try {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
            } catch (LobbyServiceException e) {
                Debug.Log(e);
            }
        }
    }

    public async void RefreshLobbyList() {
        try {
            QueryLobbiesOptions options = new QueryLobbiesOptions();
            options.Count = 25;

            // Filter for open lobbies only
            options.Filters = new List<QueryFilter> {
                new QueryFilter(
                    field: QueryFilter.FieldOptions.AvailableSlots,
                    op: QueryFilter.OpOptions.GT,
                    value: "0")
            };

            // Order by newest lobbies first
            options.Order = new List<QueryOrder> {
                new QueryOrder(
                    asc: false,
                    field: QueryOrder.FieldOptions.Created)
            };

            QueryResponse lobbyListQueryResponse = await LobbyService.Instance.QueryLobbiesAsync();

            OnLobbyListChanged?.Invoke(this, new OnLobbyListChangedEventArgs { lobbyList = lobbyListQueryResponse.Results });
        } catch (LobbyServiceException e) {
            Debug.Log(e);
        }
    }

    public async Task StartGame()
    {
        if(IsLobbyHost())
        {
            try
            {
                Debug.Log("Start Game");

                string relayCode = await Relay.instance.CreateRelay();

                Lobby lobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions{
                    Data = new Dictionary<string, DataObject>{
                        {KEY_START_GAME, new DataObject(DataObject.VisibilityOptions.Member, relayCode)}
                    }
                });

                joinedLobby = lobby;

                OnGameStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
                throw;
            }
        }
    }

    public Lobby GetJoinedLobby() {
        return joinedLobby;
    }

    public bool IsLobbyHost() {
        return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private bool IsPlayerInLobby() {
        if (joinedLobby != null && joinedLobby.Players != null) {
            foreach (Player player in joinedLobby.Players) {
                if (player.Id == AuthenticationService.Instance.PlayerId) {
                    // This player is in this lobby
                    return true;
                }
            }
        }
        return false;
    }

    private Player GetPlayer()
    {
        return new Player(AuthenticationService.Instance.PlayerId, null, new Dictionary<string, PlayerDataObject> {
            { KEY_PLAYER_NAME, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) },
        });
    }
}