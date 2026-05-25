using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single Unity-side manager. Instantiates the population once,
/// creates the correct ITrainingParadigm, and pumps Tick() from FixedUpdate().
/// UI reads from this via GetSnapshot().
/// </summary>
public class SimulationManager : MonoBehaviour
{
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
        DataManager.ResetToDefaults(objective.Mode);

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
        // The paradigm owns the entire lifecycle: step rewards,
        // terminal checks, generation/episode boundaries, resets.
        activeParadigm?.Tick();
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

    // ── Private helpers ──────────────────────────────────────────────

    private void ConfigureSensors(List<JetAgent> population, IObjective objective)
    {
        SensorType required = objective.RequiredSensorType;

        foreach (var jetAgent in population)
        {
            ISensor activeSensor = null;

            foreach (var sensor in jetAgent.GetComponents<ISensor>())
            {
                if (sensor is MonoBehaviour mb)
                {
                    bool match = sensor.SensorType == required;
                    mb.enabled = match;
                    if (match) activeSensor = sensor;
                }
            }

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
