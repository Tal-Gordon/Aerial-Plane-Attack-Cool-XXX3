using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// History graph for the telemetry window: two autoscaling lines, MAX and AVG,
/// over the run's progress. It samples once whenever the run advances —
///   Evolution : per generation, plotting that generation's top &amp; mean fitness
///   RL        : per episode-end (whenever a jet dies), plotting the MAX &amp; AVG of
///               the agents' most-recent episode scores
/// — then stretches the whole history across the graph area and rescales the axis
/// labels as it grows, so the line always spans the full width.
///
/// Note the graph's MAX line is the per-sample top performer (it fluctuates),
/// which is distinct from the headline BEST stat (the all-time record).
/// </summary>
public class RunGraphWidget : UIWidget
{
    [Header("Renderer")]
    [SerializeField] private TelemetryGraph graph;

    [Header("Axis labels (optional)")]
    [SerializeField] private TextMeshProUGUI yMaxLabel;
    [SerializeField] private TextMeshProUGUI yMinLabel;
    [SerializeField] private TextMeshProUGUI xMinLabel;
    [SerializeField] private TextMeshProUGUI xMaxLabel;
    [SerializeField] private TextMeshProUGUI xAxisTitleLabel;

    [Header("Legend (optional)")]
    [SerializeField] private TextMeshProUGUI maxLegendLabel;
    [SerializeField] private TextMeshProUGUI avgLegendLabel;

    [Header("Config")]
    [Tooltip("Cap on stored samples; history is halved (decimated) when exceeded, keeping full span at lower resolution.")]
    [SerializeField] private int maxSamples = 512;

    [Tooltip("Spawn axis-number / legend labels automatically for any slot left unwired above.")]
    [SerializeField] private bool autoCreateLabels = true;
    [SerializeField] private float labelFontSize = 10f;
    [SerializeField] private Color labelColor = new Color(0.85f, 0.86f, 0.92f, 0.95f);

    // Recorded history (parallel lists).
    private readonly List<float> maxHistory = new List<float>();
    private readonly List<float> avgHistory = new List<float>();
    private readonly List<int> iterationHistory = new List<int>();

    // Reused buffers handed to the renderer — no per-frame allocation once filled.
    private readonly List<Vector2> maxNorm = new List<Vector2>();
    private readonly List<Vector2> avgNorm = new List<Vector2>();

    private int lastSampledIteration = -1;
    private bool? lastWasRL;

    protected override void OnInitialize()
    {
        if (graph == null) graph = GetComponentInChildren<TelemetryGraph>(includeInactive: true);

        if (autoCreateLabels) EnsureLabels();

        // Colour the legend swatches to match the lines (constant text/colour).
        if (graph != null)
        {
            if (maxLegendLabel) { maxLegendLabel.text = "MAX"; maxLegendLabel.color = graph.MaxColor; }
            if (avgLegendLabel) { avgLegendLabel.text = "AVG"; avgLegendLabel.color = graph.AvgColor; }
        }
    }

    // Spawns a small TMP label in each corner for any slot the user didn't wire
    // by hand, so axis numbers + legend appear with zero manual setup. Anchored to
    // the graph rect's corners; positions are deliberately tight (the graph is
    // small) but readable, and overlap-free between the Y numbers (left) and X
    // numbers (bottom).
    private void EnsureLabels()
    {
        // Y scale on the left edge: high at top, low just above the X numbers.
        if (yMaxLabel == null) yMaxLabel = CreateLabel("YMax", new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -3), TextAlignmentOptions.TopLeft);
        if (yMinLabel == null) yMinLabel = CreateLabel("YMin", new Vector2(0, 0), new Vector2(0, 0), new Vector2(4, 14), TextAlignmentOptions.BottomLeft);

