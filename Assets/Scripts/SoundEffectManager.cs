using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SoundEffectManager : MonoBehaviour
{
    public static SoundEffectManager Instance;

    private List<Transform> audioSources;

    private void Awake() 
    {
        Instance = this;
        audioSources = gameObject.GetComponentsInChildren<Transform>().ToList();
    }

    public void OnPlayerCrashed(object sender, EventArgs e)
    {
        audioSources.Find(x => x.name == "CrashThud").GetComponent<AudioSource>().Play();
    }

    // public void OnCoinCollected(object sender, EventArgs e)
    // {
    //     audioSources.Find(x => x.name == "CoinCollect").GetComponent<AudioSource>().Play();
    // }

    public void OnCoinCollected(object sender, EventArgs e)
    {
        // Only play sound if the coin was collected by the local player
        if (e is CollectCoin.PlayerCoinEventArgs coinArgs)
        {
            // Get the PlayerMove component to check if this is the local player
            PlayerMove playerMove = coinArgs.PlayerObject.GetComponent<PlayerMove>();
            
            // Only play sound if this is the local player
            if (playerMove != null && playerMove.IsOwner)
            {
                audioSources.Find(x => x.name == "CoinCollect").GetComponent<AudioSource>().Play();
            }
        }
    }

    public void OnCountDownEnd(object sender, EventArgs e)
    {
        audioSources.Find(x => x.name == "Go").GetComponent<AudioSource>().Play();
    }

    public void OnCountDownMiddle(object sender, EventArgs e)
    {
        audioSources.Find(x => x.name == "Ready").GetComponent<AudioSource>().Play();
    }
}
