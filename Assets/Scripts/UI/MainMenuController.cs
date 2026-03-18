using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public GameObject buttons;
    public GameObject selectionWindow;

    public void ToggleSelectionMenu()
    {
        selectionWindow.SetActive(!selectionWindow.activeSelf);
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
