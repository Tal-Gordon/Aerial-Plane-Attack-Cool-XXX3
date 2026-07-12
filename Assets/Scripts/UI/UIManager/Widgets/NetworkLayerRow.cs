using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One layer row inside <see cref="NetworkShapeWidget"/>: an index tag ("L1"), −/+
/// stepper buttons that snap the width through powers of two, a typed width field, and
/// a remove button. Also serves as the read-only "bookend" row for the fixed input and
/// output layers via <see cref="SetupLocked"/> (steppers and remove hidden, field
/// disabled) so the whole network is visible in one aligned list. Lives on the row
/// prefab the widget instantiates per layer; the steppers are built in code.
/// </summary>
public class NetworkLayerRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI indexLabel;
    [SerializeField] private TMP_InputField widthInput;
    [SerializeField] private Button removeButton;

    // Built in code by EnsureLayout — the prefab predates them.
    private Button minusButton;
    private Button plusButton;

    private Action<NetworkLayerRow, int> onWidthChanged;
    private Action<NetworkLayerRow> onRemove;

    /// <summary>The layer's current width, kept in sync with the input field.</summary>
    public int Width { get; private set; }

    /// <param name="displayIndex">1-based index shown in the tag.</param>
    /// <param name="width">Initial width to display.</param>
    /// <param name="removable">Whether the remove button is enabled (false at the floor).</param>
    /// <param name="widthChanged">Invoked with (row, clampedWidth) on a valid edit.</param>
    /// <param name="remove">Invoked when the remove button is pressed.</param>
    // Fixed row height so the parent list reserves space for every row (without this
    // the layout groups collapse the rows onto each other — see NetworkShapeWidget).
    private const float RowHeight = 34f;

    public void Setup(int displayIndex, int width, bool removable,
                      Action<NetworkLayerRow, int> widthChanged, Action<NetworkLayerRow> remove)
    {
        onWidthChanged = widthChanged;
        onRemove = remove;
        Width = width;

        EnsureLayout();
        SetIndex(displayIndex);

        if (widthInput != null)
        {
            // The prefab's field text is auto-sized tiny — pin a readable size.
            if (widthInput.textComponent != null)
            {
                widthInput.textComponent.enableAutoSizing = false;
                widthInput.textComponent.fontSize = 16f;
            }
            widthInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            widthInput.SetTextWithoutNotify(width.ToString());
            widthInput.onEndEdit.RemoveListener(OnEndEdit);
            widthInput.onEndEdit.AddListener(OnEndEdit);
        }

        if (removeButton != null)
        {
            removeButton.gameObject.SetActive(true);
            removeButton.interactable = removable;
            removeButton.onClick.RemoveListener(OnRemoveClicked);
            removeButton.onClick.AddListener(OnRemoveClicked);
        }

        if (minusButton != null) minusButton.gameObject.SetActive(true);
        if (plusButton != null) plusButton.gameObject.SetActive(true);
    }

    /// <summary>Read-only bookend variant for the fixed input/output layers: same
    /// geometry as an editable row (so the value column stays aligned) but dimmed,
    /// with the steppers and remove button hidden and the field disabled.</summary>
    public void SetupLocked(string tag, int value)
    {
        Width = value;
        EnsureLayout();

        if (indexLabel != null)
        {
            indexLabel.text = tag;
            indexLabel.fontStyle = FontStyles.Normal;
            indexLabel.fontSize = 13f; // descriptive text, quieter than the L# tags
            indexLabel.color = UITheme.TextDimmed;
        }

        if (widthInput != null)
        {
            if (widthInput.textComponent != null)
            {
                widthInput.textComponent.enableAutoSizing = false;
                widthInput.textComponent.fontSize = 16f;
                widthInput.textComponent.alignment = TextAlignmentOptions.Center;
            }
            widthInput.SetTextWithoutNotify(value.ToString());
            widthInput.interactable = false; // the themed disabled tint does the dimming
        }

        if (removeButton != null) removeButton.gameObject.SetActive(false);
        if (minusButton != null) minusButton.gameObject.SetActive(false);
        if (plusButton != null) plusButton.gameObject.SetActive(false);
    }

    public void SetIndex(int displayIndex)
    {
        if (indexLabel != null) indexLabel.text = $"L{displayIndex}";
    }

    public void SetRemovable(bool removable)
    {
        if (removeButton != null) removeButton.interactable = removable;
    }

    /// <summary>Reflects a width set from outside (uniform-mode sync) without firing the
    /// edit callback back. Skipped while the user is typing so we don't fight input.</summary>
    public void SetWidthSilent(int width)
    {
        Width = width;
        if (widthInput != null && !widthInput.isFocused)
            widthInput.SetTextWithoutNotify(width.ToString());
    }

    private void OnEndEdit(string raw)
    {
        if (int.TryParse(raw, out int parsed))
        {
            int clamped = Mathf.Clamp(parsed, NetworkShapeController.MinLayerWidth, NetworkShapeController.MaxLayerWidth);
            Width = clamped;
            widthInput.SetTextWithoutNotify(clamped.ToString());
            onWidthChanged?.Invoke(this, clamped);
        }
        else if (widthInput != null)
        {
            // Unparseable — restore the last good value.
            widthInput.SetTextWithoutNotify(Width.ToString());
        }
    }

    // Owns the row's layout in code so it can't be broken by (or require) prefab
    // tweaking: a horizontal strip of [index][width field][remove], a fixed height, and
    // per-child widths so the number field takes the slack. Idempotent — safe to re-run.
    private void EnsureLayout()
    {
        var group = Ensure<HorizontalLayoutGroup>(gameObject);
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = true;
        group.childAlignment = TextAnchor.MiddleLeft;
        group.spacing = 8f;
        group.padding = new RectOffset(6, 6, 2, 2);

        // A ContentSizeFitter here would fight the parent list (which controls our
        // height); the explicit LayoutElement below is what the parent reads instead.
        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;

        // The row list doesn't control child height (childControlHeight = false), so the
        // row must state its own — on both the LayoutElement and the RectTransform.
        var self = Ensure<LayoutElement>(gameObject);
        self.minHeight = RowHeight;
        self.preferredHeight = RowHeight;
        self.flexibleHeight = 0f;
        if (transform is RectTransform rt)
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, RowHeight);

        // Layout: [ L#  ······  −  [ 000 ]  +  ✕ ]. The label eats the slack (flexible)
        // so the stepper cluster sits together at the right, giving an aligned column.
        if (indexLabel != null)
        {
            var le = Ensure<LayoutElement>(indexLabel.gameObject);
            le.minWidth = 32f;
            le.flexibleWidth = 1f;
            indexLabel.fontStyle = FontStyles.Bold;
            indexLabel.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (widthInput != null)
        {
            var le = Ensure<LayoutElement>(widthInput.gameObject);
            le.minWidth = 56f;
            le.preferredWidth = 56f;
            le.flexibleWidth = 0f;
            if (widthInput.textComponent != null)
                widthInput.textComponent.alignment = TextAlignmentOptions.Center;
        }
        // Remove is a quiet dim glyph, not a boxed button — it shouldn't compete with
        // the steppers for attention (matches the agreed concept).
        if (removeButton != null)
        {
            FixedWidth(removeButton.gameObject, 26f);
            var xLabel = removeButton.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            if (xLabel != null)
            {
                xLabel.fontSize = 13f;
                xLabel.color = UITheme.TextDimmed;
                xLabel.alignment = TextAlignmentOptions.Center;
                if (removeButton.image != null) removeButton.image.enabled = false;
                removeButton.targetGraphic = xLabel;

                // Label-as-button: white multipliers so the dim glyph reacts on hover/press.
                ColorBlock colors = removeButton.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
                colors.pressedColor = new Color(1f, 1f, 1f, 0.5f);
                colors.selectedColor = Color.white;
                colors.disabledColor = new Color(1f, 1f, 1f, 0.25f);
                removeButton.colors = colors;
            }
        }

        // -/+ steppers snap the width through powers of two — built in code since the
        // prefab predates them. ASCII glyphs only: the TMP font atlas lacks the
        // typographic minus (U+2212), which renders as a box.
        if (minusButton == null) minusButton = BuildStepper("-", OnMinusClicked);
        if (plusButton == null) plusButton = BuildStepper("+", OnPlusClicked);

        // Enforce sibling order so the layout group lays out: L#, −, field, +, ✕.
        if (indexLabel != null) indexLabel.transform.SetSiblingIndex(0);
        minusButton.transform.SetSiblingIndex(1);
        if (widthInput != null) widthInput.transform.SetSiblingIndex(2);
        plusButton.transform.SetSiblingIndex(3);
        if (removeButton != null) removeButton.transform.SetAsLastSibling();
    }

    private Button BuildStepper(string glyph, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(glyph == "-" ? "StepDown" : "StepUp", typeof(RectTransform));
        go.transform.SetParent(transform, worldPositionStays: false);

        var image = go.AddComponent<Image>();
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        UITheme.StyleSelectable(button); // rows are themed before Setup runs, so style here
        FixedWidth(go, 26f);

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, worldPositionStays: false);
        var rt = (RectTransform)labelGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = glyph;
        label.fontSize = 16f;
        label.fontStyle = FontStyles.Bold;
        label.color = UITheme.TextColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

        button.onClick.AddListener(onClick);
        return button;
    }

    // Steppers move through powers of two (…8, 16, 32, 64…) — the natural grid for NN
    // layer widths. A non-power value snaps to the nearest power in that direction.
    private void OnMinusClicked() => SnapWidth(up: false);
    private void OnPlusClicked() => SnapWidth(up: true);

    private void SnapWidth(bool up)
    {
        int target = up ? NextPowerOfTwoUp(Width) : NextPowerOfTwoDown(Width);
        target = Mathf.Clamp(target, NetworkShapeController.MinLayerWidth, NetworkShapeController.MaxLayerWidth);
        if (target == Width) return;

        Width = target;
        if (widthInput != null) widthInput.SetTextWithoutNotify(target.ToString());
        onWidthChanged?.Invoke(this, target);
    }

    private static int NextPowerOfTwoUp(int v)
    {
        int p = 1;
        while (p <= v) p <<= 1;
        return p;
    }

    private static int NextPowerOfTwoDown(int v)
    {
        int p = 1;
        while (p * 2 < v) p <<= 1;
        return p;
    }

    private static void FixedWidth(GameObject go, float width)
    {
        var le = Ensure<LayoutElement>(go);
        le.minWidth = width;
        le.preferredWidth = width;
        le.flexibleWidth = 0f;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private void OnRemoveClicked() => onRemove?.Invoke(this);

    private void OnDestroy()
    {
        if (widthInput) widthInput.onEndEdit.RemoveListener(OnEndEdit);
        if (removeButton) removeButton.onClick.RemoveListener(OnRemoveClicked);
    }
}
