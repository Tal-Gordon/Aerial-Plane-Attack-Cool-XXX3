using UnityEngine;
using TMPro;

/// <summary>
/// One caption-over-value stat tile (small descriptor above a big number).
/// Cloned from a single prefab by <see cref="GenerationStatsWidget"/>, so every
/// tile is identical by construction — no per-cell hand-wiring, no size drift.
/// </summary>
public class StatCell : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI caption;
    [SerializeField] private TextMeshProUGUI value;

    public void Set(string captionText, string valueText)
    {
        if (caption) caption.text = captionText;
        if (value)   value.text = valueText;
    }
}
