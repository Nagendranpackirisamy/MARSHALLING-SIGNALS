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

    [Header("Global Default Feedback Titles")]
    [SerializeField] private string defaultCorrectTitle = "Correct";
    [SerializeField] private string defaultWrongTitle = "Incorrect";
    [SerializeField] private Color correctColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color incorrectColor = new Color(0.9f, 0.65f, 0.1f);
    [SerializeField] private Button postQuizContinueButton;

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

        if (option.isCorrect)
            PlaySound(correctSound);
        else
            PlaySound(wrongSound);

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

            yield return new WaitForSeconds(3.0f);

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