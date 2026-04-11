using UnityEngine;

public interface IObjective
{
    public DataManager.GameMode Mode { get; }

    // TODO get rid of spawn radius, need to consult with Gordont
    public void SetStartingState(JetAgent agent, int index, int totalPopulation);

    public float GetStepReward(JetAgent agent);

    public float CalculateTotalFitness(JetAgent agent);

    public bool CheckTerminalState(JetAgent agent);
}
