using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScenarioManager scenarioManager;

    [Header("Instruction UI")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private TextMeshProUGUI instructionText;

    [Header("Scene Intro Panels")]
    [SerializeField] private GameObject groundIntroPanel;
    [SerializeField] private TextMeshProUGUI groundIntroText;
    [SerializeField] private GameObject flightIntroPanel;
    [SerializeField] private TextMeshProUGUI flightIntroText;
    [SerializeField] private GameObject advancedIntroPanel;
    [SerializeField] private TextMeshProUGUI advancedIntroText;

    [Header("Question")]
    [SerializeField] private GameObject questionPanel;
    [SerializeField] private TextMeshProUGUI questionText;

    [SerializeField] private Button optionAButton;
    [SerializeField] private TextMeshProUGUI optionAText;

    [SerializeField] private Button optionBButton;
    [SerializeField] private TextMeshProUGUI optionBText;

    [Header("Result Icons")]
    [SerializeField] private Image optionAResultIcon;
    [SerializeField] private Image optionBResultIcon;
    [SerializeField] private Sprite correctIconSprite;
    [SerializeField] private Sprite wrongIconSprite;

    [Header("Feedback")]
    [SerializeField] private GameObject feedbackPanel;
    [SerializeField] private TextMeshProUGUI feedbackDescriptionText;
    [SerializeField] private TextMeshProUGUI feedbackTitleText;
    [SerializeField] private float typewriterCharDelay = 0.03f;
    [SerializeField] private float popupHoldTime = 2f;

    [Header("Wrong Vignette")]
    [SerializeField] private CanvasGroup wrongVignette;
    [SerializeField] private float vignetteFadeInTime = .15f;
    [SerializeField] private float vignetteHoldTime = .2f;
    [SerializeField] private float vignetteFadeOutTime = .4f;

    [Header("Navigation")]
    [SerializeField] private Button nextButton;
    [SerializeField] private Button previousButton;

    [Header("Completion")]
    [SerializeField] private GameObject completionPanel;
    [SerializeField] private TextMeshProUGUI completionTitleText;
    [SerializeField] private TextMeshProUGUI completionBodyText;
    [SerializeField] private CompletionTextData completionTextData;
    [SerializeField] private Button completionNextButton;

    [Header("Feedback Titles")]
    [SerializeField] private string correctTitle = "Correct";
    [SerializeField] private string incorrectTitle = "Incorrect";

    private bool completionVisible;
    private bool waitingForFeedback;
    private bool wasCorrectAnswer;

    private ScenarioData currentFeedbackScenario;

    private void Awake()
    {
        optionAButton.onClick.AddListener(() => scenarioManager.SelectAnswer(0));
        optionBButton.onClick.AddListener(() => scenarioManager.SelectAnswer(1));

        nextButton.onClick.AddListener(scenarioManager.GoNext);
        previousButton.onClick.AddListener(scenarioManager.GoPrevious);

        completionNextButton.onClick.AddListener(OnCompletionNextPressed);
    }

    private void OnEnable()
    {
        scenarioManager.OnScenarioLoaded += HandleScenarioLoaded;
        scenarioManager.OnCorrectAnswer += HandleCorrectAnswer;
        scenarioManager.OnWrongAnswer += HandleWrongAnswer;

        scenarioManager.OnScenarioFirstCompleted += HandleScenarioCompleted;

        scenarioManager.OnIntroVOComplete += HandleIntroVOComplete;
        scenarioManager.OnAnimationComplete += HandleAnimationComplete;

        scenarioManager.OnAllScenariosComplete += HandleTrainingFinished;

        scenarioManager.OnFeedbackVOComplete += HandleFeedbackVOComplete;
    }

    private void OnDisable()
    {
        scenarioManager.OnScenarioLoaded -= HandleScenarioLoaded;
        scenarioManager.OnCorrectAnswer -= HandleCorrectAnswer;
        scenarioManager.OnWrongAnswer -= HandleWrongAnswer;

        scenarioManager.OnScenarioFirstCompleted -= HandleScenarioCompleted;

        scenarioManager.OnIntroVOComplete -= HandleIntroVOComplete;
        scenarioManager.OnAnimationComplete -= HandleAnimationComplete;

        scenarioManager.OnAllScenariosComplete -= HandleTrainingFinished;

        scenarioManager.OnFeedbackVOComplete -= HandleFeedbackVOComplete;
    }

    private void HandleScenarioLoaded(ScenarioData scenario, int index)
    {
        StopAllCoroutines();

        completionVisible = false;

        HideAllPanels();

        completionPanel.SetActive(false);

        nextButton.gameObject.SetActive(true);

        ClearIcons();

        if (!scenario.requiresAnswer)
        {
            ShowIntroPage(scenario);
            return;
        }

        ShowQuestionPage(scenario, index);
    }

    private void ShowQuestionPage(ScenarioData scenario, int index)
    {
        questionPanel.SetActive(true);

        questionText.text = scenario.questionText;

        optionAText.text = scenario.optionA;
        optionBText.text = scenario.optionB;

        bool completed = scenarioManager.IsCompleted(index);

        optionAButton.interactable = !completed;
        optionBButton.interactable = !completed;

        // IMPORTANT:
        // Revisits NEVER show completion panel.
        completionPanel.SetActive(false);
        completionVisible = false;

        // Always route through UnlockNavigation so the animation-playing
        // guard is respected instead of setting nextButton directly.
        UnlockNavigation();
    }

    private void ShowIntroPage(ScenarioData scenario)
    {
        LockNavigation();

        switch (scenario.pageType)
        {
            case NonInteractivePageType.InfoPanel:

                instructionPanel.SetActive(true);
                instructionText.text = scenario.instructorIntroLine;

                // UnlockNavigation() itself checks IsAnimationPlaying, so
                // it's safe to always call it here. If an intro animation
                // is configured, AnimationController will already have
                // set IsAnimationPlaying = true by the time this runs
                // (both scripts react to the same OnScenarioLoaded event),
                // so this call will correctly no-op and leave Next/Previous
                // locked until OnAnimationComplete fires.
                UnlockNavigation();

                break;

            case NonInteractivePageType.SceneIntro:

                GameObject panel = null;
                TextMeshProUGUI text = null;

                switch (scenario.context)
                {
                    case SceneContext.Ground:
                        panel = groundIntroPanel;
                        text = groundIntroText;
                        break;

                    case SceneContext.Flight:
                        panel = flightIntroPanel;
                        text = flightIntroText;
                        break;

                    case SceneContext.Advanced:
                        panel = advancedIntroPanel;
                        text = advancedIntroText;
                        break;
                }

                if (panel != null)
                    panel.SetActive(true);

                if (text != null)
                    text.text = scenario.instructorIntroLine;

                // SceneIntro pages currently unlock via VO completion only
                // (HandleIntroVOComplete). If this page ALSO has an intro
                // animation, UnlockNavigation()'s guard will prevent early
                // unlock and HandleAnimationComplete will finish the job.
                break;
        }
    }

    private void HandleIntroVOComplete()
    {
        UnlockNavigation();
    }

    private void HandleAnimationComplete()
    {
        UnlockNavigation();
    }

    private void HandleCorrectAnswer(ScenarioData scenario, int selectedIndex)
    {
        ShowIcon(selectedIndex, true);

        optionAButton.interactable = false;
        optionBButton.interactable = false;

        StartCoroutine(CorrectPopupRoutine(scenario));
    }

    private void HandleWrongAnswer(ScenarioData scenario, int selectedIndex)
    {
        ShowIcon(selectedIndex, false);

        StartCoroutine(FlashWrongVignette());

        StartCoroutine(WrongPopupRoutine(scenario));
    }
    // ------------------------------------------------------------------------
    // POPUP FLOW
    // ------------------------------------------------------------------------

    private IEnumerator CorrectPopupRoutine(ScenarioData scenario)
    {
        LockNavigation();

        currentFeedbackScenario = scenario;
        wasCorrectAnswer = true;
        waitingForFeedback = true;

        feedbackTitleText.text = correctTitle;
        feedbackPanel.SetActive(true);
        questionPanel.SetActive(false);

        yield return StartCoroutine(
            TypewriterReveal(
                feedbackDescriptionText,
                scenario.instructorCorrectLine));

        while (waitingForFeedback)
            yield return null;

        feedbackPanel.SetActive(false);

        // At this point HandleFeedbackVOComplete has already decided
        // whether an animation needs to play (via NotifyFeedbackFinished)
        // or the scenario is already complete. Either way, route through
        // UnlockNavigation so the animation guard is respected rather
        // than assuming it's safe to unlock here.
        UnlockNavigation();
    }

    private IEnumerator WrongPopupRoutine(ScenarioData scenario)
    {
        LockNavigation();

        currentFeedbackScenario = scenario;
        wasCorrectAnswer = false;
        waitingForFeedback = true;

        feedbackTitleText.text = incorrectTitle;
        feedbackPanel.SetActive(true);
        questionPanel.SetActive(false);

        yield return StartCoroutine(
            TypewriterReveal(
                feedbackDescriptionText,
                scenario.instructorWrongLine));

        while (waitingForFeedback)
            yield return null;

        feedbackPanel.SetActive(false);

        questionPanel.SetActive(true);

        optionAButton.interactable = true;
        optionBButton.interactable = true;

        // Was previously an unconditional UnlockNavigation() call, which
        // would re-enable Next/Previous even if a wrong-answer animation
        // just started playing (AnimationController.HandleWrongAnswer
        // reacts to the same OnWrongAnswer event this routine responds
        // to). UnlockNavigation() now checks IsAnimationPlaying itself,
        // so this correctly stays locked if a wrong animation is running,
        // and HandleAnimationComplete will unlock once it finishes.
        UnlockNavigation();

        ClearIcons();
    }

    // ------------------------------------------------------------------------
    // CALLED ONLY ON FIRST COMPLETION
    // ------------------------------------------------------------------------

    private void HandleScenarioCompleted(int scenarioIndex)
    {
        if (scenarioIndex != scenarioManager.CurrentIndex)
            return;

        completionVisible = true;

        ShowCompletionPanel();
    }

    private void HandleFeedbackVOComplete()
    {
        waitingForFeedback = false;

        if (wasCorrectAnswer)
        {
            ScenarioData scenario = currentFeedbackScenario;

            bool hasAnimation =
                !string.IsNullOrEmpty(scenario.correctAnimTrigger) ||
                scenario.correctAnimClip != null;

            if (hasAnimation)
            {
                scenarioManager.NotifyFeedbackFinished(scenario);
            }
            else
            {
                scenarioManager.NotifyCorrectAnimComplete(
                    scenarioManager.CurrentIndex);
            }
        }
    }

    private void ShowCompletionPanel()
    {
        completionPanel.SetActive(true);

        questionPanel.SetActive(false);

        completionTitleText.text = "Mission Complete";

        if (completionTextData != null)
            completionBodyText.text = completionTextData.GetRandom();
        else
            completionBodyText.text = "";

        nextButton.gameObject.SetActive(false);

        previousButton.interactable = false;
    }

    private void OnCompletionNextPressed()
    {
        completionVisible = false;

        completionPanel.SetActive(false);

        nextButton.gameObject.SetActive(true);

        scenarioManager.GoNext();
    }

    // ------------------------------------------------------------------------
    // TYPEWRITER
    // ------------------------------------------------------------------------

    private IEnumerator TypewriterReveal(
        TextMeshProUGUI target,
        string text)
    {
        target.text = "";

        if (string.IsNullOrEmpty(text))
            yield break;

        foreach (char c in text)
        {
            target.text += c;
            yield return new WaitForSeconds(typewriterCharDelay);
        }
    }

    // ------------------------------------------------------------------------
    // WRONG FLASH
    // ------------------------------------------------------------------------

    private IEnumerator FlashWrongVignette()
    {
        if (wrongVignette == null)
            yield break;

        float t = 0f;

        while (t < vignetteFadeInTime)
        {
            t += Time.deltaTime;

            wrongVignette.alpha =
                Mathf.Lerp(0f, 1f, t / vignetteFadeInTime);

            yield return null;
        }

        wrongVignette.alpha = 1f;

        yield return new WaitForSeconds(vignetteHoldTime);

        t = 0f;

        while (t < vignetteFadeOutTime)
        {
            t += Time.deltaTime;

            wrongVignette.alpha =
                Mathf.Lerp(1f, 0f, t / vignetteFadeOutTime);

            yield return null;
        }

        wrongVignette.alpha = 0f;
    }

    // ------------------------------------------------------------------------
    // HELPERS
    // ------------------------------------------------------------------------

    private void LockNavigation()
    {
        nextButton.interactable = false;
        previousButton.interactable = false;
    }

    /// <summary>
    /// The single gatekeeper for turning Next/Previous back on.
    /// ALWAYS call this instead of setting nextButton/previousButton
    /// .interactable directly - it guarantees both buttons stay
    /// non-interactable while any animation is playing, regardless of
    /// which page type or flow triggered the call.
    /// </summary>
    private void UnlockNavigation()
    {
        // Hard guard: no matter what else is true, if an animation is
        // playing, both buttons stay off. Do not early-return without
        // also forcing them false, in case a previous state left them on.
        if (scenarioManager.IsAnimationPlaying)
        {
            nextButton.interactable = false;
            previousButton.interactable = false;
            return;
        }

        ScenarioData current =
            scenarioManager.GetScenario(
                scenarioManager.CurrentIndex);

        bool allowNext =
            !current.requiresAnswer ||
            scenarioManager.IsCompleted(
                scenarioManager.CurrentIndex);

        nextButton.interactable = allowNext;

        previousButton.interactable =
            scenarioManager.CurrentIndex > 0;
    }

    private void HideAllPanels()
    {
        instructionPanel.SetActive(false);

        groundIntroPanel.SetActive(false);
        flightIntroPanel.SetActive(false);
        advancedIntroPanel.SetActive(false);

        questionPanel.SetActive(false);

        feedbackPanel.SetActive(false);

        completionPanel.SetActive(false);
    }

    private void ClearIcons()
    {
        optionAResultIcon.gameObject.SetActive(false);
        optionBResultIcon.gameObject.SetActive(false);
    }

    private void ShowIcon(int selected, bool correct)
    {
        Image icon =
            selected == 0
            ? optionAResultIcon
            : optionBResultIcon;

        Image other =
            selected == 0
            ? optionBResultIcon
            : optionAResultIcon;

        other.gameObject.SetActive(false);

        icon.sprite =
            correct
            ? correctIconSprite
            : wrongIconSprite;

        icon.gameObject.SetActive(true);
    }

    private void HandleTrainingFinished()
    {
        Debug.Log("ATC Training Complete");
    }
}