using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class AITypeSelector : MonoBehaviour
{
    /// <summary>Concrete event type with a field initializer, so a selector created at
    /// runtime via AddComponent has one. A bare <c>UnityEvent&lt;int&gt;</c> field is only
    /// ever non-null when Unity's deserializer builds it (i.e. scene-authored selectors) —
    /// AddComponent leaves it null and every Invoke/AddListener throws.</summary>
    [System.Serializable]
    public class OptionSelectedEvent : UnityEvent<int> { }

    public Button[] optionButtons;
    public OptionSelectedEvent onOptionSelected = new OptionSelectedEvent();

    private int currentIndex = -1;

    /// <summary>Currently selected button index, or -1 before any selection. Lets a
    /// late subscriber (e.g. a listener whose OnEnable runs after a selection was
    /// already broadcast) sync to the selection it missed.</summary>
    public int CurrentIndex => currentIndex;

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

    /// <summary>Reverts to the first option (the default) — re-enables the previously
    /// chosen button and broadcasts the change so the recorded AI type follows the
    /// visuals. No-op when the default is already selected.</summary>
    public void ResetToDefault() => SelectOption(0);

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