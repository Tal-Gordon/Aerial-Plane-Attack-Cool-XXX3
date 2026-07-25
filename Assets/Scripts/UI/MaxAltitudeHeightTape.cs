using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A fixed-pointer altitude tape for Max Altitude. The scale scrolls downward as
/// the highest active jet climbs, giving a quick visual sense of vertical progress.
/// </summary>
public sealed class MaxAltitudeHeightTape : MonoBehaviour
{
    private const float TickStepMetres = 250f;
    private const float TickSpacingPixels = 52f;
    private const int TickRadius = 7;

    private sealed class TickMark
    {
        public int Offset;
        public RectTransform Root;
        public Image Line;
        public TextMeshProUGUI Label;
    }

    private readonly List<TickMark> ticks = new();

    private SimulationManager simulationManager;
    private TextMeshProUGUI altitudeLabel;
    private float displayedAltitude;
    private bool hasAltitude;

    public static MaxAltitudeHeightTape Ensure(SimulationManager manager)
    {
        MaxAltitudeHeightTape existing = FindFirstObjectByType<MaxAltitudeHeightTape>(
            FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.simulationManager = manager;
            return existing;
        }

        // Keep this HUD independent from telemetry chrome. Some scene-authored
        // canvases clip or reorder their children during layout; a dedicated
        // non-interactive overlay guarantees the tape remains visible at either edge.
        Canvas canvas = CreateCanvas();

        var root = new GameObject("Max Altitude Height Tape", typeof(RectTransform));
        root.transform.SetParent(canvas.rootCanvas.transform, false);
        Stretch((RectTransform)root.transform);

        MaxAltitudeHeightTape tape = root.AddComponent<MaxAltitudeHeightTape>();
        tape.simulationManager = manager;
        tape.Build();
        return tape;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    private void Update()
    {
        if (!TryGetLeaderAltitude(out float leaderAltitude)) return;

        leaderAltitude = Mathf.Max(0f, leaderAltitude);
        if (!hasAltitude)
        {
            displayedAltitude = leaderAltitude;
            hasAltitude = true;
        }
        else
        {
            float follow = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            displayedAltitude = Mathf.Lerp(displayedAltitude, leaderAltitude, follow);
        }

        UpdateTape();
    }

    private bool TryGetLeaderAltitude(out float altitude)
    {
        altitude = float.NegativeInfinity;
        if (simulationManager == null)
            simulationManager = FindFirstObjectByType<SimulationManager>();
        if (simulationManager == null) return false;

        SimulationSnapshot snapshot = simulationManager.GetSnapshot();
        if (snapshot?.Population == null) return false;

        bool found = false;
        foreach (JetAgent jet in snapshot.Population)
        {
            if (jet == null || !jet.gameObject.activeInHierarchy || jet.HasCrashed)
                continue;
            if (jet.transform.position.y <= altitude) continue;

            altitude = jet.transform.position.y;
            found = true;
        }

        return found;
    }

    private void UpdateTape()
    {
        float baseAltitude =
            Mathf.Floor(displayedAltitude / TickStepMetres) * TickStepMetres;
        float fraction =
            (displayedAltitude - baseAltitude) / TickStepMetres;

        foreach (TickMark tick in ticks)
        {
            float tickAltitude = baseAltitude + tick.Offset * TickStepMetres;
            bool visible = tickAltitude >= 0f;
            tick.Root.gameObject.SetActive(visible);
            if (!visible) continue;

            tick.Root.anchoredPosition = new Vector2(
                0f,
                (tick.Offset - fraction) * TickSpacingPixels);

            int roundedAltitude = Mathf.RoundToInt(tickAltitude);
            bool major = roundedAltitude % 1000 == 0;
            bool medium = !major && roundedAltitude % 500 == 0;

            RectTransform lineRect = tick.Line.rectTransform;
            lineRect.sizeDelta = new Vector2(
                major ? 46f : medium ? 31f : 17f,
                major ? 3f : 2f);
            tick.Line.color = major
                ? UITheme.TextColor
                : medium
                    ? UITheme.TextDimmed
                    : new Color(
                        UITheme.TextDimmed.r,
                        UITheme.TextDimmed.g,
                        UITheme.TextDimmed.b,
                        0.55f);

            tick.Label.gameObject.SetActive(major || medium);
            if (major || medium)
                tick.Label.text = $"{roundedAltitude:N0}";
        }

        altitudeLabel.text = $"{Mathf.RoundToInt(displayedAltitude):N0} m";
    }

    private void Build()
    {
        Image panel = AddImage(
            transform, "AltitudeTapePanel",
            new Color(0.025f, 0.04f, 0.065f, 0.84f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot =
            new Vector2(0f, 0.5f);
        panelRect.sizeDelta = new Vector2(210f, 620f);
        panelRect.anchoredPosition = new Vector2(34f, 0f);

        Image accent = AddImage(panel.transform, "AccentLine", UITheme.Accent);
        Top(accent.rectTransform, new Vector2(210f, 3f), Vector2.zero);

        TextMeshProUGUI title = AddText(
            panel.transform, "Title", "LEADER ALTITUDE",
            18f, UITheme.Accent, FontStyles.Bold);
        Top(title.rectTransform, new Vector2(190f, 32f), new Vector2(0f, -18f));

        TextMeshProUGUI units = AddText(
            panel.transform, "Units", "METRES",
            12f, UITheme.TextDimmed, FontStyles.Normal);
        Top(units.rectTransform, new Vector2(190f, 22f), new Vector2(0f, -48f));

        var viewportObject = new GameObject(
            "TapeViewport", typeof(RectTransform), typeof(RectMask2D));
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewport = (RectTransform)viewportObject.transform;
        Center(viewport, new Vector2(182f, 520f));
        viewport.anchoredPosition = new Vector2(0f, -20f);

        for (int offset = -TickRadius; offset <= TickRadius; offset++)
            ticks.Add(BuildTick(viewport, offset));

        Image pointerBand = AddImage(
            panel.transform, "PointerBand",
            new Color(0.035f, 0.095f, 0.14f, 0.96f));
        Center(pointerBand.rectTransform, new Vector2(182f, 46f));
        pointerBand.rectTransform.anchoredPosition = new Vector2(0f, -20f);

        Image pointerLine = AddImage(
            pointerBand.transform, "PointerLine", UITheme.Accent);
        Center(pointerLine.rectTransform, new Vector2(174f, 2f));

        Image pointer = AddImage(
            pointerBand.transform, "Pointer", UITheme.Accent);
        Center(pointer.rectTransform, new Vector2(12f, 12f));
        pointer.rectTransform.anchoredPosition = new Vector2(-78f, 0f);
        pointer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        Image valueBackground = AddImage(
            pointerBand.transform, "ValueBackground",
            new Color(0.02f, 0.035f, 0.055f, 0.98f));
        Center(valueBackground.rectTransform, new Vector2(132f, 38f));
        valueBackground.rectTransform.anchoredPosition = new Vector2(18f, 0f);

        altitudeLabel = AddText(
            valueBackground.transform, "AltitudeValue", "0 m",
            21f, UITheme.TextColor, FontStyles.Bold);
        Stretch(altitudeLabel.rectTransform);
    }

    private static TickMark BuildTick(RectTransform viewport, int offset)
    {
        var rootObject = new GameObject($"Tick {offset}", typeof(RectTransform));
        rootObject.transform.SetParent(viewport, false);
        RectTransform root = (RectTransform)rootObject.transform;
        root.anchorMin = new Vector2(0f, 0.5f);
        root.anchorMax = new Vector2(1f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(0f, 24f);

        Image line = AddImage(root, "Line", UITheme.TextDimmed);
        RectTransform lineRect = line.rectTransform;
        lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot =
            new Vector2(1f, 0.5f);
        lineRect.sizeDelta = new Vector2(17f, 2f);
        lineRect.anchoredPosition = new Vector2(-10f, 0f);

        TextMeshProUGUI label = AddText(
            root, "Label", string.Empty,
            15f, UITheme.TextColor, FontStyles.Bold);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot =
            new Vector2(1f, 0.5f);
        labelRect.sizeDelta = new Vector2(105f, 23f);
        labelRect.anchoredPosition = new Vector2(-61f, 0f);
        label.alignment = TextAlignmentOptions.MidlineRight;

        return new TickMark
        {
            Offset = offset,
            Root = root,
            Line = line,
            Label = label,
        };
    }

    private static Canvas CreateCanvas()
    {
        var root = new GameObject(
            "Max Altitude HUD Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
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
