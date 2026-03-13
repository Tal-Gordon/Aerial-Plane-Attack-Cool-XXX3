using UnityEngine;

public interface IObjective
{
    public float CalculateTotalFitness(JetAgent agent);

    public float GetStepReward(JetAgent agent);

    public bool CheckTerminalState(JetAgent agent);

    public void SetStartingState(JetAgent agent, int index, int totalPopulation, Vector3 centerPoint);
}
