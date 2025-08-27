using TMPro;
using UnityEngine;

public class PlayerStatsUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI coinsText;
    [SerializeField] private TextMeshProUGUI distanceText;
    
    public void SetPlayerStats(string playerName, int coins, int distance)
    {
        if (playerNameText != null)
            playerNameText.text = playerName;
            
        if (coinsText != null)
            coinsText.text = coins.ToString();
            
        if (distanceText != null)
            distanceText.text = distance.ToString();
    }
}