using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class DynamicFollowTargetController : MonoBehaviour
{
    public enum RotationFollowMode
    {
        None,                // Leaves rotation untouched completely
        MatchTargetRotation, // Matches target rotation + custom rotation offset
        LookAtTarget         // Aims forward toward the target
    }

    [System.Serializable]
    public class FollowTargetConfig
    {
        public string label = "Follow Target";

        [Tooltip("The target object to follow.")]
        public Transform target;

        [Tooltip("Offset relative to the target.")]
        public Vector3 offset = new Vector3(0f, 2f, -5f);

        [Tooltip("If true, offset rotates with the target's local orientation. If false, uses world space.")]
        public bool useLocalOffset = true;

        [Header("Rotation")]
        public RotationFollowMode rotationMode = RotationFollowMode.LookAtTarget;
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Interpolation Speed")]
        [Tooltip("Higher = snappier follow, lower = smoother lag.")]
        public float positionSmoothSpeed = 5f;
        public float rotationSmoothSpeed = 5f;

        [Tooltip("Snap directly to offset position on the very first frame of following instead of sliding from afar.")]
        public bool snapOnStart = false;
    }

    [Header("Follower Object")]
    [Tooltip("The object being moved (leave empty to use this GameObject/Camera).")]
    [SerializeField] private Transform followerTransform;

    [Header("Target Configurations (Element-wise)")]
    [SerializeField] private List<FollowTargetConfig> followElements = new();

    [Header("Events")]
    public UnityEvent<int> onFollowStarted;
    public UnityEvent onFollowStopped;

    // Completely inactive by default
    private bool isFollowing = false;
    private int activeIndex = -1;

    private void Awake()
    {
        if (followerTransform == null)
            followerTransform = transform;

        // Ensure follow state is strictly off at boot
        isFollowing = false;
        activeIndex = -1;
    }

    private void LateUpdate()
    {
        // STRICT GATE: Do not modify position or rotation unless actively instructed
        if (!isFollowing || activeIndex < 0 || activeIndex >= followElements.Count)
            return;

        FollowTargetConfig config = followElements[activeIndex];
        if (config.target == null || followerTransform == null)
            return;

        // 1. Position update
        Vector3 desiredPosition = config.useLocalOffset
            ? config.target.TransformPoint(config.offset)
            : config.target.position + config.offset;

        followerTransform.position = Vector3.Lerp(
            followerTransform.position,
            desiredPosition,
            Time.deltaTime * config.positionSmoothSpeed
        );

        // 2. Rotation update
        switch (config.rotationMode)
        {
            case RotationFollowMode.LookAtTarget:
                Vector3 lookDirection = config.target.position - followerTransform.position;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDirection);
                    followerTransform.rotation = Quaternion.Slerp(
                        followerTransform.rotation,
                        targetRot * Quaternion.Euler(config.rotationOffset),
                        Time.deltaTime * config.rotationSmoothSpeed
                    );
                }
                break;

            case RotationFollowMode.MatchTargetRotation:
                Quaternion matchedRot = config.target.rotation * Quaternion.Euler(config.rotationOffset);
                followerTransform.rotation = Quaternion.Slerp(
                    followerTransform.rotation,
                    matchedRot,
                    Time.deltaTime * config.rotationSmoothSpeed
                );
                break;

            case RotationFollowMode.None:
            default:
                // Does not alter follower rotation in any way
                break;
        }
    }

    // =========================================================
    // PUBLIC CONTROL FUNCTIONS
    // =========================================================

    /// <summary>
    /// Activates the follow logic for the specified element index.
    /// Only after this call will position/rotation be modified.
    /// </summary>
    public void FollowElement(int index)
    {
        if (index < 0 || index >= followElements.Count)
        {
            Debug.LogWarning($"[DynamicFollowTargetController] Element index {index} is out of range!", this);
            return;
        }

        if (followElements[index].target == null)
        {
            Debug.LogWarning($"[DynamicFollowTargetController] Target at index {index} is not assigned!", this);
            return;
        }

        activeIndex = index;
        isFollowing = true;

        // If configured, immediately jump to offset point without lerping across the map
        if (followElements[index].snapOnStart && followerTransform != null)
        {
            FollowTargetConfig config = followElements[index];
            followerTransform.position = config.useLocalOffset
                ? config.target.TransformPoint(config.offset)
                : config.target.position + config.offset;

            if (config.rotationMode == RotationFollowMode.LookAtTarget)
            {
                Vector3 dir = config.target.position - followerTransform.position;
                if (dir.sqrMagnitude > 0.001f)
                    followerTransform.rotation = Quaternion.LookRotation(dir);
            }
            else if (config.rotationMode == RotationFollowMode.MatchTargetRotation)
            {
                followerTransform.rotation = config.target.rotation * Quaternion.Euler(config.rotationOffset);
            }
        }

        onFollowStarted?.Invoke(index);
    }

    /// <summary>
    /// Instantly disengages the follow loop. The Transform stops being modified.
    /// </summary>
    public void StopFollowing()
    {
        if (!isFollowing) return;

        isFollowing = false;
        activeIndex = -1;
        onFollowStopped?.Invoke();
    }
}