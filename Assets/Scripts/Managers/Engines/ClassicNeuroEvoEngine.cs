using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ClassicNeuroEvoEngine : IEvolutionEngine
{
    private SimulationSettings currentSettings;

    private List<IEvolvableBrain> currentBrains;
    private IEvolvableBrain topBrain;
    private float highestFitness;
    
    private int currentGeneration;

    public List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings)
    {
        currentSettings = settings;

        List<IEvolvableBrain> population = new List<IEvolvableBrain>();
        for (int i = 0; i < currentSettings.PopulationSize; i++)
        {
            population.Add(new NeuroEvoBrain(currentSettings.NetworkShape));
        }

        currentBrains = population;
        topBrain = new NeuroEvoBrain(currentSettings.NetworkShape);
        topBrain.Copy(population[0]);
        highestFitness = float.NegativeInfinity;
        currentGeneration = 1;
        
        return population;
    }

    public List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores)
    {
        // Don't evolve if the list is completely empty
        if (currentSettings.PopulationSize == 0) return currentBrains;

        // Bind the brains to their scores, sort descending, and hold them temporarily
        var sortedPairs = currentBrains
            .Select((brain, index) => new { Brain = brain, Score = fitnessScores[index] })
            .OrderByDescending(pair => pair.Score)
            .ToList();

        // Grab the highest score of this generation for our Elitism check
        float highestScoreThisGen = sortedPairs[0].Score;

        // Overwrite the main list with ONLY the sorted brains (throwing the scores away)
        currentBrains = sortedPairs.Select(pair => pair.Brain).ToList();

        // Keep track of the historical champion
        if (highestScoreThisGen > highestFitness || currentGeneration == 1)
        {
            // We have a new all-time champion (or it's the very first generation)
            topBrain.Copy(currentBrains[0]);
            highestFitness = fitnessScores[0];
        }
        else
        {
            // Elitism, keep the best brain from the previous generation
            currentBrains[0].Copy(topBrain);
        }

        // Mathf.Max prevents a Divide By Zero crash if you test with under 5 jets!
        int numParents = Mathf.Max(1, currentBrains.Count / 5);

        // Loop strictly through the list count!
        for (int i = numParents; i < currentSettings.PopulationSize; i++)
        {
            int parentIndex = i % numParents;

            IEvolvableBrain parentBrain = currentBrains[parentIndex];
            IEvolvableBrain loserBrain = currentBrains[i];

            loserBrain.Copy(parentBrain);
            loserBrain.Mutate(currentSettings.MutationRate);
        }

        currentGeneration++;
        return currentBrains;
    }
}
