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
        // TODO
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
