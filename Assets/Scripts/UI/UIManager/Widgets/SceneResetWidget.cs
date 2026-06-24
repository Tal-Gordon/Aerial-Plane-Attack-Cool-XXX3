using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneResetWidget : UIWidget
{
    [SerializeField] private Button resetButton;

    protected override void OnInitialize()
    {
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(ResetScene);
        }
        else
        {
            Debug.LogWarning($"{nameof(SceneResetWidget)} on {gameObject.name} is missing a Button reference!");
        }
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        // A simple reset button doesn't need to poll data from the simulation snapshot, 
        // so we leave this empty.
    }

    private void ResetScene()
    {
        if (Time.timeScale != 1f)
        {
            Time.timeScale = 1f;
        }

        // Get the currently active scene and reload it by its build index
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

    private void OnDestroy()
    {
        // Good practice to clean up listeners when the object is destroyed
        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetScene);
        }
    }
}