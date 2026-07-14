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

    // Continue-or-fresh dialog, built lazily in code (like SettingsMenu) so nothing
    // needs scene wiring and the UITheme look is applied automatically.
    private GameObject continueDialog;
    private TextMeshProUGUI continueDialogBody;
    private GameModeData pendingMode; // mode awaiting the player's continue/fresh choice

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
        heroButton.onClick.AddListener(() => OnPlayClicked(newlySelectedMode));

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

    // ── Continue-or-fresh dialog ─────────────────────────────────────

    // Play clicked: if a save exists for the (track, chosen AI type) ask the player
    // whether to resume it; otherwise load straight to defaults as before.
    private void OnPlayClicked(GameModeData mode)
    {
        string track = DataManager.ResolveTrackId(mode.SceneToLoad);
        bool hasSave = GameSession.SelectedAIType is AIType aiType
                       && DataManager.HasTrainingState(track, aiType);

        if (!hasSave)
        {
            GameSession.LoadSaveOnStart = false;
            mode.LoadScene();
            return;
        }

        pendingMode = mode;
        ShowContinueDialog(mode, GameSession.SelectedAIType.Value);
    }

    private void ShowContinueDialog(GameModeData mode, AIType aiType)
    {
        if (continueDialog == null) BuildContinueDialog();
        if (continueDialog == null)
        {
            // No canvas to host the dialog — don't swallow the click; load fresh.
            Debug.LogWarning("[GameModeSelectionController] Could not build the continue dialog; starting from defaults.");
            OnStartFresh();
            return;
        }

        continueDialogBody.text =
            $"'{mode.modeName}' has a saved {PrettyAIName(aiType)} training run.\n" +
            "Continue from the latest save, or start over from the default settings?";

        continueDialog.transform.SetAsLastSibling(); // draw above the selection window
        continueDialog.SetActive(true);
    }

    private void HideContinueDialog()
    {
        if (continueDialog != null) continueDialog.SetActive(false);
    }

    private void OnContinueSave()
    {
        HideContinueDialog();
        GameSession.LoadSaveOnStart = true; // consumed by SimulationManager.Start
        pendingMode?.LoadScene();
    }

    private void OnStartFresh()
    {
        HideContinueDialog();
        GameSession.LoadSaveOnStart = false;
        pendingMode?.LoadScene();
    }

    private static string PrettyAIName(AIType type) => type switch
    {
        AIType.FixedNeuroEvo => "NeuroEvo",
        AIType.NEAT          => "NEAT",
        AIType.PPO_MLAgents  => "PPO",
        AIType.SAC_MLAgents  => "SAC",
        _                    => type.ToString(),
    };

    // Code-built modal in the SettingsMenu style: full-screen dim (click = cancel),
    // centred panel with the accent top stroke, title, body, and two buttons.
    private void BuildContinueDialog()
    {
        Canvas canvas = heroButton != null ? heroButton.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;
        Transform parent = canvas.rootCanvas.transform;

        var root = new GameObject("ContinueDialog", typeof(RectTransform));
        root.transform.SetParent(parent, worldPositionStays: false);
        StretchRect((RectTransform)root.transform);
        continueDialog = root;

        // Dim backdrop; clicking it dismisses the dialog (no scene load).
        Image dim = AddImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        StretchRect(dim.rectTransform);
        Button dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(HideContinueDialog);

        Image panel = AddImage(root.transform, "Panel", UITheme.Panel);
        CenterRect(panel.rectTransform, new Vector2(640f, 300f));

        Image accent = AddImage(panel.transform, "AccentLine", UITheme.Accent);
        TopRect(accent.rectTransform, new Vector2(640f, 2f), Vector2.zero);

        TextMeshProUGUI title = AddDialogLabel(panel.transform, "Title", "Continue training?", 30f, UITheme.Accent, FontStyles.Bold);
        TopRect(title.rectTransform, new Vector2(600f, 44f), new Vector2(0f, -26f));

        continueDialogBody = AddDialogLabel(panel.transform, "Body", string.Empty, 20f, UITheme.TextColor, FontStyles.Normal);
        TopRect(continueDialogBody.rectTransform, new Vector2(560f, 90f), new Vector2(0f, -84f));

        Button continueButton = BuildDialogButton(panel.transform, "Continue save", new Vector2(-150f, 36f));
        continueButton.onClick.AddListener(OnContinueSave);
        UITheme.StylePrimary(continueButton); // resuming progress is the safe default

        Button freshButton = BuildDialogButton(panel.transform, "Start from default", new Vector2(150f, 36f));
        freshButton.onClick.AddListener(OnStartFresh);

        root.SetActive(false);
    }

    private static Button BuildDialogButton(Transform parent, string label, Vector2 anchoredPos)
    {
        Image img = AddImage(parent, label + " Button", UITheme.Field);
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(260f, 52f);
        rt.anchoredPosition = anchoredPos;

        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        UITheme.StyleSelectable(button);

        TextMeshProUGUI text = AddDialogLabel(img.transform, "Text", label, 20f, UITheme.TextColor, FontStyles.Normal);
        StretchRect(text.rectTransform);
        return button;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI AddDialogLabel(Transform parent, string name, string text,
        float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static void StretchRect(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CenterRect(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void TopRect(RectTransform rt, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }
}