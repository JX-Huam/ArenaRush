using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyCreateUI : MonoBehaviour {


    public static LobbyCreateUI Instance { get; private set; }


    [SerializeField] private Button createButton;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_InputField maxPlayersInput;

    private string lobbyName;
    private string maxPlayers;

    private void Awake()
    {
        Instance = this;

        createButton.onClick.AddListener(() =>
        {
            LobbyManager.Instance.CreateLobby(
                GetLobbyName(),
                int.Parse(GetMaxPlayer()),
                false
            );

            PlayerPrefs.SetString("MaxPlayers", maxPlayers);
            PlayerPrefs.Save();
            Hide();
        });

        Hide();
    }

    private void UpdateText()
    {
        lobbyNameInput.text = lobbyName;
        maxPlayersInput.text = maxPlayers;
    }

    private string GetLobbyName()
    {
        lobbyName = lobbyNameInput.text;
        return lobbyName;
    }
    
    private string GetMaxPlayer()
    {
        maxPlayers = maxPlayersInput.text;
        return maxPlayers;
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show() {
        gameObject.SetActive(true);

        lobbyName = "MyLobby";
        maxPlayers = "3";

        UpdateText();
    }

}