using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

public class ClassicNeuroEvoEngine : IEvolutionEngine
{
    private SimulationSettings currentSettings;

    private List<NeuroEvoBrain> currentBrains;
    private NeuroEvoBrain championBrain;
    private float championScore;
    
    private int currentGeneration;

    public List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings)
    {
        currentSettings = settings;

        var neuroEvoSettings = currentSettings.NeuroEvoSettings;

        currentBrains = new List<NeuroEvoBrain>();
        for (int i = 0; i < currentSettings.PopulationSize; i++)
        {
            currentBrains.Add(new NeuroEvoBrain(neuroEvoSettings.NetworkShape));
        }

        championBrain = new NeuroEvoBrain(neuroEvoSettings.NetworkShape);
        championBrain.Copy(currentBrains[0]);
        championScore = float.NegativeInfinity;
        currentGeneration = 1;
        
        return new List<IEvolvableBrain>(currentBrains);
    }

    public List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores)
    {
        // Don't evolve if the list is completely empty
        if (currentSettings.PopulationSize == 0) return new List<IEvolvableBrain>(currentBrains);

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
        if (highestScoreThisGen > championScore || currentGeneration == 1)
        {
            // We have a new all-time champion (or it's the very first generation)
            championBrain.Copy(currentBrains[0]);
            championScore = fitnessScores[0];
        }
        else
        {
            // Elitism, keep the best brain from the previous generation
            currentBrains[0].Copy(championBrain);
        }

        // Mathf.Max prevents a Divide By Zero crash if you test with under 5 jets!
        int numParents = Mathf.Max(1, currentBrains.Count / 5);

        // Loop strictly through the list count!
        for (int i = numParents; i < currentSettings.PopulationSize; i++)
        {
            int parentIndex = i % numParents;

            NeuroEvoBrain parentBrain = currentBrains[parentIndex];
            NeuroEvoBrain loserBrain = currentBrains[i];

            loserBrain.Copy(parentBrain);
            loserBrain.Mutate(currentSettings.ActiveEvoSettings.MutationRate);
        }

        currentGeneration++;
        return new List<IEvolvableBrain>(currentBrains);
    }

    public IEvolvableBrain GetChampionBrain()
    {
        return championBrain;
    }

    public float GetChampionScore()
    {
        return championScore;
    }

    public void SaveChampion(string directoryPath)
    {
        try
        {
            string json = JsonConvert.SerializeObject(championBrain.Serialize(), Formatting.Indented);
            File.WriteAllText(Path.Combine(directoryPath, "champion.brain.json"), json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ClassicNeuroEvoEngine] Failed to save champion: {e.Message}");
        }
    }

    public void LoadChampion(string directoryPath)
    {
        try
        {
            string path = Path.Combine(directoryPath, "champion.brain.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("[ClassicNeuroEvoEngine] No saved champion found.");
                return;
            }

            string json = File.ReadAllText(path);
            float[] weights = JsonConvert.DeserializeObject<float[]>(json);
            championBrain.Deserialize(weights);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ClassicNeuroEvoEngine] Failed to load champion: {e.Message}");
        }
    }
}
