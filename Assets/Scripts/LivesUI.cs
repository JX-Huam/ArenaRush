using UnityEngine;
using TMPro;
using Unity.Netcode;

public class LivesUI : MonoBehaviour
{
    [SerializeField] private GameObject[] heartIcons; // Optional: Array of heart UI elements
    
    private PlayerMove localPlayer;
    
    private void Start()
    {
        // Find the local player
        StartCoroutine(FindLocalPlayer());
    }
    
    private System.Collections.IEnumerator FindLocalPlayer()
    {
        // Wait until PlayerMove.Instance is available
        while (PlayerMove.Instance == null)
        {
            yield return null;
        }
        
        localPlayer = PlayerMove.Instance;
        
        // Subscribe to lives changes
        if (localPlayer.playerLives != null)
        {
            localPlayer.playerLives.OnValueChanged += OnLivesChanged;
            
            // Initialize display
            UpdateLivesDisplay(localPlayer.playerLives.Value);
        }
        
        Debug.Log("LivesUI: Successfully connected to local player");
    }
    
    private void OnLivesChanged(int previousLives, int newLives)
    {
        Debug.Log($"Lives changed from {previousLives} to {newLives}");
        UpdateLivesDisplay(newLives);
        
        // Play heart loss animation if lives decreased
        if (newLives < previousLives)
        {
            StartCoroutine(HeartLossAnimation());
        }
    }
    
    private void UpdateLivesDisplay(int lives)
    {       
        // Update heart icons if available
        if (heartIcons != null && heartIcons.Length >= 3)
        {
            for (int i = 0; i < heartIcons.Length && i < 3; i++)
            {
                if (heartIcons[i] != null)
                {
                    heartIcons[i].SetActive(i < lives);
                }
            }
        }
    }
    
    private System.Collections.IEnumerator HeartLossAnimation()
    {
        // Simple shake animation for the lives display
        Vector3 originalPosition = transform.localPosition;
        float shakeIntensity = 10f;
        float shakeDuration = 0.5f;
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-shakeIntensity, shakeIntensity);
            float y = Random.Range(-shakeIntensity, shakeIntensity);
            
            transform.localPosition = originalPosition + new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Reset to original position
        transform.localPosition = originalPosition;
    }
    
    private void OnDestroy()
    {
        // Clean up event subscription
        if (localPlayer != null && localPlayer.playerLives != null)
        {
            localPlayer.playerLives.OnValueChanged -= OnLivesChanged;
        }
    }
}