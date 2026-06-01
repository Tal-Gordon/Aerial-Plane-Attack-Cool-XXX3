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
}
