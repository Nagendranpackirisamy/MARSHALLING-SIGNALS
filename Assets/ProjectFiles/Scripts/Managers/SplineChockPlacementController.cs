using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;
using static UnityEngine.Splines.SplineComponent;

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
    [SerializeField] private string takeState = "Take";

    [Header("Transitions & Blending")]
    [Tooltip("Crossfade duration between animation states.")]
    [SerializeField] private float crossFadeDuration = 0.2f;

    [Tooltip("If true, freezes the final frame of the Insert animation when complete.")]
    [SerializeField] private bool freezeOnInsertEnd = true;

    [Header("Editor Debug Keys")]
    [SerializeField] private bool enableDebugKeys = true;
    [SerializeField] private KeyCode triggerSequenceKey = KeyCode.Space;
    [SerializeField] private KeyCode triggerRemovalKey = KeyCode.T;
    [SerializeField] private KeyCode resetSequenceKey = KeyCode.R;

    [Header("Events (Placement - Forward)")]
    public UnityEvent onSplineWalkStarted;
    public UnityEvent onSplineWalkCompleted;
    public UnityEvent onInsertCompleted;

    [Header("Events (Removal - Reverse)")]
    public UnityEvent onRemovalStarted;
    public UnityEvent onSplineReverseWalkStarted;
    public UnityEvent onRemovalCompleted;

    private Coroutine activeSequence;

    private void Awake()
    {
        if (splineAnimate == null)
            splineAnimate = GetComponent<SplineAnimate>();

        if (characterAnimator == null)
            characterAnimator = GetComponent<Animator>();

        if (splineAnimate != null)
        {
            // Set initial forward axis to Object X-
            splineAnimate.ObjectForwardAxis = AlignAxis.NegativeXAxis;
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

        if (Input.GetKeyDown(triggerRemovalKey))
        {
            StartSplineRemovalSequence();
        }

        if (Input.GetKeyDown(resetSequenceKey))
        {
            ResetPlacement();
        }
    }

    // =========================================================
    // FORWARD PLACEMENT SEQUENCE (Start -> Wheel -> Insert)
    // =========================================================

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

        // Ensure forward axis is set to Object X-
        splineAnimate.ObjectForwardAxis = AlignAxis.NegativeXAxis;

        // 1. Reset and play spline path
        splineAnimate.Restart(true);
        splineAnimate.Play();

        // 2. Play walk animation (loops until destination)
        characterAnimator.speed = 1f;
        characterAnimator.CrossFadeInFixedTime(walkState, crossFadeDuration);

        // Wait while the character traverses the spline
        while (splineAnimate.IsPlaying && splineAnimate.NormalizedTime < 0.999f)
        {
            yield return null;
        }

        // Lock character at final point on spline
        splineAnimate.Pause();
        splineAnimate.NormalizedTime = 1f;

        onSplineWalkCompleted?.Invoke();

        // 3. Smoothly shift from Walk loop into Insert
        characterAnimator.CrossFadeInFixedTime(insertState, crossFadeDuration);

        yield return null;

        // 4. Wait for Insert animation to complete
        while (!characterAnimator.GetCurrentAnimatorStateInfo(0).IsName(insertState))
            yield return null;

        while (characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f &&
               characterAnimator.GetCurrentAnimatorStateInfo(0).IsName(insertState))
        {
            yield return null;
        }

        // 5. Freeze on the last frame of Insert
        if (freezeOnInsertEnd)
        {
            characterAnimator.Play(insertState, 0, 1.0f);
            characterAnimator.speed = 0f;
        }

        onInsertCompleted?.Invoke();
        activeSequence = null;
    }

    // =========================================================
    // REVERSE REMOVAL SEQUENCE (Take -> Flip X- to X+ -> Walk Back -> Idle)
    // =========================================================

    public void StartSplineRemovalSequence()
    {
        if (splineAnimate == null || characterAnimator == null)
        {
            Debug.LogError("SplineChockPlacementController: Missing SplineAnimate or Animator reference!", this);
            return;
        }

        if (activeSequence != null)
            StopCoroutine(activeSequence);

        activeSequence = StartCoroutine(RemovalSequenceRoutine());
    }

    private IEnumerator RemovalSequenceRoutine()
    {
        onRemovalStarted?.Invoke();

        // Ensure character starts facing the wheel with Object X-
        splineAnimate.ObjectForwardAxis = AlignAxis.NegativeXAxis;

        // 1. Unpause animator and play "Take" animation
        characterAnimator.speed = 1f;
        characterAnimator.CrossFadeInFixedTime(takeState, crossFadeDuration);

        yield return null;

        // 2. Wait for Take animation to complete
        while (!characterAnimator.GetCurrentAnimatorStateInfo(0).IsName(takeState))
            yield return null;

        while (characterAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f &&
               characterAnimator.GetCurrentAnimatorStateInfo(0).IsName(takeState))
        {
            yield return null;
        }

        // =====================================================================
        // 3. CHANGE THE FORWARD AXIS FROM Object X- TO Object X+
        // =====================================================================
        splineAnimate.ObjectForwardAxis = AlignAxis.XAxis;

        // Force an immediate rotation re-evaluation at the current spot
        splineAnimate.NormalizedTime = 1f;

        yield return null;

        onSplineReverseWalkStarted?.Invoke();

        // 4. Start Walk animation in loop
        characterAnimator.CrossFadeInFixedTime(walkState, crossFadeDuration);

        // 5. Drive NormalizedTime backward from 1.0 down to 0.0
        float travelDuration = Mathf.Max(splineAnimate.Duration, 0.1f);
        float currentNormalizedTime = 1f;

        while (currentNormalizedTime > 0.001f)
        {
            currentNormalizedTime -= (Time.deltaTime / travelDuration);
            splineAnimate.NormalizedTime = Mathf.Clamp01(currentNormalizedTime);
            yield return null;
        }

        // Lock at start point
        splineAnimate.Pause();
        splineAnimate.NormalizedTime = 0f;

        // 6. Return to Idle pose
        characterAnimator.CrossFadeInFixedTime(idleState, crossFadeDuration);

        onRemovalCompleted?.Invoke();
        activeSequence = null;
    }

    // =========================================================
    // RESET
    // =========================================================

    public void ResetPlacement()
    {
        if (activeSequence != null)
        {
            StopCoroutine(activeSequence);
            activeSequence = null;
        }

        if (splineAnimate != null)
        {
            splineAnimate.ObjectForwardAxis = AlignAxis.NegativeXAxis;
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