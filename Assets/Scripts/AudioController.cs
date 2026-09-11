using UnityEngine;
using System.Collections;

public class AudioController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScenarioManager scenarioManager;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource introAudioSource;
    [SerializeField] private AudioSource voAudioSource;
    [SerializeField] private AudioSource feedbackAudioSource;

    [Header("Feedback")]
    [SerializeField] private AudioClip correctChime;
    [SerializeField] private AudioClip wrongChime;

    private Coroutine introRoutine;

    private void OnEnable()
    {
        scenarioManager.OnScenarioLoaded += HandleScenarioLoaded;
        scenarioManager.OnCorrectAnswer += HandleCorrectAnswer;
        scenarioManager.OnWrongAnswer += HandleWrongAnswer;
    }

    private void OnDisable()
    {
        scenarioManager.OnScenarioLoaded -= HandleScenarioLoaded;
        scenarioManager.OnCorrectAnswer -= HandleCorrectAnswer;
        scenarioManager.OnWrongAnswer -= HandleWrongAnswer;
    }

    // ------------------------------------------------------------------------

    private void HandleScenarioLoaded(ScenarioData scenario, int index)
    {
        StopAllAudio();

        if (introRoutine != null)
        {
            StopCoroutine(introRoutine);
            introRoutine = null;
        }

        if (scenario.requiresAnswer)
            return;

        if (scenarioManager.AudioPlayed(index))
        {
            StartCoroutine(NotifyNextFrame());
            return;
        }

        introRoutine = StartCoroutine(PlayIntroRoutine(scenario, index));
    }

    // ------------------------------------------------------------------------

    private IEnumerator NotifyNextFrame()
    {
        yield return null;

        scenarioManager.NotifyIntroVOComplete();
    }

    // ------------------------------------------------------------------------

    private IEnumerator PlayIntroRoutine(
        ScenarioData scenario,
        int index)
    {
        if (introAudioSource != null &&
            scenario.introVO != null)
        {
            introAudioSource.clip = scenario.introVO;
            introAudioSource.Play();

            yield return new WaitForSeconds(
                scenario.introVO.length);
        }
        else
        {
            yield return new WaitForSeconds(2f);
        }

        scenarioManager.MarkAudioPlayed(index);

        introRoutine = null;

        scenarioManager.NotifyIntroVOComplete();
    }

    private IEnumerator WaitForFeedbackVO()
    {
        while (voAudioSource != null && voAudioSource.isPlaying)
            yield return null;

        scenarioManager.NotifyFeedbackVOComplete();
    }
    // ------------------------------------------------------------------------

    private void HandleCorrectAnswer(ScenarioData scenario, int selectedIndex)
    {
        StopVoiceOver();

        if (feedbackAudioSource != null && correctChime != null)
            feedbackAudioSource.PlayOneShot(correctChime);

        if (voAudioSource != null && scenario.correctVO != null)
        {
            voAudioSource.clip = scenario.correctVO;
            voAudioSource.Play();

            StartCoroutine(WaitForFeedbackVO());
        }
        else
        {
            scenarioManager.NotifyFeedbackVOComplete();
        }
    }

    // ------------------------------------------------------------------------

    private void HandleWrongAnswer(ScenarioData scenario, int selectedIndex)
    {
        StopVoiceOver();

        if (feedbackAudioSource != null && wrongChime != null)
            feedbackAudioSource.PlayOneShot(wrongChime);

        if (voAudioSource != null && scenario.wrongVO != null)
        {
            voAudioSource.clip = scenario.wrongVO;
            voAudioSource.Play();

            StartCoroutine(WaitForFeedbackVO());
        }
        else
        {
            scenarioManager.NotifyFeedbackVOComplete();
        }
    }

 

    // ------------------------------------------------------------------------

    private void StopVoiceOver()
    {
        if (voAudioSource == null)
            return;

        if (voAudioSource.isPlaying)
            voAudioSource.Stop();
    }

    // ------------------------------------------------------------------------

    private void StopAllAudio()
    {
        StopVoiceOver();

        if (introAudioSource != null &&
            introAudioSource.isPlaying)
        {
            introAudioSource.Stop();
        }

        if (feedbackAudioSource != null)
            feedbackAudioSource.Stop();
    }
}