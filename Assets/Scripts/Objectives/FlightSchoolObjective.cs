using Assets.Scripts.Sensors;
using System;
using System.Collections.Generic;
using UnityEngine;

public class FlightSchoolObjective : MonoBehaviour, IObjective
{
    /// <summary>Jet, zero-based hoop index, and hoop transform.</summary>
    public event Action<JetAgent, int, Transform> HoopPassed;

    public DataManager.GameMode Mode => DataManager.GameMode.FlightSchool;
    public SensorType RequiredSensorType => SensorType.Waypoint;

    // Jets crash / miss hoops at different times, so the population thins out
    // gradually — an "alive" count is a meaningful live metric here.
    public bool TracksAttrition => true;

    // The Track
    [SerializeField] private Transform[] waypoints;

    // Settings — default values are baked in DataManager.GetDefaultRewardParameters
    // and applied in Awake; runtime tuning flows through SetParameters / saves.
    [SerializeField] private float hoopRadius;
    [SerializeField] private float lambda;
    [SerializeField] private float distanceRewardMultiplier;
    [SerializeField] private float hoopPassReward;
    [SerializeField] private float backwardsDriftPenalty;
    [SerializeField] private float lookAtRewardWeight;
    [SerializeField] private float maxTimeAllowed;
    [SerializeField] private float timeBonusMultiplier; // Points per second remaining if they win
    [SerializeField] private float timeBetweenHoopsAllowed;

    // State Trackers
    private Dictionary<JetAgent, int> agentTargetIndices = new Dictionary<JetAgent, int>();
    private Dictionary<JetAgent, float> lastEffortSums = new Dictionary<JetAgent, float>();
    private Dictionary<JetAgent, float> lastDistanceToHoop = new Dictionary<JetAgent, float>();
    private Dictionary<JetAgent, float> lastLocalZ = new Dictionary<JetAgent, float>();
    private Dictionary<JetAgent, float> lastHoopTime = new Dictionary<JetAgent, float>();
    private Dictionary<JetAgent, Dictionary<string, float>> agentBreakdowns = new Dictionary<JetAgent, Dictionary<string, float>>();

    // TODO REMOVE
    private JetAgent debugAgent = null;


    private void Awake()
    {
        // Seed reward parameters from the baked defaults (single source of truth in
        // DataManager). SimulationManager overrides these via SetParameters when a
        // saved run is loaded.
        SetParameters(DataManager.GetDefaultRewardParameters(Mode));

        // Fallback: if the list wasn't captured in the editor (e.g. a build, or a
        // freshly-reset component), collect the hoops from children so the track
        // still works. In the editor it's kept in sync live (see the region below).
        if (waypoints == null || waypoints.Length == 0)
        {
            RebuildWaypointsFromChildren();
        }
    }

    // Collects every direct child as a waypoint in Hierarchy (sibling) order, so
    // the flight order is whatever order the hoops sit in under this object —
    // reorder them by dragging in the Hierarchy. (Name/position sorting is no good
    // here: the hoops carry ProBuilder auto-names like "pb_Mesh-13544" whose
    // numbers are meaningless, and position can't be ordered once a track loops.)
    // Returns true if the resulting list actually changed.
    private bool RebuildWaypointsFromChildren()
    {
        int count = transform.childCount;
        Transform[] children = new Transform[count];
        for (int i = 0; i < count; i++) children[i] = transform.GetChild(i);

        if (waypoints != null && waypoints.Length == count)
        {
            bool same = true;
            for (int i = 0; i < count; i++)
            {
                if (waypoints[i] != children[i]) { same = false; break; }
            }
            if (same) return false;
        }

        waypoints = children;
        return true;
    }

#if UNITY_EDITOR
    // --- Editor-only: keep the waypoints list mirroring the hoop children ---

    [ContextMenu("Rebuild Waypoints From Children")]
    private void EditorRebuildWaypoints()
    {
        if (RebuildWaypointsFromChildren())
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }

