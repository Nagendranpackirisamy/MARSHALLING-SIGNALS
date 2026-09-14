using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

public class SplineChockPlacementController : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("The SplineAnimate component moving this character along the path.")]
    [SerializeField] private SplineAnimate splineAnimate;

    [Tooltip("The character's Animator component.")]
    [SerializeField] private Animator characterAnimator;

    [Header("Animation States")]
    [SerializeField] private string idleState = "Idel_Chocks";
    [SerializeField] private string walkState = "Walk";
    [SerializeField] private string insertState = "Insert";

    [Header("Transitions & Blending")]
    [Tooltip("Crossfade duration between Walk and Insert.")]
    [SerializeField] private float crossFadeDuration = 0.2f;

    [Tooltip("If true, freezes the final frame of the Insert animation when complete.")]
    [SerializeField] private bool freezeOnInsertEnd = true;

    [Header("Editor Debug Keys")]
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private KeyCode triggerSequenceKey = KeyCode.Space;
    [SerializeField] private KeyCode resetSequenceKey = KeyCode.R;

    [Header("Events")]
    public UnityEvent onSplineWalkStarted;
    public UnityEvent onSplineWalkCompleted;
    public UnityEvent onInsertCompleted;

    private Coroutine activeSequence;

    private void Awake()
    {
        if (splineAnimate == null)
            splineAnimate = GetComponent<SplineAnimate>();

        if (characterAnimator == null)
            characterAnimator = GetComponent<Animator>();

        if (splineAnimate != null)
        {
            splineAnimate.Pause();
            splineAnimate.NormalizedTime = 0f;
        }

        if (characterAnimator != null && !string.IsNullOrEmpty(idleState))
        {
            characterAnimator.Play(idleState, 0, 0f);
        }
    }

    private void Update()
    {
        if (!enableDebugKeys) return;

        if (Input.GetKeyDown(triggerSequenceKey))
        {
            StartSplinePlacementSequence();
        }

        if (Input.GetKeyDown(resetSequenceKey))
        {
            ResetPlacement();
        }
    }

    /// <summary>
    /// Starts the looped walk along the spline, blending to insert at the final point.
    /// </summary>
    public void StartSplinePlacementSequence()
    {
        if (splineAnimate == null || characterAnimator == null)
        {
            Debug.LogError("SplineChockPlacementController: Missing SplineAnimate or Animator reference!", this);
            return;
        }

        if (activeSequence != null)
            StopCoroutine(activeSequence);

        activeSequence = StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        onSplineWalkStarted?.Invoke();

        // 1. Reset and play spline path
        splineAnimate.Restart(true);
        splineAnimate.Play();

        // 2. Play walk animation (loops until destination)
        characterAnimator.speed = 1f;
        characterAnimator.CrossFadeInFixedTime(walkState, crossFadeDuration);

        // 3. Keep walking in loop until the spline arrives at the end point
        while (splineAnimate.IsPlaying && splineAnimate.NormalizedTime < 0.999f)
        {
            yield return null;
        }

        // Lock character at final point on spline
        splineAnimate.Pause();
        splineAnimate.NormalizedTime = 1f;

        onSplineWalkCompleted?.Invoke();

        // 4. Smoothly shift from Walk loop into Insert
        characterAnimator.CrossFadeInFixedTime(insertState, crossFadeDuration);

        // Wait 1 frame so animator registers state change
        yield return null;

        // 5. Wait for Insert animation to complete
        while (!characterAnimator.GetCurrentAnimatorStateInfo(0).IsName(insertState))
            yield return null;

        while (characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f &&
               characterAnimator.GetCurrentAnimatorStateInfo(0).IsName(insertState))
        {
            yield return null;
        }

        // 6. Freeze on the last frame of Insert
        if (freezeOnInsertEnd)
        {
            characterAnimator.Play(insertState, 0, 1.0f);
            characterAnimator.speed = 0f;
        }

        onInsertCompleted?.Invoke();
        activeSequence = null;
    }

    public void ResetPlacement()
    {
        if (activeSequence != null)
        {
            StopCoroutine(activeSequence);
            activeSequence = null;
        }

        if (splineAnimate != null)
        {
            splineAnimate.Pause();
            splineAnimate.NormalizedTime = 0f;
        }

        if (characterAnimator != null)
        {
            characterAnimator.speed = 1f;
            characterAnimator.CrossFadeInFixedTime(idleState, 0.1f);
        }
    }
}