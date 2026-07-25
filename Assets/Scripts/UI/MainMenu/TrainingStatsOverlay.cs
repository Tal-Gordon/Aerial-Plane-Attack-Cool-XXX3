using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only main-menu view of one saved training run. Built at runtime so every
/// track and AI type shares the same two-column, non-scrolling report without
/// scene-specific wiring.
/// </summary>
public sealed class TrainingStatsOverlay : MonoBehaviour
{
    private TextMeshProUGUI leftReport;
    private TextMeshProUGUI rightReport;
    private RectTransform historyPlot;
    private TextMeshProUGUI historyNoData;
    private TextMeshProUGUI historyYMax;
    private TextMeshProUGUI historyYMin;
    private TextMeshProUGUI historyXMin;
    private TextMeshProUGUI historyXMax;
    private readonly List<Vector2> historyMaxPoints = new List<Vector2>();
    private readonly List<Vector2> historyAveragePoints = new List<Vector2>();
    private readonly List<GameObject> historyPlotItems = new List<GameObject>();

    private readonly Image[] performanceBars = new Image[3];
    private readonly Image[] performanceZeroLines = new Image[3];
    private readonly TextMeshProUGUI[] performanceValues = new TextMeshProUGUI[3];

    private RectTransform networkDiagramRoot;
    private TextMeshProUGUI networkNoData;
    private readonly List<GameObject> networkItems = new List<GameObject>();

    private readonly Image[] histogramBars = new Image[12];
    private TextMeshProUGUI histogramMinimum;
    private TextMeshProUGUI histogramMaximum;
    private TextMeshProUGUI histogramSampleCount;
    private TextMeshProUGUI histogramNoData;

    public static TrainingStatsOverlay Ensure(Canvas canvas)
    {
        TrainingStatsOverlay existing = FindFirstObjectByType<TrainingStatsOverlay>(
            FindObjectsInactive.Include);
        if (existing != null) return existing;
        if (canvas == null) return null;

        var root = new GameObject("TrainingStatsOverlay", typeof(RectTransform));
        root.transform.SetParent(canvas.rootCanvas.transform, false);
        Stretch((RectTransform)root.transform);

        TrainingStatsOverlay overlay = root.AddComponent<TrainingStatsOverlay>();
        overlay.Build();
        root.SetActive(false);
        return overlay;
    }

    public void Show(GameModeData mode, TrainingSaveData data)
    {
        if (data == null) return;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        BuildReports(mode, data, out string left, out string right);
        leftReport.text = left;
        rightReport.text = right;
        RenderHistory(data);
        RenderPerformance(data);
        RenderNetwork(data);
        RenderHistogram(data);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Build()
    {
        Image dim = AddImage(transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
        Stretch(dim.rectTransform);
        Button dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Hide);

        Image panel = AddImage(transform, "Panel", UITheme.Panel);
        Center(panel.rectTransform, new Vector2(1320f, 930f));

        Image accent = AddImage(panel.transform, "AccentLine", UITheme.Accent);
        Top(accent.rectTransform, new Vector2(1320f, 3f), Vector2.zero);

        TextMeshProUGUI title = AddText(
            panel.transform, "Title", "TRAINING STATS",
            34f, UITheme.Accent, FontStyles.Bold);
        Top(title.rectTransform, new Vector2(1220f, 48f), new Vector2(0f, -24f));

        TextMeshProUGUI subtitle = AddText(
            panel.transform, "Subtitle",
            "A read-only snapshot of the selected saved run",
            17f, UITheme.TextDimmed, FontStyles.Normal);
        Top(subtitle.rectTransform, new Vector2(1220f, 30f), new Vector2(0f, -70f));

        Image reportArea = AddImage(
            panel.transform, "ReportArea", new Color(0.025f, 0.035f, 0.05f, 0.72f));
        Top(reportArea.rectTransform, new Vector2(1210f, 720f), new Vector2(0f, -112f));

        Image divider = AddImage(reportArea.transform, "ColumnDivider", UITheme.Hairline);
        RectTransform dividerRect = divider.rectTransform;
        dividerRect.anchorMin = dividerRect.anchorMax = dividerRect.pivot =
            new Vector2(0.5f, 0.5f);
        dividerRect.sizeDelta = new Vector2(1f, 410f);
        dividerRect.anchoredPosition = new Vector2(0f, 147f);
        divider.raycastTarget = false;

        leftReport = BuildReportColumn(
            reportArea.transform, "LeftColumn", new Vector2(-302.5f, 147f));
        rightReport = BuildReportColumn(
            reportArea.transform, "RightColumn", new Vector2(302.5f, 147f));
        BuildVisualDashboard(reportArea.transform);

        Button back = BuildButton(panel.transform, "Back", "BACK");
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0.5f, 0f);
        backRect.sizeDelta = new Vector2(250f, 52f);
        backRect.anchoredPosition = new Vector2(0f, 20f);
        back.onClick.AddListener(Hide);
        UITheme.StylePrimary(back);
    }

