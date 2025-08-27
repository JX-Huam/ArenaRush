using System;
using System.Collections;
using System.Threading.Tasks;
using ReadyPlayerMe.Core;
using ReadyPlayerMe.Core.WebView;
using ReadyPlayerMe.WebView;
using Unity.Netcode;
using UnityEngine;

public class RPMAvatarManager : MonoBehaviour
{
    public static RPMAvatarManager Instance;

    [SerializeField] private WebViewPanel webViewPanel;
    [SerializeField] private GameObject webViewPanelPrefab;
    [SerializeField] private GameObject webviewPanelParent;
    [SerializeField] private GameObject defaultAvatarPrefab;

    private string avatarUrl;
    private GameObject avatarObject;
    private bool isAvatarLoaded = false;

    // Event to notify when avatar is loaded and ready
    public event EventHandler<AvatarLoadedEventArgs> OnAvatarLoaded;
    
    public class AvatarLoadedEventArgs : EventArgs
    {
        public GameObject AvatarObject;
        public string AvatarUrl;
        public string clientId;
    }

    

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // Load saved avatar URL if it exists
        if (PlayerPrefs.HasKey("AvatarUrl"))
        {
            avatarUrl = PlayerPrefs.GetString("AvatarUrl");
            Debug.Log($"Loaded saved avatar URL: {avatarUrl}");
        }
        else
        {
            avatarUrl = "https://models.readyplayer.me/67b417f62600df572bf39f64.glb";
        }
    }

    private void Start()
    {
        // Setup WebView callbacks
        if (webViewPanel != null)
        {
            webViewPanel.OnAvatarCreated.AddListener(OnWebViewAvatarCreated);
        }
        else
        {
            Debug.LogError("WebViewPanel reference is missing!");
        }
    }

    public void OpenAvatarCreator()
    {
        if (webViewPanel != null)
        {
            Debug.Log("Opening avatar creator webview");
            webViewPanel.gameObject.SetActive(true);
            webViewPanel.LoadWebView();
        }
    }
    
    public void OpenAvatarCreatorWithRecreate()
    {
        // Destroy existing panel if it exists
        if (webViewPanel != null)
        {
            Destroy(webViewPanel.gameObject);
            webViewPanel = null;
        }
        
        // Create new webview panel from prefab
        if (webViewPanelPrefab != null && webviewPanelParent != null)
        {
            GameObject newWebViewObj = Instantiate(webViewPanelPrefab, webviewPanelParent.transform);
            webViewPanel = newWebViewObj.GetComponent<WebViewPanel>();
            
            if (webViewPanel != null)
            {
                // Setup callback for new instance
                webViewPanel.OnAvatarCreated.AddListener(OnWebViewAvatarCreated);
                
                Debug.Log("Opening fresh avatar creator webview");
                webViewPanel.gameObject.SetActive(true);
                webViewPanel.LoadWebView();
            }
        }
        else
        {
            Debug.LogError("WebViewPanel prefab or parent not assigned!");
        }
    }

    private void OnWebViewAvatarCreated(string url)
    {
        avatarUrl = url;
        Debug.Log($"Avatar created with URL: {avatarUrl}");

        // Save URL to PlayerPrefs
        PlayerPrefs.SetString("AvatarUrl", avatarUrl);
        PlayerPrefs.Save();

        // Close the WebView
        webViewPanel.gameObject.SetActive(false);
    }

    public IEnumerator LoadAvatarWithURL(Transform parentTransform, string specificAvatarUrl)
    {
        Debug.Log($"Loading avatar with specific URL: {specificAvatarUrl} for {parentTransform.name}");
        
        bool isAvatarLoaded = false;
        
        // Set up avatar loader
        var avatarLoader = new AvatarObjectLoader();
        
        // Subscribe to events
        avatarLoader.OnCompleted += (sender, args) =>
        {
            Debug.Log($"Avatar loaded successfully for {parentTransform.name}");
            
            // Create a new avatar instance for this specific player
            GameObject newAvatarInstance = args.Avatar;
            
            // Set parent if specified
            if (parentTransform != null)
            {
                newAvatarInstance.transform.SetParent(parentTransform);
                newAvatarInstance.transform.localPosition = Vector3.zero;
                newAvatarInstance.transform.localRotation = Quaternion.identity;
            }
            
            isAvatarLoaded = true;
            
            // Trigger the event
            OnAvatarLoaded?.Invoke(this, new AvatarLoadedEventArgs
            {
                AvatarObject = newAvatarInstance,
                AvatarUrl = specificAvatarUrl
            });
        };
        
        avatarLoader.OnFailed += (sender, args) =>
        {
            Debug.LogError($"Failed to load avatar for {parentTransform.name}: {args.Message}");
            
            // Use default avatar as fallback
            if (defaultAvatarPrefab != null)
            {
                GameObject fallbackAvatar = Instantiate(defaultAvatarPrefab, parentTransform);
                isAvatarLoaded = true;
                
                // Trigger the event
                OnAvatarLoaded?.Invoke(this, new AvatarLoadedEventArgs
                {
                    AvatarObject = fallbackAvatar,
                    AvatarUrl = string.Empty
                });
            }
        };
        
        // Start loading the avatar from the specific URL
        avatarLoader.LoadAvatar(specificAvatarUrl);
        
        // Wait until avatar is loaded or failed
        float timeoutTime = Time.time + 30f; // 30 second timeout
        while (!isAvatarLoaded && Time.time < timeoutTime)
        {
            yield return null;
        }
        
        if (!isAvatarLoaded)
        {
            Debug.LogError($"Avatar loading timed out for {parentTransform.name}");
        }
    }

    public GameObject GetAvatarObject()
    {
        return avatarObject;
    }
    
    public string GetAvatarUrl()
    {
        return avatarUrl;
    }

    // Clear saved avatar URL and object
    public void ClearAvatar()
    {
        PlayerPrefs.DeleteKey("AvatarUrl");
        if (avatarObject != null)
        {
            Destroy(avatarObject);
            avatarObject = null;
        }
        isAvatarLoaded = false;
        avatarUrl = string.Empty;
    }

    public WebViewPanel GetWebViewPanel()
    {
        return webViewPanel;
    }
}