using System.Collections.Generic;
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

    /// <summary>
    /// Enters or leaves inference mode (replay the saved champion/policy, no
    /// learning) for the currently loaded AI. Both calls are no-ops if already in
    /// the requested state, and entering may be refused (e.g. no saved run) — the
    /// UI should re-sync from the snapshot rather than assume the change took.
    /// </summary>
    public void SetInferenceMode(bool on)
    {
        if (on) simManager.EnterInferenceMode();
        else simManager.ExitInferenceMode();
    }

    /// <summary>
    /// Saves the full training state for the current mode + AI type, overwriting any
    /// previous save. No-op while no run is active.
    /// </summary>
    public void SaveState() => simManager.SaveState();

    /// <summary>
    /// Rebuilds the run from the saved state for the current mode + AI type. No-op
    /// (logs a warning) if there is no save to load.
    /// </summary>
    public void LoadState() => simManager.LoadState();

    /// <summary>
    /// Cold-path commit for the hyperparameter editor: bakes the staged values in,
    /// persists them, optionally saves progress, then reloads the scene.
    /// </summary>
    public void ApplyHyperparametersAndReload(Dictionary<string, float> staged, bool saveProgress)
        => simManager.ApplyHyperparametersAndReload(staged, saveProgress);

    /// <summary>Baked-default hyperparameter values for the current run (reset-to-default).</summary>
    public Dictionary<string, float> GetDefaultHyperparameters()
        => simManager.GetDefaultHyperparameters();

    /// <summary>Baked-default reward parameters for the current mode (reset-to-default).</summary>
    public Dictionary<string, float> GetDefaultRewardParameters()
        => simManager.GetDefaultRewardParameters();
}