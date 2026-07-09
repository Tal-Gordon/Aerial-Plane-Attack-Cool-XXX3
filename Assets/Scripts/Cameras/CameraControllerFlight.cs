using System.Collections;
using UnityEngine;

public class CameraControllerFlight : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("How long it takes to move between waypoints in seconds.")]
    public float transitionDuration = 1.5f;

    [Tooltip("Easing curve for smooth starts and stops.")]
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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
        // Subscribe to global events
        FlightInputManager.OnNextCommand += GoToNextWaypoint;
        FlightInputManager.OnPreviousCommand += GoToPreviousWaypoint;
    }

    private void OnDisable()
    {
        // Unsubscribe
        FlightInputManager.OnNextCommand -= GoToNextWaypoint;
        FlightInputManager.OnPreviousCommand -= GoToPreviousWaypoint;
    }

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
        transform.position = targetWaypoint.transform.position;
        transform.rotation = targetWaypoint.transform.rotation;
    }

    private IEnumerator MoveToWaypointRoutine(Waypoint target)
    {
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 endPos = target.transform.position;
        Quaternion endRot = target.transform.rotation;

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;

            // Normalize time and apply easing
            float t = Mathf.Clamp01(elapsedTime / transitionDuration);
            float curveT = easeCurve.Evaluate(t);

            transform.position = Vector3.Lerp(startPos, endPos, curveT);
            transform.rotation = Quaternion.Slerp(startRot, endRot, curveT);

            yield return null;
        }

        // Snap to exact final position to prevent floating point drift
        transform.position = endPos;
        transform.rotation = endRot;
        movementCoroutine = null;
    }
}