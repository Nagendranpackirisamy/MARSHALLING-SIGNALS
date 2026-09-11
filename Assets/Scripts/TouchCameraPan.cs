using UnityEngine;
using UnityEngine.InputSystem;

public class TouchCameraPan : MonoBehaviour
{
    [Header("Touch Settings")]
    [SerializeField] private float sensitivity = 0.1f;

    [Header("Head Movement Limits")]
    [SerializeField] private float leftLimit = 80f;
    [SerializeField] private float rightLimit = 80f;
    [SerializeField] private float upLimit = 50f;
    [SerializeField] private float downLimit = 60f;

    private float yaw;
    private float pitch;

    private float startYaw;
    private float startPitch;

    private Vector2 lastPosition;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;

        yaw = angles.y;

        pitch = angles.x;
        if (pitch > 180f)
            pitch -= 360f;

        startYaw = yaw;
        startPitch = pitch;
    }

    private void Update()
    {
        if (Touchscreen.current == null)
            return;

        var touch = Touchscreen.current.primaryTouch;

        if (!touch.press.isPressed)
            return;

        Vector2 currentPosition = touch.position.ReadValue();

        if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
        {
            lastPosition = currentPosition;
            return;
        }

        Vector2 delta = currentPosition - lastPosition;

        // Drag direction feels natural for cockpit viewing
        yaw -= delta.x * sensitivity;
        pitch += delta.y * sensitivity;

        // Clamp relative to initial forward view
        yaw = Mathf.Clamp(
            yaw,
            startYaw - leftLimit,
            startYaw + rightLimit);

        pitch = Mathf.Clamp(
            pitch,
            startPitch - downLimit,
            startPitch + upLimit);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        lastPosition = currentPosition;
    }
}