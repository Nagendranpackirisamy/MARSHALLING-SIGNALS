using UnityEngine;

public class DualTurbineFanRotator : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Engine Fan Transforms")]
    [Tooltip("Left engine fan blade transform (e.g., Low_pressure_shaft_00).")]
    [SerializeField] private Transform fanBladeOne;

    [Tooltip("Right engine fan blade transform.")]
    [SerializeField] private Transform fanBladeTwo;

    [Header("Rotation Settings")]
    [Tooltip("Rotation axis relative to the fan blades.")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;

    [Tooltip("Target rotation speed in degrees per second at full throttle.")]
    [SerializeField] private float rotationSpeed = 1200f;

    [Tooltip("Invert direction if the fan spins backwards.")]
    [SerializeField] private bool reverseDirection = false;

    [Header("Auto Start")]
    [Tooltip("If checked, the fans begin spinning on Play. If unchecked, wait until StartRotation() is called.")]
    [SerializeField] private bool rotateOnStart = true;

    [Header("Gradual Spool Up & Spool Down (Turbine Inertia)")]
    [Tooltip("Time in seconds to gradually reach max speed from a stop.")]
    [SerializeField] private float spoolUpTime = 5f;

    [Tooltip("Time in seconds to gradually coast down to a full stop from max speed.")]
    [SerializeField] private float spoolDownTime = 8f;

    private bool isSpinning = false;
    private float currentSpeedMultiplier = 0f;

    private void Awake()
    {
        isSpinning = rotateOnStart;
        currentSpeedMultiplier = rotateOnStart ? 1f : 0f;
    }

    private void Update()
    {
        float targetMultiplier = isSpinning ? 1f : 0f;

        // Choose acceleration or deceleration rate based on whether spooling up or slowing down
        float transitionRate = isSpinning
            ? (1f / Mathf.Max(spoolUpTime, 0.01f))
            : (1f / Mathf.Max(spoolDownTime, 0.01f));

        currentSpeedMultiplier = Mathf.MoveTowards(
            currentSpeedMultiplier,
            targetMultiplier,
            Time.deltaTime * transitionRate
        );

        // Apply rotation whenever blades have momentum
        if (currentSpeedMultiplier > 0.0001f)
        {
            float direction = reverseDirection ? -1f : 1f;
            float step = rotationSpeed * currentSpeedMultiplier * direction * Time.deltaTime;

            Vector3 axisVector = rotationAxis switch
            {
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                _ => Vector3.forward
            };

            if (fanBladeOne != null)
                fanBladeOne.Rotate(axisVector * step, Space.Self);

            if (fanBladeTwo != null)
                fanBladeTwo.Rotate(axisVector * step, Space.Self);
        }
    }

    // =========================================================
    // PUBLIC CONTROL FUNCTIONS
    // =========================================================

    /// <summary>
    /// Gradually spools the fans up to full speed over spoolUpTime.
    /// </summary>
    public void StartRotation()
    {
        isSpinning = true;
    }

    /// <summary>
    /// Gradually coasts the fans down to a complete stop over spoolDownTime.
    /// </summary>
    public void StopRotation()
    {
        isSpinning = false;
    }

    /// <summary>
    /// Toggles between spooling up and spooling down.
    /// </summary>
    public void ToggleRotation()
    {
        isSpinning = !isSpinning;
    }

    /// <summary>
    /// Instantly halts rotation with zero coasting time.
    /// </summary>
    public void StopInstant()
    {
        isSpinning = false;
        currentSpeedMultiplier = 0f;
    }

    /// <summary>
    /// Updates the top rotation speed dynamically.
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        rotationSpeed = newSpeed;
    }
}