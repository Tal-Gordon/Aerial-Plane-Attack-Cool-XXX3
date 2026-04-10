using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; } // Singleton

    [Header("Simulation References")]
    [SerializeField] private GeneticManager geneticManager;
    /* [SerializeField] private RLManager rlManager; // maybe hold a reference to the highest AI abstraction class instead? IBrain or whatever? */

    [Header("UI Wiring")]
    [SerializeField] private GameObject telemetryWindow;
    [SerializeField] private UISection[] sections;

    public GeneticManager GeneticManager
    {
        get
        {
            return geneticManager;
        }
    }
    /* public RLManager RLManager
    {
        get
        {
            return rlManager;
        }
    } */
    public SimulationSnapshot Snapshot
    {
        get
        {
            return snapshot;
        }
    }

    private SimulationSnapshot snapshot = new SimulationSnapshot();
    private JetAgent selectedAgent;
    private UIWidget[] allWidgets;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (sections == null || sections.Length == 0)
            sections = GetComponentsInChildren<UISection>(includeInactive: true);

        allWidgets = GetComponentsInChildren<UIWidget>(includeInactive: true);
        foreach (var widget in allWidgets)
            widget.Initialize(this);
    }

    private void Update()
    {
        RefreshSnapshot();
        TickSections();
    }

    // Snapshot design pattern
    // TODO Opus Note #4: Replace this with simManager.GetSnapshot(). The paradigm
    // returns a ParadigmTelemetry (iteration, alive count, best score, paradigm name,
    // population, EvoData/RLData). SimulationManager wraps it into a full SimulationSnapshot
    // by adding manager-level fields like TimeScale and SelectedAgent.
    // This way UIManager never knows which paradigm is active.
    private void RefreshSnapshot()
    {
        snapshot.CurrentGeneration = geneticManager.currentGeneration;
        snapshot.AliveCount = geneticManager.aliveCount;
        snapshot.TimeScale = Time.timeScale;
        snapshot.Population = geneticManager.GetPopulation();
        snapshot.TopAgent = geneticManager.GetTopAgent();
        snapshot.SelectedAgent = selectedAgent;
        snapshot.MutationRate = geneticManager.GetMutationRate();
        snapshot.Lambda = geneticManager.GetLambda();
    }

    private void TickSections()
    {
        foreach (var section in sections)
            section.TickWidgets(snapshot);
    }

    public void SelectAgent(JetAgent agent)
    {
        if (selectedAgent == agent) return;

        foreach (var widget in allWidgets) widget.OnDeselected();
        selectedAgent = agent;
        foreach (var widget in allWidgets) widget.OnSelected(agent);
    }

    public void ClearSelection() => SelectAgent(null);

    public void SetTimeScale(float scale) => Time.timeScale = scale;
    public void TriggerEvolve() => geneticManager.EvolvePopulation();
    public void TriggerReset() { /* call reset on managers */ }
}