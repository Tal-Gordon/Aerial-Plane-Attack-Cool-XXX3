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

    protected override void Awake()
    {
        base.Awake();
        if (headerLabel) headerLabel.text = sectionTitle;
        childWidgets = GetComponentsInChildren<UIWidget>(includeInactive: true);

        if (widgetSeperatorPrefab != null && childWidgets.Length > 1)
        {
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
        if (IsFolded) return;
        foreach (var widget in childWidgets)
        {
            if (widget.gameObject.activeInHierarchy)
            {
                widget.Tick(snapshot);
            }
        }
    }
}