        // X range along the bottom: first sample left, latest right, unit centred.
        if (xMinLabel == null) xMinLabel = CreateLabel("XMin", new Vector2(0, 0), new Vector2(0, 0), new Vector2(4, 0), TextAlignmentOptions.BottomLeft);
        if (xMaxLabel == null) xMaxLabel = CreateLabel("XMax", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-4, 0), TextAlignmentOptions.BottomRight);
        if (xAxisTitleLabel == null) xAxisTitleLabel = CreateLabel("XTitle", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), TextAlignmentOptions.Bottom);

        // Legend stacked in the top-right corner.
        if (maxLegendLabel == null) maxLegendLabel = CreateLabel("MaxLegend", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -3), TextAlignmentOptions.TopRight);
        if (avgLegendLabel == null) avgLegendLabel = CreateLabel("AvgLegend", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -16), TextAlignmentOptions.TopRight);
    }

    private TextMeshProUGUI CreateLabel(string objectName, Vector2 anchor, Vector2 pivot, Vector2 pos, TextAlignmentOptions align)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(72f, 16f);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.fontSize = labelFontSize;
        label.color = labelColor;
        label.alignment = align;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

        return label;
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        if (snapshot == null || graph == null) return;

        // Inference is a frozen replay — nothing is progressing, so leave the last
        // training graph as-is and record nothing.
        bool isInference = snapshot.ParadigmName != null &&
                           snapshot.ParadigmName.StartsWith("Inference");
        if (isInference) return;

        bool isRL = snapshot.RLData != null;

        // A new/rebuilt run: paradigm flipped, or the iteration counter jumped
        // backwards (load, rebuild-from-scratch). Drop the old history and rebaseline.
        if (lastWasRL != isRL || snapshot.IterationNumber < lastSampledIteration)
            ResetHistory(isRL, snapshot.IterationNumber);

        lastWasRL = isRL;

        // First sighting of this run: baseline without recording (the "current"
        // generation/episode hasn't completed yet, so its stats aren't valid).
        if (lastSampledIteration < 0)
        {
            lastSampledIteration = snapshot.IterationNumber;
            return;
        }

        // Record one sample each time the run advances.
        if (snapshot.IterationNumber > lastSampledIteration)
        {
            if (TryGetSample(snapshot, isRL, out float max, out float avg))
            {
                Append(snapshot.IterationNumber, max, avg);
                Redraw(isRL);
            }
            lastSampledIteration = snapshot.IterationNumber;
        }
    }

    // Pulls the MAX/AVG for the just-completed iteration. Both paradigms now expose
    // the same shape: the top and the mean of a single per-unit quantity (a jet's
    // last-life score for RL, a generation's final fitness for evo), so the graph's
    // two lines are always directly comparable.
    private bool TryGetSample(SimulationSnapshot snapshot, bool isRL, out float max, out float avg)
    {
        max = 0f;
        avg = 0f;

        if (isRL)
        {
            if (snapshot.RLData == null) return false;
            max = snapshot.RLData.CurrentMax;
            avg = snapshot.RLData.CurrentAvg;
            return true;
        }

        if (snapshot.EvoData == null) return false;
        max = snapshot.EvoData.LastGenerationMax;
        avg = snapshot.EvoData.LastGenerationAverage;
        return true;
    }

    private void Append(int iteration, float max, float avg)
    {
        iterationHistory.Add(iteration);
        maxHistory.Add(max);
        avgHistory.Add(avg);

        if (iterationHistory.Count > maxSamples) Decimate();
    }

    // Keep every other sample so the full iteration span survives at half the
    // resolution, instead of dropping the oldest (which would shrink the window).
    private void Decimate()
    {
        int write = 0;
        for (int read = 0; read < iterationHistory.Count; read += 2, write++)
        {
            iterationHistory[write] = iterationHistory[read];
            maxHistory[write] = maxHistory[read];
            avgHistory[write] = avgHistory[read];
        }
        iterationHistory.RemoveRange(write, iterationHistory.Count - write);
        maxHistory.RemoveRange(write, maxHistory.Count - write);
        avgHistory.RemoveRange(write, avgHistory.Count - write);
    }

    private void Redraw(bool isRL)
    {
        int count = iterationHistory.Count;
        if (count == 0) { graph.Clear(); return; }

        // Y range spans both lines, with a hair of headroom so a flat run still
        // renders mid-graph instead of divide-by-zero.
        float yMin = float.PositiveInfinity, yMax = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            yMin = Mathf.Min(yMin, Mathf.Min(avgHistory[i], maxHistory[i]));
            yMax = Mathf.Max(yMax, Mathf.Max(avgHistory[i], maxHistory[i]));
        }
        float range = yMax - yMin;
        if (range < 0.0001f) { yMin -= 1f; yMax += 1f; range = yMax - yMin; }

        // With a single sample, full-range normalisation would pin MAX to the top
        // and AVG to the bottom — looking like a huge gap. Pad symmetrically so the
        // two points sit comfortably mid-graph until more data arrives.
        if (count == 1)
        {
            float mid = (yMin + yMax) * 0.5f;
            float half = Mathf.Max(yMax - yMin, 1f);
            yMin = mid - half; yMax = mid + half; range = yMax - yMin;
        }

        // X is index-based (0..1 across the width) so the line always covers the
        // whole area regardless of uneven iteration spacing; axis labels carry the
        // real iteration numbers.
        maxNorm.Clear();
        avgNorm.Clear();
        float denom = count > 1 ? count - 1 : 1;
        for (int i = 0; i < count; i++)
        {
            float x = count > 1 ? i / denom : 0f;
            maxNorm.Add(new Vector2(x, (maxHistory[i] - yMin) / range));
            avgNorm.Add(new Vector2(x, (avgHistory[i] - yMin) / range));
        }

        graph.SetData(avgNorm, maxNorm);
        UpdateLabels(isRL, yMin, yMax);
    }

    private void UpdateLabels(bool isRL, float yMin, float yMax)
    {
        if (yMaxLabel) yMaxLabel.text = Format(yMax);
        if (yMinLabel) yMinLabel.text = Format(yMin);
        if (xMinLabel && iterationHistory.Count > 0) xMinLabel.text = iterationHistory[0].ToString();
        if (xMaxLabel && iterationHistory.Count > 0) xMaxLabel.text = iterationHistory[iterationHistory.Count - 1].ToString();
        if (xAxisTitleLabel) xAxisTitleLabel.text = isRL ? "EPISODE" : "GEN";
    }

    private void ResetHistory(bool isRL, int iteration)
    {
        iterationHistory.Clear();
        maxHistory.Clear();
        avgHistory.Clear();
        lastSampledIteration = -1; // force rebaseline on the next tick
        graph.Clear();

        if (xAxisTitleLabel) xAxisTitleLabel.text = isRL ? "EPISODE" : "GEN";
        if (yMaxLabel) yMaxLabel.text = "";
        if (yMinLabel) yMinLabel.text = "";
        if (xMinLabel) xMinLabel.text = "";
        if (xMaxLabel) xMaxLabel.text = "";
    }

    // 1200 -> "1.2k", 1500000 -> "1.5M"; magnitude-based so negatives format too.
    private string Format(float value)
    {
        float mag = Mathf.Abs(value);
        if (mag >= 1_000_000f) return $"{value / 1_000_000f:F1}M";
        if (mag >= 1_000f)     return $"{value / 1_000f:F1}k";
        return $"{value:F0}";
    }
}
