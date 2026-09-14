using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class GlobalCameraController : MonoBehaviour
{
    [System.Serializable]
    public class PageCameraConfig
    {
        [Tooltip("Manual page index matching PageNavigationController.")]
        public int pageIndex;

        [Tooltip("Primary target point for this page.")]
        public Transform primaryTarget;

        [Tooltip("Optional secondary target used for subsequent visits.")]
        public Transform secondaryTarget;

        [Tooltip("Custom move duration for this specific page. Set to <= 0 to use the default duration.")]
        public float customMoveDuration = 0f;
    }

    [Header("Camera Target")]
    [Tooltip("Target camera to animate. Defaults to Camera.main if not assigned.")]
    [SerializeField] private Camera targetCamera;

    [Header("Page Configurations (Manual Index & Custom Duration)")]
    [SerializeField] private List<PageCameraConfig> pageConfigs = new();

    [Header("Default Movement Settings")]
    [Tooltip("Default transition time used when a page's custom duration is <= 0.")]
    [SerializeField] private float defaultMoveDuration = 1f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [Tooltip("Ensures camera animation runs smoothly regardless of Time.timeScale.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Events")]
    public UnityEvent OnMoveStart;
    public UnityEvent OnMoveEnd;

    private Coroutine routine;
    private int currentPageIndex = -1;

    // Tracks how many times each page was visited
    private readonly Dictionary<int, int> pageVisitCount = new();

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        PageNavigationController.OnPageChanged += MoveToPage;
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void OnDisable()
    {
        PageNavigationController.OnPageChanged -= MoveToPage;
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void Start()
    {
        // Automatically start the transition for Page 0 if configured
        if (pageConfigs.Exists(c => c.pageIndex == 0))
        {
            MoveToPage(0);
        }
    }

    // ================= PAGE MOVEMENT =================

    /// <summary>
    /// Called automatically whenever PageNavigationController changes page.
    /// </summary>
    public void MoveToPage(int pageIndex)
    {
        currentPageIndex = pageIndex;

        PageCameraConfig config = pageConfigs.Find(c => c.pageIndex == pageIndex);
        if (config == null)
        {
            Debug.LogWarning($"[GlobalCameraController] No configuration found for Page Index: {pageIndex}");
            return;
        }

        // Track page visits
        if (!pageVisitCount.ContainsKey(pageIndex))
            pageVisitCount[pageIndex] = 0;

        pageVisitCount[pageIndex]++;

        Transform target = GetTargetFromConfig(config);
        if (target == null)
        {
            Debug.LogWarning($"[GlobalCameraController] Target point is missing for Page Index: {pageIndex}");
            return;
        }

        // Prevent camera from trying to move to its own transform
        if (targetCamera != null && target == targetCamera.transform)
        {
            Debug.LogWarning($"[GlobalCameraController] Primary Target on Page {pageIndex} is set to the Camera itself. Assign a waypoint marker instead.");
            return;
        }

        float duration = config.customMoveDuration > 0f ? config.customMoveDuration : defaultMoveDuration;
        StartMove(target, duration);
    }

    private Transform GetTargetFromConfig(PageCameraConfig config)
    {
        int visits = pageVisitCount[config.pageIndex];

        // Return secondary target on subsequent visits if assigned
        if (visits > 1 && config.secondaryTarget != null)
            return config.secondaryTarget;

        return config.primaryTarget;
    }

    public void ResetToPageDefault()
    {
        if (currentPageIndex >= 0)
            MoveToPage(currentPageIndex);
    }

    // ================= DIRECT MOVEMENT =================

    public void MoveTo(Transform target)
    {
        if (target == null) return;
        StartMove(target, defaultMoveDuration);
    }

    public void MoveTo(Transform target, float duration)
    {
        if (target == null) return;
        StartMove(target, duration > 0f ? duration : defaultMoveDuration);
    }

    // ================= MOVEMENT CORE =================

    private void StartMove(Transform target, float duration)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null) return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(MoveRoutine(target, duration));
    }

    private IEnumerator MoveRoutine(Transform target, float duration)
    {
        OnMoveStart?.Invoke();

        Transform camTransform = targetCamera.transform;

        Vector3 startPos = camTransform.position;
        Quaternion startRot = camTransform.rotation;

        Vector3 endPos = target.position;
        Quaternion endRot = target.rotation;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += delta;

            float progress = ease.Evaluate(Mathf.Clamp01(elapsed / duration));

            camTransform.position = Vector3.Lerp(startPos, endPos, progress);
            camTransform.rotation = Quaternion.Slerp(startRot, endRot, progress);

            yield return null;
        }

        camTransform.position = endPos;
        camTransform.rotation = endRot;

        routine = null;
        OnMoveEnd?.Invoke();
    }
}