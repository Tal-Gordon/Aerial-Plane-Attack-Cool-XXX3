using System.Collections.Generic;
using UnityEngine;

public interface IObjective
{
    DataManager.GameMode Mode { get; }

    SensorType RequiredSensorType { get; }

    // TODO get rid of spawn radius, need to consult with Gordont
    public void SetStartingState(JetAgent agent, int index, int totalPopulation);

    public float GetStepReward(JetAgent agent);

    public float CalculateTotalFitness(JetAgent agent);

    public bool CheckTerminalState(JetAgent agent);

    public Dictionary<string, float> GetRewardBreakdown(JetAgent agent);

    /// <summary>
    /// Returns this objective's tunable reward/terminal parameters as a flat
    /// name → value map, for saving. Keys must match those read by
    /// <see cref="SetParameters"/>.
    /// </summary>
    public Dictionary<string, float> GetParameters();

    /// <summary>
    /// Applies previously-saved parameters (from <see cref="GetParameters"/>).
    /// Unknown keys are ignored; missing keys keep their current value.
    /// </summary>
    public void SetParameters(Dictionary<string, float> parameters);
}
