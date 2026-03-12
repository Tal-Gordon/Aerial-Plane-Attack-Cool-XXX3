using UnityEngine;

[System.Serializable]
public class MaxAltitudeObjective : IObjective
{
    [SerializeField]
    private float maxTimeAllowed = 10f;

    public float CalculateTotalFitness(JetAgent agent)
    {
        return agent.transform.position.y - agent.startingPosition.y;
    }

    // TODO implement GetStepReward using the jet's rigid body
    public float GetStepReward(JetAgent agent)
    {
        return 0f;
    }

    public bool CheckTerminalState(JetAgent agent)
    {
        if (agent.hasCrashed)
            return true;

        if (agent.timeAlive > maxTimeAllowed)
            return true;

        // Stop the jet if it gets far below the starting point
        if (agent.transform.position.y < agent.startingPosition.y - 5f)
            return true;

        return false;
    }
}
