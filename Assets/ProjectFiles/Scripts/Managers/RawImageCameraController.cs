using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class RawImageCameraController : MonoBehaviour, IDragHandler, IScrollHandler
{
    [Header("Target Camera")]
    [Tooltip("The camera that renders to this RawImage's RenderTexture (e.g., 2nd Cam).")]
    [SerializeField] private Camera targetCamera;

    [Header("Rotation Limits (Degrees)")]
    [SerializeField] private float minPitchX = -30f;
    [SerializeField] private float maxPitchX = 30f;
    [SerializeField] private float minYawY = -60f;
    [SerializeField] private float maxYawY = 60f;
    [SerializeField] private float rotationSpeed = 0.2f;

    [Header("Invert Controls")]
    [Tooltip("Inverts the vertical (pitch) look direction.")]
    [SerializeField] private bool invertPitch = false;
    [Tooltip("Inverts the horizontal (yaw) look direction.")]
    [SerializeField] private bool invertYaw = false;

    [Header("Zoom Limits (Camera FOV)")]
    [Tooltip("Field Of View minimum (closest zoom).")]
    [SerializeField] private float minFOV = 15f;
    [Tooltip("Field Of View maximum (widest view).")]
    [SerializeField] private float maxFOV = 60f;
    [SerializeField] private float zoomSpeedScroll = 5f;
    [SerializeField] private float zoomSpeedTouch = 0.05f;

    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothSpeed = 15f;

    private float currentPitch;
    private float currentYaw;
    private float targetPitch;
    private float targetYaw;

    private float currentFOV;
    private float targetFOV;

    private Quaternion baseLocalRotation;

    private void Awake()
    {
        if (targetCamera == null)
        {
            Debug.LogError("RawImageCameraController: Please assign the Target Camera (2nd Cam) in the inspector.");
            return;
        }

        // Cache base orientation
        baseLocalRotation = targetCamera.transform.localRotation;
        Vector3 initialAngles = baseLocalRotation.eulerAngles;

        // Convert angles from 0..360 to -180..180
        currentPitch = NormalizeAngle(initialAngles.x);
        currentYaw = NormalizeAngle(initialAngles.y);

        targetPitch = currentPitch;
        targetYaw = currentYaw;

        currentFOV = targetCamera.fieldOfView;
        targetFOV = currentFOV;
    }

    private void Update()
    {
        if (targetCamera == null) return;

        // Touch pinch-to-zoom for mobile/tablets
        HandleTouchPinchZoom();

        // Apply smooth interpolation
        if (enableSmoothing)
        {
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.unscaledDeltaTime * smoothSpeed);
            currentYaw = Mathf.Lerp(currentYaw, targetYaw, Time.unscaledDeltaTime * smoothSpeed);
            currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.unscaledDeltaTime * smoothSpeed);
        }
        else
        {
            currentPitch = targetPitch;
            currentYaw = targetYaw;
            currentFOV = targetFOV;
        }

        // Apply rotation clamped to origin
        targetCamera.transform.localRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        targetCamera.fieldOfView = currentFOV;
    }

    /// <summary>
    /// Dragging inside the RawImage rotates the target camera within clamp limits.
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        // Ignore single-finger drag if a two-finger pinch gesture is happening
        if (Input.touchCount >= 2) return;

        // Determine axis direction based on invert flags
        float yawMultiplier = invertYaw ? -1f : 1f;
        float pitchMultiplier = invertPitch ? 1f : -1f;

        targetYaw += eventData.delta.x * rotationSpeed * yawMultiplier;
        targetPitch += eventData.delta.y * rotationSpeed * pitchMultiplier;

        targetPitch = Mathf.Clamp(targetPitch, minPitchX, maxPitchX);
        targetYaw = Mathf.Clamp(targetYaw, minYawY, maxYawY);
    }

    /// <summary>
    /// Scroll wheel zooming when cursor is over the RawImage.
    /// </summary>
    public void OnScroll(PointerEventData eventData)
    {
        targetFOV -= eventData.scrollDelta.y * zoomSpeedScroll;
        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
    }

    private void HandleTouchPinchZoom()
    {
        if (Input.touchCount != 2) return;

        Touch touchZero = Input.GetTouch(0);
        Touch touchOne = Input.GetTouch(1);

        // Previous touch positions
        Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
        Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

        float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
        float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

        // Difference in distance between touches
        float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

        targetFOV += deltaMagnitudeDiff * zoomSpeedTouch;
        targetFOV = Mathf.Clamp(targetFOV, minFOV, maxFOV);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle -= 360f;
        return angle;
    }

    /// <summary>
    /// Resets rotation and zoom to their default limits.
    /// </summary>
    public void ResetCameraView()
    {
        Vector3 initialAngles = baseLocalRotation.eulerAngles;
        targetPitch = NormalizeAngle(initialAngles.x);
        targetYaw = NormalizeAngle(initialAngles.y);
        targetFOV = (minFOV + maxFOV) * 0.5f;
    }
}