using UnityEngine;

// This line lets you right-click in the Unity Project window to create new Game Modes
[CreateAssetMenu(fileName = "NewGameMode", menuName = "UI/Game Mode Data")]
public class GameModeData : ScriptableObject
{
    public string modeName;
    [TextArea(3, 5)] // Makes the text box bigger in the inspector
    public string description;
    public Sprite heroArtwork;
}