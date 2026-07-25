using UnityEngine;
using UnityEngine.UI;
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
        if (headerLabel)
        {
            headerLabel.text = sectionTitle;
            FixHeaderLabelLayout();
        }
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
        if (headerLabel)
        {
            headerLabel.text = sectionTitle;
            FixHeaderLabelLayout();
        }
        RebindWidgets();
    }

    /// <summary>
    /// Keeps section names on one line in both the compact runtime chrome and the
    /// older scene-authored Canvas prefab. The latter serialized a narrow fixed
    /// Title rect, which made "Simulation Controls" wrap despite spare header room.
    /// </summary>
    private void FixHeaderLabelLayout()
    {
        headerLabel.textWrappingMode = TextWrappingModes.NoWrap;
        headerLabel.overflowMode = TextOverflowModes.Overflow;

        RectTransform rect = headerLabel.rectTransform;
        HorizontalLayoutGroup layout = rect.parent != null
            ? rect.parent.GetComponent<HorizontalLayoutGroup>()
            : null;
        if (layout != null)
        {
            LayoutElement element = headerLabel.GetComponent<LayoutElement>();
            if (element == null) element = headerLabel.gameObject.AddComponent<LayoutElement>();
            element.minWidth = 0f;
            element.flexibleWidth = 1f;
            return;
        }

        // Preserve every serialized RectTransform value in scene-authored headers.
        // Their 287 px title field is slightly too narrow at 36 pt, so allow TMP to
        // shrink only as much as needed instead of rewriting anchors at Awake.
        float authoredSize = headerLabel.fontSize;
        headerLabel.enableAutoSizing = true;
        headerLabel.fontSizeMax = authoredSize;
        headerLabel.fontSizeMin = Mathf.Min(18f, authoredSize);
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
                    // The prefab's solid-black line vanishes on the dark theme — hairline it.
                    Image line = seperator.GetComponentInChildren<Image>(includeInactive: true);
                    if (line != null) line.color = UITheme.Hairline;
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
