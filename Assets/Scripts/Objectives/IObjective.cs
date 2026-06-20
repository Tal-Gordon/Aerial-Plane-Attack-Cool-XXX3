using System.Collections.Generic;
using UnityEngine;

// IObjective extends ITunableParameters so its reward/terminal knobs
// (GetParameters / SetParameters / GetParameterDescriptors) plug straight into
// the generic ParameterTuner. Save/load already round-trips GetParameters().
public interface IObjective : ITunableParameters
{
    DataManager.GameMode Mode { get; }

    SensorType RequiredSensorType { get; }

    // TODO get rid of spawn radius, need to consult with Gordont
    public void SetStartingState(JetAgent agent, int index, int totalPopulation);

    public float GetStepReward(JetAgent agent);

    public float CalculateTotalFitness(JetAgent agent);

    public bool CheckTerminalState(JetAgent agent);

    public Dictionary<string, float> GetRewardBreakdown(JetAgent agent);
}
