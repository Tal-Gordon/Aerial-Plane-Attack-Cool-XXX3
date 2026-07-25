using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Central colour palette + runtime re-skinning for every screen in the game.
/// The palette extends the LoadingOverlay's dark navy with the sky-blue / amber
/// accents the telemetry graphs and brain visualizer already use, so all screens
/// read as one family.
///
/// Scenes and prefabs keep their default Unity (white/grey) visuals in the editor;
/// callers apply the theme at runtime via <see cref="Skin"/>. This deliberately
/// avoids hand-editing .unity/.prefab YAML (which has corrupted scenes before —
/// see CLAUDE.md) and gives one shared palette for the menus, pause screen and
/// telemetry widgets. Only "plain" images (no sprite, or one of Unity's built-in
/// UI sprites) are recoloured — real artwork (hero images, icons) is left alone.
/// </summary>
public static class UITheme
{
    // ── Palette ──
    public static readonly Color Background = new Color(0.06f, 0.07f, 0.09f, 1f);   // near-black navy (LoadingOverlay)
    public static readonly Color Panel = new Color(0.13f, 0.14f, 0.17f, 1f);        // card / window body
    public static readonly Color Field = new Color(0.19f, 0.20f, 0.24f, 1f);        // buttons, inputs, dropdowns
    public static readonly Color FieldHover = new Color(0.26f, 0.28f, 0.34f, 1f);
    public static readonly Color FieldPressed = new Color(0.10f, 0.11f, 0.14f, 1f);
    public static readonly Color Accent = new Color(0.30f, 0.68f, 0.95f, 1f);       // sky blue (graph avg / brain-viz positive)
    public static readonly Color AccentHover = new Color(0.42f, 0.76f, 1.00f, 1f);
    public static readonly Color AccentPressed = new Color(0.20f, 0.52f, 0.78f, 1f);
    public static readonly Color AccentMuted = new Color(0.16f, 0.33f, 0.47f, 1f);  // "selected" fills — light text stays readable
    public static readonly Color AccentDim = new Color(0.30f, 0.68f, 0.95f, 0.45f);
    public static readonly Color Amber = new Color(0.96f, 0.55f, 0.22f, 1f);        // secondary accent (brain-viz negative)
    public static readonly Color TextColor = new Color(0.85f, 0.87f, 0.92f, 1f);
    public static readonly Color TextDimmed = new Color(0.85f, 0.87f, 0.92f, 0.55f);
    public static readonly Color TextOnAccent = new Color(0.05f, 0.08f, 0.12f, 1f); // dark ink for accent-filled buttons
    public static readonly Color Hairline = new Color(0.85f, 0.87f, 0.92f, 0.10f);  // separators
    public static readonly Color HandleColor = new Color(0.62f, 0.66f, 0.74f, 1f);  // slider/scrollbar grips

    // Sprites that ship with Unity's default UI — safe to recolour. Anything else
    // is treated as authored art and keeps its own colour.
    private static readonly HashSet<string> BuiltinSprites = new HashSet<string>
    {
        "UISprite", "Background", "InputFieldBackground", "Knob", "Checkmark", "DropdownArrow", "UIMask",
        RoundedRectSpriteName, // ours, but it's chrome rather than artwork — recolour it
    };

    private const string RoundedRectSpriteName = "RoundedRect";
    private static readonly Dictionary<int, Sprite> RoundedRects = new Dictionary<int, Sprite>();

    /// <summary>
    /// Re-skins an existing UI hierarchy in place: plain images become dark panels,
    /// text becomes light, and every Selectable gets themed state colours. Safe to
    /// call on inactive objects (popup dialogs, the pause menu) and idempotent.
    /// </summary>
    public static void Skin(GameObject root)
    {
        if (root == null) return;

        // Pass 1 — flat recolour. Alpha is preserved so semi-transparent dims/overlays
        // keep their weight; only the hue moves to the dark palette.
        foreach (Image img in root.GetComponentsInChildren<Image>(includeInactive: true))
        {
            if (!IsPlain(img)) continue;
            Color panel = PanelColorFor(img.gameObject.name);
            // Chrome hints (title bars, headers, separators) carry their own alpha —
            // scene-authored strips are often ghostly semi-transparent white and should
            // become solid. Everything else keeps its weight (dims, overlays).
            img.color = panel != Panel ? panel : new Color(panel.r, panel.g, panel.b, img.color.a);
        }

        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            label.color = IsIconGlyph(label) ? Accent : TextColorFor(label.gameObject.name);
        foreach (Text label in root.GetComponentsInChildren<Text>(includeInactive: true))
            label.color = TextColorFor(label.gameObject.name);

        // Pass 2 — controls override the parts they own (fills, handles, checkmarks…).
        foreach (Selectable selectable in root.GetComponentsInChildren<Selectable>(includeInactive: true))
            StyleSelectable(selectable);
    }

