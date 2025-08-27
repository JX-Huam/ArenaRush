using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class PowerUp : MonoBehaviour
{
    public enum PowerUpType
    {
        SpeedUp,
        SlowDown,
        Invincibility,
    }

    [SerializeField] private PowerUpType powerUpType;

    private void Awake() {
        
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerMove playerMove = other.GetComponent<PlayerMove>();

        if (playerMove != null)
        {            
            playerMove.TriggerOnPowerUpCollected(powerUpType, other.GetComponent<NetworkObject>().OwnerClientId);

            MakeInvisible();
            
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
}