    private void BuildVisualDashboard(Transform parent)
    {
        Image sectionDivider = AddImage(parent, "VisualSectionDivider", UITheme.Hairline);
        Place(sectionDivider.rectTransform, new Vector2(1174f, 1f), new Vector2(0f, -78f));
        sectionDivider.raycastTarget = false;

        Image railDivider = AddImage(parent, "VisualRailDivider", UITheme.Hairline);
        Place(railDivider.rectTransform, new Vector2(1f, 250f), new Vector2(180f, -221f));
        railDivider.raycastTarget = false;

        AddDashboardDivider(parent, new Vector2(400f, 1f), new Vector2(387f, -177.5f));
        AddDashboardDivider(parent, new Vector2(400f, 1f), new Vector2(387f, -264.5f));

        Image historyCard = BuildCard(
            parent, "HistoryCard", new Vector2(760f, 250f), new Vector2(-207f, -221f));
        BuildHistoryCard(historyCard.transform);

        Image performanceCard = BuildCard(
            parent, "PerformanceCard", new Vector2(400f, 76f), new Vector2(387f, -134f));
        BuildPerformanceCard(performanceCard.transform);

        Image networkCard = BuildCard(
            parent, "NetworkCard", new Vector2(400f, 76f), new Vector2(387f, -221f));
        BuildNetworkCard(networkCard.transform);

        Image histogramCard = BuildCard(
            parent, "DistributionCard", new Vector2(400f, 76f), new Vector2(387f, -308f));
        BuildHistogramCard(histogramCard.transform);
    }

    private static void AddDashboardDivider(
        Transform parent, Vector2 size, Vector2 position)
    {
        Image divider = AddImage(parent, "DashboardDivider", UITheme.Hairline);
        Place(divider.rectTransform, size, position);
        divider.raycastTarget = false;
    }

    private void BuildHistoryCard(Transform parent)
    {
        TextMeshProUGUI title = AddText(
            parent, "Title", "TRAINING HISTORY", 17f, UITheme.Accent, FontStyles.Bold);
        Top(title.rectTransform, new Vector2(720f, 22f), new Vector2(-10f, -7f));
        title.alignment = TextAlignmentOptions.Left;

        AddLegend(parent, "MAX", new Color(0.35f, 0.90f, 0.45f), 186f);
        AddLegend(parent, "AVG", new Color(0.35f, 0.70f, 1f), 245f);
        AddLegend(parent, "BEST", new Color(1f, 0.72f, 0.24f), 305f);

        var plotObject = new GameObject("Plot", typeof(RectTransform));
        plotObject.transform.SetParent(parent, false);
        RectTransform plotRect = (RectTransform)plotObject.transform;
        Place(plotRect, new Vector2(720f, 195f), new Vector2(0f, -18f));

        var drawingArea = new GameObject("DrawingArea", typeof(RectTransform));
        drawingArea.transform.SetParent(plotObject.transform, false);
        historyPlot = (RectTransform)drawingArea.transform;
        Place(historyPlot, new Vector2(620f, 140f), new Vector2(30f, 5f));

        historyNoData = AddText(
            plotObject.transform, "NoHistory",
            "History will appear after this run completes an iteration and is saved.",
            15f, UITheme.TextDimmed, FontStyles.Normal);
        Stretch(historyNoData.rectTransform);

        historyYMax = AddGraphLabel(plotObject.transform, "YMax", new Vector2(-318f, 78f),
            TextAlignmentOptions.TopLeft);
        historyYMin = AddGraphLabel(plotObject.transform, "YMin", new Vector2(-318f, -58f),
            TextAlignmentOptions.BottomLeft);
        historyXMin = AddGraphLabel(plotObject.transform, "XMin", new Vector2(-318f, -80f),
            TextAlignmentOptions.BottomLeft);
        historyXMax = AddGraphLabel(plotObject.transform, "XMax", new Vector2(318f, -80f),
            TextAlignmentOptions.BottomRight);
    }

    private void AddLegend(Transform parent, string value, Color color, float x)
    {
        TextMeshProUGUI legend = AddText(
            parent, value + "Legend", value, 11f, color, FontStyles.Bold);
        Place(legend.rectTransform, new Vector2(52f, 18f), new Vector2(x, 105f));
    }

    private static TextMeshProUGUI AddGraphLabel(
        Transform parent, string name, Vector2 position, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = AddText(
            parent, name, string.Empty, 10f, UITheme.TextDimmed, FontStyles.Normal);
        Place(label.rectTransform, new Vector2(84f, 18f), position);
        label.alignment = alignment;
        return label;
    }