    /// <summary>Themed state colours for a single control. Called by <see cref="Skin"/>;
    /// also usable directly for code-built UI.</summary>
    public static void StyleSelectable(Selectable selectable)
    {
        ApplyBlock(selectable, Field, FieldHover, FieldPressed, new Color(Field.r, Field.g, Field.b, 0.45f));

        switch (selectable)
        {
            case Button button:
                // Fold/minimise arrows read best as a bare accent glyph, not a boxed one.
                if (IsIconGlyph(button.GetComponentInChildren<TMP_Text>(includeInactive: true)))
                {
                    if (button.targetGraphic is Image)
                    {
                        // Scene-authored: box image behind the glyph — ghost it (invisible
                        // at rest, subtle box on hover).
                        ApplyBlock(button,
                            new Color(1f, 1f, 1f, 0f),
                            new Color(Field.r, Field.g, Field.b, 0.9f),
                            FieldPressed,
                            new Color(1f, 1f, 1f, 0f));
                    }
                    else
                    {
                        // Label-as-button: white multipliers so the glyph keeps its accent,
                        // dimming slightly on hover/press.
                        ApplyBlock(button,
                            Color.white,
                            new Color(1f, 1f, 1f, 0.75f),
                            new Color(1f, 1f, 1f, 0.5f),
                            new Color(1f, 1f, 1f, 0.3f));
                    }
                }
                break;

            case Slider slider:
                // Handle is usually the target graphic — keep it light on the dark track.
                ApplyBlock(slider, HandleColor, Color.white, Accent, new Color(HandleColor.r, HandleColor.g, HandleColor.b, 0.4f));
                SetImage(slider.fillRect, Accent);
                SetImage(slider.handleRect, Color.white); // tinted by the block above
                SetImage(slider.transform.Find("Background") as RectTransform, FieldPressed);
                break;

            case Toggle toggle:
                if (toggle.graphic != null) toggle.graphic.color = Accent;
                break;

            case TMP_InputField input:
                if (input.textComponent != null) input.textComponent.color = TextColor;
                if (input.placeholder != null) input.placeholder.color = TextDimmed;
                input.customCaretColor = true;
                input.caretColor = Accent;
                input.selectionColor = AccentDim;
                break;

            case Scrollbar scrollbar:
                ApplyBlock(scrollbar, FieldHover, HandleColor, Accent, new Color(FieldHover.r, FieldHover.g, FieldHover.b, 0.4f));
                SetImage(scrollbar.GetComponent<RectTransform>(), FieldPressed); // the track behind the handle
                break;

            case TMP_Dropdown dropdown:
                if (dropdown.captionText != null) dropdown.captionText.color = TextColor;
                if (dropdown.itemText != null) dropdown.itemText.color = TextColor;
                SetImage(dropdown.transform.Find("Arrow") as RectTransform, TextColor);
                break;
        }
    }

    /// <summary>Accent-filled call-to-action (Play / Resume / Apply). Dark label for contrast.</summary>
    public static void StylePrimary(Button button)
    {
        if (button == null) return;
        ApplyBlock(button, Accent, AccentHover, AccentPressed, AccentDim);
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            label.color = TextOnAccent;
        foreach (Text label in button.GetComponentsInChildren<Text>(includeInactive: true))
            label.color = TextOnAccent;
    }

