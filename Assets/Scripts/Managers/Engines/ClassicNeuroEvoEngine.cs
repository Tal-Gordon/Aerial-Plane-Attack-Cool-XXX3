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

    // Reusable read-only snapshot of the previous generation. Offspring are bred
    // from this so tournament selection never copies a slot that was already
    // overwritten by an earlier offspring in the same loop.
    private List<NeuroEvoBrain> parentPool;

    private int currentGeneration;
    private int lastGenerationBestEliteIndex;

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

        // If the historical champion is older, retain BOTH it and this generation's
        // winner. Previously the champion overwrote slot zero and could erase the
        // latest winner despite elitism, leaving no exact jet for the camera to follow.
        if (highestScoreThisGen < championScore)
        {
            if (popSize > 1)
            {
                currentBrains[1].Copy(currentBrains[0]);
                eliteCount = Mathf.Max(eliteCount, 2);
                lastGenerationBestEliteIndex = 1;
            }
            else
            {
                lastGenerationBestEliteIndex = 0;
            }
            currentBrains[0].Copy(championBrain);
        }
        else
        {
            lastGenerationBestEliteIndex = 0;
        }

        // Snapshot the parents BEFORE we start overwriting offspring slots. Each
        // parentPool[i] mirrors the (now sorted, champion-injected) currentBrains[i]
        // and lines up with sortedScores[i]. Breeding reads only from this pool, so a
        // tournament winner is always the genome that earned its score — never a slot
        // already replaced by a mutated child earlier in the loop.
        if (parentPool == null || parentPool.Count != popSize)
        {
            int[] shape = currentSettings.NeuroEvoSettings.NetworkShape;
            parentPool = new List<NeuroEvoBrain>(popSize);
            for (int i = 0; i < popSize; i++)
                parentPool.Add(new NeuroEvoBrain(shape));
        }
        for (int i = 0; i < popSize; i++)
            parentPool[i].Copy(currentBrains[i]);

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

            // Copy the tournament winner (from the immutable parent snapshot) and mutate it
            currentBrains[i].Copy(parentPool[bestIdx]);
            currentBrains[i].Mutate(currentSettings.ActiveEvoSettings.MutationRate);
        }

        currentGeneration++;
        return new List<IEvolvableBrain>(currentBrains);
    }

    public int GetLastGenerationBestEliteIndex() => lastGenerationBestEliteIndex;

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

    public string CaptureState()
    {
        var state = new NeuroEvoEngineState
        {
            Shape = currentSettings.NeuroEvoSettings.NetworkShape,
            Population = new List<float[]>(currentBrains.Count),
            Champion = championBrain.Serialize(),
            ChampionScore = championScore,
            Generation = currentGeneration,
        };

        foreach (NeuroEvoBrain brain in currentBrains)
            state.Population.Add(brain.Serialize());

        return JsonConvert.SerializeObject(state);
    }

    public List<IEvolvableBrain> RestoreState(string stateJson, SimulationSettings settings)
    {
        currentSettings = settings;

        var state = JsonConvert.DeserializeObject<NeuroEvoEngineState>(stateJson);

        currentBrains = new List<NeuroEvoBrain>(state.Population.Count);
        foreach (float[] weights in state.Population)
        {
            var brain = new NeuroEvoBrain(state.Shape);
            brain.Deserialize(weights);
            currentBrains.Add(brain);
        }

        championBrain = new NeuroEvoBrain(state.Shape);
        championBrain.Deserialize(state.Champion);
        championScore = state.ChampionScore;
        currentGeneration = state.Generation;

        return new List<IEvolvableBrain>(currentBrains);
    }
}

/// <summary>
/// Serializable brain payload for ClassicNeuroEvoEngine. Stored as the opaque
/// EngineState string inside a TrainingSaveData.
/// </summary>
[Serializable]
public class NeuroEvoEngineState
{
    public int[] Shape;
    public List<float[]> Population;  // each brain's flattened weights + biases
    public float[] Champion;
    public float ChampionScore;
    public int Generation;
}
