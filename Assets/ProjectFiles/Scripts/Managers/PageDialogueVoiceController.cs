using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class PageDialogueVoiceController : MonoBehaviour
{
    public enum UIPopOrigin
    {
        Center,
        LeftCorner,
        RightCorner
    }

    [System.Serializable]
    public class QuizOption
    {
        [TextArea(1, 3)] public string optionText;
        public bool isCorrect;
    }

    [System.Serializable]
    public class QuizData
    {
        [TextArea(2, 4)] public string questionText;
        public QuizOption[] options = new QuizOption[4];

        [Header("Feedback Titles (Leave empty to use Global defaults)")]
        public string correctTitle = "Correct";
        public string wrongTitle = "Incorrect";

        [Header("Feedback Explanations (Common for this quiz)")]
        [TextArea(2, 4)] public string correctExplanation;
        [TextArea(2, 4)] public string wrongExplanation;
    }

    [System.Serializable]
    public class PageContentConfig
    {
        [Header("Page Identity")]
        public string pageLabel = "Page";

        [Header("Target UI / GameObject")]
        public GameObject targetGameObject;

        [Header("Optional Header / Title")]
        public TMP_Text titleComponent;
        public string titleContent;

        [Header("Body Content")]
        public TMP_Text textComponent;
        [TextArea(3, 6)] public string textContent;

        [Header("UI Pop-In Transition")]
        [Tooltip("If checked, animates the target GameObject from the chosen origin.")]
        public bool enableUIPop = false;
        public UIPopOrigin popOrigin = UIPopOrigin.Center;

        [Header("Typewriter Settings")]
        public bool useTypewriterEffect = false;
        public float typingSpeed = 0.04f;

        [Header("Voice-Over Settings")]
        public bool playVoiceOver = false;
        public AudioClip[] voiceClips;

        [Tooltip("If true, automatically pops up the post-audio continue button when voice over ends. If false, trigger it via TriggerPostAudioContinue().")]
        public bool showContinueButtonAfterAudio = true;

        [Header("Quiz Settings For This Page")]
        public bool hasQuiz = false;
        public QuizData quizData;

        [Header("Objects Visibility On Quiz")]
        [Tooltip("GameObjects to hide when the quiz UI appears on this page.")]
        public GameObject[] objectsToHideOnQuiz;
        [Tooltip("GameObjects to show when the quiz UI appears on this page.")]
        public GameObject[] objectsToShowOnQuiz;

        [Header("Page Callbacks")]
        public UnityEvent onAllVoiceClipsEnded;
        public UnityEvent<int> onSingleVoiceClipEnded;
    }

    [Header("Audio Output")]
    [SerializeField] private AudioSource voiceAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;
    [Tooltip("Common sound played when UI pops into view.")]
    [SerializeField] private AudioClip uiPopSound;

    [Header("Quiz Audio Feedback (Common For All Quizzes)")]
    [Tooltip("Common sound played on a correct answer.")]
    [SerializeField] private AudioClip correctSound;
    [Tooltip("Common sound played on a wrong answer.")]
    [SerializeField] private AudioClip wrongSound;

    [Header("UI Animation Settings")]
    [SerializeField] private float animationDuration = 0.4f;

    [Header("Intermediate Continue Button (Post-Audio)")]
    [Tooltip("Button that appears after audio ends to transition to quiz or unlock.")]
    [SerializeField] private Button postAudioContinueButton;

    [Header("Shared Quiz UI Panel")]
    [SerializeField] private GameObject quizContainer;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button[] optionButtons = new Button[4];
    [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[4];

    [Header("Quiz Feedback Pop-Up")]
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TMP_Text feedbackTitleText;
    [SerializeField] private TMP_Text feedbackExplanationText;
    [Tooltip("How many seconds the wrong answer UI remains visible before returning to the quiz.")]
    [SerializeField] private float wrongFeedbackDuration = 3.0f;

    [Header("Feedback Sprite Swapping")]
    [SerializeField] private Image feedbackBgImage;
    [SerializeField] private Image feedbackIconImage;
    [SerializeField] private Sprite correctBgSprite;
    [SerializeField] private Sprite wrongBgSprite;
    [SerializeField] private Sprite correctIconSprite;
    [SerializeField] private Sprite wrongIconSprite;

    [Header("Global Default Feedback Titles")]
    [SerializeField] private string defaultCorrectTitle = "Correct";
    [SerializeField] private string defaultWrongTitle = "Incorrect";
    [SerializeField] private Color correctColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color incorrectColor = new Color(0.9f, 0.65f, 0.1f);
    [SerializeField] private Button postQuizContinueButton;

    [Header("Results & Summary Screen (Final Page)")]
    [Tooltip("Index of the final summary page in the Pages list.")]
    [SerializeField] private int resultsPageIndex = 10;
    [SerializeField] private TMP_Text scoreRevealText;
    [SerializeField] private TMP_Text bestStreakText;
    [SerializeField] private TMP_Text rankBadgeText;
    [SerializeField] private TMP_Text passFailBannerText;
    [SerializeField] private TMP_Text closingLineText;
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private Button saveAndExitButton;

    [Header("Scoring & Badges Settings")]
    [SerializeField] private int pointsFirstTry = 10;
    [SerializeField] private int pointsSecondTry = 5;
    [SerializeField] private int pointsMultipleTries = 2;
    [SerializeField] private int passingScoreThreshold = 70;

    [Header("Try Again Navigation Callback")]
    [Tooltip("Hook up PageNavigationController's page jump method here or let the script auto-detect it.")]
    public UnityEvent onTryAgainRequested;

    [Header("Pages Configuration")]
    [SerializeField] private List<PageContentConfig> pages = new();

    // Global Events
    public static event Action<int> OnPageVoiceSequenceFinished;
    public static event Action<int, int> OnPageSingleClipFinished;

    private Coroutine currentTypewriterCoroutine;
    private Coroutine currentAudioCoroutine;
    private Coroutine currentAnimCoroutine;
    private Coroutine feedbackCoroutine;
    private Coroutine continueBtnAnimCoroutine;

    private int activePageIndex = -1;
    private readonly Dictionary<Transform, Vector3> defaultLocalPositions = new();
    private readonly Dictionary<Transform, Vector3> defaultLocalScales = new();

    // Tracking Scores & Attempts
    private readonly Dictionary<int, int> quizAttemptsPerPage = new();
    private readonly Dictionary<int, int> quizScoreEarnedPerPage = new();
    private int currentStreak = 0;
    private int bestStreak = 0;

    private void Awake()
    {
        if (voiceAudioSource == null)
            voiceAudioSource = gameObject.AddComponent<AudioSource>();

        if (sfxAudioSource == null)
            sfxAudioSource = gameObject.AddComponent<AudioSource>();

        voiceAudioSource.playOnAwake = false;
        sfxAudioSource.playOnAwake = false;

        CacheTransforms();

        if (postAudioContinueButton != null)
            postAudioContinueButton.onClick.AddListener(OnPostAudioContinueClicked);

        if (postQuizContinueButton != null)
            postQuizContinueButton.onClick.AddListener(OnPostQuizContinueClicked);

        if (tryAgainButton != null)
            tryAgainButton.onClick.AddListener(OnTryAgainClicked);

        if (saveAndExitButton != null)
            saveAndExitButton.onClick.AddListener(OnSaveAndExitClicked);

        for (int i = 0; i < optionButtons.Length; i++)
        {
            int index = i;
            if (optionButtons[i] != null)
            {
                optionButtons[i].onClick.AddListener(() => OnOptionSelected(index));
            }
        }
    }

    private void CacheTransforms()
    {
        foreach (var page in pages)
        {
            if (page.targetGameObject != null)
            {
                Transform t = page.targetGameObject.transform;
                if (!defaultLocalPositions.ContainsKey(t))
                    defaultLocalPositions[t] = t.localPosition;
                if (!defaultLocalScales.ContainsKey(t))
                    defaultLocalScales[t] = t.localScale;
            }
        }

        if (quizContainer != null)
        {
            Transform qt = quizContainer.transform;
            if (!defaultLocalPositions.ContainsKey(qt))
                defaultLocalPositions[qt] = qt.localPosition;
            if (!defaultLocalScales.ContainsKey(qt))
                defaultLocalScales[qt] = qt.localScale;
        }

        if (feedbackPanel != null)
        {
            Transform ft = feedbackPanel.transform;
            if (!defaultLocalPositions.ContainsKey(ft))
                defaultLocalPositions[ft] = ft.localPosition;
            if (!defaultLocalScales.ContainsKey(ft))
                defaultLocalScales[ft] = ft.localScale;
        }

        if (postAudioContinueButton != null)
        {
            Transform bt = postAudioContinueButton.transform;
            if (!defaultLocalScales.ContainsKey(bt))
                defaultLocalScales[bt] = bt.localScale;
        }
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += HandlePageChanged;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= HandlePageChanged;
        StopAllRoutines();
    }

    private void HandlePageChanged(int newPageIndex)
    {
        StopAllRoutines();
        activePageIndex = newPageIndex;

        if (postAudioContinueButton != null)
            postAudioContinueButton.gameObject.SetActive(false);

        if (quizContainer != null)
            quizContainer.SetActive(false);

        if (feedbackPanel != null)
            feedbackPanel.SetActive(false);

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i].targetGameObject != null)
                pages[i].targetGameObject.SetActive(false);
        }

        // Display results if entering the Summary page
        if (newPageIndex == resultsPageIndex)
        {
            DisplayFinalResults();
        }

        if (newPageIndex < 0 || newPageIndex >= pages.Count)
            return;

        PageContentConfig config = pages[newPageIndex];

        // 1. Activate & Animate Page UI GameObject
        if (config.targetGameObject != null)
        {
            config.targetGameObject.SetActive(true);

            if (config.enableUIPop)
            {
                PlayPopSound();
                currentAnimCoroutine = StartCoroutine(AnimatePopRoutine(config.targetGameObject.transform, config.popOrigin));
            }
            else
            {
                ResetTransform(config.targetGameObject.transform);
            }
        }

        // 2. Set Optional Title Header
        if (config.titleComponent != null)
        {
            config.titleComponent.text = config.titleContent ?? "";
        }

        // 3. Typewriter vs Instant Body Text
        if (config.textComponent != null)
        {
            if (config.useTypewriterEffect)
            {
                currentTypewriterCoroutine = StartCoroutine(TypewriterRoutine(config.textComponent, config.textContent, config.typingSpeed));
            }
            else
            {
                config.textComponent.text = config.textContent;
            }
        }

        // 4. Audio Handling
        if (config.playVoiceOver && config.voiceClips != null && config.voiceClips.Length > 0)
        {
            currentAudioCoroutine = StartCoroutine(SequentialAudioRoutine(newPageIndex, config));
        }
        else
        {
            HandleVoiceSequenceComplete(newPageIndex, config);
        }
    }

    private void HandleVoiceSequenceComplete(int pageIndex, PageContentConfig config)
    {
        config.onAllVoiceClipsEnded?.Invoke();
        OnPageVoiceSequenceFinished?.Invoke(pageIndex);

        if (config.showContinueButtonAfterAudio)
        {
            TriggerPostAudioContinue();
        }
    }

    public void TriggerPostAudioContinue()
    {
        if (postAudioContinueButton != null)
        {
            postAudioContinueButton.gameObject.SetActive(true);
            PlayPopSound();

            if (continueBtnAnimCoroutine != null)
                StopCoroutine(continueBtnAnimCoroutine);

            continueBtnAnimCoroutine = StartCoroutine(PopScaleRoutine(postAudioContinueButton.transform));
        }
        else
        {
            OnPostAudioContinueClicked();
        }
    }

    private void OnPostAudioContinueClicked()
    {
        if (postAudioContinueButton != null)
            postAudioContinueButton.gameObject.SetActive(false);

        if (activePageIndex < 0 || activePageIndex >= pages.Count) return;
        PageContentConfig config = pages[activePageIndex];

        if (config.hasQuiz && config.quizData != null && quizContainer != null)
        {
            if (config.targetGameObject != null)
                config.targetGameObject.SetActive(false);

            if (config.objectsToHideOnQuiz != null)
            {
                foreach (GameObject obj in config.objectsToHideOnQuiz)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }

            if (config.objectsToShowOnQuiz != null)
            {
                foreach (GameObject obj in config.objectsToShowOnQuiz)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }

            ShowQuiz(config.quizData, config.popOrigin);
        }
        else
        {
            PageNavigationController.RequestNavigationUnlock();
        }
    }

    private void ShowQuiz(QuizData data, UIPopOrigin origin)
    {
        quizContainer.SetActive(true);
        PlayPopSound();
        currentAnimCoroutine = StartCoroutine(AnimatePopRoutine(quizContainer.transform, origin));

        if (questionText != null)
            questionText.text = data.questionText;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (i < data.options.Length && data.options[i] != null)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].interactable = true;
                if (optionLabels[i] != null)
                    optionLabels[i].text = data.options[i].optionText;
            }
            else
            {
                optionButtons[i].gameObject.SetActive(false);
            }
        }

        if (feedbackPanel != null)
            feedbackPanel.SetActive(false);

        if (postQuizContinueButton != null)
            postQuizContinueButton.gameObject.SetActive(false);
    }

    private void OnOptionSelected(int optionIndex)
    {
        if (activePageIndex < 0 || activePageIndex >= pages.Count) return;
        PageContentConfig config = pages[activePageIndex];

        if (config.quizData == null || optionIndex >= config.quizData.options.Length) return;

        QuizOption selectedOption = config.quizData.options[optionIndex];

        // Track attempts for this question
        if (!quizAttemptsPerPage.ContainsKey(activePageIndex))
            quizAttemptsPerPage[activePageIndex] = 0;

        quizAttemptsPerPage[activePageIndex]++;

        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackCoroutine = StartCoroutine(FeedbackRoutine(selectedOption, config.quizData, config.popOrigin));
    }

    private IEnumerator FeedbackRoutine(QuizOption option, QuizData quizData, UIPopOrigin origin)
    {
        if (feedbackPanel == null) yield break;

        if (quizContainer != null)
            quizContainer.SetActive(false);

        feedbackPanel.SetActive(true);

        // Swap sprites for background panel and icon
        if (feedbackBgImage != null)
        {
            Sprite targetBg = option.isCorrect ? correctBgSprite : wrongBgSprite;
            if (targetBg != null) feedbackBgImage.sprite = targetBg;
        }

        if (feedbackIconImage != null)
        {
            Sprite targetIcon = option.isCorrect ? correctIconSprite : wrongIconSprite;
            if (targetIcon != null) feedbackIconImage.sprite = targetIcon;
        }

        if (option.isCorrect)
        {
            PlaySound(correctSound);

            // Calculate score for this question if not already scored
            if (!quizScoreEarnedPerPage.ContainsKey(activePageIndex))
            {
                int attempts = quizAttemptsPerPage[activePageIndex];
                int earned = 0;

                if (attempts == 1)
                {
                    earned = pointsFirstTry;
                    currentStreak++;
                    if (currentStreak > bestStreak) bestStreak = currentStreak;
                }
                else if (attempts == 2)
                {
                    earned = pointsSecondTry;
                    currentStreak = 0;
                }
                else
                {
                    earned = pointsMultipleTries;
                    currentStreak = 0;
                }

                quizScoreEarnedPerPage[activePageIndex] = earned;
            }
        }
        else
        {
            PlaySound(wrongSound);
            currentStreak = 0;
        }

        currentAnimCoroutine = StartCoroutine(AnimatePopRoutine(feedbackPanel.transform, origin));

        if (feedbackTitleText != null)
        {
            if (option.isCorrect)
            {
                string title = !string.IsNullOrEmpty(quizData.correctTitle) ? quizData.correctTitle : defaultCorrectTitle;
                feedbackTitleText.text = title;
                feedbackTitleText.color = correctColor;
            }
            else
            {
                string title = !string.IsNullOrEmpty(quizData.wrongTitle) ? quizData.wrongTitle : defaultWrongTitle;
                feedbackTitleText.text = title;
                feedbackTitleText.color = incorrectColor;
            }
        }

        if (feedbackExplanationText != null)
        {
            feedbackExplanationText.text = option.isCorrect
                ? quizData.correctExplanation
                : quizData.wrongExplanation;
        }

        if (option.isCorrect)
        {
            SetOptionButtonsInteractable(false);

            if (postQuizContinueButton != null)
            {
                postQuizContinueButton.gameObject.SetActive(true);
                StartCoroutine(PopScaleRoutine(postQuizContinueButton.transform));
            }
        }
        else
        {
            SetOptionButtonsInteractable(false);

            yield return new WaitForSeconds(wrongFeedbackDuration);

            feedbackPanel.SetActive(false);

            if (quizContainer != null)
            {
                quizContainer.SetActive(true);
                currentAnimCoroutine = StartCoroutine(AnimatePopRoutine(quizContainer.transform, origin));
            }

            SetOptionButtonsInteractable(true);
        }

        feedbackCoroutine = null;
    }

    private void OnPostQuizContinueClicked()
    {
        if (feedbackPanel != null) feedbackPanel.SetActive(false);
        if (quizContainer != null) quizContainer.SetActive(false);

        PageNavigationController.RequestNavigationUnlock();
    }

    // =========================================================
    // RESULTS & SUMMARY CALCULATION
    // =========================================================

    public void DisplayFinalResults()
    {
        int totalScore = 0;
        bool allFirstTry = true;
        int quizCount = 0;

        foreach (var page in pages)
        {
            if (page.hasQuiz)
                quizCount++;
        }

        foreach (var entry in quizScoreEarnedPerPage)
        {
            totalScore += entry.Value;
        }

        foreach (var entry in quizAttemptsPerPage)
        {
            if (entry.Value > 1)
            {
                allFirstTry = false;
                break;
            }
        }

        if (quizAttemptsPerPage.Count < quizCount)
            allFirstTry = false;

        // 1. Score Headline
        if (scoreRevealText != null)
            scoreRevealText.text = $"{totalScore} <size=60%>/ 100</size>";

        // 2. Best Streak
        if (bestStreakText != null)
            bestStreakText.text = $"Best streak: {bestStreak}";

        // 3. Rank Badge Copy
        if (rankBadgeText != null)
        {
            if (allFirstTry && totalScore >= 100)
            {
                rankBadgeText.text = "MASTER MARSHALLER — every signal, first try.";
            }
            else if (totalScore >= passingScoreThreshold)
            {
                rankBadgeText.text = "MARSHALLER — solid, confident signal reading.";
            }
            else
            {
                rankBadgeText.text = "TRAINEE — the basics are in, keep practising.";
            }
        }

        // 4. Pass / Fail Banner
        if (passFailBannerText != null)
        {
            if (totalScore >= passingScoreThreshold)
            {
                passFailBannerText.text = "✓ Passed — you can read the ramp like a pro.";
                passFailBannerText.color = correctColor;
            }
            else
            {
                passFailBannerText.text = "Not quite there yet — but every signal you missed is one tap away in the Library.";
                passFailBannerText.color = incorrectColor;
            }
        }

        // 5. Closing Line (100% score only)
        if (closingLineText != null)
        {
            if (totalScore >= 100 && allFirstTry)
            {
                closingLineText.gameObject.SetActive(true);
                closingLineText.text = "Perfect run. Ten for ten — that's a real marshaller's eye.";
            }
            else
            {
                closingLineText.gameObject.SetActive(false);
            }
        }
    }

    public void ResetAllQuizScores()
    {
        quizAttemptsPerPage.Clear();
        quizScoreEarnedPerPage.Clear();
        currentStreak = 0;
        bestStreak = 0;
    }

    private void OnTryAgainClicked()
    {
        ResetAllQuizScores();

        // 1. Fire Inspector event if hooked up
        if (onTryAgainRequested != null && onTryAgainRequested.GetPersistentEventCount() > 0)
        {
            onTryAgainRequested.Invoke();
            return;
        }

        // 2. Fallback: find PageNavigationController in scene
#if UNITY_2023_1_OR_NEWER
        PageNavigationController navController = UnityEngine.Object.FindFirstObjectByType<PageNavigationController>();
#else
        PageNavigationController navController = UnityEngine.Object.FindObjectOfType<PageNavigationController>();
#endif
        if (navController != null)
        {
            // Unlocks navigation and moves back to the beginning
            PageNavigationController.RequestNavigationUnlock();
            navController.SendMessage("GoToPage", 0, SendMessageOptions.DontRequireReceiver);
            navController.SendMessage("SetPage", 0, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void OnSaveAndExitClicked()
    {
        Debug.Log("[PageDialogueVoiceController] Results Saved. Exiting application.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetOptionButtonsInteractable(bool state)
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            if (optionButtons[i] != null)
                optionButtons[i].interactable = state;
        }
    }

    private void PlayPopSound() => PlaySound(uiPopSound);

    private void PlaySound(AudioClip clip)
    {
        if (sfxAudioSource != null && clip != null)
            sfxAudioSource.PlayOneShot(clip);
    }

    private IEnumerator AnimatePopRoutine(Transform target, UIPopOrigin origin)
    {
        Vector3 finalPos = defaultLocalPositions.ContainsKey(target) ? defaultLocalPositions[target] : target.localPosition;
        Vector3 finalScale = defaultLocalScales.ContainsKey(target) ? defaultLocalScales[target] : target.localScale;

        Vector3 startPos = finalPos;
        Vector3 startScale = finalScale;

        float screenWidth = Screen.width;

        switch (origin)
        {
            case UIPopOrigin.LeftCorner:
                startPos = finalPos + new Vector3(-screenWidth * 0.8f, 0, 0);
                break;
            case UIPopOrigin.RightCorner:
                startPos = finalPos + new Vector3(screenWidth * 0.8f, 0, 0);
                break;
            case UIPopOrigin.Center:
                startScale = Vector3.zero;
                break;
        }

        target.localPosition = startPos;
        target.localScale = startScale;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            target.localPosition = Vector3.Lerp(startPos, finalPos, t);
            target.localScale = Vector3.Lerp(startScale, finalScale, t);
            yield return null;
        }

        target.localPosition = finalPos;
        target.localScale = finalScale;
        currentAnimCoroutine = null;
    }

    private IEnumerator PopScaleRoutine(Transform target)
    {
        Vector3 finalScale = defaultLocalScales.ContainsKey(target) ? defaultLocalScales[target] : target.localScale;
        target.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);
            target.localScale = Vector3.Lerp(Vector3.zero, finalScale, t);
            yield return null;
        }

        target.localScale = finalScale;
    }

    private void ResetTransform(Transform target)
    {
        if (defaultLocalPositions.TryGetValue(target, out Vector3 pos))
            target.localPosition = pos;

        if (defaultLocalScales.TryGetValue(target, out Vector3 scale))
            target.localScale = scale;
    }

    private IEnumerator TypewriterRoutine(TMP_Text targetText, string fullText, float delay)
    {
        targetText.text = "";
        WaitForSeconds wait = new(delay);

        for (int i = 0; i < fullText.Length; i++)
        {
            if (fullText[i] == '<')
            {
                int closeTag = fullText.IndexOf('>', i);
                if (closeTag != -1)
                {
                    targetText.text += fullText.Substring(i, closeTag - i + 1);
                    i = closeTag;
                    continue;
                }
            }

            targetText.text += fullText[i];
            yield return wait;
        }

        currentTypewriterCoroutine = null;
    }

    private IEnumerator SequentialAudioRoutine(int pageIndex, PageContentConfig config)
    {
        for (int i = 0; i < config.voiceClips.Length; i++)
        {
            AudioClip clip = config.voiceClips[i];
            if (clip != null)
            {
                voiceAudioSource.clip = clip;
                voiceAudioSource.Play();

                yield return new WaitForSeconds(clip.length);

                config.onSingleVoiceClipEnded?.Invoke(i);
                OnPageSingleClipFinished?.Invoke(pageIndex, i);
            }
        }

        HandleVoiceSequenceComplete(pageIndex, config);
        currentAudioCoroutine = null;
    }

    private void StopAllRoutines()
    {
        if (currentTypewriterCoroutine != null) { StopCoroutine(currentTypewriterCoroutine); currentTypewriterCoroutine = null; }
        if (currentAudioCoroutine != null) { StopCoroutine(currentAudioCoroutine); currentAudioCoroutine = null; }
        if (currentAnimCoroutine != null) { StopCoroutine(currentAnimCoroutine); currentAnimCoroutine = null; }
        if (feedbackCoroutine != null) { StopCoroutine(feedbackCoroutine); feedbackCoroutine = null; }
        if (continueBtnAnimCoroutine != null) { StopCoroutine(continueBtnAnimCoroutine); continueBtnAnimCoroutine = null; }

        if (voiceAudioSource != null && voiceAudioSource.isPlaying) voiceAudioSource.Stop();
    }
}