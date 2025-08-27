using System.Collections;
using Unity.Netcode;
using UnityEngine;


public class ObstacleCollision : MonoBehaviour
{
    public Camera playerCamera;
    [SerializeField] private float disableDuration = 0.5f; // Disable collider after collision for this duration
    private BoxCollider boxCollider;

    private void Awake() 
    {
        playerCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();

        boxCollider = GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            Debug.LogError("BoxCollider component not found on obstacle!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            PlayerMove playerMove = other.GetComponent<PlayerMove>();

            if(playerMove != null)
            {
                playerMove.TriggerOnPlayerCrashedToObstacle(other.GetComponent<NetworkObject>().OwnerClientId);
                if (boxCollider != null)
                {
                    StartCoroutine(DisableColliderTemporarily());
                }
            }
            
        }
    }

    private IEnumerator DisableColliderTemporarily()
    {
        boxCollider.enabled = false;
        yield return new WaitForSeconds(disableDuration);
        boxCollider.enabled = true;
    }

}
