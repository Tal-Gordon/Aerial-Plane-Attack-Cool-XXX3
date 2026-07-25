using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only main-menu view of one saved training run. Built at runtime so every
/// track and AI type shares the same report without scene-specific wiring.
/// </summary>
public sealed class TrainingStatsOverlay : MonoBehaviour
{
    private RectTransform content;
    private TextMeshProUGUI report;
    private ScrollRect scrollRect;

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
        report.text = BuildReport(mode, data);
        report.ForceMeshUpdate();

        float height = Mathf.Max(610f, report.preferredHeight + 36f);
        content.sizeDelta = new Vector2(0f, height);
        report.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, report.preferredHeight);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
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
        Center(panel.rectTransform, new Vector2(1180f, 900f));

        Image accent = AddImage(panel.transform, "AccentLine", UITheme.Accent);
        Top(accent.rectTransform, new Vector2(1180f, 3f), Vector2.zero);

        TextMeshProUGUI title = AddText(
            panel.transform, "Title", "TRAINING STATS",
            34f, UITheme.Accent, FontStyles.Bold);
        Top(title.rectTransform, new Vector2(1080f, 48f), new Vector2(0f, -24f));

        TextMeshProUGUI subtitle = AddText(
            panel.transform, "Subtitle",
            "A read-only snapshot of the selected saved run",
            17f, UITheme.TextDimmed, FontStyles.Normal);
        Top(subtitle.rectTransform, new Vector2(1080f, 30f), new Vector2(0f, -70f));

        Image scrollArea = AddImage(
            panel.transform, "ScrollArea", new Color(0.025f, 0.035f, 0.05f, 0.72f));
        Top(scrollArea.rectTransform, new Vector2(1070f, 690f), new Vector2(0f, -112f));

        scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 42f;

        var viewportObject = new GameObject(
            "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollArea.transform, false);
        RectTransform viewport = (RectTransform)viewportObject.transform;
        Stretch(viewport);
        viewport.offsetMin = new Vector2(0f, 0f);
        viewport.offsetMax = new Vector2(-18f, 0f);
        viewportObject.GetComponent<Image>().color = Color.clear;
        scrollRect.viewport = viewport;

        var contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        content = (RectTransform)contentObject.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        scrollRect.content = content;

        report = AddText(
            content, "Report", string.Empty,
            18f, UITheme.TextColor, FontStyles.Normal);
        RectTransform reportRect = report.rectTransform;
        reportRect.anchorMin = new Vector2(0f, 1f);
        reportRect.anchorMax = Vector2.one;
        reportRect.pivot = new Vector2(0.5f, 1f);
        reportRect.anchoredPosition = new Vector2(0f, -18f);
        reportRect.sizeDelta = new Vector2(-56f, 0f);
        report.alignment = TextAlignmentOptions.TopLeft;
        report.textWrappingMode = TextWrappingModes.Normal;
        report.richText = true;

        Image scrollbarTrack = AddImage(
            scrollArea.transform, "Scrollbar", new Color(1f, 1f, 1f, 0.08f));
        RectTransform scrollbarRect = scrollbarTrack.rectTransform;
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.sizeDelta = new Vector2(8f, 0f);
        scrollbarRect.anchoredPosition = Vector2.zero;

        var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleObject.transform.SetParent(scrollbarTrack.transform, false);
        RectTransform handleRect = (RectTransform)handleObject.transform;
        Stretch(handleRect);
        handleObject.GetComponent<Image>().color = UITheme.Accent;

        Scrollbar scrollbar = scrollbarTrack.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handleObject.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 8f;

        Button back = BuildButton(panel.transform, "Back", "BACK");
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0.5f, 0f);
        backRect.sizeDelta = new Vector2(250f, 52f);
        backRect.anchoredPosition = new Vector2(0f, 24f);
        back.onClick.AddListener(Hide);
        UITheme.StylePrimary(back);
    }

    private static string BuildReport(GameModeData mode, TrainingSaveData data)
    {
        var text = new StringBuilder(2500);
        string accent = ColorUtility.ToHtmlStringRGB(UITheme.Accent);
        string dimmed = ColorUtility.ToHtmlStringRGB(UITheme.TextDimmed);

        Section(text, "SUMMARY", accent);
        Row(text, "Track", !string.IsNullOrWhiteSpace(mode?.modeName) ? mode.modeName : data.Track, dimmed);
        Row(text, "Objective", data.Mode.DisplayName(), dimmed);
        Row(text, "AI", data.AIType.DisplayName(), dimmed);
        Row(text, "Saved", FormatSavedAt(data.SavedAtUtc), dimmed);
        Row(text, "Training time", data.TrainingElapsedSeconds > 0f
            ? FormatDuration(data.TrainingElapsedSeconds)
            : "Not recorded in this save", dimmed);
        Row(text, "Population", data.PopulationSize.ToString("N0"), dimmed);
        Row(text,
            data.AIType is AIType.PPO_MLAgents or AIType.SAC_MLAgents
                ? "Episodes completed"
                : "Current generation",
            data.Generation.ToString("N0"), dimmed);

        Section(text, "PERFORMANCE AT SAVE", accent);
        Row(text, "All-time best", FormatScore(data.ChampionScore), dimmed);
        Row(text, "Population top", FormatScore(data.TopScore), dimmed);
        Row(text, "Population average", FormatScore(data.AverageScore), dimmed);

        SimulationSettings settings = data.Settings;
        Section(text, "RUN CONFIGURATION", accent);
        if (settings == null)
        {
            text.AppendLine("Configuration was not present in this save.");
        }
        else
        {
            Row(text, "Configured population", settings.PopulationSize.ToString("N0"), dimmed);
            Row(text, "Spawn formation", settings.SpawnFormation.ToString(), dimmed);
            Row(text, "Spawn radius", Number(settings.SpawnRadius), dimmed);
            AppendAISettings(text, data.AIType, settings, dimmed);
        }

        Section(text, "OBJECTIVE PARAMETERS", accent);
        if (data.ObjectiveParameters == null || data.ObjectiveParameters.Count == 0)
        {
            text.AppendLine("No objective parameters were stored.");
        }
        else
        {
            var keys = new List<string>(data.ObjectiveParameters.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string key in keys)
                Row(text, FriendlyName(key), Number(data.ObjectiveParameters[key]), dimmed);
        }

        Section(text, "SAVE DETAILS", accent);
        bool hasState = !string.IsNullOrEmpty(data.EngineState);
        Row(text,
            data.AIType is AIType.PPO_MLAgents or AIType.SAC_MLAgents
                ? "Checkpoint reference"
                : "Population brain state",
            hasState ? "Stored" : "Missing", dimmed);
        if (hasState && data.AIType is AIType.FixedNeuroEvo or AIType.NEAT)
            Row(text, "Serialized state size", FormatBytes(
                Encoding.UTF8.GetByteCount(data.EngineState)), dimmed);
        Row(text, "Slot", $"{data.Track} / {data.AIType}", dimmed);

        return text.ToString();
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
        text.Append("<size=23><color=#")
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

    private static void Top(RectTransform rect, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }
}
