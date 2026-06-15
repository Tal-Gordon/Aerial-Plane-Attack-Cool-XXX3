using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The single Unity-side manager. Instantiates the population once,
/// creates the correct ITrainingParadigm, and pumps Tick() from FixedUpdate().
/// UI reads from this via GetSnapshot().
/// </summary>
public class SimulationManager : MonoBehaviour
{
    // TODO: Add a controls per step parameter
    [Header("Simulation Setup")]
    [SerializeField] private GameObject jetPrefab;

    [Header("Objective Setup")]
    [Tooltip("Drag a GameObject with an IObjective component here.")]
    [SerializeField] private MonoBehaviour objectiveProvider;

    // ── Runtime state ────────────────────────────────────────────────
    private ITrainingParadigm activeParadigm;
    private IObjective objective;
    private SimulationSettings settings;
    private List<JetAgent> population;
    private JetAgent selectedAgent;

    // ── Inference mode ───────────────────────────────────────────────
    // Inference reduces the run to a single jet that replays the saved champion /
    // policy on a loop, owned by the active paradigm. Training is fully torn down
    // while inference is active; nothing learns and nothing is saved. Pressing the
    // toggle key rebuilds the training run from the same save (LoadState).
    private bool inInferenceMode = false;

    private void Start()
    {
        // Resolve the objective
        objective = objectiveProvider as IObjective;
        if (objective == null)
        {
            Debug.LogError("[SimulationManager] Objective Provider is missing or does not implement IObjective!");
            return;
        }

        // TODO: Remove in production
        // DataManager.ResetToDefaults(objective.Mode);

        // Load settings for this mode
        var mode = objective.Mode;
        settings = DataManager.LoadSettings(mode);

        // Instantiate the population (factory — done once)
        population = InstantiatePopulation(settings.PopulationSize);

        // Activate the correct sensor on each agent for this mode
        ConfigureSensors(population, objective);

        // Create the correct paradigm for the chosen AI type
        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        // Hand the population & objective to the paradigm
        activeParadigm.Initialize(population, settings, objective);
    }

    private void FixedUpdate()
    {
        if (inInferenceMode)
        {
            // The paradigm owns the replay loop (evo pumps the objective + respawns;
            // RL is driven by ML-Agents and this is a no-op).
            activeParadigm?.TickInference();
            return;
        }

        // The paradigm owns the entire lifecycle: step rewards,
        // terminal checks, generation/episode boundaries, resets.
        activeParadigm?.Tick();
    }

    private void Update()
    {
        // Temporary hotkeys for testing save/load until UI buttons are wired up.
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Save/load only apply to a live training run, not during inference replay.
        if (!inInferenceMode)
        {
            if (keyboard.sKey.wasPressedThisFrame)
                SaveState();

            if (keyboard.lKey.wasPressedThisFrame)
                LoadState();
        }

        // Inference toggle: 'i' replays the saved champion, 't' resumes training.
        if (keyboard.iKey.wasPressedThisFrame)
            EnterInferenceMode();

        if (keyboard.tKey.wasPressedThisFrame)
            ExitInferenceMode();
    }

    private void OnDestroy()
    {
        activeParadigm?.Dispose();
    }

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a snapshot with paradigm data + manager-level fields.
    /// Called by UIManager every Update().
    /// </summary>
    public SimulationSnapshot GetSnapshot()
    {
        if (activeParadigm == null) return new SimulationSnapshot();

        // The paradigm fills its own snapshot for both training and inference.
        SimulationSnapshot snapshot = activeParadigm.GetSnapshot();

        // Manager stamps on the fields only IT knows about
        snapshot.TimeScale = Time.timeScale;
        snapshot.SelectedAgent = selectedAgent;

        return snapshot;
    }

    public void SelectAgent(JetAgent agent)
    {
        selectedAgent = agent;
    }

    public IBrain GetChampionBrain()
    {
        return activeParadigm?.GetChampionBrain();
    }

    public float GetChampionScore()
    {
        return activeParadigm.GetChampionScore();
    }

    public void SaveChampion()
    {
        if (activeParadigm == null) return;

        string dir = DataManager.ModePath(objective.Mode);
        DataManager.EnsureDirectory(dir);
        activeParadigm.SaveChampion(dir);
    }

    /// <summary>
    /// Saves the full training state (all brains + AI/objective params + stats)
    /// for the current mode and AI type, overwriting any previous save.
    /// </summary>
    public void SaveState()
    {
        if (activeParadigm == null) return;
        activeParadigm.SaveState();
    }

    /// <summary>
    /// Completely resets the simulation and rebuilds it from the saved run for
    /// the current mode + AI type: re-applies the saved settings and objective
    /// parameters, re-instantiates the population at the saved size, and loads
    /// the saved brains. Training then resumes normally.
    /// </summary>
    public void LoadState()
    {
        if (objective == null) return;

        TrainingSaveData data = DataManager.LoadTrainingState(objective.Mode, settings.AIType);
        if (data == null)
        {
            Debug.LogWarning($"[SimulationManager] No saved state found for {objective.Mode}/{settings.AIType}.");
            return;
        }

        // Tear down the current run
        activeParadigm?.Dispose();
        activeParadigm = null;
        DestroyPopulation();

        // Re-apply saved settings (population size, mutation rate, shape, ...)
        // and persist them so a later restart matches the loaded run.
        if (data.Settings != null)
        {
            settings = data.Settings;
            DataManager.SaveSettings(objective.Mode, settings);
        }

        // Re-apply saved objective parameters
        objective.SetParameters(data.ObjectiveParameters);

        // Rebuild the population at the saved size and re-wire sensors
        population = InstantiatePopulation(settings.PopulationSize);
        ConfigureSensors(population, objective);

        // Recreate the paradigm, seed it, then overwrite the fresh brains with the saved ones
        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        activeParadigm.Initialize(population, settings, objective);
        activeParadigm.LoadState();

        Debug.Log($"[SimulationManager] Loaded saved {settings.AIType} run for {objective.Mode}.");
    }

