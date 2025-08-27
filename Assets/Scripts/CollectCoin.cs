using System;
using System.Collections;
using UnityEngine;

public class CollectCoin : MonoBehaviour
{
    public class PlayerCoinEventArgs : EventArgs
    {
        public GameObject PlayerObject { get; set; }
    }

    public event EventHandler<PlayerCoinEventArgs> OnCoinCollected;

    private void Awake() 
    {
        OnCoinCollected += SoundEffectManager.Instance.OnCoinCollected;
        OnCoinCollected += GameManager.Instance.OnCoinCollected;
    }
    
    void OnTriggerEnter(Collider other)
    {
        PlayerMove playerMove = other.GetComponent<PlayerMove>();

        if (playerMove != null)
        {            
            // Trigger the event with the player reference
            OnCoinCollected?.Invoke(this, new PlayerCoinEventArgs { 
                PlayerObject = other.gameObject
            });
            
            // Make the coin visually disappear
            MakeInvisible();
            
            // Destroy the game object
            StartCoroutine(DelayedDestroy());
        }
    }

    private void MakeInvisible()
    {
        // Disable renderer and collider
        if (TryGetComponent<Collider>(out var collider))
            collider.enabled = false;
            
        // Disable any renderers in children
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;
    }
    
    private IEnumerator DelayedDestroy()
    {
        // Small delay before actual destruction to ensure event is processed
        yield return new WaitForSeconds(0.1f);
        Destroy(gameObject);
    }

    private void OnDestroy() 
    {
        OnCoinCollected -= SoundEffectManager.Instance.OnCoinCollected;
        OnCoinCollected -= GameManager.Instance.OnCoinCollected;
    }

}