    /// <summary>Amber-filled button for actions that discard something (e.g. reload
    /// without saving training progress). Dark label for contrast.</summary>
    public static void StyleDestructive(Button button)
    {
        if (button == null) return;
        ApplyBlock(button, Amber,
            new Color(1.00f, 0.65f, 0.34f, 1f),
            new Color(0.78f, 0.42f, 0.14f, 1f),
            new Color(Amber.r, Amber.g, Amber.b, 0.45f));
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(includeInactive: true))
            label.color = TextOnAccent;
        foreach (Text label in button.GetComponentsInChildren<Text>(includeInactive: true))
            label.color = TextOnAccent;
    }

    /// <summary>
    /// For selectors that show the chosen option by disabling its button
    /// (<see cref="AITypeSelector"/>): the "disabled" state becomes a muted accent
    /// fill instead of greyed-out, so it reads as *selected*.
    /// </summary>
    public static void StyleSelectedWhenDisabled(Button button)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        colors.disabledColor = AccentMuted;
        button.colors = colors;
    }

    /// <summary>1×N vertical gradient sprite for large background fills (menus).
    /// Generated once per call — cache the result, don't call per frame.</summary>
    public static Sprite CreateVerticalGradientSprite(Color bottom, Color top, int steps = 128)
    {
        var tex = new Texture2D(1, steps, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
        };
        for (int y = 0; y < steps; y++)
            tex.SetPixel(0, y, Color.Lerp(bottom, top, (float)y / (steps - 1)));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, 1f, steps), new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// A 9-sliced rounded-rectangle sprite for panels, cards and buttons — set it on an
    /// Image with <c>type = Sliced</c> and the corners hold their radius at any size.
    /// Generated rather than taken from Unity's built-in UISprite so the radius is ours to
    /// choose, and anti-aliased over one pixel so the curve doesn't stair-step.
    /// Cached per radius; the texture is tiny (a couple of hundred bytes).
    /// </summary>
    public static Sprite RoundedRectSprite(int cornerRadius)
    {
        cornerRadius = Mathf.Clamp(cornerRadius, 1, 64);
        if (RoundedRects.TryGetValue(cornerRadius, out Sprite cached) && cached != null)
            return cached;

        int size = cornerRadius * 2 + 2; // the 2 spare pixels are the stretchable middle
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name = RoundedRectSpriteName,
        };

        float radius = cornerRadius;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance past the corner arc, measured from the nearest corner centre.
                // Straight edges and the middle sit at 0 and come out fully opaque.
                float dx = Mathf.Max(0f, Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius)));
                float dy = Mathf.Max(0f, Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius)));
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.name = RoundedRectSpriteName;

        RoundedRects[cornerRadius] = sprite;
        return sprite;
    }

    // ── internals ──

    private static void ApplyBlock(Selectable selectable, Color normal, Color hover, Color pressed, Color disabled)
    {
        ColorBlock colors = selectable.colors;
        colors.normalColor = normal;
        colors.highlightedColor = hover;
        colors.pressedColor = pressed;
        colors.selectedColor = normal; // no sticky post-click highlight
        colors.disabledColor = disabled;
        colors.colorMultiplier = 1f;
        selectable.colors = colors;

        // The block multiplies the target graphic's own colour — neutralise it so the
        // state colours land exactly as specified (also keeps custom art sprites intact).
        // Images only: a text target (label-as-button) must keep its own colour.
        if (selectable.transition == Selectable.Transition.ColorTint && selectable.targetGraphic is Image)
            selectable.targetGraphic.color = Color.white;
    }

    // Name hints keep chrome strips distinguishable in scene-authored windows,
    // mirroring the code-built chrome's colours (TelemetryLayoutConfig).
    private static Color PanelColorFor(string objectName)
    {
        if (objectName.Contains("TitleBar")) return new Color(0.08f, 0.11f, 0.16f, 1f);
        if (objectName.Contains("Header")) return new Color(0.16f, 0.22f, 0.32f, 1f);
        // Separator lines must stay light on the dark panels (alpha is preserved by the caller).
        if (objectName.Contains("eperator") || objectName.Contains("eparator")) return Hairline;
        return Panel;
    }

    private static Color TextColorFor(string objectName)
    {
        // No accent-by-name here: scene section headers are also named "Title" and
        // accent text on the blue-tinted header strips reads badly. Screens that
        // want an accent title set it explicitly (SettingsMenu, MainMenuController,
        // GameModeSelectionController).
        if (objectName == "Caption") return TextDimmed; // stat-cell captions recede
        return TextColor;
    }

    private static void SetImage(RectTransform rect, Color color)
    {
        if (rect == null) return;
        Image img = rect.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    private static bool IsPlain(Image img)
    {
        return img.sprite == null || BuiltinSprites.Contains(img.sprite.name);
    }

    // A label that is just a directional glyph — a fold/minimise arrow.
    private static bool IsIconGlyph(TMP_Text label)
    {
        if (label == null || label.text == null) return false;
        string glyph = label.text.Trim();
        return glyph == "↓" || glyph == "→" || glyph == "▼" || glyph == "▶" || glyph == "↑" || glyph == "←";
    }
}
