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
        }
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