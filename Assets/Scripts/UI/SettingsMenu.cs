using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Self-contained settings screen, shared by the main menu and the pause menu. Like
/// <see cref="LoadingOverlay"/> it auto-boots onto a DontDestroyOnLoad object and builds
/// its whole UGUI panel in code, so there is nothing to place or wire in the Inspector —
/// the menu buttons just call <see cref="Open"/>.
///
/// <para>Exposes only what this game actually needs: a resolution dropdown and a display-mode
/// dropdown. The 60 fps cap is enforced globally by <see cref="AppBootstrap"/> and is not a
/// user setting; there is intentionally no audio or quality section (no sound, fixed graphics).</para>
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    public static SettingsMenu Instance { get; private set; }

    // ── Palette (light / default Unity-UI look) ──
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.45f);          // backdrop behind the panel
    private static readonly Color PanelColor = new Color(0.93f, 0.93f, 0.94f, 1f);  // light gray card
    private static readonly Color FieldColor = new Color(1f, 1f, 1f, 1f);           // white fields/buttons
    private static readonly Color AccentColor = new Color(0.40f, 0.56f, 0.85f, 0.45f); // selected-item highlight
    private static readonly Color TextColor = new Color(0.13f, 0.13f, 0.14f, 1f);   // dark text

    private static readonly (string label, FullScreenMode mode)[] DisplayModes =
    {
        ("Fullscreen", FullScreenMode.ExclusiveFullScreen),
        ("Borderless", FullScreenMode.FullScreenWindow),
        ("Windowed",   FullScreenMode.Windowed),
    };

    private CanvasGroup group;
    private Dropdown resolutionDropdown;
    private Dropdown displayModeDropdown;
    private readonly List<Resolution> resolutions = new(); // unique by width×height, dropdown-aligned

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[SettingsMenu]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<SettingsMenu>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
        SetVisible(false);
    }

    // ===================================================================
    //  PUBLIC API
    // ===================================================================
    public void Open()
    {
        PopulateFromCurrent();
        SetVisible(true);
    }

    public void Close() => SetVisible(false);

    // ===================================================================
    //  BEHAVIOUR
    // ===================================================================
    private void SetVisible(bool show)
    {
        group.alpha = show ? 1f : 0f;
        group.blocksRaycasts = show;
        group.interactable = show;
    }

    private void PopulateFromCurrent()
    {
        // Resolutions — de-duplicate by width×height (ignore refresh-rate variants).
        resolutions.Clear();
        var seen = new HashSet<long>();
        foreach (Resolution r in Screen.resolutions)
            if (seen.Add(((long)r.width << 32) | (uint)r.height))
                resolutions.Add(r);

        // Guarantee the current resolution is present even if the platform list is odd (e.g. in-editor).
        if (!seen.Contains(((long)Screen.width << 32) | (uint)Screen.height))
            resolutions.Add(new Resolution { width = Screen.width, height = Screen.height });

        var resLabels = new List<string>(resolutions.Count);
        int currentRes = 0;
        for (int i = 0; i < resolutions.Count; i++)
        {
            resLabels.Add($"{resolutions[i].width} × {resolutions[i].height}");
            if (resolutions[i].width == Screen.width && resolutions[i].height == Screen.height)
                currentRes = i;
        }
        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(resLabels);
        resolutionDropdown.SetValueWithoutNotify(currentRes);
        resolutionDropdown.RefreshShownValue();

        // Display mode.
        var modeLabels = new List<string>(DisplayModes.Length);
        int currentMode = 0;
        for (int i = 0; i < DisplayModes.Length; i++)
        {
            modeLabels.Add(DisplayModes[i].label);
            if (DisplayModes[i].mode == Screen.fullScreenMode) currentMode = i;
        }
        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(modeLabels);
        displayModeDropdown.SetValueWithoutNotify(currentMode);
        displayModeDropdown.RefreshShownValue();
    }

    private void OnApply()
    {
        int resIndex = Mathf.Clamp(resolutionDropdown.value, 0, resolutions.Count - 1);
        int modeIndex = Mathf.Clamp(displayModeDropdown.value, 0, DisplayModes.Length - 1);
        Resolution r = resolutions[resIndex];
        GameSettings.ApplyAndSave(r.width, r.height, DisplayModes[modeIndex].mode);
        Close();
    }

    // ===================================================================
    //  UI CONSTRUCTION (all code — no prefab, no imported art)
    // ===================================================================
    private void BuildUI()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29000; // above normal UI, below the loading overlay (30000)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.AddComponent<CanvasGroup>();

        // Full-screen dim that also eats clicks to the menu behind.
        var dim = AddImage(transform, "Dim", DimColor);
        Stretch(dim.rectTransform);

        // Centre panel — sized to fit the content so there's no dead space.
        var panel = AddImage(transform, "Panel", PanelColor);
        Center(panel.rectTransform, new Vector2(560, 340), Vector2.zero);

        var title = AddText(panel.transform, "Title", font, 32, TextColor);
        title.alignment = TextAnchor.MiddleCenter;
        Top(title.rectTransform, new Vector2(520, 52), new Vector2(0, -32));
        title.text = "Settings";

        resolutionDropdown = AddSettingRow(panel.transform, font, "Resolution", 36);
        displayModeDropdown = AddSettingRow(panel.transform, font, "Display Mode", -28);

        // Buttons centred at the bottom with comfortable spacing (Back | Apply).
        var back = AddButton(panel.transform, font, "Back", new Vector2(-100, 36), new Vector2(150, 48));
        back.onClick.AddListener(Close);
        var apply = AddButton(panel.transform, font, "Apply", new Vector2(100, 36), new Vector2(150, 48));
        apply.onClick.AddListener(OnApply);
    }

    /// <summary>A label on the left and a dropdown on the right, anchored at the panel's top.</summary>
    private Dropdown AddSettingRow(Transform panel, Font font, string label, float y)
    {
        var lbl = AddText(panel, label + " Label", font, 24, TextColor);
        lbl.text = label;
        lbl.alignment = TextAnchor.MiddleLeft;
        var lblRT = lbl.rectTransform;
        lblRT.anchorMin = lblRT.anchorMax = lblRT.pivot = new Vector2(0.5f, 0.5f);
        lblRT.sizeDelta = new Vector2(220, 44);
        lblRT.anchoredPosition = new Vector2(-130, y);

        Dropdown dd = CreateDropdown(panel, font, new Vector2(240, 44));
        var ddRT = (RectTransform)dd.transform;
        ddRT.anchoredPosition = new Vector2(140, y);
        return dd;
    }

    // ── Generic UGUI builders ──

    private Button AddButton(Transform parent, Font font, string label, Vector2 anchoredPos, Vector2 size)
    {
        var img = AddImage(parent, label + " Button", FieldColor);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var txt = AddText(img.transform, "Text", font, 24, TextColor);
        txt.alignment = TextAnchor.MiddleCenter;
        Stretch(txt.rectTransform);
        txt.text = label;
        return btn;
    }

    /// <summary>Builds a fully-working legacy <see cref="Dropdown"/> in code, mirroring the
    /// structure the editor's "UI &gt; Legacy &gt; Dropdown" produces (Template &gt; Viewport(Mask)
    /// &gt; Content &gt; Item(Toggle)).</summary>
    private Dropdown CreateDropdown(Transform parent, Font font, Vector2 size)
    {
        var ddImg = AddImage(parent, "Dropdown", FieldColor);
        var ddRT = ddImg.rectTransform;
        ddRT.anchorMin = ddRT.anchorMax = ddRT.pivot = new Vector2(0.5f, 0.5f);
        ddRT.sizeDelta = size;
        var dd = ddImg.gameObject.AddComponent<Dropdown>();

        // Caption (currently selected value).
        var caption = AddText(ddImg.transform, "Label", font, 22, TextColor);
        caption.alignment = TextAnchor.MiddleLeft;
        var capRT = caption.rectTransform;
        capRT.anchorMin = Vector2.zero; capRT.anchorMax = Vector2.one;
        capRT.offsetMin = new Vector2(12, 2); capRT.offsetMax = new Vector2(-12, -2);

        // Template (the popup list) — must start inactive.
        var template = AddImage(ddImg.transform, "Template", PanelColor);
        var tmplRT = template.rectTransform;
        tmplRT.anchorMin = new Vector2(0, 0);
        tmplRT.anchorMax = new Vector2(1, 0);
        tmplRT.pivot = new Vector2(0.5f, 1f);
        tmplRT.anchoredPosition = new Vector2(0, 2);
        tmplRT.sizeDelta = new Vector2(0, 180);
        var scroll = template.gameObject.AddComponent<ScrollRect>();

        var viewport = AddImage(template.transform, "Viewport", new Color(0f, 0f, 0f, 0.004f));
        var vpRT = viewport.rectTransform;
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.sizeDelta = Vector2.zero; vpRT.pivot = new Vector2(0f, 1f);
        var mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var content = NewUIChild("Content", viewport.transform);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0, 44);

        // Item prototype — Dropdown clones this per option.
        var item = NewUIChild("Item", content.transform);
        var itemRT = item.GetComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 0.5f); itemRT.anchorMax = new Vector2(1, 0.5f);
        itemRT.pivot = new Vector2(0.5f, 0.5f);
        itemRT.sizeDelta = new Vector2(0, 40);
        var itemToggle = item.AddComponent<Toggle>();

        var itemBg = AddImage(item.transform, "Item Background", FieldColor);
        Stretch(itemBg.rectTransform);
        var itemCheck = AddImage(item.transform, "Item Checkmark",
            new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.30f));
        Stretch(itemCheck.rectTransform);
        var itemLabel = AddText(item.transform, "Item Label", font, 20, TextColor);
        itemLabel.alignment = TextAnchor.MiddleLeft;
        var ilRT = itemLabel.rectTransform;
        ilRT.anchorMin = Vector2.zero; ilRT.anchorMax = Vector2.one;
        ilRT.offsetMin = new Vector2(12, 1); ilRT.offsetMax = new Vector2(-12, -1);

        itemToggle.targetGraphic = itemBg;
        itemToggle.graphic = itemCheck;
        itemToggle.isOn = true;

        scroll.content = contentRT;
        scroll.viewport = vpRT;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        dd.targetGraphic = ddImg;
        dd.template = tmplRT;
        dd.captionText = caption;
        dd.itemText = itemLabel;

        template.gameObject.SetActive(false);
        return dd;
    }

    // ── Layout helpers (shared shape with LoadingOverlay's) ──
    private static GameObject NewUIChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = NewUIChild(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text AddText(Transform parent, string name, Font font, int size, Color color)
    {
        var go = NewUIChild(name, parent);
        var txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = size;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rt, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = offset;
    }

    // Anchored to the top-centre of the parent.
    private static void Top(RectTransform rt, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size; rt.anchoredPosition = offset;
    }
}
