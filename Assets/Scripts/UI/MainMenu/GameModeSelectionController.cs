using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GameModeSelectionController : MonoBehaviour
{
    [Header("Hero Panel Elements")]
    public TextMeshProUGUI heroTitleText;
    public TextMeshProUGUI heroDescriptionText;
    public Image heroImage;
    public Button heroButton;

    [Header("List Setup")]
    public Transform buttonContainer; // The layout group holding your buttons
    public GameObject buttonPrefab;   // A prefab of a single UI button
    public List<GameModeData> availableModes; // Drag your ScriptableObjects here

    private AITypeSelector aiTypeSelector; // found lazily — lives on the selection window

    private readonly List<GameModeButton> modeButtons = new(); // spawned by PopulateList
    private GameModeData currentMode; // mode shown in the hero panel, null before the first SelectMode

    void Start()
    {
        PopulateList();

        // The Play call-to-action pops in accent against the themed panel, and the
        // hero title carries the accent (set explicitly — the generic skin keeps
        // all text neutral).
        UITheme.StylePrimary(heroButton);
        if (heroTitleText != null) heroTitleText.color = UITheme.Accent;

        // Auto-select the first mode so the screen isn't empty when it loads
        if (availableModes.Count > 0)
        {
            SelectMode(availableModes[0]);
        }
    }

    void PopulateList()
    {
        // Loop through all our modes and spawn a button for each
        foreach (GameModeData mode in availableModes)
        {
            GameObject newBtnObj = Instantiate(buttonPrefab, buttonContainer);
            UITheme.Skin(newBtnObj); // spawned after the canvas-wide skin — theme each clone
            GameModeButton btnScript = newBtnObj.GetComponent<GameModeButton>();

            // Pass the data and a reference to 'this' controller
            btnScript.Setup(mode, this);
            modeButtons.Add(btnScript);
        }
    }

    // This gets called by the individual buttons when clicked
    public void SelectMode(GameModeData newlySelectedMode)
    {
        // Re-selecting the current mode is a no-op — in particular it must NOT
        // reset the AI choice below (picking Track 1 → PPO → Track 1 keeps PPO).
        if (newlySelectedMode == currentMode) return;
        currentMode = newlySelectedMode;

        // Light up the chosen mode's button (disabled-as-selected, like the AI
        // selector) and release the previous one.
        foreach (GameModeButton btn in modeButtons)
            btn.SetSelected(btn.Data == newlySelectedMode);

        // Update the Single Hero Panel with the new data
        heroTitleText.text = newlySelectedMode.modeName;
        heroDescriptionText.text = newlySelectedMode.description;
        heroImage.sprite = newlySelectedMode.heroArtwork;
        
        // Clear old listeners so we don't load multiple scenes or repeat actions!
        heroButton.onClick.RemoveAllListeners();
        heroButton.onClick.AddListener(() => newlySelectedMode.LoadScene());

        // Switching mode resets the AI choice to its default so a previous pick
        // doesn't silently carry over to the new mode. Active selectors only: driving
        // an inactive one fires the selection event with no listener enabled to hear
        // it, and pre-sets the index so the selector's own Start broadcast later
        // early-returns — losing the default choice entirely.
        if (aiTypeSelector == null)
            aiTypeSelector = FindFirstObjectByType<AITypeSelector>();
        if (aiTypeSelector != null && aiTypeSelector.isActiveAndEnabled)
            aiTypeSelector.ResetToDefault();

        // Note: This is exactly where you would add your DOTween or LeanTween
        // code to fade the text in or briefly scale up the hero image!
    }

    public void ResetSelection()
    {
        if (availableModes != null && availableModes.Count > 0)
        {
            SelectMode(availableModes[0]);
        }
    }
}