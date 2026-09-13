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

    [Tooltip("Rotation speed in degrees per second.")]
    [SerializeField] private float rotationSpeed = 1200f;

    [Tooltip("Invert direction if the fan spins backwards.")]
    [SerializeField] private bool reverseDirection = false;

    [Header("Auto Start")]
    [Tooltip("If checked, the fans spin immediately on Play. If unchecked, wait until StartRotation() is called.")]
    [SerializeField] private bool rotateOnStart = true;

    [Header("Smooth Acceleration / Deceleration")]
    [SerializeField] private bool useSmoothTransition = true;
    [SerializeField] private float accelerationRate = 4f;

    private bool isSpinning = false;
    private float currentSpeedMultiplier = 0f;

    private void Awake()
    {
        isSpinning = rotateOnStart;
        currentSpeedMultiplier = rotateOnStart ? 1f : 0f;
    }

    private void Update()
    {
        // Smooth speed interpolation
        float targetMultiplier = isSpinning ? 1f : 0f;
        if (useSmoothTransition)
        {
            currentSpeedMultiplier = Mathf.MoveTowards(
                currentSpeedMultiplier,
                targetMultiplier,
                Time.deltaTime * accelerationRate
            );
        }
        else
        {
            currentSpeedMultiplier = targetMultiplier;
        }

        // Only rotate when speed is above zero
        if (currentSpeedMultiplier > 0.001f)
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
    /// Starts fan rotation (can be called repeatedly).
    /// </summary>
    public void StartRotation()
    {
        isSpinning = true;
    }

    /// <summary>
    /// Stops fan rotation (can be called repeatedly).
    /// </summary>
    public void StopRotation()
    {
        isSpinning = false;
    }

    /// <summary>
    /// Toggles rotation between spinning and stopped.
    /// </summary>
    public void ToggleRotation()
    {
        isSpinning = !isSpinning;
    }

    /// <summary>
    /// Instantly halts rotation without decelerating.
    /// </summary>
    public void StopInstant()
    {
        isSpinning = false;
        currentSpeedMultiplier = 0f;
    }

    /// <summary>
    /// Updates the rotation speed dynamically.
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        rotationSpeed = newSpeed;
    }
}