    private void DrawHistoryPlot(
        IReadOnlyList<Vector2> averagePoints,
        IReadOnlyList<Vector2> maxPoints,
        float? bestReference)
    {
        ClearHistoryPlot();

        const float left = -310f;
        const float right = 310f;
        const float bottom = -70f;
        const float top = 70f;

        Color axis = new Color(0.85f, 0.87f, 0.92f, 0.38f);
        AddHistorySegment("XAxis", new Vector2(left, bottom), new Vector2(right, bottom), 1.25f, axis);
        AddHistorySegment("YAxis", new Vector2(left, bottom), new Vector2(left, top), 1.25f, axis);

        if (bestReference.HasValue)
        {
            float y = Mathf.Lerp(bottom, top, Mathf.Clamp01(bestReference.Value));
            const int dashCount = 18;
            float dashCell = (right - left) / dashCount;
            Color bestColor = new Color(1f, 0.72f, 0.24f, 0.72f);
            for (int i = 0; i < dashCount; i += 2)
            {
                AddHistorySegment(
                    $"BestDash{i}",
                    new Vector2(left + i * dashCell, y),
                    new Vector2(left + (i + 1) * dashCell, y),
                    1.5f,
                    bestColor);
            }
        }

        DrawHistorySeries(
            averagePoints, "Average", new Color(0.35f, 0.70f, 1f), left, right, bottom, top);
        DrawHistorySeries(
            maxPoints, "Maximum", new Color(0.35f, 0.90f, 0.45f), left, right, bottom, top);
    }

    private void DrawHistorySeries(
        IReadOnlyList<Vector2> points,
        string name,
        Color color,
        float left,
        float right,
        float bottom,
        float top)
    {
        if (points == null || points.Count == 0) return;

        Vector2 ToPlot(Vector2 point) => new Vector2(
            Mathf.Lerp(left, right, Mathf.Clamp01(point.x)),
            Mathf.Lerp(bottom, top, Mathf.Clamp01(point.y)));

        if (points.Count == 1)
        {
            Vector2 point = ToPlot(points[0]);
            AddHistorySegment(name, point + Vector2.left * 4f, point + Vector2.right * 4f, 3f, color);
            return;
        }

        for (int i = 0; i < points.Count - 1; i++)
            AddHistorySegment(
                $"{name}{i}", ToPlot(points[i]), ToPlot(points[i + 1]), 2.5f, color);
    }

    private void AddHistorySegment(
        string name, Vector2 start, Vector2 end, float thickness, Color color)
    {
        Vector2 direction = end - start;
        float length = direction.magnitude;
        if (length < 0.001f) return;

        Image segment = AddImage(historyPlot, name, color);
        Place(segment.rectTransform, new Vector2(length, thickness), (start + end) * 0.5f);
        segment.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        segment.raycastTarget = false;
        historyPlotItems.Add(segment.gameObject);
    }

    private void ClearHistoryPlot()
    {
        foreach (GameObject item in historyPlotItems)
        {
            if (item == null) continue;
            item.SetActive(false);
            Destroy(item);
        }
        historyPlotItems.Clear();
    }

    private void BuildPerformanceCard(Transform parent)
    {
        TextMeshProUGUI title = AddText(
            parent, "Title", "PERFORMANCE", 14f, UITheme.Accent, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(190f, 18f), new Vector2(-95f, 27f));
        title.alignment = TextAlignmentOptions.Left;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;

        string[] names = { "BEST", "TOP", "AVG" };
        Color[] colors =
        {
            new Color(1f, 0.72f, 0.24f),
            new Color(0.35f, 0.90f, 0.45f),
            new Color(0.35f, 0.70f, 1f),
        };

        for (int i = 0; i < names.Length; i++)
        {
            float y = 9f - i * 18f;
            TextMeshProUGUI label = AddText(
                parent, names[i] + "Label", names[i], 10f, UITheme.TextDimmed, FontStyles.Bold);
            Place(label.rectTransform, new Vector2(42f, 15f), new Vector2(-169f, y));
            label.alignment = TextAlignmentOptions.Left;

            Image track = AddImage(parent, names[i] + "Track", new Color(1f, 1f, 1f, 0.08f));
            Place(track.rectTransform, new Vector2(210f, 10f), new Vector2(-33f, y));
            track.raycastTarget = false;

            Image bar = AddImage(track.transform, "Fill", colors[i]);
            bar.raycastTarget = false;
            performanceBars[i] = bar;

            Image zero = AddImage(track.transform, "Zero", new Color(1f, 1f, 1f, 0.5f));
            zero.raycastTarget = false;
            performanceZeroLines[i] = zero;

            TextMeshProUGUI number = AddText(
                parent, names[i] + "Value", string.Empty, 11f, UITheme.TextColor, FontStyles.Normal);
            Place(number.rectTransform, new Vector2(98f, 15f), new Vector2(143f, y));
            number.alignment = TextAlignmentOptions.Right;
            performanceValues[i] = number;
        }
    }

