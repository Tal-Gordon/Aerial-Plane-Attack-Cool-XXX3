using UnityEngine;
using UnityEngine.SceneManagement;

// This line lets you right-click in the Unity Project window to create new Game Modes
[CreateAssetMenu(fileName = "NewGameMode", menuName = "UI/Game Mode Data")]
public class GameModeData : ScriptableObject
{
    public string modeName;
    [TextArea(3, 5)] // Makes the text box bigger in the inspector
    public string description;
    public Sprite heroArtwork;

    [Tooltip("Scene to load for this mode. Leave empty to fall back to Mode Name.")]
    public string sceneName;

    /// <summary>Scene this mode loads — the explicit sceneName, or modeName as a fallback.</summary>
    public string SceneToLoad =>
        string.IsNullOrWhiteSpace(sceneName) ? modeName : sceneName;

    /// <summary>Loads this mode's scene, validating it first.</summary>
    public void LoadScene()
    {
        string scene = SceneToLoad;

        if (string.IsNullOrWhiteSpace(scene))
        {
            Debug.LogError($"[GameModeData] '{name}' has no scene to load. Set Scene Name (or Mode Name) on the asset.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"[GameModeData] Scene '{scene}' cannot be loaded. Ensure it is added to the Build Settings (File > Build Settings).");
            return;
        }

        // Route through the loading overlay (animated, async). It always exists by
        // play time (auto-bootstrapped); fall back to a direct load just in case.
        if (LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.LoadScene(scene);
        else
            SceneManager.LoadScene(scene);
    }
}