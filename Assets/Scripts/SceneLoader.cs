using UnityEngine;
using Unity.Netcode;
using System;

public class SceneLoader : NetworkBehaviour
{
    public static SceneLoader Instance;
    private NetworkList<ulong> loadedPlayers = new NetworkList<ulong>();
    public enum Scene
    {
        LobbyScene,
        GameScene
    }

    private void Awake() {
        if(Instance != null) return;

        Instance = this;

        DontDestroyOnLoad(gameObject);
    }

    private void Start() {
        LobbyManager.Instance.OnGameStarted += LobbyManager_OnGameStarted;
    }

    private void LobbyManager_OnGameStarted(object sender, EventArgs e)
    {
        LoadSceneNetworkServerRpc(Scene.GameScene);
    }

    [ServerRpc]
    public void LoadSceneNetworkServerRpc(Scene targetScene)
    {
        NetworkManager.Singleton.SceneManager.LoadScene($"Scenes/{targetScene}", UnityEngine.SceneManagement.LoadSceneMode.Single);
        Debug.Log("Game Scene Loaded");
    }
}
