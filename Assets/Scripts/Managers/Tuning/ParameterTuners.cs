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
}
