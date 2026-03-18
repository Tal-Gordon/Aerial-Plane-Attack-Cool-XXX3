using UnityEngine;
using UnityEngine.UI;
using TMPro; // Standard Unity UI Text

[RequireComponent(typeof(Button))]
public class GameModeButton : MonoBehaviour
{
    public TextMeshProUGUI buttonText;

    private GameModeData myData;
    private GameModeSelectionController controller;
    private Button selfButton;

    private void Awake()
    {
        selfButton = GetComponent<Button>();
    }

    // The controller calls this when spawning the buttons
    public void Setup(GameModeData data, GameModeSelectionController mainController)
    {
        myData = data;
        controller = mainController;

        buttonText.text = data.modeName;

        // Listen for the click event
        selfButton.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        // Tell the main controller to update the Hero Panel with this button's data
        controller.SelectMode(myData);
    }
}