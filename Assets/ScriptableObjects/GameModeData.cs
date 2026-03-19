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
    public void LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[GameModeData] Scene '{sceneName}' cannot be loaded. Please ensure it is added to the Build Settings (File > Build Settings)!");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}