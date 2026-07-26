using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The whole main menu on one screen: title, a grid of mode cards, and a footer holding
/// the selected mode's description, the AI selector and saved-run actions.
///
/// Replaces the two-step flow (three buttons → selection window) of the original MainMenu
/// scene. With a fixed set of four modes there is nothing to page through, so every mode
/// is on screen as its own artwork tile and picking one is a single click.
///
/// Everything below the Canvas is built at runtime, like SettingsMenu and LoadingOverlay:
/// the scene holds a Canvas, an EventSystem and this component, and nothing else needs
/// wiring. Drop it on a new scene, fill in <see cref="modes"/>, done.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ModeGridMenu : MonoBehaviour
{
    [Header("Content")]
    [Tooltip("Modes shown as cards, in grid order (row-major).")]
    public List<GameModeData> modes = new List<GameModeData>();
    public string gameTitle = "AERIAL PLANE ATTACK";
    public string tagline = "AI  FLIGHT  TRAINING  SIMULATOR";

    [Header("AI options")]
    [Tooltip("Labels for the AI buttons, in the same order as Option AI Types.")]
    public string[] aiOptionLabels = { "Neural\nEvolution", "NEAT", "PPO", "SAC" };
    public AIType[] aiOptionTypes =
    {
        AIType.FixedNeuroEvo,
        AIType.NEAT,
        AIType.PPO_MLAgents,
        AIType.SAC_MLAgents,
    };

    [Header("Layout")]
    [Tooltip("Card size in canvas units. Keep it 16:9 — the track artwork is a screenshot.")]
    public Vector2 cardSize = new Vector2(520f, 293f);
    public float cardSpacing = 28f;
    [Tooltip("Columns in the grid. Four modes over two columns gives the 2x2 layout.")]
    public int columns = 2;

    // Fixed metrics shared by the header and footer, so the two edges line up.
    private const float SideMargin = 180f;
    private const float FooterHeight = 236f;
    private const float AIButtonWidth = 132f;
    private const float AIRowSpacing = 8f;
    private const float AIRowWidth = 4f * AIButtonWidth + 3f * AIRowSpacing;
    private const float ActionGap = 12f;
    private const float SecondaryActionWidth = 130f;
    private const float LoadActionWidth = 240f;
    private const float ActionRowWidth =
        2f * SecondaryActionWidth + LoadActionWidth + 2f * ActionGap;
    private const int CardRadius = 12;
    private static readonly Vector2 CornerButtonSize = new Vector2(140f, 50f);

    private readonly List<ModeCard> cards = new List<ModeCard>();
    private readonly List<HeroArtworkFitter> fitters = new List<HeroArtworkFitter>();
    private GameModeData currentMode;
    private TextMeshProUGUI modeTitle;
    private TextMeshProUGUI modeDescription;
    private AITypeSelector aiSelector;
    private ContinueTrainingDialog continueDialog;
    private Button challengeButton;
    private Button trainingStatsButton;
    private TrainingStatsOverlay trainingStatsOverlay;

    private void Start()
    {
        BuildBackdrop();
        BuildHeader();
        BuildGrid();
        BuildFooter();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) continueDialog = ContinueTrainingDialog.Attach(canvas.rootCanvas.transform);

        if (modes.Count > 0) SelectMode(modes[0]);
    }

    // ── Screen ───────────────────────────────────────────────────────

    private void BuildBackdrop()
    {
        Image backdrop = MenuUI.Panel(transform, "BackgroundGradient", Color.white);
        MenuUI.Stretch(backdrop.rectTransform);
        backdrop.sprite = UITheme.CreateVerticalGradientSprite(
            new Color(0.045f, 0.055f, 0.075f, 1f), new Color(0.10f, 0.12f, 0.16f, 1f));
        backdrop.raycastTarget = false;
    }

    private void BuildHeader()
    {
        var header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(transform, worldPositionStays: false);
        MenuUI.Band((RectTransform)header.transform, 1f, 190f);

        TextMeshProUGUI title = MenuUI.Label(header.transform, "GameTitle", gameTitle, 56f,
            UITheme.TextColor, TextAlignmentOptions.Center, FontStyles.Bold);
        title.characterSpacing = 10f;
        MenuUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(1400f, 72f), new Vector2(0f, -48f));

        Image underline = MenuUI.Panel(header.transform, "TitleUnderline", UITheme.Accent);
        underline.raycastTarget = false;
        MenuUI.Place(underline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(320f, 3f), new Vector2(0f, -126f));

        TextMeshProUGUI sub = MenuUI.Label(header.transform, "GameSubtitle", tagline, 18f,
            UITheme.TextDimmed, TextAlignmentOptions.Center);
        sub.characterSpacing = 6f;
        MenuUI.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(1400f, 28f), new Vector2(0f, -140f));

        // Settings and Quit live in the corner rather than the old button column —
        // the grid itself is now the menu's main action.
        Button settings = MenuUI.TextButton(header.transform, "SettingsButton", "Settings", 20f);
        MenuUI.Place(settings.image.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            CornerButtonSize, new Vector2(-(60f + CornerButtonSize.x + 12f), -50f));
        settings.onClick.AddListener(OpenSettings);

        Button quit = MenuUI.TextButton(header.transform, "QuitButton", "Quit", 20f);
        MenuUI.Place(quit.image.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            CornerButtonSize, new Vector2(-60f, -50f));
        quit.onClick.AddListener(QuitGame);
    }

    private void BuildGrid()
    {
        int rows = Mathf.Max(1, Mathf.CeilToInt(modes.Count / (float)Mathf.Max(1, columns)));
        var size = new Vector2(
            columns * cardSize.x + (columns - 1) * cardSpacing,
            rows * cardSize.y + (rows - 1) * cardSpacing);

        var gridRoot = new GameObject("ModeGrid", typeof(RectTransform));
        gridRoot.transform.SetParent(transform, worldPositionStays: false);
        // Nudged up from dead centre: the header is shallower than the footer.
        MenuUI.Place((RectTransform)gridRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            size, new Vector2(0f, 20f));

        var grid = gridRoot.AddComponent<GridLayoutGroup>();
        grid.cellSize = cardSize;
        grid.spacing = new Vector2(cardSpacing, cardSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.MiddleCenter;

        foreach (GameModeData mode in modes)
        {
            if (mode == null) continue;
            cards.Add(BuildCard(gridRoot.transform, mode));
        }

        // The grid only sizes its cells on the next layout pass, so the fitters were built
        // against a placeholder rect. They correct themselves when the cells resize, but
        // forcing the pass here means the first frame is already right.
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)gridRoot.transform);
        foreach (HeroArtworkFitter fitter in fitters) fitter.Refit();
    }

    private ModeCard BuildCard(Transform parent, GameModeData mode)
    {
        Image card = MenuUI.Rounded(MenuUI.Panel(parent, $"Card ({mode.modeName})", UITheme.Panel), CardRadius);

        // Cast from the card body, so the tiles read as sitting above the backdrop. Shadow
        // duplicates its own graphic's mesh, so it picks up the rounded corners for free.
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -6f);

        // Selection ring: an oversized accent plate, with an opaque fill on top of it at
        // the card's exact bounds. Only the 4px that sticks out shows, and it stays a ring
        // even for a mode with no artwork to cover the middle. Toggled by ModeCard.
        Image frame = MenuUI.Rounded(MenuUI.Panel(card.transform, "SelectionFrame", UITheme.Accent), CardRadius + 2);
        MenuUI.Stretch(frame.rectTransform, -4f);
        frame.raycastTarget = false;

        Image fill = MenuUI.Rounded(MenuUI.Panel(card.transform, "Fill", UITheme.Panel), CardRadius);
        MenuUI.Stretch(fill.rectTransform);
        fill.raycastTarget = false;

        // Everything below is clipped to the rounded shape. A Mask (stencil, sprite-shaped)
        // rather than a RectMask2D (rectangular): the artwork covers the whole tile, so a
        // rectangular clip would put square corners back over the rounded card.
        Image maskFrame = MenuUI.Rounded(MenuUI.Panel(card.transform, "RoundedClip", Color.white), CardRadius);
        MenuUI.Stretch(maskFrame.rectTransform);
        maskFrame.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        // Artwork, cover-cropped to the tile. The tile is 16:9 like the screenshots, so
        // in practice nothing is cropped — the fitter just absorbs odd sizes.
        Image art = MenuUI.Panel(maskFrame.transform, "Art", Color.white);
        MenuUI.Place(art.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        art.raycastTarget = false;
        art.sprite = mode.heroArtwork;
        art.enabled = mode.heroArtwork != null; // a sprite-less Image still paints a full quad
        fitters.Add(art.gameObject.AddComponent<HeroArtworkFitter>());

        // The scrim dims a resting tile and clears as it lifts — driven by ModeCard, not by
        // the button's ColorBlock. It is also the card's raycast target, so it covers the
        // whole tile and everything above it in draw order stays undimmed.
        Image scrim = MenuUI.Panel(maskFrame.transform, "Scrim", Color.white);
        MenuUI.Stretch(scrim.rectTransform);

        Image strip = MenuUI.Panel(maskFrame.transform, "NameStrip", Color.white);
        MenuUI.Band(strip.rectTransform, 0f, 56f);
        strip.raycastTarget = false;

        TextMeshProUGUI cardName = MenuUI.Label(strip.transform, "Name", mode.modeName, 24f,
            UITheme.TextColor, TextAlignmentOptions.Left);
        MenuUI.Stretch(cardName.rectTransform);
        cardName.margin = new Vector4(18f, 0f, 18f, 0f);

        GameObject badge = BuildSaveBadge(maskFrame.transform);

        var button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = scrim;

        var modeCard = card.gameObject.AddComponent<ModeCard>();
        modeCard.Init(mode, frame.gameObject, badge, scrim, strip, SelectMode);
        return modeCard;
    }

    // Top-left chip marking a track that already has a saved run for the chosen AI type —
    // the same condition that makes Load open the continue dialog instead of starting fresh.
    private static GameObject BuildSaveBadge(Transform card)
    {
        Image badge = MenuUI.Rounded(MenuUI.Panel(card, "SavedBadge", UITheme.Accent), 6);
        MenuUI.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(96f, 30f), new Vector2(14f, -14f));
        badge.raycastTarget = false;

        TextMeshProUGUI label = MenuUI.Label(badge.transform, "Label", "SAVED", 15f,
            UITheme.TextOnAccent, TextAlignmentOptions.Center, FontStyles.Bold);
        MenuUI.Stretch(label.rectTransform);
        label.characterSpacing = 4f;

        badge.gameObject.SetActive(false);
        return badge.gameObject;
    }

    // Footer reading order, left to right: what you picked, how it should learn, and
    // saved-run actions. The larger Load action is flanked by smaller Stats and Challenge
    // buttons so all three remain one clear group.
    private void BuildFooter()
    {
        Image footer = MenuUI.Panel(transform, "Footer", UITheme.Panel);
        MenuUI.Band(footer.rectTransform, 0f, FooterHeight);

        Image hairline = MenuUI.Panel(footer.transform, "Separator", UITheme.Hairline);
        MenuUI.Band(hairline.rectTransform, 1f, 1f);
        hairline.raycastTarget = false;

        modeTitle = MenuUI.Label(footer.transform, "ModeTitle", string.Empty, 34f,
            UITheme.Accent, TextAlignmentOptions.TopLeft, FontStyles.Bold);
        MenuUI.Place(modeTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(400f, 44f), new Vector2(SideMargin, -34f));

        modeDescription = MenuUI.Label(footer.transform, "ModeDescription", string.Empty, 20f,
            UITheme.TextColor, TextAlignmentOptions.TopLeft);
        MenuUI.Place(modeDescription.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(400f, 120f), new Vector2(SideMargin, -86f));

        BuildActionRow(footer.transform);

        float aiRight = -(SideMargin + ActionRowWidth + 44f); // clear of actions, with a gap

        TextMeshProUGUI caption = MenuUI.Label(footer.transform, "Caption", "Select AI", 18f,
            UITheme.TextDimmed, TextAlignmentOptions.Left);
        MenuUI.Place(caption.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(AIRowWidth, 24f), new Vector2(aiRight, 42f));

        BuildAISelector(footer.transform, aiRight);
    }

    private void BuildActionRow(Transform footer)
    {
        var row = new GameObject("RunActions", typeof(RectTransform));
        row.transform.SetParent(footer, worldPositionStays: false);
        MenuUI.Place((RectTransform)row.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(ActionRowWidth, 78f), new Vector2(-SideMargin, 0f));

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = ActionGap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        trainingStatsButton = MenuUI.TextButton(row.transform, "TrainingStatsButton", "STATS", 19f);
        trainingStatsButton.image.rectTransform.sizeDelta = new Vector2(SecondaryActionWidth, 64f);
        trainingStatsButton.onClick.AddListener(ShowTrainingStats);

        Button load = MenuUI.TextButton(row.transform, "LoadButton", "LOAD", 38f);
        load.image.rectTransform.sizeDelta = new Vector2(LoadActionWidth, 78f);
        UITheme.StylePrimary(load);
        load.onClick.AddListener(OnPlayClicked);

        challengeButton = MenuUI.TextButton(row.transform, "ChallengeButton", "CHALLENGE", 17f);
        challengeButton.image.rectTransform.sizeDelta = new Vector2(SecondaryActionWidth, 64f);
        challengeButton.onClick.AddListener(OnChallengeClicked);

        RefreshSecondaryActions();
    }

    private void BuildAISelector(Transform footer, float rightOffset)
    {
        var row = new GameObject("AITypeSelector", typeof(RectTransform));
        row.transform.SetParent(footer, worldPositionStays: false);
        MenuUI.Place((RectTransform)row.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(AIRowWidth, 58f), new Vector2(rightOffset, -6f));

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = AIRowSpacing;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        int count = Mathf.Min(aiOptionLabels.Length, aiOptionTypes.Length);
        var buttons = new Button[count];
        for (int i = 0; i < count; i++)
        {
            Button option = MenuUI.TextButton(row.transform, $"AIOption ({aiOptionTypes[i]})", aiOptionLabels[i], 19f);
            option.image.rectTransform.sizeDelta = new Vector2(AIButtonWidth, 58f);
            buttons[i] = option;
        }

        // Order matters: the writer's Awake looks for the selector on its own GameObject,
        // and its OnEnable must be subscribed before the selector's Start broadcasts the
        // default choice. Adding the selector first gives us both.
        aiSelector = row.AddComponent<AITypeSelector>();
        aiSelector.optionButtons = buttons;
        var writer = row.AddComponent<AITypeSelectionWriter>();
        writer.optionAITypes = aiOptionTypes;

        // Subscribed after the writer, so GameSession already holds the new AI type by the
        // time the badges ask which tracks have a save for it.
        aiSelector.onOptionSelected.AddListener(_ => RefreshSaveState());
    }

    // Which tracks have a saved run depends on the chosen AI type, so every card refreshes
    // whenever that changes.
    private void RefreshSaveState()
    {
        bool known = GameSession.SelectedAIType.HasValue;
        foreach (ModeCard card in cards)
        {
            card.SetHasSave(known && card.Data != null && DataManager.HasTrainingState(
                DataManager.ResolveTrackId(card.Data.SceneToLoad), GameSession.SelectedAIType.Value));
        }

        RefreshSecondaryActions();
    }

    private void RefreshSecondaryActions()
    {
        TrainingSaveData saved = GetSelectedSave();
        if (trainingStatsButton != null)
            trainingStatsButton.interactable = saved != null;
        if (challengeButton != null)
            challengeButton.interactable = saved != null
                                           && saved.Mode == DataManager.GameMode.FlightSchool;
    }

    // ── Selection ────────────────────────────────────────────────────

    public void SelectMode(GameModeData mode)
    {
        if (mode == null || mode == currentMode) return;
        currentMode = mode;

        foreach (ModeCard card in cards)
            card.SetSelected(card.Data == mode);

        modeTitle.text = mode.modeName;
        modeDescription.text = mode.description;

        // Switching mode drops back to the default AI so a previous pick doesn't
        // silently carry over — same rule the old selection window followed.
        if (aiSelector != null && aiSelector.isActiveAndEnabled) aiSelector.ResetToDefault();
        RefreshSaveState();
    }

    // ── Actions ──────────────────────────────────────────────────────

    private void OnPlayClicked()
    {
        if (currentMode == null) return;

        string track = DataManager.ResolveTrackId(currentMode.SceneToLoad);
        bool hasSave = GameSession.SelectedAIType is AIType aiType
                       && DataManager.HasTrainingState(track, aiType);

        if (!hasSave || continueDialog == null)
        {
            StartRun(loadSave: false, resetSettings: false);
            return;
        }

        continueDialog.Show(
            $"'{currentMode.modeName}' has a saved {PrettyAIName(GameSession.SelectedAIType.Value)} training run.\n" +
            "Continue from the latest save, or start over from the default settings?",
            () => StartRun(loadSave: true, resetSettings: false),
            () => StartRun(loadSave: false, resetSettings: true));
    }

    private void StartRun(bool loadSave, bool resetSettings)
    {
        GameSession.StartChallengeOnStart = false;
        GameSession.LoadSaveOnStart = loadSave; // consumed by SimulationManager.Start
        GameSession.ResetSettingsOnStart = resetSettings;
        currentMode.LoadScene();
    }

    private void OnChallengeClicked()
    {
        TrainingSaveData saved = GetSelectedSave();
        if (saved == null || saved.Mode != DataManager.GameMode.FlightSchool)
        {
            RefreshSecondaryActions();
            return;
        }

        GameSession.LoadSaveOnStart = false;
        GameSession.ResetSettingsOnStart = false;
        GameSession.StartChallengeOnStart = true; // consumed by SimulationManager.Start
        currentMode.LoadScene();
    }

    private void ShowTrainingStats()
    {
        TrainingSaveData saved = GetSelectedSave();
        if (saved == null)
        {
            RefreshSecondaryActions();
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        trainingStatsOverlay ??= TrainingStatsOverlay.Ensure(canvas);
        trainingStatsOverlay?.Show(currentMode, saved);
    }

    private TrainingSaveData GetSelectedSave()
    {
        if (currentMode == null || GameSession.SelectedAIType is not AIType aiType)
            return null;

        string track = DataManager.ResolveTrackId(currentMode.SceneToLoad);
        return DataManager.LoadTrainingState(track, aiType);
    }

    public void OpenSettings()
    {
        if (SettingsMenu.Instance != null)
            SettingsMenu.Instance.Open();
        else
            Debug.LogWarning("[ModeGridMenu] SettingsMenu not available.");
    }

    public void QuitGame() => Application.Quit();

    private static string PrettyAIName(AIType type) => type switch
    {
        AIType.FixedNeuroEvo => "NeuroEvo",
        AIType.NEAT          => "NEAT",
        AIType.PPO_MLAgents  => "PPO",
        AIType.SAC_MLAgents  => "SAC",
        _                    => type.ToString(),
    };
}
