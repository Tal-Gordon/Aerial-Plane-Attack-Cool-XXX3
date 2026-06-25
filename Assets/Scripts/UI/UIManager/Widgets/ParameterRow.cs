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

        if (label) label.text = desc.DisplayName;

        if (input)
        {
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
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
