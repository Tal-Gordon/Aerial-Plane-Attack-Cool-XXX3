using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Small builders for code-built menu screens, in the palette from <see cref="UITheme"/>.
///
/// Menus that build themselves (this one, SettingsMenu, the continue dialog) deliberately
/// skip <see cref="UITheme.Skin"/>: Skin exists to retrofit scene-authored hierarchies,
/// and its blanket recolour would flatten deliberate choices like a card's scrim. Code-built
/// UI picks its colours here instead.
/// </summary>
public static class MenuUI
{
    public static Image Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    /// <summary>Gives an image rounded corners that hold at any size (9-sliced).</summary>
    public static Image Rounded(Image img, int cornerRadius = 10)
    {
        if (img == null) return null;
        img.sprite = UITheme.RoundedRectSprite(cornerRadius);
        img.type = Image.Type.Sliced;
        img.fillCenter = true;
        img.pixelsPerUnitMultiplier = 1f; // 1 sprite pixel = 1 canvas unit, so the radius is literal
        return img;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, string text, float size,
        Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = align;
        label.raycastTarget = false;
        label.richText = false; // descriptions are authored data — never treat < > as markup
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    /// <summary>A themed button with a centred label. Style it further with
    /// <see cref="UITheme.StylePrimary"/> for a call-to-action.</summary>
    public static Button TextButton(Transform parent, string name, string text, float size)
    {
        Image fill = Rounded(Panel(parent, name, UITheme.Field), 8);
        var button = fill.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        UITheme.StyleSelectable(button);

        TextMeshProUGUI label = Label(fill.transform, "Label", text, size,
            UITheme.TextColor, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    public static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    /// <summary>Anchors a rect to a single point (both anchors together) and sizes it.
    /// anchor (0,1) is the parent's top-left, (1,1) top-right, (0.5,0.5) the centre.</summary>
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
        return rt;
    }

    /// <summary>Stretches across one axis and pins to an edge — footers, headers, hairlines.
    /// edge 0 = bottom, 1 = top.</summary>
    public static RectTransform Band(RectTransform rt, float edge, float height, float offset = 0f)
    {
        rt.anchorMin = new Vector2(0f, edge);
        rt.anchorMax = new Vector2(1f, edge);
        rt.pivot = new Vector2(0.5f, edge);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = new Vector2(0f, offset);
        return rt;
    }
}
