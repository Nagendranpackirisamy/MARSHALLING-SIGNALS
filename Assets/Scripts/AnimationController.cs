using UnityEngine;
using System.Collections;

public class AnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScenarioManager scenarioManager;
    [SerializeField] private Animator animatorReference;

    [Header("Scene Context Transitions")]
    [SerializeField] private string enterFlightTrigger;
    [SerializeField] private string enterAdvancedTrigger;

    private SceneContext? lastContext;

    private Coroutine animationRoutine;

    private void OnEnable()
    {
        scenarioManager.OnScenarioLoaded += HandleScenarioLoaded;
        scenarioManager.OnFeedbackFinished += HandleFeedbackFinished;

        scenarioManager.OnWrongAnswer += HandleWrongAnswer;
    }

    private void OnDisable()
    {
        scenarioManager.OnScenarioLoaded -= HandleScenarioLoaded;
        scenarioManager.OnFeedbackFinished -= HandleFeedbackFinished;
        scenarioManager.OnWrongAnswer -= HandleWrongAnswer;
    }

    // ------------------------------------------------------------------------

    private void HandleFeedbackFinished(ScenarioData scenario)
    {
        // No animation configured
        if (string.IsNullOrEmpty(scenario.correctAnimTrigger) &&
            scenario.correctAnimClip == null)
        {
            scenarioManager.NotifyCorrectAnimComplete(
                scenarioManager.CurrentIndex);

            return;
        }

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        // Set synchronously before starting the coroutine, same reasoning
        // as HandleScenarioLoaded's intro animation path.
        scenarioManager.SetAnimationPlaying(true);

        animationRoutine = StartCoroutine(
            PlayAnimation(
                scenario,
                true,
                scenarioManager.CurrentIndex));
    }

    private void HandleScenarioLoaded(ScenarioData scenario, int index)
    {
        SnapContextTransition(scenario.context);

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
            scenarioManager.SetAnimationPlaying(false);
        }

        // Play configured animation on entering a non-interactive page
        // (does NOT mark the scenario as completed / trigger completion panel)
        if (!scenario.requiresAnswer &&
            (!string.IsNullOrEmpty(scenario.correctAnimTrigger) ||
             scenario.correctAnimClip != null))
        {
            // Set the flag HERE, synchronously, before starting the
            // coroutine - not inside the coroutine body. This guarantees
            // IsAnimationPlaying is true before this method returns,
            // regardless of subscriber order on OnScenarioLoaded.
            scenarioManager.SetAnimationPlaying(true);

            animationRoutine = StartCoroutine(PlayIntroAnimation(scenario));
        }
    }

    // ------------------------------------------------------------------------

    //private void HandleCorrectAnswer(ScenarioData scenario, int selectedIndex)
    //{
    //    if (animationRoutine != null)
    //        StopCoroutine(animationRoutine);

    //    animationRoutine = StartCoroutine(
    //        PlayAnimation(
    //            scenario,
    //            true,
    //            scenarioManager.CurrentIndex));
    //}

    private void HandleWrongAnswer(ScenarioData scenario, int selectedIndex)
    {
        if (string.IsNullOrEmpty(scenario.wrongAnimTrigger) &&
            scenario.wrongAnimClip == null)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        // Set synchronously before starting the coroutine, same reasoning
        // as above.
        scenarioManager.SetAnimationPlaying(true);

        animationRoutine = StartCoroutine(
            PlayAnimation(
                scenario,
                false,
                scenarioManager.CurrentIndex));
    }

    // ------------------------------------------------------------------------
    // Plays the correct/wrong answer feedback animation. Reports
    // playing state to ScenarioManager so navigation is locked for the
    // full duration, no matter which page type triggered it.
    // ------------------------------------------------------------------------

    private IEnumerator PlayAnimation(
        ScenarioData scenario,
        bool correct,
        int scenarioIndex)
    {
        // NOTE: SetAnimationPlaying(true) is already set by the caller
        // (HandleFeedbackFinished or HandleWrongAnswer) synchronously
        // before this coroutine started. Do not set it again here.

        if (animatorReference == null)
        {
            scenarioManager.SetAnimationPlaying(false);

            if (correct)
                scenarioManager.NotifyCorrectAnimComplete(scenarioIndex);

            scenarioManager.NotifyAnimationComplete();

            yield break;
        }

        string trigger = correct
            ? scenario.correctAnimTrigger
            : scenario.wrongAnimTrigger;

        AnimationClip clip = correct
            ? scenario.correctAnimClip
            : scenario.wrongAnimClip;

        if (!string.IsNullOrEmpty(trigger))
            animatorReference.SetTrigger(trigger);

        float waitTime = 0.5f;

        if (clip != null)
            waitTime = clip.length;

        yield return new WaitForSeconds(waitTime);

        animationRoutine = null;

        scenarioManager.SetAnimationPlaying(false);

        // Only correct answers complete the scenario.
        if (correct)
            scenarioManager.NotifyCorrectAnimComplete(scenarioIndex);

        scenarioManager.NotifyAnimationComplete();
    }

    // ------------------------------------------------------------------------
    // Plays the configured "correct" animation on entering a non-interactive
    // (intro / info) page, WITHOUT notifying scenario completion.
    // Reports playing state and notifies ScenarioManager when done so
    // navigation can unlock.
    // ------------------------------------------------------------------------

    private IEnumerator PlayIntroAnimation(ScenarioData scenario)
    {
        // NOTE: SetAnimationPlaying(true) is already set by the caller
        // (HandleScenarioLoaded) synchronously before this coroutine
        // started. Do not set it again here.

        if (animatorReference == null)
        {
            animationRoutine = null;
            scenarioManager.SetAnimationPlaying(false);
            scenarioManager.NotifyAnimationComplete();
            yield break;
        }

        if (!string.IsNullOrEmpty(scenario.correctAnimTrigger))
            animatorReference.SetTrigger(scenario.correctAnimTrigger);

        float waitTime = scenario.correctAnimClip != null
            ? scenario.correctAnimClip.length
            : 0.5f;

        yield return new WaitForSeconds(waitTime);

        animationRoutine = null;

        scenarioManager.SetAnimationPlaying(false);

        scenarioManager.NotifyAnimationComplete();
    }

    // ------------------------------------------------------------------------

    private void SnapContextTransition(SceneContext context)
    {
        if (lastContext.HasValue &&
            lastContext.Value == context)
            return;

        lastContext = context;

        if (animatorReference == null)
            return;

        switch (context)
        {
            case SceneContext.Flight:

                if (!string.IsNullOrEmpty(enterFlightTrigger))
                    animatorReference.SetTrigger(enterFlightTrigger);

                break;

            case SceneContext.Advanced:

                if (!string.IsNullOrEmpty(enterAdvancedTrigger))
                    animatorReference.SetTrigger(enterAdvancedTrigger);

                break;
        }
    }
}