    private void BuildNetworkCard(Transform parent)
    {
        TextMeshProUGUI title = AddText(
            parent, "Title", "NETWORK", 14f, UITheme.Accent, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(118f, 18f), new Vector2(-132f, 27f));
        title.alignment = TextAlignmentOptions.Left;

        var root = new GameObject("Diagram", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        networkDiagramRoot = (RectTransform)root.transform;
        Place(networkDiagramRoot, new Vector2(370f, 45f), new Vector2(0f, -10f));

        networkNoData = AddText(
            root.transform, "NoNetwork", "Network shape was not stored.",
            12f, UITheme.TextDimmed, FontStyles.Normal);
        Stretch(networkNoData.rectTransform);
    }

    private void BuildHistogramCard(Transform parent)
    {
        TextMeshProUGUI title = AddText(
            parent, "Title", "SCORE DISTRIBUTION", 14f, UITheme.Accent, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(190f, 18f), new Vector2(-95f, 27f));
        title.alignment = TextAlignmentOptions.Left;

        histogramSampleCount = AddText(
            parent, "SampleCount", string.Empty, 10f, UITheme.TextDimmed, FontStyles.Normal);
        Place(histogramSampleCount.rectTransform, new Vector2(95f, 16f), new Vector2(147f, 27f));
        histogramSampleCount.alignment = TextAlignmentOptions.Right;

        // Keep a real gutter between the bars and their edge-value labels. The old
        // plot touched the label baseline, so a tall first/last bin collided with
        // the minimum/maximum text.
        Image plot = AddImage(parent, "HistogramPlot", Color.clear);
        Place(plot.rectTransform, new Vector2(350f, 30f), new Vector2(0f, -5f));
        plot.raycastTarget = false;

        const float gap = 2f;
        float width = (350f - gap * (histogramBars.Length + 1)) / histogramBars.Length;
        for (int i = 0; i < histogramBars.Length; i++)
        {
            Image bar = AddImage(plot.transform, $"Bin{i}", UITheme.Accent);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
            rect.anchoredPosition = new Vector2(gap + i * (width + gap), 0f);
            bar.raycastTarget = false;
            histogramBars[i] = bar;
        }

        histogramMinimum = AddText(
            parent, "Minimum", string.Empty, 9f, UITheme.TextDimmed, FontStyles.Normal);
        Place(histogramMinimum.rectTransform, new Vector2(85f, 13f), new Vector2(-146f, -30f));
        histogramMinimum.alignment = TextAlignmentOptions.Left;

        histogramMaximum = AddText(
            parent, "Maximum", string.Empty, 9f, UITheme.TextDimmed, FontStyles.Normal);
        Place(histogramMaximum.rectTransform, new Vector2(85f, 13f), new Vector2(146f, -30f));
        histogramMaximum.alignment = TextAlignmentOptions.Right;

        histogramNoData = AddText(
            plot.transform, "NoDistribution",
            "Distribution available after a completed iteration is saved.",
            11f, UITheme.TextDimmed, FontStyles.Normal);
        Stretch(histogramNoData.rectTransform);
    }

    private void RenderHistory(TrainingSaveData data)
    {
        TrainingRunHistory history = data.RunHistory;
        int count = history?.Count ?? 0;
        bool available = count > 0;
        historyPlot.gameObject.SetActive(available);
        historyNoData.gameObject.SetActive(!available);
        historyYMax.gameObject.SetActive(available);
        historyYMin.gameObject.SetActive(available);
        historyXMin.gameObject.SetActive(available);
        historyXMax.gameObject.SetActive(available);

        if (!available)
        {
            ClearHistoryPlot();
            return;
        }

        float yMin = float.PositiveInfinity;
        float yMax = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            yMin = Mathf.Min(yMin, history.MaxScores[i], history.AverageScores[i]);
            yMax = Mathf.Max(yMax, history.MaxScores[i], history.AverageScores[i]);
        }

        bool hasBest = !float.IsNaN(data.ChampionScore) && !float.IsInfinity(data.ChampionScore);
        if (hasBest)
        {
            yMin = Mathf.Min(yMin, data.ChampionScore);
            yMax = Mathf.Max(yMax, data.ChampionScore);
        }

        float range = yMax - yMin;
        if (range < 0.0001f)
        {
            float padding = Mathf.Max(1f, Mathf.Abs(yMax) * 0.1f);
            yMin -= padding;
            yMax += padding;
        }
        else
        {
            float padding = range * 0.06f;
            yMin -= padding;
            yMax += padding;
        }
        range = yMax - yMin;

        historyMaxPoints.Clear();
        historyAveragePoints.Clear();
        float denominator = count > 1 ? count - 1f : 1f;
        for (int i = 0; i < count; i++)
        {
            float x = count > 1 ? i / denominator : 0.5f;
            historyMaxPoints.Add(new Vector2(x, (history.MaxScores[i] - yMin) / range));
            historyAveragePoints.Add(new Vector2(x, (history.AverageScores[i] - yMin) / range));
        }

        DrawHistoryPlot(
            historyAveragePoints,
            historyMaxPoints,
            hasBest ? (data.ChampionScore - yMin) / range : (float?)null);

        historyYMax.text = FormatCompact(yMax);
        historyYMin.text = FormatCompact(yMin);
        historyXMin.text = history.Iterations[0].ToString("N0");
        historyXMax.text = history.Iterations[count - 1].ToString("N0");
    }

