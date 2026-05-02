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
        int popSize = currentSettings.PopulationSize;

        // Don't evolve if the list is completely empty
        if (popSize == 0) return new List<IEvolvableBrain>(currentBrains);

        // Bind the brains to their scores, sort descending, and hold them temporarily
        var sortedPairs = currentBrains
            .Select((brain, index) => new { Brain = brain, Score = fitnessScores[index] })
            .OrderByDescending(pair => pair.Score)
            .ToList();

        // Grab the highest score of this generation for our Elitism check
        float highestScoreThisGen = sortedPairs[0].Score;

        // Overwrite the main list with ONLY the sorted brains (throwing the scores away)
        currentBrains = sortedPairs.Select(pair => pair.Brain).ToList();
        var sortedScores = sortedPairs.Select(pair => pair.Score).ToList();

        // Keep track of the historical champion
        if (highestScoreThisGen > championScore || currentGeneration == 1)
        {
            championBrain.Copy(currentBrains[0]);
            championScore = highestScoreThisGen;
        }

        // --- Elitism: preserve the top ceil(1%) of the population ---
        int eliteCount = Mathf.Max(1, Mathf.CeilToInt(popSize * 0.01f));

        // If historical champion didn't make it into the current elite, inject it at index 0
        if (highestScoreThisGen < championScore)
        {
            currentBrains[0].Copy(championBrain);
        }

        // --- Tournament selection for the remaining slots ---
        int tournamentSize = 5;
        System.Random rng = new System.Random();

        for (int i = eliteCount; i < popSize; i++)
        {
            // Pick tournamentSize random individuals and find the one with the best fitness
            int bestIdx = rng.Next(popSize);
            float bestFit = sortedScores[bestIdx];

            for (int t = 1; t < tournamentSize; t++)
            {
                int candidate = rng.Next(popSize);
                if (sortedScores[candidate] > bestFit)
                {
                    bestIdx = candidate;
                    bestFit = sortedScores[candidate];
                }
            }

            // Copy the tournament winner into this slot and mutate it
            currentBrains[i].Copy(currentBrains[bestIdx]);
            currentBrains[i].Mutate(currentSettings.ActiveEvoSettings.MutationRate);
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
