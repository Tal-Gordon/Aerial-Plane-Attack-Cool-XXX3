using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; } // Singleton

    [Header("Simulation References")]
    [SerializeField] private SimulationManager simManager;

    [Header("UI Wiring")]
    [SerializeField] private GameObject telemetryWindow;
    [SerializeField] private UISection[] sections;

    public SimulationSnapshot Snapshot => snapshot;

    private SimulationSnapshot snapshot = new SimulationSnapshot();
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

    /// <summary>
    /// Single call to SimulationManager — paradigm fills its data,
    /// manager stamps TimeScale & SelectedAgent.
    /// UIManager doesn't know or care which paradigm is active.
    /// </summary>
    private void RefreshSnapshot()
    {
        snapshot = simManager.GetSnapshot();
    }

    private void TickSections()
    {
        foreach (var section in sections)
            section.TickWidgets(snapshot);
    }

    public void SelectAgent(JetAgent agent)
    {
        JetAgent previouslySelected = snapshot.SelectedAgent;
        if (previouslySelected == agent) return;

        foreach (var widget in allWidgets) widget.OnDeselected();
        simManager.SelectAgent(agent);
        foreach (var widget in allWidgets) widget.OnSelected(agent);
    }

    public void ClearSelection() => SelectAgent(null);

    public void SetTimeScale(float scale) => Time.timeScale = scale;
}