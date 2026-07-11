using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; } // Singleton

    [Header("Simulation References")]
    [SerializeField] private SimulationManager simManager;

    [Header("UI Wiring (scene-authored mode)")]
    [SerializeField] private GameObject telemetryWindow;
    [SerializeField] private UISection[] sections;

    [Header("Dynamic build (set a config to build the window at runtime instead)")]
    [SerializeField] private TelemetryLayoutConfig layoutConfig;
    [Tooltip("Canvas to build under; found automatically when left empty.")]
    [SerializeField] private Canvas targetCanvas;

    public SimulationSnapshot Snapshot => snapshot;

    private SimulationSnapshot snapshot = new SimulationSnapshot();
    private UIWidget[] allWidgets;

    /// <summary>
    /// Pre-Awake injection for bootstrappers that create the UIManager from code
    /// (add the component on an inactive GameObject, call this, then activate).
    /// </summary>
    public void SetLayout(TelemetryLayoutConfig config, Canvas canvas)
    {
        layoutConfig = config;
        targetCanvas = canvas;
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (simManager == null)
            simManager = FindFirstObjectByType<SimulationManager>();

        if (layoutConfig != null)
            BuildFromConfig();
        else if (telemetryWindow != null)
        {
            // Scene-authored windows keep default visuals; theme in place. Their root
            // backdrop is usually ghostly semi-transparent white — over a bright sky it
            // washes out to light grey, so pin it to the solid window colour explicitly.
            UITheme.Skin(telemetryWindow);
            Image windowBackground = telemetryWindow.GetComponent<Image>();
            if (windowBackground != null)
                windowBackground.color = new Color(0.13f, 0.14f, 0.17f, 0.94f);

            // The drag strip isn't reliably named "TitleBar" in scene-authored windows,
            // so the skin's name hint can miss it and leave grey glass — find it by its
            // WindowDragHandle component and pin the solid title-bar colour.
            var dragHandle = telemetryWindow.GetComponentInChildren<WindowDragHandle>(includeInactive: true);
            if (dragHandle != null)
            {
                Image dragBar = dragHandle.GetComponent<Image>();
                if (dragBar != null) dragBar.color = new Color(0.08f, 0.11f, 0.16f, 1f);
            }
        }

        if (sections == null || sections.Length == 0)
            sections = GetComponentsInChildren<UISection>(includeInactive: true);

        if (allWidgets == null)
            allWidgets = GetComponentsInChildren<UIWidget>(includeInactive: true);

        foreach (var widget in allWidgets)
            widget.Initialize(this);
    }

    // Builds the telemetry window from the layout config. Sections and widgets come
    // from the build result (not a child scan) so the UIManager can live anywhere —
    // on the canvas, on a manager object, or on a bootstrap-created GameObject.
    private void BuildFromConfig()
    {
        Canvas canvas = targetCanvas;
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError($"{nameof(UIManager)}: no Canvas found to build the telemetry window under.");
            return;
        }

        TelemetryWindowBuilder.BuildResult result = TelemetryWindowBuilder.Build(canvas, layoutConfig);
        telemetryWindow = result.Window.gameObject;
        sections = result.Sections;
        allWidgets = result.Window.GetComponentsInChildren<UIWidget>(includeInactive: true);
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
        if (simManager == null) return; // scene without a simulation — widgets keep the last snapshot
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