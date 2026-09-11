using UnityEngine;
using UnityEngine.Video;

public enum LightColor { Red, Green, White }
public enum LightPattern { Steady, Flashing }
public enum SceneContext { Ground, Flight, Advanced }
public enum SignalVisualType { None, LightGun, AlternatingRedGreen, Flare }
public enum NonInteractivePageType { None, InfoPanel, SceneIntro }

[CreateAssetMenu(fileName = "Scenario_", menuName = "ATC/Scenario Data")]
public class ScenarioData : ScriptableObject
{
    [Header("Identification")]
    public string scenarioId;
    public SceneContext context;

    [Header("Page Type")]
    public bool requiresAnswer = true;
    public NonInteractivePageType pageType = NonInteractivePageType.InfoPanel;

    [Header("Video")]
    public bool playVideo;
    public VideoClip videoClip;
    public bool pauseOnLastFrame = true;
    public bool showContinueButton = true;
    public bool autoContinue = false;
    public bool allowSkip = false;

    [Header("Signal")]
    public SignalVisualType signalVisualType = SignalVisualType.LightGun;
    public LightColor lightColor;
    public LightPattern lightPattern;

    [Header("Question")]
    [TextArea(2, 5)] public string questionText;
    [TextArea(2, 5)] public string optionA;
    [TextArea(2, 5)] public string optionB;
    public int correctOptionIndex;

    [Header("Instructor Lines")]
    [TextArea(2, 5)] public string instructorIntroLine;
    [TextArea(2, 5)] public string instructorCorrectLine;
    [TextArea(2, 5)] public string instructorWrongLine;

    [Header("Audio")]
    public AudioClip introVO;
    public AudioClip correctVO;
    public AudioClip wrongVO;

    [Header("Animations")]
    public string correctAnimTrigger;
    public AnimationClip correctAnimClip;
    public string wrongAnimTrigger;
    public AnimationClip wrongAnimClip;

    [Header("Aircraft Anchor")]
    public Transform aircraftAnchor;
}