using Assets.Scripts.Sensors;
using System.Collections.Generic;
using UnityEngine;

public class FlightSchoolObjective : MonoBehaviour, IObjective
{
    public DataManager.GameMode Mode => DataManager.GameMode.FlightSchool;

    // The Track
    [SerializeField] private Transform[] waypoints;

    // Settings
    [SerializeField] private float hoopRadius = 170f;
    // [SerializeField] private float lambda = 0.1f;
    // [SerializeField] private float distanceRewardMultiplier = 2f;
    // [SerializeField] private float hoopPassReward = 500f;
    // [SerializeField] private float backwardsDriftPenalty = 0.5f;
    // [SerializeField] private float lookAtRewardWeight = 1f;
    [SerializeField] private float lambda = 100f;
    [SerializeField] private float distanceRewardMultiplier = 0.02f;
    [SerializeField] private float hoopPassReward = 100f;
    [SerializeField] private float backwardsDriftPenalty = 2f;
    [SerializeField] private float lookAtRewardWeight = 2f;
    [SerializeField] private float maxTimeAllowed = 180f;
    [SerializeField] private float timeBonusMultiplier = 10f; // Points per second remaining if they win
    [SerializeField] private float timeBetweenHoopsAllowed = 12f;

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
        // Fallback: If waypoints are not assigned in the Inspector, try to find them from children
        if (waypoints == null || waypoints.Length == 0)
        {
            int hoopCount = transform.childCount;
            waypoints = new Transform[hoopCount];

            for (int i = 0; i < hoopCount; i++)
            {
                waypoints[i] = transform.GetChild(i);
            }
        }
    }

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
                // Dense reward: Max reward at dead center, drops off linearly the further away they look
                float lookAtReward = lookAtRewardWeight * (1f - (angleToHoop / 180f));
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
                    agentTargetIndices[agent]++;
                    if (agentBreakdowns.ContainsKey(agent)) agentBreakdowns[agent]["Hoop Pass"] += hoopPassReward;
                    stepReward += hoopPassReward;
                    lastHoopTime[agent] = agent.TimeAlive;

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
