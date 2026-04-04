using System.Collections.Generic;
using UnityEngine;

public class GeneticManager : MonoBehaviour
{
    [Header("Simulation Setup")]
    // Equipped flightschool (MaxAltitudeObjective) as the default for now
    public DataManager.GameMode activeMode = DataManager.GameMode.FlightSchool;

    public GameObject jetPrefab;

    private IObjective currentObjective;
    private List<JetAgent> population;
    private JetAgent topAgent;

    public int currentGeneration = 1;
    public int aliveCount = 0;

    private void Awake()
    {
        // Initializing the list early is still a good practice.
        // currentSettings will lazy-load on first access.
        population = new List<JetAgent>();
    }

    void Start()
    {
        // Accessing currentSettings here ensures it's loaded before we try to spawn.
        _ = currentSettings; 

        InitializeObjective();
        InitializePopulation();
        SpawnPopulation();
    }

    void Update()
    {
        // SAFETY FIX: Use <= to prevent skipping 0 if multiple die on the exact same frame!
        if (aliveCount <= 0)
        {
            currentGeneration++;
            EvolvePopulation();
            SpawnPopulation();
            return;
        }

        // If a jet dies, calculate its fitness and deactivate it
        foreach (JetAgent jet in population)
        {
            if (jet.gameObject.activeInHierarchy)
            {
                if (currentObjective.CheckTerminalState(jet))
                {
                    jet.CurrentFitness = currentObjective.CalculateTotalFitness(jet);
                    jet.gameObject.SetActive(false);
                    aliveCount--;
                }
            }
        }
    }

    private void InitializeObjective()
    {
        switch (activeMode)
        {
            case DataManager.GameMode.FlightSchool:
                currentObjective = new MaxAltitudeObjective();
                break;
            case DataManager.GameMode.Dogfight:
                // currentObjective = new DogfightObjective();
                break;
            default:
                Debug.LogError("Unknown GameMode selected!");
                break;
        }
    }

    private IEvolvableBrain CreateNewBrain()
    {
        switch (currentSettings.AIType)
        {
            case AIType.FixedNeuroEvo:
                return new NeuroEvoBrain(currentSettings.NetworkShape);
            case AIType.NEAT:
                // return new NEAT(currentSettings.NetworkShape);
                break; // Added break so it doesn't fall through to default while commented!
            default:
                Debug.LogError($"AI Type {currentSettings.AIType} is not supported by the GeneticManager!");
                return null;
        }
        return null;
    }

    private void InitializePopulation()
    {
        for (int i = 0; i < currentSettings.PopulationSize; i++)
        {
            GameObject newJetObject = Instantiate(jetPrefab);

            JetAgent newJetAgent = newJetObject.GetComponent<JetAgent>();
            newJetAgent.Brain = CreateNewBrain();
            population.Add(newJetAgent);
        }

        GameObject topAgentObject = Instantiate(jetPrefab);
        topAgentObject.SetActive(false);
        topAgentObject.name = "Top Agent";
        topAgent = topAgentObject.GetComponent<JetAgent>();
        topAgent.Copy(population[0]);
    }

    public void SpawnPopulation()
    {
        // Loop strictly through the list count, not the settings!
        for (int i = 0; i < population.Count; i++)
        {
            JetAgent jet = population[i];

            // Reset the jet in space
            currentObjective.SetStartingState(jet, i, population.Count, transform.position);

            // Activate the jet
            jet.gameObject.SetActive(true);

            // Reset stats from previous flight
            jet.ResetAgent();
        }

        aliveCount = population.Count;
    }

    public void EvolvePopulation()
    {
        // Don't evolve if the list is completely empty
        if (population.Count == 0) return;

        // Sort population based on fitness
        population.Sort((a, b) => b.CurrentFitness.CompareTo(a.CurrentFitness));

        // Mathf.Max prevents a Divide By Zero crash if you test with under 5 jets!
        int numParents = Mathf.Max(1, population.Count / 5);

        // Loop strictly through the list count!
        for (int i = numParents; i < population.Count; i++)
        {
            int parentIndex = i % numParents;

            JetAgent parent = population[parentIndex];
            JetAgent loser = population[i];

            if (parent.Brain is IEvolvableBrain parentBrain && loser.Brain is IEvolvableBrain loserBrain)
            {
                float[] winningWeights = parentBrain.ExtractWeights();
                loserBrain.InjectWeights(winningWeights);
                loserBrain.Mutate(currentSettings.MutationRate);
            }
        }

        Debug.Log($"Generation {currentGeneration} evolved. Champion Fitness: {population[0].CurrentFitness}");
        topAgent.Copy(population[0]);
    }

    public List<JetAgent> GetPopulation()
    {
        return population;  
    }

    public JetAgent GetTopAgent()
    {
        return topAgent;
    }

    // TODO: is this right?

    private SimulationSettings _currentSettings;
    private SimulationSettings currentSettings
    {
        get
        {
            if (_currentSettings == null)
            {
                _currentSettings = DataManager.LoadSettings(activeMode);
            }
            return _currentSettings;
        }
        set => _currentSettings = value;
    }

    public void SetMutationRate(float rate)
    {
        currentSettings.MutationRate = rate;
    }

    public void SetLambda(float lambda)
    {
        currentSettings.Lambda = lambda;
    }

    public float GetMutationRate()
    {
        return currentSettings.MutationRate;
    }

    public float GetLambda()
    {
        return currentSettings.Lambda;
    }
}