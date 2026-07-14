using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseMenuController : MonoBehaviour
{
    public static bool IsGamePaused = false;

    public GameObject pauseMenuUI;

    private readonly string mainMenuSceneName = "MainMenu";

    [Header("Input")]
    public InputAction pauseAction;

    private float savedTimeScale = 1f;
    private float savedFixedDeltaTime = 0.02f;

    private void Awake()
    {
        // The pause panel is scene-authored (duplicated across scenes/prefab) with
        // default visuals — theme it in place instead of editing every copy's YAML.
        if (pauseMenuUI != null)
        {
            UITheme.Skin(pauseMenuUI);
            foreach (UnityEngine.UI.Button button in pauseMenuUI.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true))
            {
                if (button.name != "Resume") continue;
                UITheme.StylePrimary(button); // Resume is the call-to-action
                break;
            }

            WirePanelButtons();
        }
    }

    // Completes the panel in code instead of editing the prefab YAML: hooks the
    // Settings button (authored with no onClick) to OpenSettings, and clones the Quit
    // button into a Main Menu button in the empty slot between Settings and Quit.
    private void WirePanelButtons()
    {
        UnityEngine.UI.Button settings = FindPanelButton("Settings");
        UnityEngine.UI.Button quit = FindPanelButton("Quit");

        // Only add our listener if nothing was wired in the editor, so a scene that
        // does wire it someday won't open the menu twice per click.
        if (settings != null && settings.onClick.GetPersistentEventCount() == 0)
            settings.onClick.AddListener(OpenSettings);

        if (quit == null || settings == null || FindPanelButton("MainMenu") != null)
            return;

        // Clone Quit so the new button inherits the panel's authored look and the
        // runtime theme already applied above.
        UnityEngine.UI.Button mainMenu = Instantiate(quit, quit.transform.parent);
        mainMenu.name = "MainMenu";
        mainMenu.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());

        // The clone copies Quit's persistent onClick (QuitGame) — persistent calls
        // can't be removed at runtime, so replace the whole event instance.
        mainMenu.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        mainMenu.onClick.AddListener(LoadMenu);

        var label = mainMenu.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
        if (label != null) label.text = "Main Menu";

        // The column is hand-anchored (no layout group); the panel leaves a
        // button-sized gap between Settings and Quit — park the clone at its midpoint.
        var settingsRt = (RectTransform)settings.transform;
        var quitRt = (RectTransform)quit.transform;
        ((RectTransform)mainMenu.transform).anchoredPosition =
            (settingsRt.anchoredPosition + quitRt.anchoredPosition) * 0.5f;
    }

    private UnityEngine.UI.Button FindPanelButton(string buttonName)
    {
        foreach (UnityEngine.UI.Button button in pauseMenuUI.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: true))
            if (button.name == buttonName)
                return button;
        return null;
    }

    private void OnEnable()
    {
        pauseAction.Enable();
    }

    private void OnDisable()
    {
        pauseAction.Disable();
    }

    void Update()
    {
        if (pauseAction.WasPressedThisFrame())
        {
            if (IsGamePaused)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Resume()
    {
        pauseMenuUI.SetActive(false);
        // Restore the speed we were running at before the pause
        Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f;
        Time.fixedDeltaTime = savedFixedDeltaTime > 0f ? savedFixedDeltaTime : 0.02f;
        IsGamePaused = false;
    }

    public void Pause()
    {
        // Save the current simulation speed and physics resolution
        savedTimeScale = Time.timeScale;
        savedFixedDeltaTime = Time.fixedDeltaTime;

        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        IsGamePaused = true;
    }

    public void LoadMenu()
    {
        // Important: Ensure time scale is reset before loading a new scene, 
        // otherwise the new scene might start paused!
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        IsGamePaused = false;

        Debug.Log("Loading menu...");
        if (LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.LoadScene(mainMenuSceneName);
        else
            SceneManager.LoadScene(mainMenuSceneName);
    }

    public void OpenSettings()
    {
        if (SettingsMenu.Instance != null)
            SettingsMenu.Instance.Open();
        else
            Debug.LogWarning("[PauseMenuController] SettingsMenu not available.");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}