using System.Collections.Generic;

/// <summary>
/// Metadata describing a single tunable parameter so a UI can build a control
/// for it (a slider, a number input, or both) without knowing the parameter in
/// advance. <see cref="Min"/>/<see cref="Max"/> double as slider bounds and as
/// input clamp limits.
/// </summary>
public struct ParameterDescriptor
{
    public string Key;          // matches the GetParameters/SetParameters keys
    public string DisplayName;  // human-readable label
    public float Min;
    public float Max;
    public float DefaultValue;

    public ParameterDescriptor(string key, string displayName, float min, float max, float defaultValue)
    {
        Key = key;
        DisplayName = displayName;
        Min = min;
        Max = max;
        DefaultValue = defaultValue;
    }
}

/// <summary>
/// A category-agnostic contract for any group of float parameters that can be
/// exposed, edited, and re-applied at runtime (reward parameters today, model
/// hyperparameters later). Implementers expose a flat name → value map plus
/// descriptors for building controls. Keys must agree across all three methods.
///
/// Drives <see cref="ParameterTuner"/>, which adds staging + an observer event
/// + commit on top of this contract so every parameter group reuses the same
/// machinery.
/// </summary>
public interface ITunableParameters
{
    /// <summary>Per-parameter metadata for building dials. Keys match GetParameters.</summary>
    IReadOnlyList<ParameterDescriptor> GetParameterDescriptors();

    /// <summary>Current values as a flat name → value map.</summary>
    Dictionary<string, float> GetParameters();

    /// <summary>Applies values. Unknown keys are ignored; missing keys keep their current value.</summary>
    void SetParameters(Dictionary<string, float> parameters);
}
