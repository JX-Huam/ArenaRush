using UnityEngine;

public class SectionTracker : MonoBehaviour
{
    private GameManager gameManager;
    private float creationTime;
    
    public void Initialize(GameManager manager)
    {
        gameManager = manager;
        creationTime = Time.time;
        Debug.Log($"Section tracker initialized at Z:{transform.position.z}");
    }
    
    private void OnDestroy()
    {
        // Notify GameManager that this section was destroyed
        if (gameManager != null)
        {
            gameManager.OnSectionDestroyed();
            
            float lifetime = Time.time - creationTime;
            Debug.Log($"Section lived for {lifetime} seconds");
        }
    }
}
