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

    /// <summary>The mode this button represents, so the controller can match the
    /// selected mode back to its button when updating highlights.</summary>
    public GameModeData Data => myData;

    // The controller calls this when spawning the buttons
    public void Setup(GameModeData data, GameModeSelectionController mainController)
    {
        myData = data;
        controller = mainController;
        selfButton = GetComponent<Button>();

        buttonText.text = data.modeName;

        // Selection is shown via the disabled state, same as the AI selector —
        // muted accent fill rather than greyed-out.
        UITheme.StyleSelectedWhenDisabled(selfButton);

        // Listen for the click event
        selfButton.onClick.AddListener(OnClick);
    }

    /// <summary>Marks this button as the selected mode: disabled reads as "chosen"
    /// (see Setup) and also swallows re-clicks of the current selection.</summary>
    public void SetSelected(bool selected)
    {
        selfButton.interactable = !selected;
    }

    private void OnClick()
    {
        // Tell the main controller to update the Hero Panel with this button's data
        controller.SelectMode(myData);
    }
}