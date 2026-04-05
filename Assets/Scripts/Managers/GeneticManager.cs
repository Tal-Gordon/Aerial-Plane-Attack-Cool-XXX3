using System.Collections.Generic;
using UnityEngine;

public class GeneticManager : MonoBehaviour
{
    [Header("Simulation Setup")]
    private DataManager.GameMode activeMode;

    [Header("Prefabs")]
    public GameObject jetPrefab;

    [Header("Objective Setup")]
    [Tooltip("Drag a GameObject with an IObjective component (like FlightSchoolObjective) here.")]
    [SerializeField] private MonoBehaviour objectiveProvider;

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
        // First, find out WHAT we are flying (Initialize the Objective)
        InitializeObjective();

        if (currentObjective == null) return;

        // Discover the mode FROM the objective and load settings
        activeMode = currentObjective.Mode;

        // TODO REMOVE IN PRODUCTION
        // TEMPORARY FIX: Force the DataManager to wipe the old file and save the new defaults!
        currentSettings = DataManager.ResetToDefaults(activeMode);

        // LoadSettings for this specific mode
        currentSettings = DataManager.LoadSettings(activeMode);

        InitializePopulation();
        SpawnPopulation();
    }

    void FixedUpdate()
    {
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
                jet.CurrentFitness += currentObjective.GetStepReward(jet);

                if (currentObjective.CheckTerminalState(jet))
                {
                    //Debug.Log("get step fitness :" + jet.CurrentFitness);
                    jet.CurrentFitness = currentObjective.CalculateTotalFitness(jet);
                    //Debug.Log("total fitness :" + jet.CurrentFitness);
                    jet.gameObject.SetActive(false);
                    aliveCount--;
                }
            }
        }
    }

    private void InitializeObjective()
    {
        if (objectiveProvider == null)
        {
            Debug.LogError("[GeneticManager] No Objective Provider assigned! Drag an IObjective (like FlightSchoolObjective) into the inspector.");
            return;
        }

        currentObjective = objectiveProvider as IObjective;

        if (currentObjective == null)
        {
            Debug.LogError("[GeneticManager] The assigned Objective Provider does not implement IObjective!");
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