using UnityEngine;

public interface IObjective
{
    public DataManager.GameMode Mode { get; }

    public void SetStartingState(JetAgent agent, int index, int totalPopulation, Vector3 centerPoint);

    public float GetStepReward(JetAgent agent);

    public float CalculateTotalFitness(JetAgent agent);

    public bool CheckTerminalState(JetAgent agent);
}
