using UnityEngine;
using System.Collections.Generic;

    // TODO Opus Note #2: This entire FixedUpdate body moves into EvolutionaryParadigm.Tick().
    // SimulationManager.FixedUpdate() should only call activeParadigm.Tick().
    // The paradigm owns aliveCount, generation boundaries, and agent resets.
    //
    // TODO Opus Note #3: RL paradigm's Tick() will look fundamentally different:
    // Evo = batch (wait for ALL dead → evolve → respawn all)
    // RL  = per-agent (agent dies → record reward → reset THAT agent immediately)
    // Both fit behind a single Tick() interface, but share no loop logic.
    // Zero-allocation cached snapshot
public class EvolutionaryParadigm : ITrainingParadigm
{
    private IEvolutionEngine engine;
    private IObjective objective;

    private SimulationSettings settings;
    private List<JetAgent> population;

    private int aliveCount = 0;
    private int currentGeneration = 1;

    private SimulationSnapshot cachedSnapshot;

    public EvolutionaryParadigm(IEvolutionEngine engine)
    {
        this.engine = engine;
    }

    public void Initialize(List<JetAgent> population, SimulationSettings settings, IObjective objective)
    {
        this.population = population;
        this.settings = settings;
        this.objective = objective;

        // Subscribe to UI Events
        EvoControlsWidget.OnMutationRateChanged += OnMutationRateChanged;
        EvoControlsWidget.OnLambdaChanged += OnLambdaChanged;

        // Initialize the cached snapshot exactly once
        cachedSnapshot = new SimulationSnapshot
        {
            ParadigmName = "Evolution",
            Population = this.population,
            EvoData = new EvoSnapshot
            {
                MutationRate = settings.ActiveEvoSettings.MutationRate,
                Lambda = settings.ActiveEvoSettings.Lambda,
            }
        };

        // Initialize the Engine and get the first batch of brains
        List<IEvolvableBrain> initialBrains = engine.InitializeGeneration(settings);
        
        // Assign the brains and properly spawn the jets for Generation 1
        for (int i = 0; i < population.Count; i++)
        {
            population[i].Brain = initialBrains[i];
            population[i].ResetAgent();
            objective.SetStartingState(population[i], i, population.Count);
            population[i].gameObject.SetActive(true);
        }
    }

    public void Tick()
    {
        if (aliveCount <= 0)
        {
            // Get fitness scores from all jets
            List<float> fitnessScores = new List<float>();
            foreach (JetAgent jet in population)
            {
                fitnessScores.Add(jet.CurrentFitness);
            }

            // Evolve the population
            List<IEvolvableBrain> evolvedBrains = engine.EvolveNextGeneration(fitnessScores);

            // Log statistics
            float maxScore = float.NegativeInfinity;
            float minScore = float.PositiveInfinity;
            JetAgent bestAgent = null;

            for (int i = 0; i < population.Count; i++)
            {
                float score = fitnessScores[i];
                if (score > maxScore)
                {
                    maxScore = score;
                    bestAgent = population[i];
                }
                if (score < minScore) minScore = score;
            }

            string breakdownStr = "";
            if (bestAgent != null)
            {
                var breakdown = objective.GetRewardBreakdown(bestAgent);
                if (breakdown != null && breakdown.Count > 0)
                {
                    float absSum = 0f;
                    foreach (var kvp in breakdown) absSum += Mathf.Abs(kvp.Value);

                    List<string> parts = new List<string>();
                    foreach (var kvp in breakdown)
                    {
                        float pct = absSum > 0 ? (Mathf.Abs(kvp.Value) / absSum) * 100f : 0f;
                        
                        // Gradient from gray to green
                        Color c = Color.Lerp(new Color(0.6f, 0.6f, 0.6f), Color.green, pct / 100f);
                        string hexColor = ColorUtility.ToHtmlStringRGB(c);
                        
                        parts.Add($"<color=#{hexColor}>{kvp.Key}: {kvp.Value:F1} ({pct:F0}%)</color>");
                    }
                    if (parts.Count > 0)
                    {
                        breakdownStr = "\nMax Reward Breakdown: " + string.Join(" | ", parts);
                    }
                }
            }

            Debug.Log($"<color=cyan>[EvolutionaryParadigm]</color> Generation {currentGeneration} complete. Max: {maxScore:F2} | Min: {minScore:F2} | Best So Far: {engine.GetChampionScore():F2}{breakdownStr}");

            // Assign the evolved brains to the population
            for (int i = 0; i < population.Count; i++)
            {
                population[i].Brain = evolvedBrains[i];

                population[i].ResetAgent();

                objective.SetStartingState(population[i], i, population.Count);

                population[i].gameObject.SetActive(true);
            }

            currentGeneration++;
            aliveCount = population.Count;
            return;
        }

        // If a jet dies, calculate its fitness and deactivate it
        foreach (JetAgent jet in population)
        {
            if (jet.gameObject.activeInHierarchy)
            {
                jet.CurrentFitness += objective.GetStepReward(jet);

                if (objective.CheckTerminalState(jet))
                {
                    jet.CurrentFitness = objective.CalculateTotalFitness(jet);
                    jet.gameObject.SetActive(false);
                    aliveCount--;
                }
            }
        }

        // Update the cached snapshot values
        cachedSnapshot.IterationNumber = currentGeneration;
        cachedSnapshot.AgentsAlive = aliveCount;
        cachedSnapshot.ChampionScore = engine.GetChampionScore();
        cachedSnapshot.EvoData.ChampionBrain = engine.GetChampionBrain();
        cachedSnapshot.EvoData.MutationRate = settings.ActiveEvoSettings.MutationRate;
        cachedSnapshot.EvoData.Lambda = settings.ActiveEvoSettings.Lambda;
    }

    public SimulationSnapshot GetSnapshot()
    {
        // Return the exact same memory reference every frame! Zero GC allocations.
        return cachedSnapshot;
    }

    public IBrain GetChampionBrain()
    {
        return engine.GetChampionBrain();
    }

    public float GetChampionScore()
    {
        return engine.GetChampionScore();
    }

    public void Dispose()
    {
        // MUST unsubscribe to prevent memory leaks from static events
        EvoControlsWidget.OnMutationRateChanged -= OnMutationRateChanged;
        EvoControlsWidget.OnLambdaChanged -= OnLambdaChanged;
    }

    public void SaveChampion(string directoryPath)
    {
        engine.SaveChampion(directoryPath);
    }

    // ── UI Event Listeners ───────────────────────────────────────────

    private void OnMutationRateChanged(float rate)
    {
        settings.ActiveEvoSettings.MutationRate = rate;
        cachedSnapshot.EvoData.MutationRate = rate; // Update snapshot instantly
    }

    private void OnLambdaChanged(float lambda)
    {
        settings.ActiveEvoSettings.Lambda = lambda;
        cachedSnapshot.EvoData.Lambda = lambda; // Update snapshot instantly
    }
}