    private void DestroyPopulation()
    {
        if (population == null) return;

        foreach (JetAgent jet in population)
            if (jet != null) Destroy(jet.gameObject);

        population.Clear();
        selectedAgent = null;
    }

    // ── Inference mode ───────────────────────────────────────────────

    /// <summary>
    /// Tears down the training run, reduces the scene to a single jet, and hands
    /// that jet to the paradigm to replay the saved champion/policy on repeat.
    /// Only works when a saved run exists and the active paradigm supports
    /// inference. Triggering inference never writes a save of its own; the actual
    /// replay (and its per-step loop) is owned by the paradigm.
    /// </summary>
    public void EnterInferenceMode()
    {
        if (inInferenceMode) return;
        if (objective == null || activeParadigm == null) return;

        if (!activeParadigm.CanRunInference)
        {
            Debug.LogWarning($"[SimulationManager] Inference mode is not yet implemented for {settings.AIType}.");
            return;
        }

        // Inference replays a saved run, so one must exist first.
        if (!DataManager.HasTrainingState(objective.Mode, settings.AIType))
        {
            Debug.LogWarning($"[SimulationManager] No saved run for {objective.Mode}/{settings.AIType}. Train and save (S) before entering inference.");
            return;
        }

        // Stop training and clear the whole population off the screen, then rebuild
        // a fresh single-jet run for the paradigm to drive in inference.
        activeParadigm.Dispose();
        activeParadigm = null;
        DestroyPopulation();

        population = InstantiatePopulation(1);
        ConfigureSensors(population, objective);

        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        activeParadigm.Initialize(population, settings, objective);

        if (!activeParadigm.StartInference())
        {
            Debug.LogWarning("[SimulationManager] Could not start inference; rebuilding the training run from the save.");
            LoadState();
            return;
        }

        inInferenceMode = true;
        Debug.Log($"<color=yellow>[SimulationManager]</color> Inference ON for {objective.Mode}/{settings.AIType}. Press 'T' to resume training from the save.");
    }

    /// <summary>
    /// Leaves inference mode and rebuilds the training run from the same save the
    /// champion came from, so training continues exactly where it left off.
    /// </summary>
    public void ExitInferenceMode()
    {
        if (!inInferenceMode) return;

        inInferenceMode = false;

        // LoadState disposes the inference paradigm (evo: nothing; RL: kills the
        // inference trainer + recycles the Academy) and rebuilds the training run
        // from the save, resuming where it left off.
        LoadState();
        Debug.Log("<color=yellow>[SimulationManager]</color> Inference OFF. Resumed training from the saved run.");
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void ConfigureSensors(List<JetAgent> population, IObjective objective)
    {
        SensorType required = objective.RequiredSensorType;

        foreach (var jetAgent in population)
        {
            ISensor activeSensor = null;
            int matchCount = 0;

            foreach (var sensor in jetAgent.GetComponents<ISensor>())
            {
                if (sensor is MonoBehaviour mb)
                {
                    bool match = sensor.SensorType == required;
                    mb.enabled = match;
                    if (match)
                    {
                        matchCount++;
                        // Keep the FIRST match so this agrees with the GetComponent<T>()
                        // calls elsewhere (e.g. objectives writing currentWaypoint).
                        if (activeSensor == null) activeSensor = sensor;
                    }
                }
            }

            if (matchCount > 1)
                Debug.LogWarning($"[SimulationManager] {jetAgent.name} has {matchCount} sensors of type {required}. Only one per type is supported — remove the duplicates from the jet prefab, or the wrong instance may be used.");

            if (activeSensor != null)
                jetAgent.Sensor = activeSensor;
            else
                Debug.LogWarning($"[SimulationManager] No sensor of type {required} found on {jetAgent.name}. Add the matching sensor component to the jet prefab.");
        }
    }

    private List<JetAgent> InstantiatePopulation(int count)
    {
        var pop = new List<JetAgent>(count);

        for (int i = 0; i < count; i++)
        {
            GameObject jetObject = Instantiate(jetPrefab);
            JetAgent agent = jetObject.GetComponent<JetAgent>();
            pop.Add(agent);
        }

        return pop;
    }

    private ITrainingParadigm CreateParadigm(AIType type)
    {        
        switch (type)
        {
            case AIType.FixedNeuroEvo:
                return new EvolutionaryParadigm(new ClassicNeuroEvoEngine());
            case AIType.NEAT:
                return new EvolutionaryParadigm(new NeatEngine());
            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                return new RLParadigm();
            default:
                Debug.LogError($"[SimulationManager] Unsupported AI type: {type}");
                return null;
        }
    }
}
