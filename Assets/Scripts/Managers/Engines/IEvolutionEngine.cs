using System.Collections.Generic;

public interface IEvolutionEngine
{
    public List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings);

    public List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores);

    /// <summary>
    /// Index in the newly evolved population that carries the best brain from the
    /// generation most recently scored by <see cref="EvolveNextGeneration"/>.
    /// Used by spectator systems that want to follow the previous winner.
    /// </summary>
    public int GetLastGenerationBestEliteIndex();

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
