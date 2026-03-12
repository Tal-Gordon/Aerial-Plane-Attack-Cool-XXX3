using UnityEngine;

public class MaxAltitudeObjective
{
    [SerializeField]
    private float maxTimeAllowed = 10f;

    public float CalculateTotalFitness(JetAgent agent)
    {
        return agent.transform.position.y - agent.startingPosition.y;
    }

    public float GetStepReward(JetAgent agent)
    {
        return -1f;
    }

    public bool CheckTerminalState(JetAgent agent)
    {
        if (agent.hasCrashed)
            return true;

        if (agent.timeAlive > maxTimeAllowed)
            return true;

        if (agent.transform.position.y < agent.startingPosition.y - 5f)
            return true;

        return false;
    }
}
