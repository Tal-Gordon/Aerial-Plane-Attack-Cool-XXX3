using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControllerFlight : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("How long it takes to move between waypoints in seconds.")]
    public float transitionDuration = 1.5f;

    [Header("Input")]
    [Tooltip("Action that advances to the next waypoint.")]
    public InputAction nextWaypointAction;

    [Tooltip("Action that returns to the previous waypoint.")]
    public InputAction previousWaypointAction;

    [Header("State")]
    public Waypoint startingWaypoint;
    private Waypoint currentWaypoint;
    private Coroutine movementCoroutine;

    private void Start()
    {
        if (startingWaypoint != null)
        {
            JumpToWaypointImmediate(startingWaypoint);
        }
    }

    private void OnEnable()
    {
        nextWaypointAction.Enable();
        previousWaypointAction.Enable();

        nextWaypointAction.performed += OnNextPerformed;
        previousWaypointAction.performed += OnPreviousPerformed;
    }

    private void OnDisable()
    {
        nextWaypointAction.performed -= OnNextPerformed;
        previousWaypointAction.performed -= OnPreviousPerformed;

        nextWaypointAction.Disable();
        previousWaypointAction.Disable();
    }

    private void OnNextPerformed(InputAction.CallbackContext context) => GoToNextWaypoint();
    private void OnPreviousPerformed(InputAction.CallbackContext context) => GoToPreviousWaypoint();

    public void GoToWaypoint(Waypoint targetWaypoint)
    {
        if (targetWaypoint == null || targetWaypoint == currentWaypoint) return;

        currentWaypoint = targetWaypoint;

        // Stop current transition if the user presses a button mid-flight
        if (movementCoroutine != null)
        {
            StopCoroutine(movementCoroutine);
        }

        movementCoroutine = StartCoroutine(MoveToWaypointRoutine(targetWaypoint));
    }

    public void GoToNextWaypoint()
    {
        if (currentWaypoint != null && currentWaypoint.Next != null)
        {
            GoToWaypoint(currentWaypoint.Next);
        }
    }

    public void GoToPreviousWaypoint()
    {
        if (currentWaypoint != null && currentWaypoint.Previous != null)
        {
            GoToWaypoint(currentWaypoint.Previous);
        }
    }

    public void JumpToWaypointImmediate(Waypoint targetWaypoint)
    {
        if (targetWaypoint == null) return;

        currentWaypoint = targetWaypoint;
        transform.SetPositionAndRotation(targetWaypoint.transform.position, targetWaypoint.transform.rotation);
    }

    private IEnumerator MoveToWaypointRoutine(Waypoint target)
    {
        transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        target.transform.GetPositionAndRotation(out Vector3 endPos, out Quaternion endRot);
        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;

            // Normalize time and apply easing
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            float curveT = Smootherstep(t);

            transform.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, curveT), Quaternion.Slerp(startRot, endRot, curveT));
            yield return null;
        }

        // Snap to exact final position to prevent floating point drift
        transform.SetPositionAndRotation(endPos, endRot);
        movementCoroutine = null;
    }

    // Ken Perlin's smootherstep: 6t^5 - 15t^4 + 10t^3.
    // Zero 1st and 2nd derivatives at both ends for gentler starts/stops than smoothstep.
    private static float Smootherstep(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}