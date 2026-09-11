using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PageNavigationController : MonoBehaviour
{
    [System.Serializable]
    public class PageRule
    {
        [Tooltip("If true, requires interaction to unlock the NEXT buttons.")]
        public bool requiresInteraction = false;

        [Tooltip("If true, locks BOTH Next and Previous buttons until EnableNavigationButtons() / RequestNavigationUnlock() is called.")]
        public bool lockNavigationTillUnlocked = false;
    }

    [Header("Navigation Buttons")]
    [SerializeField] private Button[] nextButtons;
    [SerializeField] private Button previousButton;

    [Header("Continuous Pop Animation")]
    [Tooltip("If true, pulses/pops the next buttons continuously until clicked.")]
    [SerializeField] private bool popNextButtonOnUnlock = true;
    [SerializeField] private float popScaleMultiplier = 1.2f;
    [SerializeField] private float popPulseSpeed = 4f;

    [Header("Page Display")]
    [SerializeField] private TMP_Text pageNumberText;

    [Header("Developer Settings")]
    [Tooltip("Displays the current page using its actual index (0-based). Disable this before making a build.")]
    [SerializeField] private bool developerIndexMode = false;

    [Header("Testing Mode (Ignore Locks)")]
    [SerializeField] private bool testing = false;

    [Header("Requires Interaction & Manual Lock Per Page")]
    [SerializeField] private List<bool> requiresInteraction = new();
    [SerializeField] private List<bool> lockNavigationTillUnlocked = new();

    // Events
    public static event Action<int> OnPageChanged;
    public static event Action OnNavigationUnlockRequested;

    // State
    public static int CurrentIndex { get; private set; }
    public static PageNavigationController Instance { get; private set; }

    [SerializeField] private int currentIndex = 0;

    // Runtime State
    private readonly HashSet<int> visitedPages = new();
    private readonly HashSet<int> completedPages = new();
    private readonly Dictionary<Transform, Vector3> defaultScales = new();

    private Coroutine continuousPopCoroutine;

    private int NavigationPageCount => Mathf.Max(1, requiresInteraction.Count);

    private void Awake()
    {
        Instance = this;
        currentIndex = Mathf.Clamp(currentIndex, 0, NavigationPageCount - 1);

        // Store original scales for all next buttons
        if (nextButtons != null)
        {
            foreach (Button btn in nextButtons)
            {
                if (btn != null && !defaultScales.ContainsKey(btn.transform))
                {
                    defaultScales[btn.transform] = btn.transform.localScale;
                }
            }
        }
    }

    private void OnEnable()
    {
        OnNavigationUnlockRequested += EnableNavigationButtons;
    }

    private void Start()
    {
        BindNextButtons();

        if (previousButton)
            previousButton.onClick.AddListener(PreviousPage);

        visitedPages.Add(currentIndex);

        UpdateButtons();
        UpdateDisplay();
        RaisePageChanged();
    }

    private void OnDisable()
    {
        OnNavigationUnlockRequested -= EnableNavigationButtons;
        StopContinuousPopAnimation();
    }

    private void OnDestroy()
    {
        UnbindNextButtons();

        if (previousButton)
            previousButton.onClick.RemoveListener(PreviousPage);

        if (Instance == this)
            Instance = null;
    }

    private void BindNextButtons()
    {
        if (nextButtons == null) return;

        foreach (Button btn in nextButtons)
        {
            if (btn != null)
            {
                btn.onClick.AddListener(NextPage);
            }
        }
    }

    private void UnbindNextButtons()
    {
        if (nextButtons == null) return;

        foreach (Button btn in nextButtons)
        {
            if (btn != null)
            {
                btn.onClick.RemoveListener(NextPage);
            }
        }
    }

    public void NextPage()
    {
        if (currentIndex >= NavigationPageCount - 1)
            return;

        // Stop pulsing and disable all next buttons immediately upon click
        StopContinuousPopAnimation();
        SetNextButtonsState(false);

        currentIndex++;

        visitedPages.Add(currentIndex);

        UpdateButtons();
        UpdateDisplay();
        RaisePageChanged();
    }

    public void PreviousPage()
    {
        if (currentIndex <= 0)
            return;

        StopContinuousPopAnimation();
        currentIndex--;

        visitedPages.Add(currentIndex);

        UpdateButtons();
        UpdateDisplay();
        RaisePageChanged();
    }

    private void RaisePageChanged()
    {
        CurrentIndex = currentIndex;
        OnPageChanged?.Invoke(currentIndex);
    }

    private void UpdateButtons()
    {
        if (testing)
        {
            SetNormalButtonState();
            return;
        }

        bool isCompleted = completedPages.Contains(currentIndex);

        // Check manual page lock
        bool isPageLocked = currentIndex < lockNavigationTillUnlocked.Count && lockNavigationTillUnlocked[currentIndex];

        if (isPageLocked && !isCompleted)
        {
            if (previousButton) previousButton.interactable = false;
            SetNextButtonsState(false);
            StopContinuousPopAnimation();
            return;
        }

        bool needsInteraction =
            currentIndex < requiresInteraction.Count &&
            requiresInteraction[currentIndex];

        // Previous button
        if (previousButton)
            previousButton.interactable = currentIndex > 0;

        // Next buttons
        bool canAdvance = !needsInteraction || isCompleted;
        SetNextButtonsState(canAdvance);

        // Run or stop pulsing depending on interactability
        if (canAdvance && popNextButtonOnUnlock)
        {
            StartContinuousPopAnimation();
        }
        else
        {
            StopContinuousPopAnimation();
        }
    }

    private void SetNextButtonsState(bool isInteractable)
    {
        if (nextButtons == null) return;

        foreach (Button btn in nextButtons)
        {
            if (btn != null)
            {
                btn.interactable = isInteractable;
            }
        }
    }

    private void SetNormalButtonState()
    {
        if (previousButton)
            previousButton.interactable = currentIndex > 0;

        SetNextButtonsState(true);
    }

    /// <summary>
    /// Unlocks navigation, activates next buttons, and starts looping pop animation.
    /// </summary>
    public void EnableNavigationButtons()
    {
        completedPages.Add(currentIndex);
        UpdateButtons();
    }

    public static void RequestNavigationUnlock()
    {
        OnNavigationUnlockRequested?.Invoke();
    }

    // --- Continuous Pop Logic ---

    private void StartContinuousPopAnimation()
    {
        if (continuousPopCoroutine != null) return;
        continuousPopCoroutine = StartCoroutine(ContinuousPopRoutine());
    }

    private void StopContinuousPopAnimation()
    {
        if (continuousPopCoroutine != null)
        {
            StopCoroutine(continuousPopCoroutine);
            continuousPopCoroutine = null;
        }

        // Reset scales back to cached originals
        if (nextButtons != null)
        {
            foreach (Button btn in nextButtons)
            {
                if (btn != null && defaultScales.TryGetValue(btn.transform, out Vector3 originalScale))
                {
                    btn.transform.localScale = originalScale;
                }
            }
        }
    }

    private IEnumerator ContinuousPopRoutine()
    {
        float timer = 0f;

        while (true)
        {
            timer += Time.unscaledDeltaTime * popPulseSpeed;

            // Ping-pong scale wave between 1.0 and popScaleMultiplier
            float wave = (Mathf.Sin(timer) + 1f) * 0.5f;

            if (nextButtons != null)
            {
                foreach (Button btn in nextButtons)
                {
                    if (btn != null && btn.gameObject.activeInHierarchy && defaultScales.TryGetValue(btn.transform, out Vector3 originalScale))
                    {
                        btn.transform.localScale = Vector3.Lerp(originalScale, originalScale * popScaleMultiplier, wave);
                    }
                }
            }

            yield return null;
        }
    }

    private void UpdateDisplay()
    {
        if (!pageNumberText)
            return;

        int displayedPage = developerIndexMode
            ? currentIndex
            : currentIndex + 1;

        pageNumberText.text = $"{displayedPage}/{NavigationPageCount}";
    }

    public bool IsPageVisited(int pageIndex) => visitedPages.Contains(pageIndex);
    public bool IsPageCompleted(int pageIndex) => completedPages.Contains(pageIndex);
}