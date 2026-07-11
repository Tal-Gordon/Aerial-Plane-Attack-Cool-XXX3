using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the telemetry window at runtime from a <see cref="TelemetryLayoutConfig"/>:
/// window shell (draggable title bar + fold button + scroll view), one foldable
/// <see cref="UISection"/> per enabled section definition, and the section's widget
/// prefabs in list order. Widgets marked with <see cref="PopupRoot"/> children get
/// those children promoted to the canvas root so dialogs aren't clipped by the
/// scroll view.
///
/// Chrome and sections are plain code-built visuals unless the config supplies
/// hand-authored prefabs for them; widgets are always prefabs (their internals are
/// too bespoke to be worth generating).
/// </summary>
public static class TelemetryWindowBuilder
{
    public struct BuildResult
    {
        public TelemetryWindow Window;
        public UISection[] Sections;
    }

    public static BuildResult Build(Canvas canvas, TelemetryLayoutConfig config)
    {
        TelemetryWindow window;
        RectTransform sectionParent;

        if (config.windowChromePrefab != null)
        {
            window = Object.Instantiate(config.windowChromePrefab, canvas.transform);
            window.name = "TelemetryWindow";
            ScrollRect scroll = window.GetComponentInChildren<ScrollRect>(includeInactive: true);
            sectionParent = scroll != null && scroll.content != null
                ? scroll.content
                : (RectTransform)window.transform;
        }
        else
        {
            window = BuildWindowChrome(canvas, config, out sectionParent);
        }

        var sections = new List<UISection>();
        foreach (TelemetryLayoutConfig.SectionDefinition def in config.sections)
        {
            if (def == null || !def.enabled) continue;
            sections.Add(BuildSection(def, sectionParent, config, canvas));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(sectionParent);
        return new BuildResult { Window = window, Sections = sections.ToArray() };
    }

    // ── Window shell ─────────────────────────────────────────────────────────────

    private static TelemetryWindow BuildWindowChrome(Canvas canvas, TelemetryLayoutConfig config, out RectTransform content)
    {
        // Root panel, anchored to the canvas' top-right corner.
        RectTransform windowRt = NewRect("TelemetryWindow", canvas.transform);
        windowRt.anchorMin = windowRt.anchorMax = Vector2.one;
        windowRt.pivot = Vector2.one;
        windowRt.sizeDelta = config.windowSize;
        windowRt.anchoredPosition = config.windowOffset;
        windowRt.gameObject.AddComponent<Image>().color = config.windowBackground;

        // Title bar: fold button + title, and the drag handle for the whole window.
        RectTransform titleBar = NewRect("TitleBar", windowRt);
        titleBar.anchorMin = new Vector2(0f, 1f);
        titleBar.anchorMax = Vector2.one;
        titleBar.pivot = new Vector2(0.5f, 1f);
        titleBar.sizeDelta = new Vector2(0f, config.titleBarHeight);
        titleBar.gameObject.AddComponent<Image>().color = config.titleBarColor;

        var titleLayout = titleBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        titleLayout.padding = new RectOffset(6, 6, 2, 2);
        titleLayout.spacing = 6f;
        titleLayout.childControlWidth = true;
        titleLayout.childControlHeight = true;
        titleLayout.childForceExpandWidth = false;
        titleLayout.childForceExpandHeight = true;
        titleLayout.childAlignment = TextAnchor.MiddleLeft;

        (Button foldButton, TextMeshProUGUI foldLabel) = BuildFoldButton(titleBar, config);
        TextMeshProUGUI title = BuildLabel("Title", titleBar, config.windowTitle, 13f, config.textColor, FontStyles.Bold);
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        // Thin accent line separating the title bar from the content — the one bright
        // stroke that ties the window to the rest of the theme.
        RectTransform accentLine = NewRect("AccentLine", windowRt);
        accentLine.anchorMin = new Vector2(0f, 1f);
        accentLine.anchorMax = Vector2.one;
        accentLine.pivot = new Vector2(0.5f, 1f);
        accentLine.sizeDelta = new Vector2(0f, 2f);
        accentLine.anchoredPosition = new Vector2(0f, -config.titleBarHeight);
        accentLine.gameObject.AddComponent<Image>().color = UITheme.Accent;

        // Scroll view fills the rest of the window; it's also the window's fold content.
        RectTransform scrollRt = NewRect("ScrollView", windowRt);
        Stretch(scrollRt);
        scrollRt.offsetMax = new Vector2(0f, -config.titleBarHeight);

        RectTransform viewport = NewRect("Viewport", scrollRt);
        Stretch(viewport);
        viewport.offsetMax = new Vector2(-config.scrollbarWidth, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(6, 6, 6, 6);
        contentLayout.spacing = 8f;
        ConfigureVertical(contentLayout);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = BuildScrollbar(scrollRt, config);

        var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // Component last: TelemetryWindow.Awake caches the position for double-click reset.
        var window = windowRt.gameObject.AddComponent<TelemetryWindow>();
        window.SetFoldWiring(foldButton, foldLabel, scrollRt.gameObject, foldedAtStart: false);
        titleBar.gameObject.AddComponent<WindowDragHandle>().Bind(window);

        return window;
    }

    private static Scrollbar BuildScrollbar(RectTransform scrollArea, TelemetryLayoutConfig config)
    {
        RectTransform barRt = NewRect("Scrollbar Vertical", scrollArea);
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = Vector2.one;
        barRt.pivot = new Vector2(1f, 0.5f);
        barRt.sizeDelta = new Vector2(config.scrollbarWidth, 0f);
        barRt.gameObject.AddComponent<Image>().color = config.titleBarColor;

        RectTransform handleRt = NewRect("Handle", barRt);
        Stretch(handleRt);
        Image handleImage = handleRt.gameObject.AddComponent<Image>();
        handleImage.color = config.sectionHeaderColor;

        var scrollbar = barRt.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handleRt;
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    // ── Sections ─────────────────────────────────────────────────────────────────

    private static UISection BuildSection(TelemetryLayoutConfig.SectionDefinition def,
        RectTransform parent, TelemetryLayoutConfig config, Canvas canvas)
    {
        UISection section;
        RectTransform widgetParent;

        if (config.sectionPrefab != null)
        {
            section = Object.Instantiate(config.sectionPrefab, parent);
            section.name = "UISection_" + def.title.Replace(" ", "");
            widgetParent = section.FoldContent != null
                ? (RectTransform)section.FoldContent.transform
                : (RectTransform)section.transform;
            InstantiateWidgets(def, widgetParent, canvas);
            section.Configure(def.title, null, config.widgetSeparatorPrefab); // keeps the prefab's own header label
            section.SetFolded(def.startFolded, animate: false);
        }
        else
        {
            RectTransform sectionRt = NewRect("UISection_" + def.title.Replace(" ", ""), parent);
            var sectionLayout = sectionRt.gameObject.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 2f;
            ConfigureVertical(sectionLayout);

            // UISection first so its Awake runs before children exist (harmless),
            // then Configure/SetFoldWiring below do the real setup.
            section = sectionRt.gameObject.AddComponent<UISection>();

            // Header: fold button + title on a tinted strip.
            RectTransform header = NewRect("Header", sectionRt);
            header.gameObject.AddComponent<Image>().color = config.sectionHeaderColor;
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = config.sectionHeaderHeight;
            var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(6, 6, 2, 2);
            headerLayout.spacing = 6f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;

            (Button foldButton, TextMeshProUGUI foldLabel) = BuildFoldButton(header, config);
            TextMeshProUGUI headerLabel = BuildLabel("HeaderLabel", header, def.title, 12f, config.textColor, FontStyles.Bold);
            headerLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Widget container = the section's fold content.
            RectTransform widgets = NewRect("Widgets", sectionRt);
            var widgetsLayout = widgets.gameObject.AddComponent<VerticalLayoutGroup>();
            widgetsLayout.padding = new RectOffset(4, 4, 4, 4);
            widgetsLayout.spacing = 4f;
            ConfigureVertical(widgetsLayout);
            widgetParent = widgets;

            InstantiateWidgets(def, widgetParent, canvas);
            section.SetFoldWiring(foldButton, foldLabel, widgets.gameObject, def.startFolded);
            section.Configure(def.title, headerLabel, config.widgetSeparatorPrefab);
        }

        return section;
    }

    private static void InstantiateWidgets(TelemetryLayoutConfig.SectionDefinition def,
        RectTransform parent, Canvas canvas)
    {
        foreach (TelemetryLayoutConfig.WidgetEntry entry in def.widgets)
        {
            if (entry == null || !entry.enabled || entry.prefab == null) continue;

            UIWidget widget = Object.Instantiate(entry.prefab, parent);
            widget.name = entry.prefab.name;
            UITheme.Skin(widget.gameObject); // prefabs keep default (white) visuals; theme at runtime

            // Full-screen dialogs inside the widget escape the scroll view's clipping.
            foreach (PopupRoot popup in widget.GetComponentsInChildren<PopupRoot>(includeInactive: true))
                popup.transform.SetParent(canvas.transform, worldPositionStays: false);
        }
    }

    // ── Primitive helpers ────────────────────────────────────────────────────────

    private static (Button, TextMeshProUGUI) BuildFoldButton(RectTransform parent, TelemetryLayoutConfig config)
    {
        TextMeshProUGUI label = BuildLabel("FoldButton", parent, "↓", 12f, UITheme.Accent, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = true;
        label.gameObject.AddComponent<LayoutElement>().preferredWidth = 18f;

        Button button = label.gameObject.AddComponent<Button>();
        button.targetGraphic = label;
        return (button, label);
    }

    private static TextMeshProUGUI BuildLabel(string name, RectTransform parent, string text,
        float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        RectTransform rt = NewRect(name, parent);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, worldPositionStays: false);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Children keep their own preferred heights; width follows the parent.
    private static void ConfigureVertical(VerticalLayoutGroup layout)
    {
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }
}
