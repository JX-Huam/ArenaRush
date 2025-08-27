using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

public class SceneCamera : MonoBehaviour
{
    private void Awake() 
    {
        StartCoroutine(WaitForPlayerSpawned());
    }

    IEnumerator WaitForPlayerSpawned()
    {
        // Wait until PlayerMove.Instance is assigned
        while (PlayerMove.Instance == null)
        {
            yield return null; // Check again in the next frame
        }

        // Now we can safely subscribe
        PlayerMove.Instance.OnPlayerObjectSpawned += OnPlayerObjectSpawned;
    }

    private void OnPlayerObjectSpawned(object sender, EventArgs e)
    {
        try
        {
            if(this != null || this.isActiveAndEnabled)
            {
                Debug.Log(gameObject);
                gameObject.transform.position = new Vector3(PlayerMove.Instance.transform.position.x, transform.position.y, transform.position.z);
            }
        }
        catch(Exception)
        {
            
        }
    }
}
