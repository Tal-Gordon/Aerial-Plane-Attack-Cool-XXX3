using System.Collections.Generic;
using UnityEngine;

public class FlightSchoolObjective : IObjective
{
    // The Track
    private Transform[] waypoints;

    // State Trackers
    private Dictionary<JetAgent, int> agentTargetIndices = new Dictionary<JetAgent, int>();
    private Dictionary<JetAgent, float> lastEffortSums = new Dictionary<JetAgent, float>();

    // NEW: Track the distance to the active hoop for the Dense Reward
    private Dictionary<JetAgent, float> lastDistanceToHoop = new Dictionary<JetAgent, float>();

    // Settings
    private float hoopRadius = 15f;
    private float hoopThickness = 3f;
    private float lambda = 0.1f;
    private float maxTimeAllowed = 10f;
    private float timeBonusMultiplier = 10f; // Points per second remaining if they win

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
    }

    public float GetStepReward(JetAgent agent)
    {
        // Safety checks (CRITICAL to keep these uncommented)
        if (!agentTargetIndices.ContainsKey(agent) || waypoints.Length == 0) return 0f;
        if (!lastEffortSums.ContainsKey(agent) || !lastDistanceToHoop.ContainsKey(agent)) return 0f;

        float stepReward = 0f;
        int currentIndex = agentTargetIndices[agent];

        // L2 penalty
        float currentEffort = agent.TotalControlEffort;
        float effortGained = currentEffort - lastEffortSums[agent];
        lastEffortSums[agent] = currentEffort;

        stepReward -= lambda * effortGained;

        // Distance reward
        if (currentIndex < waypoints.Length)
        {
            Transform targetHoop = waypoints[currentIndex];
            // TODO potentially accommodate for distance from any point on hoop instead of center
            float currentDistance = Vector3.Distance(agent.transform.position, targetHoop.position);

            // Did we get closer reward
            float distanceDelta = lastDistanceToHoop[agent] - currentDistance;
            stepReward += distanceDelta;

            // Update tracker
            lastDistanceToHoop[agent] = currentDistance;

            // Hoop crossing reward
            // FIXED: We must compare the hoop to the AGENT'S position
            Vector3 localPos = targetHoop.InverseTransformPoint(agent.transform.position);
            float distanceFromCenter = new Vector2(localPos.x, localPos.y).magnitude;

            if (Mathf.Abs(localPos.z) < hoopThickness && distanceFromCenter < hoopRadius)
            {
                agentTargetIndices[agent]++;
                stepReward += 500f; // Massive reward for crossing

                // TODO change sensors to look at the next target (Sensors will pull from this class)
                if (agentTargetIndices[agent] < waypoints.Length)
                {
                    Transform nextHoop = waypoints[agentTargetIndices[agent]];
                    lastDistanceToHoop[agent] = Vector3.Distance(agent.transform.position, nextHoop.position);
                }
            }
        }

        // FIXED: Return the accumulated reward
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
