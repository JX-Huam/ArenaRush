using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Button closeTutorialButton;
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;
    [SerializeField] private TextMeshProUGUI pageNumberText;
    [SerializeField] private Button tutorialButton;
    
    [Header("Tutorial Pages")]
    [SerializeField] private GameObject[] tutorialPages;
    

    
    // Events
    public event EventHandler OnTutorialClosed;
    public event EventHandler<TutorialPageChangedEventArgs> OnPageChanged;
    
    public class TutorialPageChangedEventArgs : EventArgs
    {
        public int CurrentPage;
        public int TotalPages;
    }
    
    // State
    private int currentPageIndex = 0;
    private bool isInitialized = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        InitializeTutorial();
    }

    private void Start()
    {
        SetupButtonListeners();
        ShowCurrentPage();

        gameObject.SetActive(false);
    }

    private void InitializeTutorial()
    {
        // Validate tutorial pages
        if (tutorialPages == null || tutorialPages.Length == 0)
        {
            Debug.LogError("❌ TutorialManager: No tutorial pages assigned!");
            return;
        }

        // Hide all pages initially
        foreach (GameObject page in tutorialPages)
        {
            if (page != null)
                page.SetActive(false);
        }

        isInitialized = true;
        Debug.Log($"✅ TutorialManager: Initialized with {tutorialPages.Length} pages");
    }

    private void SetupButtonListeners()
    {
        // Close button
        if (closeTutorialButton != null)
        {
            closeTutorialButton.onClick.AddListener(CloseTutorial);
            tutorialButton.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("⚠️ TutorialManager: Close button not assigned!");
        }

        // Navigation buttons
        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.AddListener(GoToPreviousPage);
        }
        else
        {
            Debug.LogWarning("⚠️ TutorialManager: Left arrow button not assigned!");
        }

        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.AddListener(GoToNextPage);
        }
        else
        {
            Debug.LogWarning("⚠️ TutorialManager: Right arrow button not assigned!");
        }
    }

    public void ShowTutorial()
    {
        if (!isInitialized)
        {
            Debug.LogError("TutorialManager: Cannot show tutorial - not initialized!");
            return;
        }

        gameObject.SetActive(true);
        currentPageIndex = 0;
        ShowCurrentPage();
        Debug.Log("TutorialManager: Tutorial opened");
    }

    public void GoToNextPage()
    {
        if (!isInitialized) return;

        if (currentPageIndex < tutorialPages.Length - 1)
        {
            currentPageIndex++;
            ShowCurrentPage();
            Debug.Log($"TutorialManager: Moved to page {currentPageIndex + 1}");
        }
        else
        {
            Debug.Log("TutorialManager: Already on last page");
        }
    }

    public void GoToPreviousPage()
    {
        if (!isInitialized) return;

        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            ShowCurrentPage();
            Debug.Log($"TutorialManager: Moved to page {currentPageIndex + 1}");
        }
        else
        {
            Debug.Log("TutorialManager: Already on first page");
        }
    }
    
    public void CloseTutorial()
    {
        gameObject.SetActive(false);
        OnTutorialClosed?.Invoke(this, EventArgs.Empty);
        Debug.Log("📖 TutorialManager: Tutorial closed");
    }

    private void ShowCurrentPage()
    {
        if (!isInitialized) return;

        // Hide all pages
        foreach (GameObject page in tutorialPages)
        {
            if (page != null)
                page.SetActive(false);
        }

        // Show current page
        if (tutorialPages[currentPageIndex] != null)
        {
            tutorialPages[currentPageIndex].SetActive(true);
        }

        // Update page number text
        UpdatePageNumberDisplay();

        // Update navigation buttons
        UpdateNavigationButtons();

        // Fire page changed event
        OnPageChanged?.Invoke(this, new TutorialPageChangedEventArgs
        {
            CurrentPage = currentPageIndex + 1,
            TotalPages = tutorialPages.Length
        });
    }

    private void UpdatePageNumberDisplay()
    {
        if (pageNumberText != null)
        {
            pageNumberText.text = $"{currentPageIndex + 1}/{tutorialPages.Length}";
        }
    }

    private void UpdateNavigationButtons()
    {
        // Hide/Show left arrow button
        if (leftArrowButton != null)
        {
            leftArrowButton.gameObject.SetActive(currentPageIndex > 0);
        }

        // Hide/Show right arrow button
        if (rightArrowButton != null)
        {
            rightArrowButton.gameObject.SetActive(currentPageIndex < tutorialPages.Length - 1);
        }
    }

    // Public getters
    public int CurrentPageIndex => currentPageIndex;
    public int TotalPages => tutorialPages?.Length ?? 0;
    public bool IsLastPage => currentPageIndex >= tutorialPages.Length - 1;
    public bool IsFirstPage => currentPageIndex <= 0;

    private void OnDestroy()
    {
        // Clean up button listeners
        if (closeTutorialButton != null)
            closeTutorialButton.onClick.RemoveListener(CloseTutorial);
            
        if (leftArrowButton != null)
            leftArrowButton.onClick.RemoveListener(GoToPreviousPage);
            
        if (rightArrowButton != null)
            rightArrowButton.onClick.RemoveListener(GoToNextPage);
    }
}