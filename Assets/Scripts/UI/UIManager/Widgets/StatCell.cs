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

    private void Awake()
    {
        // Uniform typography regardless of prefab-serialized auto-sizing: captions
        // otherwise render at different sizes per string length, so the tile row
        // looks ragged. Values keep auto-size (bounded) to fit "1111 / 1111".
        if (caption)
        {
            caption.enableAutoSizing = false;
            caption.fontSize = 12f;
            caption.color = UITheme.TextDimmed;
        }
        if (value)
        {
            value.enableAutoSizing = true;
            value.fontSizeMin = 9f;
            value.fontSizeMax = 17f;
            value.fontStyle = FontStyles.Bold;
            value.color = UITheme.TextColor;
        }
    }

    public void Set(string captionText, string valueText)
    {
        if (caption) caption.text = captionText;
        if (value)   value.text = valueText;
    }
}
