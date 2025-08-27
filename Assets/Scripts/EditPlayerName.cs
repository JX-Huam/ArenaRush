using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EditPlayerName : MonoBehaviour {
    public static EditPlayerName Instance { get; private set; }
    public event EventHandler OnNameChanged;
    [SerializeField] private TextMeshProUGUI playerNameText;

    private string playerName;

    private void Awake()
    {
        Instance = this;

        GetComponent<Button>().onClick.AddListener(() =>
        {
            ChangeNameInputWindow.Instance.Show();
        });

        // Load player name from PlayerPrefs
        if (PlayerPrefs.HasKey("PlayerName"))
        {
            playerName = PlayerPrefs.GetString("PlayerName");
        }
        else
        {
            // Set a default name if not found
            playerName = "Player" + UnityEngine.Random.Range(1000, 9999);
        }
        
        playerNameText.text = playerName;

    }

    private void Start() {
        ChangeNameInputWindow.Instance.OnOK += ChangeNameInputWindow_OnOK;
        OnNameChanged += EditPlayerName_OnNameChanged;
    }

    private void EditPlayerName_OnNameChanged(object sender, EventArgs e)
    {
        LobbyManager.Instance.UpdatePlayerName(GetPlayerName());
    }

    private void ChangeNameInputWindow_OnOK(object sender, ChangeNameInputWindow.OnOKEventArgs e)
    {
        playerName = e.newName;
        playerNameText.text = playerName;

        // Save player name to PlayerPrefs
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();

        OnNameChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetPlayerName() {
        return playerName;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }


}