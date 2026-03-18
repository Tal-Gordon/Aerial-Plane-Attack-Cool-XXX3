using UnityEngine;
using UnityEngine.UIElements;

[System.Serializable]
public class MaxAltitudeObjective : IObjective
{
    [SerializeField]
    private float maxTimeAllowed = 10f;
    private int spawnRadius = 0;
    private float lambda = 0.2f;

    public float CalculateTotalFitness(JetAgent agent)
    {
        float heightScore = agent.transform.position.y - agent.StartingPosition.y;
        float l2Panelty = lambda * agent.TotalControlEffort;
        return heightScore - l2Panelty;
    }

    // TODO implement GetStepReward using the jet's rigid body
    public float GetStepReward(JetAgent agent)
    {
        return 0f;
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

    public void SetStartingState(JetAgent agent, int index, int totalPopulation, Vector3 centerPoint)
    {
        // Extract and calculate position of the jet
        Vector2 randomDisk = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPosition = centerPoint + new Vector3(randomDisk.x, 200f, randomDisk.y);

        // Move the jet to that position
        agent.transform.position = spawnPosition;

        // Face north
        agent.transform.rotation = Quaternion.identity;

        // Give it 150 speed so it doesn't stall, and clear any spin
        Rigidbody rb = agent.GetComponent<Rigidbody>();
        rb.linearVelocity = agent.transform.forward * 150;
        rb.angularVelocity = Vector3.zero;

        // Update the Jet's memory so CalculateTotalFitness works
        agent.StartingPosition = agent.transform.position;
    }
}
