using UnityEngine;
using System.Collections;

public class AircraftController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ScenarioManager scenarioManager;

    [Header("Aircraft Root")]
    [SerializeField] private Transform aircraftRoot;

    [Header("Movement")]
    [SerializeField] private float moveDuration = 1f;

    private Coroutine moveRoutine;

    private void OnEnable()
    {
        scenarioManager.OnWrongAnswer += HandleWrongAnswer;
        scenarioManager.OnScenarioLoaded += HandleScenarioLoaded;
    }

    private void OnDisable()
    {
        scenarioManager.OnWrongAnswer -= HandleWrongAnswer;
        scenarioManager.OnScenarioLoaded -= HandleScenarioLoaded;
    }

    // ------------------------------------------------------------------------

    private void HandleScenarioLoaded(ScenarioData scenario, int index)
    {
        // Cancel any movement from a previous scenario
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }
    }

    // ------------------------------------------------------------------------

    private void HandleWrongAnswer(ScenarioData scenario, int selectedIndex)
    {
        if (aircraftRoot == null)
            return;

        if (scenario.aircraftAnchor == null)
            return;

        // Prevent stacked movement coroutines
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(
            MoveAircraft(
                scenario.aircraftAnchor.position,
                scenario.aircraftAnchor.rotation));
    }

    // ------------------------------------------------------------------------

    private IEnumerator MoveAircraft(
        Vector3 targetPosition,
        Quaternion targetRotation)
    {
        Vector3 startPosition = aircraftRoot.position;
        Quaternion startRotation = aircraftRoot.rotation;

        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / moveDuration);

            t = Mathf.SmoothStep(0f, 1f, t);

            aircraftRoot.position =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    t);

            aircraftRoot.rotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t);

            yield return null;
        }

        aircraftRoot.position = targetPosition;
        aircraftRoot.rotation = targetRotation;

        moveRoutine = null;
    }
}