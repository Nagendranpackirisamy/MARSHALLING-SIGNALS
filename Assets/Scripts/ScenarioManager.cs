using UnityEngine;
using System;
using System.Collections.Generic;

public class ScenarioManager : MonoBehaviour
{
    [Header("Scenario Data")]
    [SerializeField] private List<ScenarioData> scenarios;

    // ------------------------------------------------------------------
    // EVENTS
    // ------------------------------------------------------------------

    /// <summary>
    /// Fired when a scenario should begin.
    /// </summary>
    public event Action<ScenarioData, int> OnScenarioLoaded;

    /// <summary>
    /// Fired when the user selects the correct answer.
    /// </summary>
    public event Action<ScenarioData, int> OnCorrectAnswer;

    /// <summary>
    /// Fired when the user selects the wrong answer.
    /// </summary>
    public event Action<ScenarioData, int> OnWrongAnswer;

    /// <summary>
    /// Fired ONLY the first time a scenario is completed.
    /// Used to show the completion panel.
    /// </summary>
    public event Action<int> OnScenarioFirstCompleted;

    /// <summary>
    /// Fired when intro narration finishes.
    /// </summary>
    public event Action OnIntroVOComplete;

    /// <summary>
    /// Fired when a scenario requests a video.
    /// VideoController should play it and later call NotifyVideoComplete().
    /// </summary>
    public event Action<ScenarioData, int> OnVideoRequired;

    /// <summary>
    /// Fired after the final scenario.
    /// </summary>
    public event Action OnAllScenariosComplete;

    public event Action OnFeedbackVOComplete;

    public event Action<ScenarioData> OnFeedbackFinished;

    /// <summary>
    /// Fired whenever ANY animation (intro, correct, or wrong) finishes
    /// playing. Used by UIController to re-check whether Next/Previous
    /// should be re-enabled now that animating has stopped.
    /// </summary>
    public event Action OnAnimationComplete;

    // ------------------------------------------------------------------
    // STATE
    // ------------------------------------------------------------------

    private int currentIndex;

    private bool[] completedScenarios;
    private bool[] introAudioPlayed;

    /// <summary>
    /// True while AnimationController is actively playing any animation
    /// (intro, correct-answer, or wrong-answer). While true, Next and
    /// Previous must both stay non-interactable.
    /// </summary>
    public bool IsAnimationPlaying { get; private set; }

    // ------------------------------------------------------------------
    // PUBLIC
    // ------------------------------------------------------------------

    public int CurrentIndex => currentIndex;

    public int ScenarioCount => scenarios.Count;

    public ScenarioData CurrentScenario =>
        currentIndex >= 0 && currentIndex < scenarios.Count
            ? scenarios[currentIndex]
            : null;

    public bool IsCompleted(int index)
    {
        if (completedScenarios == null)
            return false;

        if (index < 0 || index >= completedScenarios.Length)
            return false;

        return completedScenarios[index];
    }

    public bool AudioPlayed(int index)
    {
        if (introAudioPlayed == null)
            return false;

        if (index < 0 || index >= introAudioPlayed.Length)
            return false;

        return introAudioPlayed[index];
    }

    public void MarkAudioPlayed(int index)
    {
        if (introAudioPlayed == null)
            return;

        if (index < 0 || index >= introAudioPlayed.Length)
            return;

        introAudioPlayed[index] = true;
    }

    public ScenarioData GetScenario(int index)
    {
        if (index < 0 || index >= scenarios.Count)
            return null;

        return scenarios[index];
    }

    // ------------------------------------------------------------------
    // INITIALIZE
    // ------------------------------------------------------------------

    private void Start()
    {
        completedScenarios = new bool[scenarios.Count];
        introAudioPlayed = new bool[scenarios.Count];

        // Startup flow:
        //
        // If using startup video:
        // VideoController -> ShowScenario(0)
        //
        // Otherwise:
        // scenarioManager.ShowScenario(0);
    }

    // ------------------------------------------------------------------
    // LOADING
    // ------------------------------------------------------------------

