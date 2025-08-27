using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class NetworkResetManager : MonoBehaviour
{
    public static NetworkResetManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Call this when the game ends and you need to return to lobby
    public void ResetNetworkState()
    {
        Debug.Log("⚠️ Resetting all network state ⚠️");
        StartCoroutine(PerformNetworkReset());
    }

    private IEnumerator PerformNetworkReset()
    {
        // Step 1: Shutdown NetworkManager
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("Shutting down NetworkManager");
            NetworkManager.Singleton.Shutdown();
            
            // Wait for shutdown to complete
            float timeout = Time.time + 3f;
            while (NetworkManager.Singleton.IsListening && Time.time < timeout)
            {
                yield return null;
            }
        }

        // Step 2: Clear references and cached data
        if (LobbyManager.Instance != null)
        {
            Debug.Log("Resetting LobbyManager state");
            // Properly handle the async operation in a coroutine
            LobbyManager.Instance.LeaveLobby();
            // Give it a moment to process
            yield return new WaitForSeconds(0.5f);
        }

        // Step 3: Reset other managers as needed
        if (GameManager.Instance != null)
        {
            Debug.Log("Resetting GameManager state");
            GameManager.Instance.ResetGameState();
        }

        // Step 4: Load the lobby scene
        Debug.Log("Loading lobby scene");
        SceneManager.LoadScene(0); // Assuming 0 is your lobby scene index
        
        yield return null;

        Debug.Log("✅ Network reset complete");
    }
}