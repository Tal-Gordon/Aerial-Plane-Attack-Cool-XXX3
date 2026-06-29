using UnityEngine;

/// <summary>
/// User-facing display settings (resolution + mode), persisted to PlayerPrefs and
/// re-applied once at startup before any scene loads, so a saved choice holds everywhere.
/// Edited via <see cref="SettingsMenu"/>, which routes Apply through <see cref="ApplyAndSave"/>.
///
/// <para>The 60 fps cap is intentionally NOT handled here — <see cref="AppBootstrap"/> already
/// caps every scene at process start (and the RL trainer re-applies the matching
/// RLSettings.TargetFrameRate on connect). This type only owns resolution and display mode.</para>
/// </summary>
public static class GameSettings
{
    private const string KeyWidth = "settings.display.width";
    private const string KeyHeight = "settings.display.height";
    private const string KeyMode = "settings.display.mode";
    private const string KeyHasSaved = "settings.display.saved";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // Re-apply the saved resolution/mode if the player ever changed it; otherwise
        // leave whatever the platform/launcher picked.
        if (PlayerPrefs.GetInt(KeyHasSaved, 0) != 1) return;

        int w = PlayerPrefs.GetInt(KeyWidth, Screen.width);
        int h = PlayerPrefs.GetInt(KeyHeight, Screen.height);
        var mode = (FullScreenMode)PlayerPrefs.GetInt(KeyMode, (int)Screen.fullScreenMode);
        Screen.SetResolution(w, h, mode);
    }

    /// <summary>Applies a resolution + display mode immediately and persists it.</summary>
    public static void ApplyAndSave(int width, int height, FullScreenMode mode)
    {
        Screen.SetResolution(width, height, mode);

        PlayerPrefs.SetInt(KeyWidth, width);
        PlayerPrefs.SetInt(KeyHeight, height);
        PlayerPrefs.SetInt(KeyMode, (int)mode);
        PlayerPrefs.SetInt(KeyHasSaved, 1);
        PlayerPrefs.Save();
    }
}