    public void ShowScenario(int index)
    {
        if (index < 0 || index >= scenarios.Count)
            return;

        currentIndex = index;

        ScenarioData scenario = scenarios[index];

        // --------------------------------------------------------------
        // VIDEO PAGE
        // --------------------------------------------------------------

        if (scenario.playVideo && scenario.videoClip != null)
        {
            OnVideoRequired?.Invoke(scenario, index);
            return;
        }

        LoadCurrentScenario();
    }

    /// <summary>
    /// Called by VideoController once the video has finished.
    /// </summary>
    public void NotifyVideoComplete()
    {
        LoadCurrentScenario();
    }

    public void NotifyFeedbackVOComplete()
    {
        OnFeedbackVOComplete?.Invoke();
    }

    public void NotifyFeedbackFinished(ScenarioData scenario)
    {
        OnFeedbackFinished?.Invoke(scenario);
    }

    private void LoadCurrentScenario()
    {
        OnScenarioLoaded?.Invoke(
            scenarios[currentIndex],
            currentIndex);
    }

    // ------------------------------------------------------------------
    // ANSWERS
    // ------------------------------------------------------------------

    public void SelectAnswer(int selectedAnswer)
    {
        ScenarioData scenario = scenarios[currentIndex];

        // Ignore if already completed
        if (completedScenarios[currentIndex])
            return;

        bool correct =
            selectedAnswer ==
            scenario.correctOptionIndex;

        if (correct)
            OnCorrectAnswer?.Invoke(scenario, selectedAnswer);
        else
            OnWrongAnswer?.Invoke(scenario, selectedAnswer);
    }

    // ------------------------------------------------------------------
    // AUDIO
    // ------------------------------------------------------------------

    public void NotifyIntroVOComplete()
    {
        OnIntroVOComplete?.Invoke();
    }

    // ------------------------------------------------------------------
    // ANIMATION
    // ------------------------------------------------------------------

    /// <summary>
    /// Called by AnimationController whenever it starts OR stops playing
    /// any animation. UIController reads IsAnimationPlaying to decide
    /// whether Next/Previous are allowed to be interactable.
    /// </summary>
    public void SetAnimationPlaying(bool isPlaying)
    {
        IsAnimationPlaying = isPlaying;
    }

    /// <summary>
    /// Called by AnimationController after the correct answer's animation ends.
    /// </summary>
    public void NotifyCorrectAnimComplete(int scenarioIndex)
    {
        // Ignore stale coroutine
        if (scenarioIndex != currentIndex)
        {
            Debug.Log(
                $"Ignored stale animation callback ({scenarioIndex})");
            return;
        }

        // Already completed
        if (completedScenarios[scenarioIndex])
            return;

        completedScenarios[scenarioIndex] = true;

        OnScenarioFirstCompleted?.Invoke(scenarioIndex);
    }

    /// <summary>
    /// Called by AnimationController after ANY animation (intro, correct,
    /// or wrong) finishes playing. Does NOT mark the scenario as completed
    /// by itself - NotifyCorrectAnimComplete handles that separately.
    /// </summary>
    public void NotifyAnimationComplete()
    {
        OnAnimationComplete?.Invoke();
    }

    // ------------------------------------------------------------------
    // NAVIGATION
    // ------------------------------------------------------------------

    public void GoNext()
    {
        ScenarioData current = scenarios[currentIndex];

        if (current.requiresAnswer &&
            !completedScenarios[currentIndex])
            return;

        if (currentIndex >= scenarios.Count - 1)
        {
            OnAllScenariosComplete?.Invoke();
            return;
        }

        ShowScenario(currentIndex + 1);
    }

    public void GoPrevious()
    {
        if (currentIndex <= 0)
            return;

        ShowScenario(currentIndex - 1);
    }

    // ------------------------------------------------------------------
    // UTILITIES
    // ------------------------------------------------------------------

    public void RestartTraining()
    {
        Array.Clear(completedScenarios, 0, completedScenarios.Length);
        Array.Clear(introAudioPlayed, 0, introAudioPlayed.Length);

        currentIndex = 0;

        ShowScenario(0);
    }
}