    private void RenderPerformance(TrainingSaveData data)
    {
        float[] values = { data.ChampionScore, data.TopScore, data.AverageScore };
        float minimum = 0f;
        float maximum = 0f;
        foreach (float value in values)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) continue;
            minimum = Mathf.Min(minimum, value);
            maximum = Mathf.Max(maximum, value);
        }

        float range = maximum - minimum;
        if (range < 0.0001f)
        {
            minimum -= 1f;
            maximum += 1f;
            range = maximum - minimum;
        }
        float zero = Mathf.Clamp01(-minimum / range);

        for (int i = 0; i < values.Length; i++)
        {
            float value = values[i];
            bool valid = !float.IsNaN(value) && !float.IsInfinity(value);
            float normalised = valid ? Mathf.Clamp01((value - minimum) / range) : zero;
            float start = Mathf.Min(zero, normalised);
            float end = Mathf.Max(zero, normalised);

            RectTransform bar = performanceBars[i].rectTransform;
            bar.anchorMin = new Vector2(start, 0f);
            bar.anchorMax = new Vector2(end, 1f);
            bar.offsetMin = bar.offsetMax = Vector2.zero;
            performanceBars[i].enabled = valid;

            RectTransform zeroLine = performanceZeroLines[i].rectTransform;
            zeroLine.anchorMin = zeroLine.anchorMax = new Vector2(zero, 0.5f);
            zeroLine.pivot = new Vector2(0.5f, 0.5f);
            zeroLine.sizeDelta = new Vector2(1f, 14f);
            zeroLine.anchoredPosition = Vector2.zero;

            performanceValues[i].text = valid ? FormatCompact(value) : "—";
        }
    }

    private void RenderNetwork(TrainingSaveData data)
    {
        foreach (GameObject item in networkItems)
        {
            if (item == null) continue;
            item.SetActive(false);
            Destroy(item);
        }
        networkItems.Clear();

        List<string> layers = GetNetworkLayers(data);
        bool available = layers.Count > 0;
        networkNoData.gameObject.SetActive(!available);
        if (!available) return;

        const float availableWidth = 356f;
        const float arrowWidth = 12f;
        float boxWidth = Mathf.Clamp(
            (availableWidth - arrowWidth * (layers.Count - 1)) / layers.Count,
            34f, 62f);
        float totalWidth = boxWidth * layers.Count + arrowWidth * (layers.Count - 1);
        float x = -totalWidth * 0.5f + boxWidth * 0.5f;

        for (int i = 0; i < layers.Count; i++)
        {
            bool edge = i == 0 || i == layers.Count - 1;
            Color fill = edge ? new Color(
                UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.82f) : UITheme.Field;
            Image box = MenuUI.Rounded(AddImage(
                networkDiagramRoot, $"Layer{i}", fill), 5);
            Place(box.rectTransform, new Vector2(boxWidth, 32f), new Vector2(x, -2f));
            box.raycastTarget = false;

            TextMeshProUGUI label = AddText(
                box.transform, "Label", layers[i], 10f,
                edge ? UITheme.TextOnAccent : UITheme.TextColor, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = 10f;
            networkItems.Add(box.gameObject);

            if (i < layers.Count - 1)
            {
                TextMeshProUGUI arrow = AddText(
                    networkDiagramRoot, $"Arrow{i}", "›", 18f,
                    UITheme.TextDimmed, FontStyles.Normal);
                Place(arrow.rectTransform, new Vector2(arrowWidth, 32f),
                    new Vector2(x + boxWidth * 0.5f + arrowWidth * 0.5f, -2f));
                networkItems.Add(arrow.gameObject);
                x += boxWidth + arrowWidth;
            }
        }
    }

    private static List<string> GetNetworkLayers(TrainingSaveData data)
    {
        var layers = new List<string>();
        SimulationSettings settings = data.Settings;
        if (settings == null) return layers;

        switch (data.AIType)
        {
            case AIType.FixedNeuroEvo:
            {
                int[] shape = settings.NeuroEvoSettings?.NetworkShape;
                if (shape == null || shape.Length == 0) break;
                for (int i = 0; i < shape.Length; i++)
                {
                    string kind = i == 0 ? "IN" : i == shape.Length - 1 ? "OUT" : "H";
                    layers.Add($"{kind}\n{shape[i]:N0}");
                }
                break;
            }
            case AIType.NEAT:
            {
                NeatSettings neat = settings.NeatSettings;
                if (neat == null) break;
                layers.Add($"IN\n{neat.InputSize:N0}");
                layers.Add("EVOLVING");
                layers.Add($"OUT\n{neat.OutputSize:N0}");
                break;
            }
            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
            {
                RLSettings rl = settings.RLSettings;
                if (rl == null) break;
                layers.Add($"IN\n{rl.InputSize:N0}");
                for (int i = 0; i < Mathf.Clamp(rl.NumLayers, 0, 5); i++)
                    layers.Add($"H\n{rl.HiddenUnits:N0}");
                layers.Add($"OUT\n{rl.OutputSize:N0}");
                break;
            }
        }

        if (layers.Count <= 7) return layers;
        return new List<string>
        {
            layers[0], layers[1], layers[2], "…",
            layers[layers.Count - 2], layers[layers.Count - 1],
        };
    }

    private void RenderHistogram(TrainingSaveData data)
    {
        ScoreDistributionData distribution = data.ScoreDistribution;
        bool available = distribution?.Bins != null
                         && distribution.Bins.Length > 0
                         && distribution.SampleCount > 0;
        histogramNoData.gameObject.SetActive(!available);
        histogramMinimum.gameObject.SetActive(available);
        histogramMaximum.gameObject.SetActive(available);
        histogramSampleCount.gameObject.SetActive(available);

        if (!available)
        {
            foreach (Image bar in histogramBars) bar.enabled = false;
            return;
        }

        int[] displayBins = new int[histogramBars.Length];
        for (int i = 0; i < distribution.Bins.Length; i++)
        {
            int target = Mathf.Min(
                displayBins.Length - 1,
                Mathf.FloorToInt(i * displayBins.Length / (float)distribution.Bins.Length));
            displayBins[target] += distribution.Bins[i];
        }

        int maximumBin = 1;
        foreach (int value in displayBins) maximumBin = Mathf.Max(maximumBin, value);
        for (int i = 0; i < histogramBars.Length; i++)
        {
            Image bar = histogramBars[i];
            bar.enabled = displayBins[i] > 0;
            RectTransform rect = bar.rectTransform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 28f * displayBins[i] / maximumBin);
        }

        histogramMinimum.text = FormatCompact(distribution.Minimum);
        histogramMaximum.text = FormatCompact(distribution.Maximum);
        histogramSampleCount.text = $"N={distribution.SampleCount:N0}";
    }

    private static TextMeshProUGUI BuildReportColumn(
        Transform parent, string name, Vector2 position)
    {
        TextMeshProUGUI column = AddText(
            parent, name, string.Empty, 18f, UITheme.TextColor, FontStyles.Normal);
        RectTransform rect = column.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(560f, 410f);
        rect.anchoredPosition = position;
        column.alignment = TextAlignmentOptions.TopLeft;
        column.textWrappingMode = TextWrappingModes.Normal;
        column.richText = true;
        column.enableAutoSizing = true;
        column.fontSizeMin = 9f;
        column.fontSizeMax = 18f;
        column.overflowMode = TextOverflowModes.Truncate;
        return column;
    }

    private static void BuildReports(
        GameModeData mode, TrainingSaveData data, out string leftReport, out string rightReport)
    {
        var left = new StringBuilder(1100);
        var right = new StringBuilder(1800);
        string accent = ColorUtility.ToHtmlStringRGB(UITheme.Accent);
        string dimmed = ColorUtility.ToHtmlStringRGB(UITheme.TextDimmed);

        Section(left, "SUMMARY", accent);
        Row(left, "Track", !string.IsNullOrWhiteSpace(mode?.modeName) ? mode.modeName : data.Track, dimmed);
        Row(left, "Objective", data.Mode.DisplayName(), dimmed);
        Row(left, "AI", data.AIType.DisplayName(), dimmed);
        Row(left, "Saved", FormatSavedAt(data.SavedAtUtc), dimmed);
        Row(left, "Training time", data.TrainingElapsedSeconds > 0f
            ? FormatDuration(data.TrainingElapsedSeconds)
            : "Not recorded in this save", dimmed);
        Row(left, "Population", data.PopulationSize.ToString("N0"), dimmed);
        Row(left,
            data.AIType is AIType.PPO_MLAgents or AIType.SAC_MLAgents
                ? "Episodes completed"
                : "Current generation",
            data.Generation.ToString("N0"), dimmed);

        Section(left, "PERFORMANCE AT SAVE", accent);
        Row(left, "All-time best", FormatScore(data.ChampionScore), dimmed);
        Row(left, "Population top", FormatScore(data.TopScore), dimmed);
        Row(left, "Population average", FormatScore(data.AverageScore), dimmed);

        Section(left, "SAVE DETAILS", accent);
        bool hasState = !string.IsNullOrEmpty(data.EngineState);
        Row(left,
            data.AIType is AIType.PPO_MLAgents or AIType.SAC_MLAgents
                ? "Checkpoint reference"
                : "Population brain state",
            hasState ? "Stored" : "Missing", dimmed);
        if (hasState && data.AIType is AIType.FixedNeuroEvo or AIType.NEAT)
            Row(left, "Serialized state size", FormatBytes(
                Encoding.UTF8.GetByteCount(data.EngineState)), dimmed);
        Row(left, "Slot", $"{data.Track} / {data.AIType}", dimmed);

        SimulationSettings settings = data.Settings;
        Section(right, "RUN CONFIGURATION", accent);
        if (settings == null)
        {
            right.AppendLine("Configuration was not present in this save.");
        }
        else
        {
            Row(right, "Configured population", settings.PopulationSize.ToString("N0"), dimmed);
            Row(right, "Spawn formation", settings.SpawnFormation.ToString(), dimmed);
            Row(right, "Spawn radius", Number(settings.SpawnRadius), dimmed);
            AppendAISettings(right, data.AIType, settings, dimmed);
        }

        Section(right, "OBJECTIVE PARAMETERS", accent);
        if (data.ObjectiveParameters == null || data.ObjectiveParameters.Count == 0)
        {
            right.AppendLine("No objective parameters were stored.");
        }
        else
        {
            var keys = new List<string>(data.ObjectiveParameters.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string key in keys)
                Row(right, FriendlyName(key), Number(data.ObjectiveParameters[key]), dimmed);
        }

        leftReport = left.ToString();
        rightReport = right.ToString();
    }

    private static void AppendAISettings(
        StringBuilder text, AIType aiType, SimulationSettings settings, string dimmed)
    {
        switch (aiType)
        {
            case AIType.FixedNeuroEvo:
            {
                NeuroEvoSettings evo = settings.NeuroEvoSettings;
                if (evo == null) break;
                Row(text, "Decision period", evo.DecisionPeriod.ToString(), dimmed);
                Row(text, "Mutation rate", Percent(evo.MutationRate), dimmed);
                Row(text, "Evolution lambda", Number(evo.Lambda), dimmed);
                Row(text, "Network shape", evo.NetworkShape != null
                    ? string.Join(" x ", evo.NetworkShape)
                    : "Not stored", dimmed);
                break;
            }
            case AIType.NEAT:
            {
                NeatSettings neat = settings.NeatSettings;
                if (neat == null) break;
                Row(text, "Decision period", neat.DecisionPeriod.ToString(), dimmed);
                Row(text, "Inputs / outputs", $"{neat.InputSize} / {neat.OutputSize}", dimmed);
                Row(text, "Species", neat.SpecieCount.ToString(), dimmed);
                Row(text, "Elitism", Percent(neat.ElitismProportion), dimmed);
                Row(text, "Selection", Percent(neat.SelectionProportion), dimmed);
                Row(text, "Add-node mutation", Percent(neat.AddNodeMutationProbability), dimmed);
                Row(text, "Add-connection mutation", Percent(neat.AddConnectionMutationProbability), dimmed);
                Row(text, "Delete-connection mutation", Percent(neat.DeleteConnectionMutationProbability), dimmed);
                Row(text, "Weight mutation", Percent(neat.ConnectionWeightMutationProbability), dimmed);
                break;
            }
            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                AppendRLSettings(text, aiType, settings.RLSettings, dimmed);
                break;
        }
    }

    private static void AppendRLSettings(
        StringBuilder text, AIType aiType, RLSettings rl, string dimmed)
    {
        if (rl == null) return;

        Row(text, "Inputs / outputs", $"{rl.InputSize} / {rl.OutputSize}", dimmed);
        Row(text, "Hidden network", $"{rl.NumLayers} x {rl.HiddenUnits}", dimmed);
        Row(text, "Normalize observations", rl.Normalize ? "Yes" : "No", dimmed);
        Row(text, "Learning rate", Number(rl.LearningRate), dimmed);
        Row(text, "Batch / buffer", $"{rl.BatchSize:N0} / {rl.BufferSize:N0}", dimmed);
        Row(text, "Gamma", Number(rl.Gamma), dimmed);
        Row(text, "Time horizon", rl.TimeHorizon.ToString("N0"), dimmed);
        Row(text, "Decision period", rl.DecisionPeriod.ToString(), dimmed);
        Row(text, "Maximum steps", rl.MaxSteps.ToString("N0"), dimmed);
        Row(text, "Checkpoint interval", rl.CheckpointInterval.ToString("N0"), dimmed);

        if (aiType == AIType.PPO_MLAgents)
        {
            Row(text, "Beta", Number(rl.Beta), dimmed);
            Row(text, "Epsilon", Number(rl.Epsilon), dimmed);
            Row(text, "Lambda", Number(rl.Lambd), dimmed);
            Row(text, "Epochs", rl.NumEpoch.ToString(), dimmed);
        }
        else
        {
            Row(text, "Tau", Number(rl.Tau), dimmed);
            Row(text, "Steps per update", Number(rl.StepsPerUpdate), dimmed);
            Row(text, "Initial entropy coefficient", Number(rl.InitEntCoef), dimmed);
            Row(text, "Buffer initialization steps", rl.BufferInitSteps.ToString("N0"), dimmed);
        }

        Row(text, "Training time scale", Number(rl.TrainingTimeScale), dimmed);
        Row(text, "Trainer window", $"{rl.WindowWidth} x {rl.WindowHeight}", dimmed);
        Row(text, "Target frame rate", rl.TargetFrameRate.ToString(), dimmed);
    }

    private static void Section(StringBuilder text, string title, string accent)
    {
        if (text.Length > 0) text.AppendLine();
        text.Append("<size=19><color=#")
            .Append(accent)
            .Append("><b>")
            .Append(title)
            .AppendLine("</b></color></size>");
    }

    private static void Row(StringBuilder text, string label, string value, string dimmed)
    {
        text.Append("<color=#")
            .Append(dimmed)
            .Append('>')
            .Append(label)
            .Append(":</color>  ")
            .AppendLine(value ?? "Not available");
    }

    private static string FormatSavedAt(string value)
    {
        return DateTimeOffset.TryParse(value, out DateTimeOffset utc)
            ? utc.ToLocalTime().ToString("yyyy-MM-dd  HH:mm")
            : "Unknown";
    }

    private static string FormatDuration(float seconds)
    {
        TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0f, seconds));
        if (duration.TotalDays >= 1d)
            return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
        if (duration.TotalHours >= 1d)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
        return $"{duration.Minutes}m {duration.Seconds}s";
    }

    private static string FormatScore(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return "Not available";
        return value.ToString("N2");
    }

    private static string FormatCompact(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return "—";
        float magnitude = Mathf.Abs(value);
        if (magnitude >= 1_000_000f) return $"{value / 1_000_000f:0.#}M";
        if (magnitude >= 1_000f) return $"{value / 1_000f:0.#}k";
        if (magnitude > 0f && magnitude < 0.01f) return value.ToString("0.##E+0");
        return value.ToString("0.##");
    }

    private static string Number(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return "Not available";
        float magnitude = Mathf.Abs(value);
        return magnitude > 0f && magnitude < 0.001f
            ? value.ToString("0.###E+0")
            : value.ToString("0.######");
    }

    private static string Percent(float value) => $"{value * 100f:0.###}%";

    private static string FormatBytes(int bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024f:0.0} KB";
        return $"{bytes} B";
    }

    private static string FriendlyName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "Parameter";

        var result = new StringBuilder(key.Length + 8);
        result.Append(char.ToUpperInvariant(key[0]));
        for (int i = 1; i < key.Length; i++)
        {
            char current = key[i];
            if (char.IsUpper(current) && !char.IsUpper(key[i - 1]))
                result.Append(' ');
            result.Append(current);
        }
        return result.ToString();
    }

    private static Button BuildButton(
        Transform parent, string name, string label)
    {
        Image image = AddImage(parent, name, UITheme.Field);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UITheme.StyleSelectable(button);

        TextMeshProUGUI text = AddText(
            image.transform, "Label", label,
            19f, UITheme.TextColor, FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    private static Image BuildCard(
        Transform parent, string name, Vector2 size, Vector2 position)
    {
        // These are layout groups, not floating cards. A transparent surface keeps
        // the dashboard visually continuous with the report area; hairline dividers
        // provide structure without making the lower section look pasted on.
        Image card = AddImage(parent, name, Color.clear);
        Place(card.rectTransform, size, position);
        card.raycastTarget = false;
        return card;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI AddText(
        Transform parent, string name, string value,
        float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Top(RectTransform rect, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }
}
