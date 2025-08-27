using Unity.Netcode;
using UnityEngine;

public class Destroyer : NetworkBehaviour
{
    // Minimum distance behind players for destroying sections
    private float destroyDistance = 30f;

    private void Update()
    {
        // Only let the server handle destruction
        if (IsServer)
        {
            float sectionPosZ = transform.position.z;
            
            // Calculate the Z position of the player that's furthest behind
            float minPlayerZ = GetFurthestBehindPlayerZ();
            
            // If there are no players, don't destroy anything
            if (minPlayerZ == float.MaxValue)
                return;
            
            float difference = sectionPosZ - minPlayerZ;
            
            // Only destroy if ALL players have passed this section by at least destroyDistance
            if (difference < -destroyDistance)
            {
                Debug.Log($"Destroying section at {sectionPosZ}. Furthest player at {minPlayerZ}, difference: {difference}");
                NetworkObject netObj = GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Despawn(true); // Despawn and destroy
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
    
    // Find the Z position of the player that's furthest behind
    private float GetFurthestBehindPlayerZ()
    {
        float minPlayerZ = float.MaxValue;
        
        // Look through all player objects
        foreach (PlayerMove player in FindObjectsByType<PlayerMove>(FindObjectsSortMode.None))
        {
            if(player.canMove.Value == false) continue;
            float playerZ = player.transform.position.z;
            if (playerZ < minPlayerZ)
            {
                minPlayerZ = playerZ;
            }
        }
        
        return minPlayerZ;
    }
}