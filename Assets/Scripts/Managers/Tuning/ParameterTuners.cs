/// <summary>
/// Static locator for the active <see cref="ParameterTuner"/> instances so UI
/// can reach a tuner by category without per-category wiring. The owner (e.g.
/// <see cref="SimulationManager"/>) creates and assigns each tuner; consumers
/// (a future dials window) just read it and subscribe to its events.
///
/// Add a slot here per parameter group — <see cref="Reward"/> now,
/// <c>Hyperparameters</c> later — each pointing at its own tuner instance.
/// </summary>
public static class ParameterTuners
{
    /// <summary>Tuner for the active objective's reward/terminal parameters.
    /// Null when no run is active.</summary>
    public static ParameterTuner Reward { get; set; }

    /// <summary>Tuner for the active model's hyperparameters (learning rate,
    /// mutation, population size, architecture, …). Descriptors carry
    /// <see cref="ParameterDescriptor.RequiresReset"/> so consumers can tell
    /// "hot" knobs (kept on a round-trip) from "cold" ones (force a rebuild).
    /// Null when no run is active.</summary>
    public static ParameterTuner Hyperparameters { get; set; }

    /// <summary>Controller for the active model's hidden-layer shape — the one
    /// architecture knob that can't ride the flat-float tuner (it's a variable-length
    /// int[]), so it has its own type. Disabled for NEAT (evolves its own topology).
    /// Null when no run is active.</summary>
    public static NetworkShapeController NetworkShape { get; set; }
}
