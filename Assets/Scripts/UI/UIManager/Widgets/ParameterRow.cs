using System;
using UnityEngine;
using TMPro;

/// <summary>
/// One editable parameter row for <see cref="HyperparameterEditorWidget"/>: a label
/// plus a TMP input field bound to a single <see cref="ParameterDescriptor"/>. Lives
/// on the row prefab the widget instantiates per descriptor. Parses and clamps input
/// to the descriptor's [Min, Max], then reports the committed value back by key.
/// </summary>
public class ParameterRow : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TMP_InputField input;

    // Readable-over-dark tints (UITheme palette) signalling how a committed change is
    // applied (see ParameterDescriptor.RequiresReset). Kept as constants rather than
    // SerializeFields to dodge the prefab-default trap noted in CLAUDE.md.
    //   hot  (RequiresReset == false) → soft amber (kept in place, trained state survives)
    //   cold (RequiresReset == true)  → soft blue  (forces a rebuild / reload)
    private static readonly Color HotColor = new Color(0.96f, 0.62f, 0.32f);
    private static readonly Color ColdColor = new Color(0.47f, 0.72f, 0.98f);

    private ParameterDescriptor descriptor;
    private Action<string, float> onCommitted;

    public string Key => descriptor.Key;
    public bool IsFocused => input != null && input.isFocused;

    /// <param name="desc">The parameter this row edits.</param>
    /// <param name="value">Initial value to display.</param>
    /// <param name="committedCallback">Invoked with (key, clampedValue) on a valid edit.</param>
    public void Setup(ParameterDescriptor desc, float value, Action<string, float> committedCallback)
    {
        descriptor = desc;
        onCommitted = committedCallback;

        if (label)
        {
            label.text = desc.DisplayName;
            // Left-aligned reads as a settings list; the prefab centres it.
            label.alignment = TextAlignmentOptions.MidlineLeft;
            // Paint the label by commit type so the user can see at a glance which
            // edits keep trained state (hot/amber) and which force a rebuild (cold/blue).
            label.color = desc.RequiresReset ? ColdColor : HotColor;
        }

        if (input)
        {
            // The prefab's field text is auto-sized tiny — pin a readable size.
            if (input.textComponent != null)
            {
                input.textComponent.enableAutoSizing = false;
                input.textComponent.fontSize = 14f;
            }
            // Toggle params are 0/1 flags — restrict to whole numbers so the field
            // can't take a fractional value; everything else allows decimals.
            input.contentType = desc.IsToggle
                ? TMP_InputField.ContentType.IntegerNumber
                : TMP_InputField.ContentType.DecimalNumber;
            input.onEndEdit.RemoveListener(OnEndEdit);
            input.onEndEdit.AddListener(OnEndEdit);
            SetDisplayedValue(value);
        }
    }

    /// <summary>Updates the shown value without firing the edit callback. Skipped
    /// while the user is typing so we don't fight their input.</summary>
    public void SetDisplayedValue(float value)
    {
        if (input == null || input.isFocused) return;

        string text = value.ToString();
        if (input.text != text) input.SetTextWithoutNotify(text);
    }

    private void OnEndEdit(string raw)
    {
        if (float.TryParse(raw, out float parsed))
        {
            float clamped = Mathf.Clamp(parsed, descriptor.Min, descriptor.Max);
            // A toggle only accepts 0/1: snap to the nearest after clamping.
            if (descriptor.IsToggle) clamped = Mathf.Round(clamped);
            input.SetTextWithoutNotify(clamped.ToString());
            onCommitted?.Invoke(descriptor.Key, clamped);
        }
        // Unparseable input is left as-is; the widget re-displays the effective
        // value on its next Tick, so the bad text is replaced automatically.
    }

    private void OnDestroy()
    {
        if (input) input.onEndEdit.RemoveListener(OnEndEdit);
    }
}
