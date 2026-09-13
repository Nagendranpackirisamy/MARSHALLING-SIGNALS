using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DualAnimatorSequenceController : MonoBehaviour
{
    [System.Serializable]
    public class AnimationStep
    {
        [Tooltip("Exact state name in the Animator Controller (e.g., '1_Staff_Guide', 'Idel').")]
        public string stateName;

        [Tooltip("Cross-fade transition duration (0 for instant cut).")]
        public float crossFadeDuration = 0.1f;

        [Tooltip("Delay in seconds BEFORE playing this step.")]
        public float delayBeforePlay = 0f;

        [Tooltip("If true, waits until this clip finishes before executing the next step.")]
        public bool waitUntilFinished = true;

        [Tooltip("Extra delay after this clip finishes.")]
        public float delayAfterFinished = 0f;

        [Tooltip("If true and this is the final step, freezes/holds on the last frame.")]
        public bool holdLastFrame = true;
    }

    [System.Serializable]
    public class SequenceElement
    {
        public string label = "Sequence Step";
        public AnimationStep[] animationSteps;
        public bool returnToIdleOnFinish = false;
        public UnityEvent onSequenceStarted;
        public UnityEvent onSequenceCompleted;
    }

    [Header("Animator 1 (Aircraft / Cockpit)")]
    [SerializeField] private Animator animatorOne;
    [SerializeField] private string animatorOneDefaultIdle = "Idel";
    [SerializeField] private List<SequenceElement> sequencesAnimatorOne = new();

    [Header("Animator 2 (Marshaller / Staff)")]
    [SerializeField] private Animator animatorTwo;
    [SerializeField] private string animatorTwoDefaultIdle = "Idel";
    [SerializeField] private List<SequenceElement> sequencesAnimatorTwo = new();

    private Coroutine routineAnimOne;
    private Coroutine routineAnimTwo;

    // Helper: checks if the animator is actually active and turned on
    private bool IsAnimatorActive(Animator anim)
    {
        return anim != null && anim.gameObject.activeInHierarchy && anim.enabled;
    }

    // =========================================================
    // ANIMATOR 1 FUNCTIONS
    // =========================================================

    public void TriggerAnimatorOne(int elementIndex)
    {
        // Don't touch if null, inactive, or disabled
        if (!IsAnimatorActive(animatorOne)) return;

        if (elementIndex < 0 || elementIndex >= sequencesAnimatorOne.Count) return;

        if (routineAnimOne != null) StopCoroutine(routineAnimOne);
        routineAnimOne = StartCoroutine(PlaySequenceRoutine(animatorOne, sequencesAnimatorOne[elementIndex], animatorOneDefaultIdle, () => routineAnimOne = null));
    }

    public void StopAnimatorOne()
    {
        if (routineAnimOne != null)
        {
            StopCoroutine(routineAnimOne);
            routineAnimOne = null;
        }
    }

    // =========================================================
    // ANIMATOR 2 FUNCTIONS
    // =========================================================

    public void TriggerAnimatorTwo(int elementIndex)
    {
        // Don't touch if null, inactive, or disabled
        if (!IsAnimatorActive(animatorTwo)) return;

        if (elementIndex < 0 || elementIndex >= sequencesAnimatorTwo.Count) return;

        if (routineAnimTwo != null) StopCoroutine(routineAnimTwo);
        routineAnimTwo = StartCoroutine(PlaySequenceRoutine(animatorTwo, sequencesAnimatorTwo[elementIndex], animatorTwoDefaultIdle, () => routineAnimTwo = null));
    }

    public void StopAnimatorTwo()
    {
        if (routineAnimTwo != null)
        {
            StopCoroutine(routineAnimTwo);
            routineAnimTwo = null;
        }
    }

    // =========================================================
    // COMBINED FUNCTIONS
    // =========================================================

    public void TriggerBoth(int elementIndex)
    {
        TriggerAnimatorOne(elementIndex);
        TriggerAnimatorTwo(elementIndex);
    }

    public void StopBoth()
    {
        StopAnimatorOne();
        StopAnimatorTwo();
    }

    // =========================================================
    // SEQUENCE PLAYER COROUTINE
    // =========================================================

    private IEnumerator PlaySequenceRoutine(Animator anim, SequenceElement element, string defaultIdleState, Action onCompleteCallback)
    {
        element.onSequenceStarted?.Invoke();

        if (element.animationSteps != null && element.animationSteps.Length > 0)
        {
            for (int i = 0; i < element.animationSteps.Length; i++)
            {
                // Safety check: stop coroutine if someone disables the animator mid-sequence
                if (!IsAnimatorActive(anim))
                {
                    onCompleteCallback?.Invoke();
                    yield break;
                }

                AnimationStep step = element.animationSteps[i];
                bool isLastStep = (i == element.animationSteps.Length - 1);

                if (string.IsNullOrEmpty(step.stateName)) continue;

                if (step.delayBeforePlay > 0f)
                    yield return new WaitForSeconds(step.delayBeforePlay);

                anim.speed = 1f;

                if (step.crossFadeDuration > 0f)
                    anim.CrossFadeInFixedTime(step.stateName, step.crossFadeDuration);
                else
                    anim.Play(step.stateName, 0, 0f);

                yield return null;

                if (step.waitUntilFinished)
                {
                    while (IsAnimatorActive(anim) && !anim.GetCurrentAnimatorStateInfo(0).IsName(step.stateName))
                        yield return null;

                    while (IsAnimatorActive(anim) &&
                           anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f &&
                           anim.GetCurrentAnimatorStateInfo(0).IsName(step.stateName))
                        yield return null;
                }

                if (isLastStep && step.holdLastFrame && IsAnimatorActive(anim))
                {
                    anim.Play(step.stateName, 0, 1.0f);
                    anim.speed = 0f;
                }

                if (step.delayAfterFinished > 0f)
                    yield return new WaitForSeconds(step.delayAfterFinished);
            }
        }

        if (element.returnToIdleOnFinish && IsAnimatorActive(anim) && anim.speed > 0f && !string.IsNullOrEmpty(defaultIdleState))
        {
            anim.speed = 1f;
            anim.CrossFadeInFixedTime(defaultIdleState, 0.1f);
        }

        element.onSequenceCompleted?.Invoke();
        onCompleteCallback?.Invoke();
    }
}