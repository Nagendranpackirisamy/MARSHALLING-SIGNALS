using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

public class VideoController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScenarioManager scenarioManager;

    [Header("Video UI")]
    [SerializeField] private GameObject videoPanel;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Button continueButton;

    [Header("Startup")]
    [SerializeField] private bool playStartupVideo = true;
    [SerializeField] private VideoClip startupVideo;

    private bool startupFinished;

    private Coroutine videoRoutine;

    private void Awake()
    {
        continueButton.onClick.AddListener(OnContinuePressed);
    }

    private void OnEnable()
    {
        scenarioManager.OnVideoRequired += HandleScenarioVideo;
    }

    private void OnDisable()
    {
        scenarioManager.OnVideoRequired -= HandleScenarioVideo;

        if (videoPlayer != null)
            videoPlayer.loopPointReached -= HandleVideoFinished;
    }

    private void Start()
    {
        videoPanel.SetActive(false);
        continueButton.gameObject.SetActive(false);

        if (playStartupVideo && startupVideo != null)
        {
            startupFinished = false;
            PlayVideo(startupVideo, true, true, false);
        }
        else
        {
            startupFinished = true;
            scenarioManager.ShowScenario(0);
        }
    }

    // --------------------------------------------------------------------

    private void HandleScenarioVideo(ScenarioData scenario, int index)
    {
        if (videoRoutine != null)
            StopCoroutine(videoRoutine);

        PlayVideo(
            scenario.videoClip,
            scenario.pauseOnLastFrame,
            scenario.showContinueButton,
            scenario.autoContinue);
    }

    // --------------------------------------------------------------------

    private void PlayVideo(
        VideoClip clip,
        bool pauseOnLastFrame,
        bool showButton,
        bool autoContinue)
    {
        if (clip == null)
        {
            scenarioManager.NotifyVideoComplete();
            return;
        }

        videoPanel.SetActive(true);

        continueButton.gameObject.SetActive(false);

        videoPlayer.Stop();

        videoPlayer.loopPointReached -= HandleVideoFinished;

        videoPlayer.clip = clip;

        this.pauseOnLastFrame = pauseOnLastFrame;
        this.showButton = showButton;
        this.autoContinue = autoContinue;

        videoPlayer.loopPointReached += HandleVideoFinished;

        videoPlayer.Play();
    }

    // --------------------------------------------------------------------

    private bool pauseOnLastFrame;
    private bool showButton;
    private bool autoContinue;

    private void HandleVideoFinished(VideoPlayer vp)
    {
        videoPlayer.loopPointReached -= HandleVideoFinished;

        if (pauseOnLastFrame)
            videoPlayer.Pause();
        else
            videoPlayer.Stop();

        if (autoContinue)
        {
            CloseVideo();

            return;
        }

        if (showButton)
        {
            continueButton.gameObject.SetActive(true);
        }
        else
        {
            CloseVideo();
        }
    }

    // --------------------------------------------------------------------

    private void OnContinuePressed()
    {
        CloseVideo();
    }

    // --------------------------------------------------------------------

    private void CloseVideo()
    {
        continueButton.gameObject.SetActive(false);

        videoPlayer.Stop();

        videoPanel.SetActive(false);

        if (!startupFinished)
        {
            startupFinished = true;
            scenarioManager.ShowScenario(0);
            return;
        }

        scenarioManager.NotifyVideoComplete();
    }
}