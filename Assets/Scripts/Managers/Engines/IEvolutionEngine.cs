using System.Collections.Generic;

public interface IEvolutionEngine
{
    public List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings);

    public List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores);

    public IEvolvableBrain GetChampionBrain();

    public float GetChampionScore();

    /// <summary>
    /// Saves the champion brain to the given directory. Format is engine-specific.
    /// </summary>
    public void SaveChampion(string directoryPath);

    /// <summary>
    /// Loads a champion brain from the given directory. Format is engine-specific.
    /// </summary>
    public void LoadChampion(string directoryPath);

    /// <summary>
    /// Serializes the full evolvable state (every brain in the population, the
    /// champion, and its score) to a JSON string. Format is engine-specific and
    /// opaque to callers — it is stored verbatim inside a TrainingSaveData.
    /// </summary>
    public string CaptureState();

    /// <summary>
    /// Rebuilds the engine's brains from a <see cref="CaptureState"/> string and
    /// returns the restored population so the paradigm can re-assign it to the
    /// jets (same contract as InitializeGeneration/EvolveNextGeneration).
    /// </summary>
    public List<IEvolvableBrain> RestoreState(string stateJson, SimulationSettings settings);
}
