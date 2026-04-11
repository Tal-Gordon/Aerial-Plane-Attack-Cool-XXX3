using UnityEngine;
using System.Collections.Generic;

public interface IEvolutionEngine
{
    public List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings);

    public List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores);

    public IEvolvableBrain GetChampionBrain();

    public float GetChampionScore();
}
