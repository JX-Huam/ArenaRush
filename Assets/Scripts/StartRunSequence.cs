using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class StartRunSequence : NetworkBehaviour
{
    [SerializeField] private GameObject countDown3;
    [SerializeField] private GameObject countDown2;
    [SerializeField] private GameObject countDown1;
    [SerializeField] private GameObject countDownGo;
    [SerializeField] private GameObject fadeIn;
    [SerializeField] private GameObject waitingForAvatarsText; // Add this UI element

    public event EventHandler OnCountDownStart;
    public event EventHandler OnCountDownMiddle;
    public event EventHandler OnCountDownEnd;

    private bool allPlayersJoined = false;
    private bool allAvatarsLoaded = false;

    private void Awake() 
    {   
        OnCountDownStart += GameManager.Instance.StartRunSequence_OnCountDownStart;
        OnCountDownEnd += SoundEffectManager.Instance.OnCountDownEnd;
        OnCountDownMiddle += SoundEffectManager.Instance.OnCountDownMiddle;

        // Subscribe to both events
        GameManager.Instance.OnAllPlayerJoined += GameManager_OnAllPlayerJoined;
        GameManager.Instance.OnAllAvatarsLoaded += GameManager_OnAllAvatarsLoaded;

        StartCoroutine(WaitForPlayerSpawned());
        
        // Show the waiting text if it exists
        if (waitingForAvatarsText != null)
        {
            waitingForAvatarsText.SetActive(true);
        }
    }

    private void GameManager_OnAllPlayerJoined(object sender, EventArgs e)
    {
        allPlayersJoined = true;
        Debug.Log("All players joined, waiting for avatars");
        CheckStartConditions();
    }
    
    private void GameManager_OnAllAvatarsLoaded(object sender, EventArgs e)
    {
        allAvatarsLoaded = true;
        Debug.Log("All avatars loaded, checking if we can start");
        CheckStartConditions();
    }
    
    private void CheckStartConditions()
    {
        // Only start countdown when both conditions are met
        if (IsServer && allPlayersJoined && allAvatarsLoaded)
        {
            Debug.Log("All players joined AND all avatars loaded - Starting countdown");
            StartCountSequenceClientRpc();
        }
    }

    [ClientRpc]
    private void StartCountSequenceClientRpc()
    {
        // Hide the waiting text
        if (waitingForAvatarsText != null)
        {
            waitingForAvatarsText.SetActive(false);
        }
        
        StartCoroutine(CountSequence());
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

    IEnumerator CountSequence()
    {
        OnCountDownStart?.Invoke(this,EventArgs.Empty);
        yield return new WaitForSeconds(1.5f);
        countDown3.SetActive(true);
        OnCountDownMiddle?.Invoke(this,EventArgs.Empty);
        yield return new WaitForSeconds(.7f);
        countDown2.SetActive(true);
        OnCountDownMiddle?.Invoke(this,EventArgs.Empty);
        yield return new WaitForSeconds(.7f);
        countDown1.SetActive(true);
        OnCountDownMiddle?.Invoke(this,EventArgs.Empty);
        yield return new WaitForSeconds(.7f);
        countDownGo.SetActive(true);


        OnCountDownEnd?.Invoke(this,EventArgs.Empty);
    }

    public void OnPlayerObjectSpawned(object sender, EventArgs e)
    {
        OnCountDownEnd += PlayerMove.Instance.OnCountDownEnd;
        Debug.Log("oncountdownend has subscribed!");
    }
}
