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
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        IsGamePaused = false;
    }

    public void Pause()
    {
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
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quitting game...");
        Application.Quit();
    }
}