/// <summary>
/// Carries the player's main-menu choices across the scene load into the
/// gameplay scene. Static (like <see cref="DataManager"/>) so the values survive
/// <c>SceneManager.LoadScene</c> within a play session; they reset on domain
/// reload / when the app exits.
///
/// <para><see cref="SelectedAIType"/> is <c>null</c> when the player has made no
/// choice (e.g. pressing Play directly in a gameplay scene). In that case
/// SimulationManager falls back to the mode's default AI type.</para>
/// </summary>
public static class GameSession
{
    /// <summary>
    /// AI type chosen in the main menu, or <c>null</c> if none was chosen. Written
    /// by <see cref="AITypeSelectionWriter"/>; read by SimulationManager on Start.
    /// </summary>
    public static AIType? SelectedAIType { get; set; }

    /// <summary>
    /// True when the player chose "Continue" in the main menu's continue-or-fresh
    /// dialog: SimulationManager consumes it on Start and loads the latest save for
    /// the (track, AI type) instead of starting from defaults. One-shot — the
    /// consumer resets it so scene reloads don't re-trigger the load.
    /// </summary>
    public static bool LoadSaveOnStart { get; set; }

    /// <summary>Clears any carried selection (e.g. when returning to the menu).</summary>
    public static void Clear()
    {
        SelectedAIType = null;
        LoadSaveOnStart = false;
    }
}
