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

    /// <summary>
    /// Whether committing a change to this parameter requires a full reset that
    /// discards trained state. False = "hot": the change is adopted via a
    /// save→load round-trip that keeps the trained brains/policy (e.g. learning
    /// rate, mutation probabilities). True = "cold": the change makes the saved
    /// weights structurally incompatible, so the run must be rebuilt from scratch
    /// (e.g. population size, network architecture). Reward parameters are always
    /// hot. The owner of the tuner routes commit based on this flag; a UI can
    /// also read it to section dials and warn before a destructive change.
    /// </summary>
    public bool RequiresReset;

    /// <summary>
    /// Whether this parameter is a boolean flag rather than a continuous value.
    /// When true a UI should render it as a 0/1 toggle and accept only those two
    /// values (Min/Max are expected to be 0/1). Backed by the same flat float map
    /// as every other parameter — 0 = off, 1 = on.
    /// </summary>
    public bool IsToggle;

    public ParameterDescriptor(string key, string displayName, float min, float max, float defaultValue, bool requiresReset = false, bool isToggle = false)
    {
        Key = key;
        DisplayName = displayName;
        Min = min;
        Max = max;
        DefaultValue = defaultValue;
        RequiresReset = requiresReset;
        IsToggle = isToggle;
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
