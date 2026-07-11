using UnityEngine;
using TMPro;

public class UISection : FoldablePanel
{
    [Header("UI Wiring")]
    [SerializeField] private TextMeshProUGUI headerLabel;

    [Header("Config")]
    [SerializeField] private string sectionTitle = "Section Title"; // default value
    [SerializeField] private GameObject widgetSeperatorPrefab;

    private UIWidget[] childWidgets;
    private bool separatorsBuilt;

    protected override void Awake()
    {
        base.Awake();
        if (headerLabel) headerLabel.text = sectionTitle;
        RebindWidgets();
    }

    /// <summary>
    /// Runtime setup for procedurally built sections (TelemetryWindowBuilder).
    /// Call after the section's widgets have been instantiated: sets the title,
    /// rescans child widgets and inserts separators between them.
    /// </summary>
    public void Configure(string title, TextMeshProUGUI header, GameObject separatorPrefab)
    {
        sectionTitle = title;
        if (header != null) headerLabel = header; // null keeps a prefab's own label
        widgetSeperatorPrefab = separatorPrefab;
        if (headerLabel) headerLabel.text = sectionTitle;
        RebindWidgets();
    }

    // Rescans children for widgets; inserts separators once, the first time more
    // than one widget is present (Awake for scene-authored sections, Configure for
    // built ones — whichever sees the widgets first).
    public void RebindWidgets()
    {
        childWidgets = GetComponentsInChildren<UIWidget>(includeInactive: true);

        if (!separatorsBuilt && widgetSeperatorPrefab != null && childWidgets.Length > 1)
        {
            separatorsBuilt = true;
            for (int i = 0; i < childWidgets.Length - 1; i++)
            {
                UIWidget currentWidget = childWidgets[i];
                if (currentWidget != null && currentWidget.transform.parent != null)
                {
                    GameObject seperator = Instantiate(widgetSeperatorPrefab, currentWidget.transform.parent);
                    seperator.name = "WidgetSeperator";
                    seperator.transform.SetSiblingIndex(currentWidget.transform.GetSiblingIndex() + 1);
                }
            }
        }
    }

    // Propagate Tick() downward to only this section's widgets
    public void TickWidgets(SimulationSnapshot snapshot)
    {
        if (IsFolded || childWidgets == null) return;
        foreach (var widget in childWidgets)
        {
            if (widget.gameObject.activeInHierarchy)
            {
                widget.Tick(snapshot);
            }
        }
    }
}
