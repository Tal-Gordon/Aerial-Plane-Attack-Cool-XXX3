using Assets.Scripts.Sensors;
using System.Collections.Generic;
using UnityEngine;

public class FlightSchoolObjective : IObjective
{
    // The Track
    private Transform[] waypoints;

    // State Trackers
    private Dictionary<JetAgent, int> agentTargetIndices = new Dictionary<JetAgent, int>();
    private Dictionary<JetAgent, float> lastEffortSums = new Dictionary<JetAgent, float>();
    private Dictionary<JetAgent, float> lastDistanceToHoop = new Dictionary<JetAgent, float>();
    private Dictionary<JetAgent, float> lastLocalZ = new Dictionary<JetAgent, float>();

    // Settings
    private float hoopRadius = 170f;
    private float lambda = 0.1f;
    private float maxTimeAllowed = 15f;
    private float timeBonusMultiplier = 10f; // Points per second remaining if they win

    // TODO REMOVE
    private JetAgent debugAgent = null;

    public FlightSchoolObjective()
    {
        // Find the parent holding all waypoints
        GameObject trackParent = GameObject.Find("HoopTrack");

        if (trackParent == null)
        {
            Debug.LogError("[FlightSchoolObjective] Could not find 'HoopTrack' in the scene!");
            return;
        }

        // Extract hoops
        int hoopCount = trackParent.transform.childCount;
        waypoints = new Transform[hoopCount];

        for (int i = 0; i < hoopCount; i++)
        {
            waypoints[i] = trackParent.transform.GetChild(i);
        }
    }

    public void SetStartingState(JetAgent agent, int index, int totalPopulation, Vector3 centerPoint)
    {
        // Move the jet to position
        agent.transform.position = centerPoint;

        // Update the Jet's memory
        agent.StartingPosition = agent.transform.position;

        // Face north
        agent.transform.rotation = Quaternion.identity;

        // Give it starting velocity so it doesn't stall, and clear any spin
        Rigidbody rb = agent.GetComponent<Rigidbody>();
        rb.linearVelocity = agent.transform.forward * 600;
        rb.angularVelocity = Vector3.zero;

        // Initiate trackers
        agentTargetIndices[agent] = 0;
        lastEffortSums[agent] = 0;
        lastDistanceToHoop[agent] = Vector3.Distance(agent.transform.position, waypoints[0].position);
        lastLocalZ[agent] = waypoints[0].InverseTransformPoint(agent.transform.position).z;

        // Set the sensors
        WaypointSensors sensors = agent.GetComponent<WaypointSensors>();
        sensors.currentWaypoint = waypoints[0];

        // TODO REMOVE
        if (debugAgent == null) debugAgent = agent;
    }

    public float GetStepReward(JetAgent agent)
    {
        if (!agentTargetIndices.ContainsKey(agent) || waypoints.Length == 0) return 0f;

        float stepReward = 0f;
        int currentIndex = agentTargetIndices[agent];

        // L2 penalty
        float currentEffort = agent.TotalControlEffort;
        float effortGained = currentEffort - lastEffortSums[agent];
        lastEffortSums[agent] = currentEffort;
        stepReward -= lambda * effortGained;

        if (currentIndex < waypoints.Length)
        {
            Transform targetHoop = waypoints[currentIndex];

            // Distance Reward (Breadcrumbs)
            float currentDistance = Vector3.Distance(agent.transform.position, targetHoop.position);
            float distanceDelta = lastDistanceToHoop[agent] - currentDistance;
            stepReward += distanceDelta;
            lastDistanceToHoop[agent] = currentDistance;

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
                    Debug.Log($"[Debug Jet] Target: Hoop {currentIndex} | Local Z: {localPos.z:F2} | Dist from center: {distanceFromCenter:F2}");
                }

                // Were they inside the ring when they crossed?
                if (distanceFromCenter < hoopRadius)
                {
                    agentTargetIndices[agent]++;
                    stepReward += 500f;

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
            finalScore += timeLeft * timeBonusMultiplier;
        }

        return finalScore;
    }

    public bool CheckTerminalState(JetAgent agent)
    {
        if (agent.HasCrashed) return true;

        if (agent.TimeAlive > maxTimeAllowed) return true;

        if (agentTargetIndices.ContainsKey(agent) && agentTargetIndices[agent] >= waypoints.Length)
        {
            return true;
        }

        return false;
    }
}
