using System.Collections;
using System.Collections.Generic;
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

    [Header("Automatic Track Coverage")]
    [Tooltip("The Flight School objective. Found automatically when left empty.")]
    [SerializeField] private FlightSchoolObjective flightSchoolObjective;

    [Tooltip("Optional action that toggles automatic camera progression.")]
    [SerializeField] private InputAction toggleAutoFollowAction;

    [Tooltip("1-based hoop numbers. Each entry advances one section camera when the first jet passes that hoop. Example: 4, 9, 14.")]
    [SerializeField] private int[] advanceAfterHoopNumbers;

    [Tooltip("First section camera. If empty, Starting Waypoint.Next is used (Starting Waypoint remains the overview).")]
    [SerializeField] private Waypoint firstSectionWaypoint;

    [Header("State")]
    public Waypoint startingWaypoint;
    private Waypoint currentWaypoint;
    private Coroutine movementCoroutine;
    private bool autoFollow;
    private int[] triggerOccupancy;
    private readonly Dictionary<JetAgent, int> agentFurthestTrigger = new();
    private readonly List<JetAgent> staleAgents = new();

    public bool IsAutoFollowEnabled => autoFollow;

    private void Awake()
    {
        if (flightSchoolObjective == null)
            flightSchoolObjective = FindFirstObjectByType<FlightSchoolObjective>();
    }

    private void Start()
    {
        if (startingWaypoint != null)
        {
            JumpToWaypointImmediate(startingWaypoint);
        }

        // Automatic coverage is opt-in through CameraAutoFollowToggleWidget.
        // Always start off, including scenes that still carry the old serialized
        // autoFollowOnStart value from before the toggle existed.
        SetAutoFollow(false);
    }

    private void OnEnable()
    {
        nextWaypointAction.Enable();
        previousWaypointAction.Enable();

        nextWaypointAction.performed += OnNextPerformed;
        previousWaypointAction.performed += OnPreviousPerformed;
        if (toggleAutoFollowAction != null)
        {
            toggleAutoFollowAction.Enable();
            toggleAutoFollowAction.performed += OnToggleAutoFollowPerformed;
        }
        SubscribeToObjective();
    }

    private void OnDisable()
    {
        nextWaypointAction.performed -= OnNextPerformed;
        previousWaypointAction.performed -= OnPreviousPerformed;
        if (toggleAutoFollowAction != null)
            toggleAutoFollowAction.performed -= OnToggleAutoFollowPerformed;

        nextWaypointAction.Disable();
        previousWaypointAction.Disable();
        if (toggleAutoFollowAction != null) toggleAutoFollowAction.Disable();
        UnsubscribeFromObjective();
    }

    private void OnNextPerformed(InputAction.CallbackContext context)
    {
        SetAutoFollow(false);
        GoToNextWaypoint();
    }

    private void OnPreviousPerformed(InputAction.CallbackContext context)
    {
        SetAutoFollow(false);
        GoToPreviousWaypoint();
    }

    private void OnToggleAutoFollowPerformed(InputAction.CallbackContext context) => ToggleAutoFollow();

    /// <summary>Wire this directly to a UI Button's OnClick.</summary>
    public void ToggleAutoFollow() => SetAutoFollow(!autoFollow);

    /// <summary>Can also be wired to a Toggle's OnValueChanged(bool).</summary>
    public void SetAutoFollow(bool enabled)
    {
        autoFollow = enabled;
        if (enabled) RefreshAutomaticCoverage();
    }

    private void SubscribeToObjective()
    {
        if (flightSchoolObjective == null) return;
        flightSchoolObjective.HoopPassed -= OnHoopPassed;
        flightSchoolObjective.AgentFinished -= OnAgentFinished;
        flightSchoolObjective.HoopPassed += OnHoopPassed;
        flightSchoolObjective.AgentFinished += OnAgentFinished;
    }

    private void UnsubscribeFromObjective()
    {
        if (flightSchoolObjective == null) return;
        flightSchoolObjective.HoopPassed -= OnHoopPassed;
        flightSchoolObjective.AgentFinished -= OnAgentFinished;
    }

    private void OnHoopPassed(JetAgent jet, int zeroBasedHoopIndex, Transform hoop)
    {
        EnsureTrackingCapacity();
        if (jet == null || triggerOccupancy.Length == 0) return;

        int hoopNumber = zeroBasedHoopIndex + 1;
        int reachedTrigger = -1;
        for (int i = 0; i < advanceAfterHoopNumbers.Length; i++)
        {
            if (advanceAfterHoopNumbers[i] == hoopNumber)
            {
                reachedTrigger = i;
                break;
            }
        }
        if (reachedTrigger < 0) return;

        int previousTrigger = agentFurthestTrigger.TryGetValue(jet, out int previous)
            ? previous
            : -1;
        if (reachedTrigger <= previousTrigger) return;

        // A jet contributes to every configured checkpoint it has visited. Thus a
        // lone jet reaching hoop 6 with triggers [4,6,9] produces [1,1,0].
        for (int i = previousTrigger + 1; i <= reachedTrigger; i++)
            triggerOccupancy[i]++;

        agentFurthestTrigger[jet] = reachedTrigger;
        RefreshAutomaticCoverage();
    }

    private void OnAgentFinished(JetAgent jet)
    {
        RemoveAgentContribution(jet);
        RefreshAutomaticCoverage();
    }

    private void RemoveAgentContribution(JetAgent jet)
    {
        EnsureTrackingCapacity();
        // A destroyed Unity object compares equal to null but remains a valid CLR
        // dictionary key, so do not reject it before removing its stale entry.
        if (!agentFurthestTrigger.TryGetValue(jet, out int furthest)) return;

        for (int i = 0; i <= furthest && i < triggerOccupancy.Length; i++)
            triggerOccupancy[i] = Mathf.Max(0, triggerOccupancy[i] - 1);

        agentFurthestTrigger.Remove(jet);
    }

    private void LateUpdate()
    {
        // Population rebuilds can destroy jets without running their objective
        // terminal check. Remove those stale contributions as a safety net.
        if (agentFurthestTrigger.Count == 0) return;

        staleAgents.Clear();
        foreach (var pair in agentFurthestTrigger)
            if (pair.Key == null) staleAgents.Add(pair.Key);

        if (staleAgents.Count == 0) return;
        foreach (JetAgent stale in staleAgents) RemoveAgentContribution(stale);
        RefreshAutomaticCoverage();
    }

    private void EnsureTrackingCapacity()
    {
        int required = advanceAfterHoopNumbers?.Length ?? 0;
        if (triggerOccupancy != null && triggerOccupancy.Length == required) return;

        // The mapping changed in the Inspector, so old per-agent indices no longer
        // describe the same hoops. Start the occupancy model cleanly.
        triggerOccupancy = new int[required];
        agentFurthestTrigger.Clear();
    }

    private void RefreshAutomaticCoverage()
    {
        if (!autoFollow) return;
        EnsureTrackingCapacity();

        int furthestOccupied = -1;
        for (int i = triggerOccupancy.Length - 1; i >= 0; i--)
        {
            if (triggerOccupancy[i] <= 0) continue;
            furthestOccupied = i;
            break;
        }

        Waypoint targetCamera = GetFirstSectionWaypoint();
        // No occupied trigger means the second camera (first track section).
        // Trigger 0 means one camera after it, trigger 1 means two after it, etc.
        int cameraSteps = furthestOccupied + 1;
        for (int i = 0; i < cameraSteps && targetCamera != null; i++)
            targetCamera = targetCamera.Next;

        if (targetCamera != null) GoToWaypoint(targetCamera);
        else Debug.LogWarning("[CameraControllerFlight] The camera waypoint chain is shorter than the configured hoop trigger list.", this);
    }

    private Waypoint GetFirstSectionWaypoint()
    {
        return firstSectionWaypoint != null
            ? firstSectionWaypoint
            : startingWaypoint != null ? startingWaypoint.Next : null;
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
