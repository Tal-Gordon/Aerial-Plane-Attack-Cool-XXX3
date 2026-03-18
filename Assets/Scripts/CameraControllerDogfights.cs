using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraControllerDogfights : MonoBehaviour
{
    [Header("Smart Zoom Settings")]
    [SerializeField] private float transitionDuration = 0.375f;
    [Tooltip("How much padding to leave around the cube")]
    [SerializeField] private float framingMultiplier = 1.75f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Camera cam;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Coroutine movementCoroutine;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    // Subscribe to events
    private void OnEnable()
    {
        SelectionManager.OnCubeSelected += FocusOnTarget;
        SelectionManager.OnCubeDeselected += ResetView;
    }

    // Unsubscribe to prevent memory leaks
    private void OnDisable()
    {
        SelectionManager.OnCubeSelected -= FocusOnTarget;
        SelectionManager.OnCubeDeselected -= ResetView;
    }

    private void FocusOnTarget(Transform target)
    {
        float framingDistance = 5f; // Fallback distance
        Renderer targetRenderer = target.GetComponent<Renderer>();

        if (targetRenderer != null && cam != null)
        {
            // Get the physical size of the object
            float maxExtent = targetRenderer.bounds.extents.magnitude;

            // Calculate how far back the camera needs to be based on the FOV
            // using the standard frustum math formula: distance=size/(tan(FOV/2))
            framingDistance = (maxExtent * framingMultiplier) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        // Instead of rotating to look at the cube, we keep the camera's original rotation.
        // We just move it backwards along its own line of sight by the calculated distance.
        Vector3 targetPos = target.position - (initialRotation * Vector3.forward * framingDistance);

        MoveCamera(targetPos, initialRotation);
    }

    private void ResetView()
    {
        MoveCamera(initialPosition, initialRotation);
    }

    private void MoveCamera(Vector3 targetPos, Quaternion targetRot)
    {
        if (movementCoroutine != null) StopCoroutine(movementCoroutine);
        movementCoroutine = StartCoroutine(TransitionRoutine(targetPos, targetRot));
    }

    private IEnumerator TransitionRoutine(Vector3 targetPos, Quaternion targetRot)
    {
        transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        float timePassed = 0f;

        while (timePassed < transitionDuration)
        {
            timePassed += Time.deltaTime;
            float t = Mathf.Clamp01(timePassed / transitionDuration);
            float curveT = transitionCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPos, targetPos, curveT);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, curveT);

            yield return null;
        }

        transform.SetPositionAndRotation(targetPos, targetRot);
    }
}