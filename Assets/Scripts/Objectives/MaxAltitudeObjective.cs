using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MaxAltitudeObjective : MonoBehaviour, IObjective
{
    public DataManager.GameMode Mode => DataManager.GameMode.MaxAltitude;
    [SerializeField]
    private float maxTimeAllowed = 15f;
    private int spawnRadius = 0;
    private float lambda = 0.1f;

    // Previous state trackers
    private Dictionary<JetAgent, float> lastYPosition = new Dictionary<JetAgent, float>();
    private Dictionary<JetAgent, float> lastEffortSums = new Dictionary<JetAgent, float>();

    public void SetStartingState(JetAgent agent, int index, int totalPopulation)
    {
        // Extract and calculate position of the jet based on where this Objective component sits in the world
        Vector2 randomDisk = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = this.transform.position + new Vector3(randomDisk.x, 200f, randomDisk.y);

        // Move the jet to that position
        agent.transform.position = spawnPosition;

        // Update the Jet's memory
        agent.StartingPosition = agent.transform.position;

        // Face north
        agent.transform.rotation = Quaternion.identity;

        // Give it 150 speed so it doesn't stall, and clear any spin
        Rigidbody rb = agent.GetComponent<Rigidbody>();
        rb.linearVelocity = agent.transform.forward * 600;
        rb.angularVelocity = Vector3.zero;

        // Initialize trackers
        lastYPosition[agent] = spawnPosition.y;
        lastEffortSums[agent] = 0;
    }

    public float GetStepReward(JetAgent agent)
    {
        //if (!lastYPosition.ContainsKey(agent) || !lastEffortSums.ContainsKey(agent))
        //    return 0f;

        float currentY = agent.transform.position.y;
        float heightGained = currentY - lastYPosition[agent];
        lastYPosition[agent] = currentY;

        float currentEffort = agent.TotalControlEffort;
        float effortGained = currentEffort - lastEffortSums[agent];
        lastEffortSums[agent] = currentEffort;
        float l2Penalty = lambda * effortGained;

        return heightGained - l2Penalty;
    }
    public float CalculateTotalFitness(JetAgent agent)
    {
        float heightScore = agent.transform.position.y - agent.StartingPosition.y;
        float l2Penalty = lambda * agent.TotalControlEffort;
        return heightScore - l2Penalty;
    }

    public Dictionary<string, float> GetRewardBreakdown(JetAgent agent)
    {
        float heightScore = agent.transform.position.y - agent.StartingPosition.y;
        float l2Penalty = lambda * agent.TotalControlEffort;
        
        return new Dictionary<string, float>
        {
            { "Height", heightScore },
            { "Effort Penalty", -l2Penalty }
        };
    }

    public bool CheckTerminalState(JetAgent agent)
    {
        if (agent.HasCrashed)
            return true;

        if (agent.TimeAlive > maxTimeAllowed)
            return true;

        // Stop the jet if it gets far below the starting point
        //if (agent.transform.position.y < agent.StartingPosition.y - 50f)
        //    return true;

        return false;
    }
}
