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

    void Start()
    {
        PopulateList();

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
            GameModeButton btnScript = newBtnObj.GetComponent<GameModeButton>();

            // Pass the data and a reference to 'this' controller
            btnScript.Setup(mode, this);
        }
    }

    // This gets called by the individual buttons when clicked
    public void SelectMode(GameModeData newlySelectedMode)
    {
        // Update the Single Hero Panel with the new data
        heroTitleText.text = newlySelectedMode.modeName;
        heroDescriptionText.text = newlySelectedMode.description;
        heroImage.sprite = newlySelectedMode.heroArtwork;
        
        // Clear old listeners so we don't load multiple scenes or repeat actions!
        heroButton.onClick.RemoveAllListeners();
        heroButton.onClick.AddListener(() => newlySelectedMode.LoadScene(newlySelectedMode.modeName));

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