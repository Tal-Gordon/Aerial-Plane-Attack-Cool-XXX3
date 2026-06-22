using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// TODO: An old best jet might be worse but have a higher score due to change in hyperparameters, fix this.

public class MaxAltitudeObjective : MonoBehaviour, IObjective
{
    public DataManager.GameMode Mode => DataManager.GameMode.MaxAltitude;
    public SensorType RequiredSensorType => SensorType.BasicFlight;

    // Every jet ends on the same maxTimeAllowed limit, so the population doesn't
    // thin out gradually — an "alive" count would be full then snap to zero.
    public bool TracksAttrition => false;
    
    [SerializeField] private float maxTimeAllowed = 15f;
    private int spawnRadius = 0;
    [SerializeField] private float lambda = 10f;

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

    // To add a tunable parameter: add the field, an entry here, in SetParameters,
    // and a descriptor in GetParameterDescriptors below.
    public Dictionary<string, float> GetParameters() => new Dictionary<string, float>
    {
        { "lambda", lambda },
        { "maxTimeAllowed", maxTimeAllowed },
    };

    // Only lambda is a user-tunable dial. maxTimeAllowed is still saved/loaded via
    // GetParameters but intentionally NOT exposed here, so it can't be changed
    // from the UI. Descriptors are immutable metadata — built once and shared.
    private static readonly ParameterDescriptor[] Descriptors =
    {
        new ParameterDescriptor("lambda", "Effort Penalty (lambda)", 0f, 50f, 10f),
    };

    public IReadOnlyList<ParameterDescriptor> GetParameterDescriptors() => Descriptors;

    public void SetParameters(Dictionary<string, float> parameters)
    {
        if (parameters == null) return;
        if (parameters.TryGetValue("lambda", out float l)) lambda = l;
        if (parameters.TryGetValue("maxTimeAllowed", out float t)) maxTimeAllowed = t;
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
