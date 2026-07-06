using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public GameObject buttons;
    public GameObject selectionWindow;

    private GameModeSelectionController selectionController;

    private void Awake()
    {
        selectionController = GetComponent<GameModeSelectionController>();

        // Theme the whole menu canvas (title, buttons, selection window) in place —
        // the scene keeps its default visuals in the editor, UITheme restyles at runtime.
        Transform themeRoot = buttons != null ? buttons.transform.root : transform.root;
        UITheme.Skin(themeRoot.gameObject);
    }

    public void ToggleSelectionMenu()
    {
        selectionWindow.SetActive(!selectionWindow.activeSelf);
        
        // Reset the mode selection whenever the menu is toggled
        if (selectionController != null)
        {
            selectionController.ResetSelection();
        }
    }

    private void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void OpenSettings()
    {
        if (SettingsMenu.Instance != null)
            SettingsMenu.Instance.Open();
        else
            Debug.LogWarning("[MainMenuController] SettingsMenu not available.");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
