using UnityEngine;
using TMPro;
using Dan.Main;
using System.Collections.Generic;

namespace LeaderboardCreatorDemo
{
    public class LeaderboardManager : MonoBehaviour
    {
        [SerializeField] private List<TextMeshProUGUI> names;
        [SerializeField] private List<TextMeshProUGUI> scores;
        [SerializeField] private GameObject LeaderboardUI;

        public static LeaderboardManager Instance;

        private string publicLeaderboardKey = "756e0fbdb9150586dde0292f3e4ec3345651d5af5334702e3dd9382594911ac1";

        private void Start()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this.gameObject);
                return;
            }

            LeaderboardUI.SetActive(false);
        }

        public void SetLeaderboardEntry(string username, int score)
        {
            LeaderboardCreator.UploadNewEntry(publicLeaderboardKey, username, score, ((msg) => {
                
            }));
        }
        
        public void GetLeaderboard()
        {
            // Add null checks before proceeding
            if (names == null || scores == null)
            {
                Debug.LogWarning("Leaderboard UI lists are not properly initialized!");
                return;
            }

            LeaderboardCreator.GetLeaderboard(publicLeaderboardKey, ((msg) =>
            {
                if (msg == null || msg.Length == 0)
                {
                    Debug.LogWarning("No leaderboard data received");
                    return;
                }

                int loopLength = Mathf.Min(msg.Length, names.Count, scores.Count);

                for (int i = 0; i < loopLength; i++)
                {
                    if (names[i] != null && scores[i] != null)
                    {
                        names[i].text = msg[i].Username ?? "Unknown";
                        scores[i].text = msg[i].Score.ToString();
                    }
                    else
                    {
                        Debug.LogWarning($"UI element at index {i} is null!");
                    }
                }
            }));
        }

        public void Show()
        {
            if (LeaderboardUI != null)
            {
                LeaderboardUI.SetActive(true);
                GetLeaderboard();
            }
            else
            {
                Debug.LogError("LeaderboardUI is not assigned!");
            }
        }

        public void Hide()
        {
            // Add comprehensive null checks
            if (names != null && scores != null)
            {
                int maxCount = Mathf.Min(names.Count, scores.Count);
                
                for (int i = 0; i < maxCount; i++)
                {
                    if (names[i] != null)
                        names[i].text = "...";
                    else
                        Debug.LogWarning($"Names UI element at index {i} is null!");
                        
                    if (scores[i] != null)
                        scores[i].text = "...";
                    else
                        Debug.LogWarning($"Scores UI element at index {i} is null!");
                }
            }
            else
            {
                Debug.LogWarning("Names or Scores list is null - cannot clear leaderboard display!");
            }
            
            if (LeaderboardUI != null)
            {
                LeaderboardUI.SetActive(false);
            }
            else
            {
                Debug.LogWarning("LeaderboardUI is null - cannot hide leaderboard!");
            }
        }
    }
}