    // Fires on load, recompile, and Inspector edits.
    private void OnValidate()
    {
        if (!Application.isPlaying) EditorRebuildWaypoints();
    }

    // Adding/removing/reordering a hoop in the Hierarchy doesn't call OnValidate,
    // so listen to the editor's hierarchy-changed event and resync every objective.
    [UnityEditor.InitializeOnLoadMethod]
    private static void HookHierarchyChanges()
    {
        UnityEditor.EditorApplication.hierarchyChanged += () =>
        {
            if (Application.isPlaying) return;
            var objectives = FindObjectsByType<FlightSchoolObjective>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var objective in objectives) objective.EditorRebuildWaypoints();
        };
    }
#endif

    public void SetStartingState(JetAgent agent, int index, int totalPopulation)
    {
        // Define center behind the first hoop (500 units along its local Z)
        Vector3 spawnCenter = waypoints[0].position - (waypoints[0].forward * 1500f);

        // Move the jet to position
        agent.transform.position = spawnCenter;

        // Update the Jet's memory
        agent.StartingPosition = agent.transform.position;

        // Face the exact same direction as the first hoop instead of strictly north!
        agent.transform.rotation = waypoints[0].rotation;

        // Give it starting velocity so it doesn't stall, and clear any spin
        Rigidbody rb = agent.GetComponent<Rigidbody>();
        rb.linearVelocity = agent.transform.forward * 600;
        rb.angularVelocity = Vector3.zero;

        // Initiate trackers
        agentTargetIndices[agent] = 0;
        lastEffortSums[agent] = 0;
        lastDistanceToHoop[agent] = Vector3.Distance(agent.transform.position, waypoints[0].position);
        lastLocalZ[agent] = waypoints[0].InverseTransformPoint(agent.transform.position).z;
        lastHoopTime[agent] = 0f;

        agentBreakdowns[agent] = new Dictionary<string, float> {
            { "Distance", 0f },
            { "Look At", 0f },
            { "Hoop Pass", 0f },
            { "Effort Penalty", 0f },
            { "Backwards Drift Penalty", 0f },
            { "Time Bonus", 0f }
        };

        // Set the sensors
        WaypointSensors sensors = agent.GetComponent<WaypointSensors>();
        sensors.currentWaypoint = waypoints[0];

        // TODO REMOVE
        if (debugAgent == null) debugAgent = agent;

    }

    public float GetStepReward(JetAgent agent)
    {
        // TODO potentially normalize reward by distance to each hoop
        if (!agentTargetIndices.ContainsKey(agent) || waypoints.Length == 0) return 0f;

        float stepReward = 0f;
        int currentIndex = agentTargetIndices[agent];

        // L2 penalty
        float currentEffort = agent.TotalControlEffort;
        float effortGained = currentEffort - lastEffortSums[agent];
        lastEffortSums[agent] = currentEffort;
        float effortPenalty = -lambda * effortGained;
        if (agentBreakdowns.ContainsKey(agent)) agentBreakdowns[agent]["Effort Penalty"] += effortPenalty;
        stepReward += effortPenalty;

        if (currentIndex < waypoints.Length)
        {
            Transform targetHoop = waypoints[currentIndex];

            // Distance Reward
            float currentDistance = Vector3.Distance(agent.transform.position, targetHoop.position);
            float distanceDelta = lastDistanceToHoop[agent] - currentDistance;
            float progressReward = distanceRewardMultiplier * distanceDelta;
            
            float distanceAdded = 0f;
            float driftPenaltyAdded = 0f;

            // Alignment Multiplier (Prevent falling/drifting backwards)
            Rigidbody rb = agent.GetComponent<Rigidbody>();
            float alignment = rb.linearVelocity.sqrMagnitude > 0.01f 
                ? Vector3.Dot(agent.transform.forward, rb.linearVelocity.normalized) 
                : 0f;

            if (alignment > 0f)
            {
                // If they align, apply the multiplier to the progress reward
                // (Only scale positive rewards so we don't accidentally reduce distance penalties)
                if (progressReward > 0f)
                {
                    progressReward *= alignment;
                }
                distanceAdded = progressReward;
            }
            else
            {
                // If they don't align, crush positive progress to zero and add a penalty
                if (progressReward > 0f)
                {
                    progressReward = 0f;
                }
                distanceAdded = progressReward;
                driftPenaltyAdded = -backwardsDriftPenalty;
                progressReward += driftPenaltyAdded; // Penalty for drifting/falling backwards
            }

            if (agentBreakdowns.ContainsKey(agent)) 
            {
                agentBreakdowns[agent]["Distance"] += distanceAdded;
                agentBreakdowns[agent]["Backwards Drift Penalty"] += driftPenaltyAdded;
            }
            stepReward += progressReward;
            lastDistanceToHoop[agent] = currentDistance;

            // Look-At Reward
            if (currentDistance > 0.01f)
            {
                Vector3 dirToHoop = (targetHoop.position - agent.transform.position).normalized;
                float angleToHoop = Vector3.Angle(agent.transform.forward, dirToHoop);
                // Dense penalty: 0 penalty at dead center, drops to negative the further away they look.
                // Making this strictly negative prevents the agent from farming points by flying slowly!
                float lookAtReward = lookAtRewardWeight * -(angleToHoop / 180f);
                if (agentBreakdowns.ContainsKey(agent)) agentBreakdowns[agent]["Look At"] += lookAtReward;
                stepReward += lookAtReward;
            }

            // --- THE TUNNELING FIX ---
            Vector3 localPos = targetHoop.InverseTransformPoint(agent.transform.position);
            float currentZ = localPos.z;
            float previousZ = lastLocalZ[agent];

            // Did the jet cross the doorway from front (-) to back (+) this exact frame?
            if (previousZ <= 0f && currentZ > 0f)
            {
                float distanceFromCenter = new Vector2(localPos.x, localPos.y).magnitude;

                if (agent == debugAgent)
                {
                    // Debug.Log($"[Debug Jet] Target: Hoop {currentIndex} | Local Z: {localPos.z:F2} | Dist from center: {distanceFromCenter:F2}");
                }

                // Were they inside the ring when they crossed?
                if (distanceFromCenter < hoopRadius)
                {
                    int passedHoopIndex = currentIndex;
                    agentTargetIndices[agent]++;
                    if (agentBreakdowns.ContainsKey(agent)) agentBreakdowns[agent]["Hoop Pass"] += hoopPassReward;
                    stepReward += hoopPassReward;
                    lastHoopTime[agent] = agent.TimeAlive;
                    HoopPassed?.Invoke(agent, passedHoopIndex, targetHoop);

                    // Update trackers to look at the NEW hoop
                    if (agentTargetIndices[agent] < waypoints.Length)
                    {
                        Transform nextHoop = waypoints[agentTargetIndices[agent]];
                        lastDistanceToHoop[agent] = Vector3.Distance(agent.transform.position, nextHoop.position);

                        // Instantly calculate our starting Z for the new hoop so we don't break the math
                        lastLocalZ[agent] = nextHoop.InverseTransformPoint(agent.transform.position).z;

                        WaypointSensors sensors = agent.GetComponent<WaypointSensors>();
                        if (sensors != null) sensors.currentWaypoint = nextHoop;

                        return stepReward; // Exit early so we don't overwrite lastLocalZ below
                    }
                }
                else
                {
                    // It crossed the Z-plane, but missed the hole. Execute it.
                    agent.HasCrashed = true;
                }
            }

            // Update the Z tracker for the next frame
            lastLocalZ[agent] = currentZ;
        }

        return stepReward;
    }

    public float CalculateTotalFitness(JetAgent agent)
    {
        float finalScore = agent.CurrentFitness;

        // Bonus for speed
        if (agentTargetIndices.ContainsKey(agent) && agentTargetIndices[agent] >= waypoints.Length)
        {
            float timeLeft = maxTimeAllowed - agent.TimeAlive;
            float timeBonus = timeLeft * timeBonusMultiplier;
            finalScore += timeBonus;
            if (agentBreakdowns.ContainsKey(agent)) agentBreakdowns[agent]["Time Bonus"] += timeBonus;
        }

        return finalScore;
    }

    public Dictionary<string, float> GetRewardBreakdown(JetAgent agent)
    {
        if (agentBreakdowns.ContainsKey(agent)) return agentBreakdowns[agent];
        return new Dictionary<string, float>();
    }

    // To add a tunable parameter: add the field, an entry here, in SetParameters,
    // and a descriptor in GetParameterDescriptors below.
    public Dictionary<string, float> GetParameters() => new Dictionary<string, float>
    {
        { "hoopRadius", hoopRadius },
        { "lambda", lambda },
        { "distanceRewardMultiplier", distanceRewardMultiplier },
        { "hoopPassReward", hoopPassReward },
        { "backwardsDriftPenalty", backwardsDriftPenalty },
        { "lookAtRewardWeight", lookAtRewardWeight },
        { "maxTimeAllowed", maxTimeAllowed },
        { "timeBonusMultiplier", timeBonusMultiplier },
        { "timeBetweenHoopsAllowed", timeBetweenHoopsAllowed },
    };

    // Only the reward-shaping weights are user-tunable dials. The remaining
    // GetParameters keys (hoopRadius, the time limits) are still saved/loaded but
    // intentionally NOT exposed here, so they can't be changed from the UI.
    // Descriptors are immutable metadata — built once and shared.
    private static readonly ParameterDescriptor[] Descriptors =
    {
        new ParameterDescriptor("lambda", "Effort Penalty (lambda)", 0f, 10f, 1f),
        new ParameterDescriptor("distanceRewardMultiplier", "Distance Reward", 0f, 5f, 0.4f),
        new ParameterDescriptor("hoopPassReward", "Hoop Pass Reward", 0f, 10000f, 2000f),
        new ParameterDescriptor("backwardsDriftPenalty", "Backwards Drift Penalty", 0f, 50f, 2f),
        new ParameterDescriptor("lookAtRewardWeight", "Look-At Reward", 0f, 100f, 10f),
    };

    public IReadOnlyList<ParameterDescriptor> GetParameterDescriptors() => Descriptors;

    public void SetParameters(Dictionary<string, float> parameters)
    {
        if (parameters == null) return;
        if (parameters.TryGetValue("hoopRadius", out float v)) hoopRadius = v;
        if (parameters.TryGetValue("lambda", out v)) lambda = v;
        if (parameters.TryGetValue("distanceRewardMultiplier", out v)) distanceRewardMultiplier = v;
        if (parameters.TryGetValue("hoopPassReward", out v)) hoopPassReward = v;
        if (parameters.TryGetValue("backwardsDriftPenalty", out v)) backwardsDriftPenalty = v;
        if (parameters.TryGetValue("lookAtRewardWeight", out v)) lookAtRewardWeight = v;
        if (parameters.TryGetValue("maxTimeAllowed", out v)) maxTimeAllowed = v;
        if (parameters.TryGetValue("timeBonusMultiplier", out v)) timeBonusMultiplier = v;
        if (parameters.TryGetValue("timeBetweenHoopsAllowed", out v)) timeBetweenHoopsAllowed = v;
    }

    public bool CheckTerminalState(JetAgent agent)
    {
        if (agent.HasCrashed) return true;

        if (agent.TimeAlive > maxTimeAllowed) return true;

        if (lastHoopTime.ContainsKey(agent) && (agent.TimeAlive - lastHoopTime[agent]) > timeBetweenHoopsAllowed)
        {
            return true;
        }

        if (agentTargetIndices.ContainsKey(agent) && agentTargetIndices[agent] >= waypoints.Length)
        {
            return true;
        }

        return false;
    }
}
