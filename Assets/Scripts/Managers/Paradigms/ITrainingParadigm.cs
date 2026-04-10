using UnityEngine;

public interface ITrainingParadigm
{
    public void Initialize(SimulationSettings settings, IObjective objective);

    public void Tick();

    public SimulationSnapshot GetTelemetry();

    public void Dispose();
}
