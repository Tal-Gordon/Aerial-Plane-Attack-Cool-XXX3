using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The single Unity-side manager. Instantiates the population once,
/// creates the correct ITrainingParadigm, and pumps Tick() from FixedUpdate().
/// UI reads from this via GetSnapshot(). This replaces GeneticManager as the
/// top-level scene component.
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
        // 1. Resolve the objective
        objective = objectiveProvider as IObjective;
        if (objective == null)
        {
            Debug.LogError("[SimulationManager] Objective Provider is missing or does not implement IObjective!");
            return;
        }

        // 2. Load settings for this mode
        var mode = objective.Mode;
        settings = DataManager.LoadSettings(mode);

        // 3. Instantiate the population (factory — done once)
        population = InstantiatePopulation(settings.PopulationSize);

        // 4. Create the correct paradigm for the chosen AI type
        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        // 5. Hand the population & objective to the paradigm
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

    // ── Private helpers ──────────────────────────────────────────────

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
        // TODO: Instantiate the correct paradigm + engine pair.
        // For now returns null — fill in as paradigms are implemented.
        //
        // switch (type)
        // {
        //     case AIType.FixedNeuroEvo:
        //         return new EvolutionaryParadigm(new ClassicNeuroEvoEngine());
        //     case AIType.NEAT:
        //         return new EvolutionaryParadigm(new SharpNeatEngine());
        //     case AIType.PPO:
        //         return new RLParadigm();
        //     default:
        //         Debug.LogError($"[SimulationManager] Unsupported AI type: {type}");
        //         return null;
        // }

        Debug.LogWarning($"[SimulationManager] CreateParadigm not yet implemented for {type}. Returning null.");
        return null;
    }
}
