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

    private JetAgent inferenceJet;

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
            ConfigureDecisionCadence(population[i], i);
            population[i].ResetAgent();
            objective.SetStartingState(population[i], i, population.Count);
            population[i].gameObject.SetActive(true);
        }
    }

    // Mirror the active decision cadence onto a spawned agent so evolutionary jets
    // query their brain every N ticks and hold the action in between (see
    // JetAgent.DecisionPeriod). The slot index phase-staggers decisions across the
    // population. Clamped to >= 1; defaults to 1 (decide every frame) when unset.
    private void ConfigureDecisionCadence(JetAgent agent, int index)
    {
        agent.DecisionPeriod = Mathf.Max(1, settings.ActiveEvoSettings?.DecisionPeriod ?? 1);
        agent.AgentIndex = index;
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
            float sumScore = 0f;
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
                sumScore += score;
            }

            // Capture this generation's mean and top final fitness once, here. The
            // snapshot then reports stable per-generation figures that stay
            // comparable to the champion score, instead of climbing from zero as the
            // next generation's freshly-spawned jets accumulate fitness frame by
            // frame. Both feed the headline AVG and the history graph's two lines.
            cachedSnapshot.EvoData.LastGenerationAverage =
                population.Count > 0 ? sumScore / population.Count : 0f;
            cachedSnapshot.EvoData.LastGenerationMax =
                population.Count > 0 ? maxScore : 0f;

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

                ConfigureDecisionCadence(population[i], i);

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

    public void SaveState()
    {
        // Gather population stats at save time
        float topScore = float.NegativeInfinity;
        float sum = 0f;
        foreach (JetAgent jet in population)
        {
            float fitness = jet.CurrentFitness;
            if (fitness > topScore) topScore = fitness;
            sum += fitness;
        }
        int count = population.Count;
        float average = count > 0 ? sum / count : 0f;
        if (count == 0) topScore = 0f;

        var data = new TrainingSaveData
        {
            AIType = settings.AIType,
            Mode = objective.Mode,
            Settings = settings.Clone(),
            ObjectiveParameters = objective.GetParameters(),
            Generation = currentGeneration,
            PopulationSize = count,
            ChampionScore = engine.GetChampionScore(),
            TopScore = topScore,
            AverageScore = average,
            SavedAtUtc = System.DateTime.UtcNow.ToString("o"),
            EngineState = engine.CaptureState(),
        };

        DataManager.SaveTrainingState(objective.Mode, settings.AIType, data);
    }

    public void LoadState()
    {
        TrainingSaveData data = DataManager.LoadTrainingState(objective.Mode, settings.AIType);
        if (data == null || string.IsNullOrEmpty(data.EngineState))
        {
            Debug.LogWarning($"[EvolutionaryParadigm] No restorable brain state for {objective.Mode}/{settings.AIType}; keeping the freshly initialized population.");
            return;
        }

        // Rebuild the engine's brains from the saved blob
        List<IEvolvableBrain> restoredBrains = engine.RestoreState(data.EngineState, settings);

        // Resume the generation counter so the run continues where it left off
        currentGeneration = Mathf.Max(1, data.Generation);

        // Assign restored brains and respawn the whole population for the next generation
        int n = Mathf.Min(population.Count, restoredBrains.Count);
        for (int i = 0; i < n; i++)
        {
            population[i].Brain = restoredBrains[i];
            ConfigureDecisionCadence(population[i], i);
            population[i].ResetAgent();
            objective.SetStartingState(population[i], i, population.Count);
            population[i].gameObject.SetActive(true);
        }
        aliveCount = n;

        // Refresh the snapshot so the UI reflects the loaded values immediately
        cachedSnapshot.IterationNumber = currentGeneration;
        cachedSnapshot.AgentsAlive = aliveCount;
        cachedSnapshot.ChampionScore = engine.GetChampionScore();
        cachedSnapshot.EvoData.ChampionBrain = engine.GetChampionBrain();
        cachedSnapshot.EvoData.MutationRate = settings.ActiveEvoSettings.MutationRate;
        cachedSnapshot.EvoData.Lambda = settings.ActiveEvoSettings.Lambda;

        Debug.Log($"<color=cyan>[EvolutionaryParadigm]</color> Loaded saved run for {objective.Mode}/{settings.AIType}. Resuming at generation {currentGeneration} with {n} brains | Champion: {engine.GetChampionScore():F2}");
    }

    public IBrain LoadChampionBrain()
    {
        TrainingSaveData data = DataManager.LoadTrainingState(objective.Mode, settings.AIType);
        if (data == null || string.IsNullOrEmpty(data.EngineState))
        {
            Debug.LogWarning($"[EvolutionaryParadigm] No saved champion to load for {objective.Mode}/{settings.AIType}.");
            return null;
        }

        // Rebuild the engine from the saved blob (this restores its champion too)
        // and hand back the champion. We discard the rest of the restored
        // population — the caller only flies the champion during inference.
        engine.RestoreState(data.EngineState, settings);
        return engine.GetChampionBrain();
    }

    // ── Inference replay ─────────────────────────────────────────────

    public bool CanRunInference => true;

    public bool StartInference()
    {
        IBrain champion = LoadChampionBrain();
        if (champion == null) return false;

        // The manager has spawned exactly one sensor-wired jet for us.
        inferenceJet = population[0];
        inferenceJet.Brain = champion;
        ConfigureDecisionCadence(inferenceJet, 0);
        inferenceJet.ResetAgent();
        objective.SetStartingState(inferenceJet, 0, 1);
        inferenceJet.gameObject.SetActive(true);

        // Repurpose the cached snapshot for the inference display.
        cachedSnapshot.ParadigmName = "Inference";
        cachedSnapshot.AgentsAlive = 1;
        cachedSnapshot.ChampionScore = engine.GetChampionScore();
        cachedSnapshot.EvoData.ChampionBrain = engine.GetChampionBrain();
        return true;
    }

    public void TickInference()
    {
        if (inferenceJet == null) return;
        if (!inferenceJet.gameObject.activeInHierarchy) return;

        // No learning, no evolution. We still pump the objective step exactly like
        // training does, because progression side-effects live there (e.g.
        // FlightSchool advances the target hoop and retargets the sensor inside
        // GetStepReward — skip it and the brain gets stale observations). The
        // reward is accumulated only so terminal checks that read it stay
        // consistent; ResetAgent clears it each loop.
        inferenceJet.CurrentFitness += objective.GetStepReward(inferenceJet);

        if (objective.CheckTerminalState(inferenceJet))
        {
            inferenceJet.ResetAgent();
            objective.SetStartingState(inferenceJet, 0, 1);
            inferenceJet.gameObject.SetActive(true);
        }

        cachedSnapshot.AgentsAlive = inferenceJet.gameObject.activeInHierarchy ? 1 : 0;
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
