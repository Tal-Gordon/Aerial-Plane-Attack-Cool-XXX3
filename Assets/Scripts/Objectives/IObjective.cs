using System.Collections.Generic;
using UnityEngine;

// IObjective extends ITunableParameters so its reward/terminal knobs
// (GetParameters / SetParameters / GetParameterDescriptors) plug straight into
// the generic ParameterTuner. Save/load already round-trips GetParameters().
public interface IObjective : ITunableParameters
{
    DataManager.GameMode Mode { get; }

    SensorType RequiredSensorType { get; }

    // True when jets terminate independently over the course of an iteration
    // (e.g. crashing at different times), so an "alive" count is a meaningful,
    // gradually-changing metric. False when they all end on the same shared
    // condition (e.g. MaxAltitude's time limit), where alive is full-then-zero
    // and the UI should hide it. See SimulationSnapshot.TracksAttrition.
    bool TracksAttrition { get; }

    // TODO get rid of spawn radius, need to consult with Gordont
    public void SetStartingState(JetAgent agent, int index, int totalPopulation);

    public float GetStepReward(JetAgent agent);

    public float CalculateTotalFitness(JetAgent agent);

    public bool CheckTerminalState(JetAgent agent);

    public Dictionary<string, float> GetRewardBreakdown(JetAgent agent);
}
