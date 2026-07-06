using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class AITypeSelector : MonoBehaviour
{
    public Button[] optionButtons;
    public UnityEvent<int> onOptionSelected;

    private int currentIndex = -1;

    void Start()
    {
        for (int i = 0; i < optionButtons.Length; i++)
        {
            // We must capture the current value of 'i' in a local variable 
            // for the lambda expression to work correctly.
            int index = i;
            optionButtons[i].onClick.AddListener(() => SelectOption(index));

            // Selection is shown via the disabled state — make it read as "chosen"
            // (muted accent fill) rather than greyed-out.
            UITheme.StyleSelectedWhenDisabled(optionButtons[i]);
        }

        // Select Option A (index 0) by default
        if (optionButtons.Length > 0)
        {
            SelectOption(0);
        }
    }

    public void SelectOption(int index)
    {
        // Ignore if clicking the already selected button
        if (index == currentIndex) return;

        // Deselect the previous button (make it clickable again)
        if (currentIndex >= 0 && currentIndex < optionButtons.Length)
        {
            optionButtons[currentIndex].interactable = true;
        }

        // Select the new button (disable clicking, triggering the "Disabled" visual state)
        currentIndex = index;
        optionButtons[currentIndex].interactable = false;

        // Broadcast the change
        onOptionSelected.Invoke(currentIndex);
    }
}