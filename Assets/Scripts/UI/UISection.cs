using UnityEngine;
using TMPro;

public class UISection : FoldablePanel
{
    [Header("UI Wiring")]
    [SerializeField] private TextMeshProUGUI headerLabel;

    [Header("Config")]
    [SerializeField] private string sectionTitle = "Section Title"; // default value

    protected override void Awake()
    {
        base.Awake();
        if (headerLabel) headerLabel.text = sectionTitle;
    }

    // Propagate Tick() downward to only this section's widgets
    public void TickWidgets(SimulationSnapshot snapshot)
    {
        if (IsFolded) return;
        foreach (var widget in GetComponentsInChildren<UIWidget>(includeInactive: false))
            widget.Tick(snapshot);
    }
}